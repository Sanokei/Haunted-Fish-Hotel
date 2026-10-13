#!/usr/bin/env python3
"""Source-only validation. Does not claim Unity import, runtime playback or GPU validation."""
import math, pathlib, re, struct, sys, json
ROOT=pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/ScenePresentationTests'))
import verify_scene_assets as base
ART=ROOT/'Assets/Art/Effects/PiranhaStun';PREFAB=ROOT/'Assets/Prefabs/Effects/PiranhaStun.prefab';PLAYER=ROOT/'Assets/Resources/HotelNetworkPlayer.prefab'
LAYOUT=json.loads(PathLayout.read_text()) if (PathLayout:=pathlib.Path(__file__).with_name('orbit_layout.json')).exists() else {}
checks=0

def check(condition,message):
 global checks
 assert condition,message
 checks+=1

def text(path):return path.read_text(encoding='utf-8-sig')
def guid(path):return re.search(r'^guid: ([0-9a-f]{32})$',text(pathlib.Path(str(path)+'.meta')),re.M)[1]
paths=list(ART.glob('*.asset'))+list(ART.glob('*.mat'))+list(ART.glob('*.anim'))+[PREFAB,PLAYER]
for p in paths:
 source=text(p)
 for validate in [base.syntax_issues,base.mesh_issues,base.reference_issues]:check(not validate(source),str(p)+': '+str(validate(source)))
# Resolve every external GUID used by the authored effect, including source texture.
catalog={}
for m in (ROOT/'Assets').rglob('*.meta'):
 match=re.search(r'^guid: ([0-9a-f]{32})$',text(m),re.M)
 if match:catalog.setdefault(match[1],[]).append(m)
for p in paths[:-1]:
 for g in re.findall(r'guid: ([0-9a-f]{32})',text(p)):check(g in catalog and len(catalog[g])==1,'Missing or duplicated GUID '+g)
for p in ART.iterdir():
 if p.is_file() and p.suffix!='.meta':check(pathlib.Path(str(p)+'.meta').exists(),'Missing meta '+str(p))
pref=text(PREFAB); docs={d.ident:d for d in base.records(pref)}
check(base.value(docs[1000].body,'m_IsActive')=='0','Effect root must begin inactive')
check(base.value(docs[1002].body,'m_PlayAutomatically')=='0','Must not auto-play')
check(base.value(docs[1002].body,'m_WrapMode')=='2','Animation component must loop')
check(set(d.kind for d in docs.values())=={1,4,33,23,111},'Unexpected runtime component')
check(sum(d.kind==23 for d in docs.values())==8,'Expected five fish plus three combined trail meshes')
check(sum(d.kind==111 for d in docs.values())==1,'Expected one animation owner')
def rotate(q,v):
 x,y,z,w=q;a,b,c=v
 tx=2*(y*c-z*b);ty=2*(z*a-x*c);tz=2*(x*b-y*a)
 return (a+w*tx+y*tz-z*ty,b+w*ty+z*tx-x*tz,c+w*tz+x*ty-y*tx)
def yaw(a):return (0,math.sin(a/2),0,math.cos(a/2))
def add(a,b):return tuple(x+y for x,y in zip(a,b))
def mesh_vertices(name):
 data=bytes.fromhex(re.search(r'_typelessdata: (\w+)',text(ART/(name+'.asset')))[1]);return [v[:3] for v in struct.iter_unpack('<8f',data)]
# Nose direction must remain tangential in each tilted plane, including all loop phases.
min_height=float('inf');fish_number=0;expected_paths=[];normals=[]
root_q=(math.sin(math.radians(10)),0,0,math.cos(math.radians(10)))
root_p=(0,1.25,0);lift=(0,LAYOUT['orbit_lift_m'],0);fish_vertices=mesh_vertices('PiranhaMini')
for group in LAYOUT['planes']:
 n=group['id'];plane_q=base.vector(base.value(docs[n+1].body,'m_LocalRotation'));normals.append(rotate(plane_q,(0,1,0)))
 check(abs(sum(v*v for v in plane_q)-1)<1e-6,'Invalid plane quaternion')
 expected_paths.append('Orbit/'+group['name']+'/Spin')
 for i,degrees in enumerate(group['fish_angles']):
  fish_number+=1;ident=n+201+i*100;q=base.vector(base.value(docs[ident].body,'m_LocalRotation'));pos=base.vector(base.value(docs[ident].body,'m_LocalPosition'));forward=rotate(q,(0,0,1))
  for step in range(97):
   t=step/96*1.6;a=group['direction']*t/1.6*2*math.pi;spin=yaw(a)
   position=(pos[0],LAYOUT['bob_amplitude_m']*math.sin(t/1.6*2*math.pi+math.radians(degrees)),pos[2]);p=rotate(spin,position);direction=rotate(plane_q,rotate(spin,forward))
   tangent=rotate(plane_q,(group['direction']*p[2],0,-group['direction']*p[0]));length=math.sqrt(sum(x*x for x in tangent));dot=sum(a*b for a,b in zip(direction,tangent))/length
   check(dot>.999999,'Fish must face travel in its tilted plane')
   for v in fish_vertices:
    world=add(root_p,rotate(root_q,add(lift,rotate(plane_q,rotate(spin,add(position,rotate(q,v)))))))
    min_height=min(min_height,world[1])
  expected_paths.append('Orbit/'+group['name']+'/Spin/Piranha%02d'%fish_number)
 for step in range(97):
  spin=yaw(group['direction']*step/96*2*math.pi)
  for v in mesh_vertices(group['mesh']):
   world=add(root_p,rotate(root_q,add(lift,rotate(plane_q,rotate(spin,v)))))
   min_height=min(min_height,world[1])
check(fish_number==5,'Exactly five piranhas required')
check(min_height>1.0,'Effect falls below crown clearance: '+str(min_height))
check(all(sum(a*b for a,b in zip(normals[i],normals[j]))<.98 for i in range(3) for j in range(i)),'Planes must have distinct tilts')
clip=text(ART/'PiranhaStunOrbit.anim')
check('m_Legacy: 1' in clip and 'm_LoopTime: 1' in clip and 'm_WrapMode: 2' in clip,'Legacy loop settings')
curves=re.findall(r'  - curve:\n(.*?)    path: ([^\n]+)',clip,re.S)
check(sorted(p for _,p in curves)==sorted(expected_paths),'Animation bindings changed')
# All bound paths must actually resolve in the authored hierarchy.
transform_paths={1001:''}
def transform_path(ident):
 if ident in transform_paths:return transform_paths[ident]
 body=docs[ident].body;parent=base.local_ref(body,'m_Father');go=base.local_ref(body,'m_GameObject');name=base.value(docs[go].body,'m_Name');prefix=transform_path(parent)
 transform_paths[ident]=(prefix+'/' if prefix else '')+name;return transform_paths[ident]
for d in docs.values():
 if d.kind==4:transform_path(d.ident)
for _,path in curves:check(path in transform_paths.values(),'Missing animation target '+path)
for block,path in curves:
 keys=[]
 for t,v,ins,outs in re.findall(r'time: ([^\n]+)\n        value: (\{[^}]+\})\n        inSlope: (\{[^}]+\})\n        outSlope: (\{[^}]+\})',block):keys.append((float(t),base.vector(v),base.vector(ins),base.vector(outs)))
 check(keys[0][0]==0 and keys[-1][0]==1.6,'Loop duration mismatch')
 check(all(abs(a-b)<1e-6 for a,b in zip(keys[0][2],keys[-1][3])),'Loop derivative discontinuity')
 if path.endswith('/Spin'):
  sign=next(g['direction'] for g in LAYOUT['planes'] if '/'+g['name']+'/' in path)
  check(keys[0][1]==(0,0,0) and keys[-1][1]==(0,sign*360,0),'Yaw must turn continuously once')
  check(all(k[2]==(0,sign*225,0) and k[3]==(0,sign*225,0) for k in keys),'Yaw rate must remain constant')
 else:check(all(abs(a-b)<1e-6 for a,b in zip(keys[0][1],keys[-1][1])),'Position seam discontinuity')
player=text(PLAYER);pdocs={d.ident:d for d in base.records(player)}
check(player.count('_StunEffect:')==1 and player.count('_StunAnimation:')==1,'Duplicate or missing bindings')
for ref,kind,sourceid in [(883400003,1,1000),(883400004,111,1002)]:
 check(pdocs[ref].kind==kind and pdocs[ref].stripped,'Must reference nested reusable component')
 check('fileID: '+str(sourceid)+', guid: '+guid(PREFAB) in pdocs[ref].body,'Wrong source component')
check('m_TransformParent: {fileID: 21000015}' in pdocs[883400001].body,'Must follow authored Fish visual')
check('  - {fileID: 883400002}' in pdocs[21000015].body,'Missing child relation')
check('propertyPath: m_IsActive' not in pdocs[883400001].body,'Initial disabled state must not be overridden')
check('Animation' not in pdocs[883400001].body,'Do not override playback flags')
# Native meshes use compact UInt16 indices, valid bounds, finite floats, and UVs.
for p in ART.glob('*.asset'):
 s=text(p);d=base.records(s)[0];count=int(re.search(r'm_VertexCount: (\d+)',s)[1]);data=bytes.fromhex(re.search(r'_typelessdata: (\w+)',s)[1]);verts=list(struct.iter_unpack('<8f',data))
 check(len(verts)==count,'Vertex stride mismatch')
 check(all(math.isfinite(x) for v in verts for x in v),'Non-finite mesh data')
 check(all(-.001<=v[j]<=1.001 for v in verts for j in [6,7]),'UV outside original atlas tolerance')
 check(base.value(d.body,'m_IsReadable')=='0','Runtime meshes should not retain CPU copies')
report=json.loads(text(ART/'Source~/mesh_report.json'))
check(report['effect_triangles']==1860,'Triangle budget changed unexpectedly')
check((ART/'Source~/PiranhaStun.blend').stat().st_size>0,'Missing editable source')
check((ART/'PiranhaStunMeshes.fbx').stat().st_size<98304,'Portable mesh budget exceeded')
print(f'PASS: {checks} source-only checks; 1,860 triangles, eight renderers, one pre-authored animation; no runtime construction.')
print(f'Minimum sampled authored height: {min_height:.3f} m; fish sprite crown approximately 0.979 m.')
print('NOT RUN: Unity import/shader compilation, runtime animation playback, gameplay capture.')
