#!/usr/bin/env python3
"""Offline deterministic authoring. Writes Unity prefabs and scene references, never runtime geometry."""
import hashlib, json, math, random, re, sys
from pathlib import Path
import yaml
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Prefabs/WatchingFrames'
ART=json.loads((ROOT/'Tools/FrameArt/frame-segmentation.json').read_text())
MASKS={f['frame_id']:f for f in json.loads((ROOT/'Tools/WatchingFrameTests/frame-opening-masks.json').read_text())['frames']}
# Exact uploaded swatch: all 11,289 pixels are RGBA(32,32,32,255).
INTERIOR_SRGB=[32/255]*3+[1]
INTERIOR_LINEAR=[((32/255+.055)/1.055)**2.4]*3+[1]
HEADER='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
def guid(p):
    return re.search(r'^guid: (\w+)', Path(str(ROOT/p)+'.meta').read_text(), re.M)[1]
def meta(p,importer='PrefabImporter'):
    p=Path(p);mp=Path(str(p)+'.meta')
    if not mp.exists():
        g=hashlib.md5(('haunted-watching-frames:'+str(p.relative_to(ROOT))).encode()).hexdigest()
        mp.write_text(f'fileFormatVersion: 2\nguid: {g}\n{importer}:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    return guid(p.relative_to(ROOT))
def ref(i,g=None,t=3): return dict(fileID=i,**(dict(guid=g,type=t) if g else {}))
def v3(a):return dict(zip(['x','y','z'],[round(float(v),7) for v in a]))
def quat(yaw,roll=0):
    a,b=math.radians(yaw)/2,math.radians(roll)/2
    return [math.sin(a)*math.sin(b),math.sin(a)*math.cos(b),math.cos(a)*math.sin(b),math.cos(a)*math.cos(b)]
def rot(q,v):
    x,y,z,w=q;vx,vy,vz=v
    tx,ty,tz=2*(y*vz-z*vy),2*(z*vx-x*vz),2*(x*vy-y*vx)
    return [vx+w*tx+y*tz-z*ty,vy+w*ty+z*tx-x*tz,vz+w*tz+x*ty-y*tx]
def common(go=None):
    return dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject=ref(0),m_PrefabInstance=ref(0),m_PrefabAsset=ref(0),**({'m_GameObject':ref(go)} if go else {}))
def go(name,i,components):return dict(**common(),serializedVersion=6,m_Component=[dict(component=ref(c)) for c in components],m_Layer=0,m_Name=name,m_TagString='Untagged',m_IsActive=1)
def tf(goid,parent=0,pos=(0,0,0),q=(0,0,0,1),scale=(1,1,1),children=()):
    return dict(**common(goid),serializedVersion=2,m_LocalRotation=dict(zip(['x','y','z','w'],[round(x,9) for x in q])),m_LocalPosition=v3(pos),m_LocalScale=v3(scale),m_ConstrainProportionsScale=0,m_Children=[ref(c) for c in children],m_Father=ref(parent),m_LocalEulerAnglesHint=v3((0,0,0)))
def renderer(goid,mat,order=0):
    return dict(**common(goid),m_Enabled=1,m_CastShadows=2,m_ReceiveShadows=1,m_DynamicOccludee=1,m_StaticShadowCaster=0,m_MotionVectors=0,m_LightProbeUsage=0,m_ReflectionProbeUsage=0,m_RayTracingMode=0,m_RayTraceProcedural=0,m_RenderingLayerMask=1,m_RendererPriority=0,m_Materials=[ref(2100000,mat,2)],m_StaticBatchInfo=dict(firstSubMesh=0,subMeshCount=0),m_StaticBatchRoot=ref(0),m_ProbeAnchor=ref(0),m_LightProbeVolumeOverride=ref(0),m_ScaleInLightmap=1,m_ReceiveGI=1,m_PreserveUVs=0,m_IgnoreNormalsForChartDetection=0,m_ImportantGI=0,m_StitchLightmapSeams=1,m_SelectedEditorRenderState=0,m_MinimumChartSize=4,m_AutoUVMaxDistance=.5,m_AutoUVMaxAngle=89,m_LightmapParameters=ref(0),m_SortingLayerID=0,m_SortingLayer=0,m_SortingOrder=order)
class UnityDumper(yaml.SafeDumper):
    def represent_dict(self, data):
        inline = bool(data) and ('fileID' in data or set(data).issubset({'x','y','z','w'}) or set(data).issubset({'r','g','b','a'}))
        return self.represent_mapping('tag:yaml.org,2002:map', data, flow_style=inline)
UnityDumper.add_representer(dict,UnityDumper.represent_dict)
def doc(typ,i,key,d):return f'--- !u!{typ} &{i}\n'+yaml.dump({key:d},Dumper=UnityDumper,sort_keys=False,width=200)

def eye_rect(frame):
    fw,fh=frame['size'];l,t,r,b=frame['interior_bbox_local'];cx,cy=frame['interior_centroid_local']
    height=(b-t)*.97/fh
    width=height*fh/fw*(1654/853)
    return [cx/fw,1-cy/fh,width,height]

EYE_SELECTION_SEED=52
EYE_TARGETS={'Lobby':121,'Game':3,'IntroTunnel':182}

def apply_eye_selection(frames,target=None):
    """Balanced wall quotas, independent hash ranking; never every-Nth authoring order."""
    if not frames:return {}
    target=EYE_TARGETS[frames[0]['scene']] if target is None else target
    groups={wall:[p for p in frames if p['wall']==wall] for wall in sorted({p['wall'] for p in frames})}
    exact={w:len(ps)*target/len(frames) for w,ps in groups.items()}
    quotas={w:math.floor(n) for w,n in exact.items()}
    remainder=target-sum(quotas.values())
    for w in sorted(groups,key=lambda w:(-(exact[w]-quotas[w]),hashlib.sha256(f'{EYE_SELECTION_SEED}:{w}'.encode()).hexdigest()))[:remainder]:quotas[w]+=1
    for wall,ps in groups.items():
        rank=lambda p:hashlib.sha256(f'watching-slot:{EYE_SELECTION_SEED}:{p["scene"]}:{wall}:{p["name"]}'.encode()).hexdigest()
        chosen={p['name'] for p in sorted(ps,key=rank)[:quotas[wall]]}
        for p in ps:
            p['has_eye']=p['name'] in chosen
            p['slot_kind']='watching_eye' if p['has_eye'] else 'empty_picture_slot'
            p['has_picture']=False
            p['picture_rect']=[.5,.5,1,1]
    return dict(target=target,total=len(frames),fraction=target/len(frames),seed=EYE_SELECTION_SEED,wall_quotas=quotas,method='Largest-remainder wall quotas, then deterministic SHA256 ranking by scene/wall/frame identity; independent of layout RNG and no every-third pattern.')

def material_path(frame,variant,scene,has_eye=True):
    suffix='' if has_eye else '-EmptySlot'
    return OUT/'Materials'/f'{scene}-Frame-{variant}-{frame["id"]:02d}{suffix}.mat'

def material(frame,variant,scene,has_eye=True):
    props={}
    paths=[('_MainTex',frame['variants'][variant]['path']),('_FrameOpening',MASKS[frame['id']]['path']),('_FrameBacking',MASKS[frame['id']]['backing_path'])]
    paths += [(k,f'Assets/Art/Frames/Eye/{n}.png') for k,n in [('_OutlineTex','HALLWAY-Frame-Eye-outline'),('_PupilTex','HALLWAY-Frame-Eye'),('_InteriorMask','HALLWAY-Frame-Eye-interior-mask')]]
    for k,path in paths:props[k]=dict(m_Texture=ref(2800000,guid(path)),m_Scale=dict(x=1,y=1),m_Offset=dict(x=0,y=0))
    props['_PictureTex']=dict(m_Texture=ref(0),m_Scale=dict(x=1,y=1),m_Offset=dict(x=0,y=0))
    rect=eye_rect(frame)
    d=dict(serializedVersion=8,**common(),m_Name=f'{scene} original {variant} frame {frame["id"]:02d} - grey isolated opening',m_Shader=ref(4800000,guid('Assets/Prefabs/WatchingFrames/Materials/WatchingEye.shader')),m_ValidKeywords=[],m_InvalidKeywords=[],m_LightmapFlags=4,m_EnableInstancingVariants=0,m_DoubleSidedGI=0,m_CustomRenderQueue=-1,stringTagMap={},disabledShaderPasses=[],m_SavedProperties=dict(serializedVersion=3,m_TexEnvs=[{k:v} for k,v in props.items()],m_Ints=[],m_Floats=[{'_HasEye':1 if has_eye else 0},{'_HasPicture':0},{'_CullMode':2 if scene=='IntroTunnel' else 0},{'_PresentationGain':1},{'_Closure':0},{'_ClosedLineWidth':.006},{'_Ambient':.52 if scene=='Game' else .8 if scene=='IntroTunnel' else .32},{'_LightResponse':.72 if scene=='Game' else .85},{'_LightLimit':1.1},{'_ShadowCutoff':.2},{'_DarknessStart':10000 if scene=='Game' else 4.25},{'_DarknessEnd':10001 if scene=='Game' else 12},{'_DarknessFloor':1 if scene=='Game' else .012}]+([{'_NearArtDistance':32},{'_NearArtVisibility':.8},{'_PupilIntensity':.125}] if scene=='IntroTunnel' else []),m_Colors=[{'_PictureRect':dict(r=.5,g=.5,b=1,a=1)},{'_InteriorColor':dict(zip('rgba',INTERIOR_LINEAR))},{'_PupilOffset':dict(r=0,g=0,b=0,a=0)},{'_EyeRect':dict(zip('rgba',rect))}]))
    p=material_path(frame,variant,scene,has_eye);p.write_text(HEADER+doc(21,2100000,'Material',d));return meta(p,'NativeFormatImporter')

def make_placement(name,scene,wall,center,yaw,w,h,rng,usage,force_frame=None,roll=None,cluster=0):
    best=[]
    for f in ART['frames']:
        ratio=f['size'][0]/f['size'][1]
        score=abs(math.log(ratio/(w/h)))+usage[f['id']]*.007+rng.random()*.15
        best.append((score,f))
    frame=force_frame or min(best,key=lambda x:x[0])[1];usage[frame['id']]+=1
    fw,fh=frame['size'];s=min(w/(fw/100),h/(fh/100))
    roll=roll if roll is not None else rng.uniform(-5,5)
    variant='filled' if rng.random()<.57 else 'empty';q=quat(yaw,roll)
    rect=eye_rect(frame);width,height=fw/100*s,fh/100*s
    ew,eh=rect[2]*width,rect[3]*height
    aim=[rect[0]-.5,rect[1]-.5,0]
    eoffset=rot(q,[aim[0]*width,aim[1]*height,0]);epos=[center[i]+eoffset[i] for i in range(3)]
    return dict(name=name,scene=scene,wall=wall,cluster=cluster,frame_id=frame['id'],frame_name=frame['name'],variant=variant,source=frame['variants'][variant]['path'],sprite_guid=frame['variants'][variant]['guid'],pixel_size=frame['size'],position=center,rotation_quaternion=q,rotation_yaw=yaw,rotation_roll=roll,scale=s,width=width,height=height,cell_width=w,cell_height=h,has_eye=True,interior_color_linear=INTERIOR_LINEAR,opening_mask=MASKS[frame['id']]['path'],opening_mask_guid=MASKS[frame['id']]['guid'],backing_mask=MASKS[frame['id']]['backing_path'],backing_mask_guid=MASKS[frame['id']]['backing_guid'],eye_frame_uv_rect=rect,eye_local_position=aim,eye_world_position=epos,eye_world_size=[ew,eh],eye_rotation_quaternion=q,composite='Single frame quad: original exterior alpha, opaque grey opening, original eye clipped to this frame opening after eye-mask and closure composition.')

def cluster_layout(width,height,rng,usage):
    """Grow freely rotated neighbors around scattered seeds. No rows, bins, cells or grid."""
    placed=[];target=round(width*height*.68);max_attempts=40000
    local_usage={f['id']:0 for f in ART['frames']}
    def geometry(x,y,w,h,angle):
        t=math.radians(angle);c,s=math.cos(t),math.sin(t)
        return dict(x=x,y=y,w=w,h=h,angle=angle,axes=((c,s),(-s,c)),bounds=(abs(c)*w/2+abs(s)*h/2,abs(s)*w/2+abs(c)*h/2))
    def collision(a,b):
        dx,dy=b['x']-a['x'],b['y']-a['y'];gap=.022
        if abs(dx)>a['bounds'][0]+b['bounds'][0]+gap or abs(dy)>a['bounds'][1]+b['bounds'][1]+gap:return False
        for ux,uy in a['axes']+b['axes']:
            ar=abs(ux*a['axes'][0][0]+uy*a['axes'][0][1])*a['w']/2+abs(ux*a['axes'][1][0]+uy*a['axes'][1][1])*a['h']/2
            br=abs(ux*b['axes'][0][0]+uy*b['axes'][0][1])*b['w']/2+abs(ux*b['axes'][1][0]+uy*b['axes'][1][1])*b['h']/2
            if abs(dx*ux+dy*uy)>=ar+br+gap:return False
        return True
    for attempt in range(max_attempts):
        if len(placed)>=target:break
        frame=rng.choices(ART['frames'],weights=[1/(1+local_usage[f['id']])**1.5 for f in ART['frames']],k=1)[0];ratio=frame['size'][0]/frame['size'][1]
        if attempt<1000:size=rng.triangular(.9,2.4,1.5)
        elif attempt<9000:size=rng.triangular(.65,1.7,1.0)
        else:size=rng.triangular(.40,1.05,.55)
        h=size/math.sqrt(ratio);w=size*math.sqrt(ratio)
        scale=min(1,3.1/w,2.8/h);w*=scale;h*=scale
        angle=rng.uniform(-22,22)
        if rng.random()<.12:angle=rng.choice([-1,1])*rng.uniform(24,32)
        candidate=geometry(0,0,w,h,angle)
        cluster=len(placed)+1
        if len(placed)>4 and rng.random()<.84:
            parent=rng.choice(placed);theta=rng.uniform(0,math.tau);ux,uy=math.cos(theta),math.sin(theta)
            def support(a):return abs(ux*a['axes'][0][0]+uy*a['axes'][0][1])*a['w']/2+abs(ux*a['axes'][1][0]+uy*a['axes'][1][1])*a['h']/2
            distance=support(parent)+support(candidate)+rng.uniform(.025,.095)
            x,y=parent['x']+ux*distance,parent['y']+uy*distance;cluster=parent['cluster']
        else:x,y=rng.uniform(0,width),rng.uniform(0,height)
        candidate=geometry(x,y,w,h,angle);bx,by=candidate['bounds']
        if x-bx<.025 or x+bx>width-.025 or y-by<.025 or y+by>height-.025:continue
        if any(collision(candidate,other) for other in placed):continue
        candidate.update(frame=frame,cluster=cluster);placed.append(candidate);local_usage[frame['id']]+=1
    print(f'Cluster wall {width:.2f}x{height:.2f}: {len(placed)} frames, {sum(p["w"]*p["h"] for p in placed)/(width*height):.1%} quad coverage, {attempt+1} candidates')
    return placed

def lobby():
    rng=random.Random(1410202617);usage={f['id']:0 for f in ART['frames']};out=[]
    walls=[('Left upper clusters',[-5.606682,0,4.321383],18.4352,-1),('Right upper clusters',[5.6193085,0,4.325416],-18.338,1)]
    for name,anchor,wall_yaw,side in walls:
        theta=math.radians(wall_yaw);normal=[-side*math.cos(theta),0,side*math.sin(theta)]
        anchor=[anchor[i]+normal[i]*.118 for i in range(3)];yaw=wall_yaw+side*90
        direction=rot(quat(yaw),(1,0,0));z0,z1=(-2.95,8.71) if direction[2]>0 else (8.71,-2.95)
        u0=(z0-anchor[2])/direction[2];width=(z1-z0)/direction[2]
        packed=cluster_layout(width,17.13,rng,usage)
        for j,p in enumerate(packed):
            center=[anchor[0]+direction[0]*(u0+p['x']),4.47+p['y'],anchor[2]+direction[2]*(u0+p['x'])]
            placement=make_placement(f'{name} {j+1:03d}','Lobby',name,center,yaw,p['w'],p['h'],rng,usage,p['frame'],p['angle'],p['cluster'])
            placement['wall_plane_position']=[p['x'],p['y']];placement['wall_plane_bounds']=[width,17.13];out.append(placement)
    packed=cluster_layout(7.98,17.13,rng,usage)
    for j,p in enumerate(packed):
        placement=make_placement(f'Back upper clusters {j+1:03d}','Lobby','Back upper clusters',[-3.99+p['x'],4.47+p['y'],8.813],0,p['w'],p['h'],rng,usage,p['frame'],p['angle'],p['cluster'])
        placement['wall_plane_position']=[p['x'],p['y']];placement['wall_plane_bounds']=[7.98,17.13];out.append(placement)
    return out

def hallway():
    rng=random.Random(20261014);usage={f['id']:0 for f in ART['frames']};out=[]
    # These eight positions occupy quiet wall gaps, away from all authored doors and sconces.
    positions=[(-13.55,2.30,1.18,1.30),(-4.35,2.35,1.14,1.38),(5.50,2.18,1.32,1.25),(15.34,2.40,1.08,1.42),(25.12,2.24,1.24,1.14),(34.94,2.32,1.12,1.34),(44.76,2.25,1.30,1.26),(51.90,2.39,1.18,1.27)]
    for i,(x,y,w,h) in enumerate(positions):
        p=make_placement(f'Hallway watching frame {i+1:02d}','Game','Hallway back wall',[x,y,1.78],0,w,h,rng,usage)
        p['variant']='filled' if i%2==0 else 'empty';f=ART['frames'][p['frame_id']-1];p['source']=f['variants'][p['variant']]['path'];p['sprite_guid']=f['variants'][p['variant']]['guid'];p['has_eye']=True
        out.append(p)
    return out

def prefab(scene,frames):
    prefix='Lobby' if scene=='Lobby' else 'Hallway';path=OUT/f'{prefix}WatchingFrames.prefab'
    ids=[100100+i*10 for i in range(len(frames))];data=HEADER
    data+=doc(1,100000,'GameObject',go(f'{prefix} authored watching frame gallery',100000,[400000,1140000]))
    data+=doc(4,400000,'Transform',tf(100000,children=[i+1 for i in ids]))
    eyes=[dict(Renderer=ref(i+2),AimPoint=ref(i+5),PresentationGain=1) for i,p in zip(ids,frames) if p['has_eye']]
    static=[dict(Renderer=ref(i+2),PresentationGain=1) for i,p in zip(ids,frames) if not p['has_eye']]
    data+=doc(114,1140000,'MonoBehaviour',dict(**common(100000),m_Enabled=1,m_EditorHideFlags=0,m_Script=ref(11500000,guid('Assets/Scripts/Presentation/WatchingFrames/WatchingFrameGallery.cs')),m_Name='',m_EditorClassIdentifier='',_Viewer=ref(0),_BeamCamera=ref(0),_ReactToFlashlight=1 if scene=='Game' else 0,_BeamOccluders=dict(serializedVersion=2,m_Bits=4294967295),_CloseSeconds=.16,_OpenSeconds=.24,_Eyes=eyes,_StaticFrames=static,_SamplesPerSecond=20,_GazeGain=.35,_MinimumDistance=4,_TravelUV=dict(x=.23,y=.12),_ViewerMovementThreshold=.004))
    materials={}
    for i,p in zip(ids,frames):
        key=(p['frame_id'],p['variant'],p['has_eye'])
        if key not in materials:materials[key]=material(ART['frames'][p['frame_id']-1],p['variant'],scene,p['has_eye'])
        data+=doc(1,i,'GameObject',go(f'{p["name"]} | {p["frame_name"]} | {p["variant"]}',i,[i+1,i+2,i+3]))
        data+=doc(4,i+1,'Transform',tf(i,400000,p['position'],p['rotation_quaternion'],[p['width'],p['height'],1],[i+5] if p['has_eye'] else []))
        data+=doc(33,i+3,'MeshFilter',dict(**common(i),m_Mesh=ref(10210,'0000000000000000e000000000000000',0)))
        data+=doc(23,i+2,'MeshRenderer',dict(**renderer(i,materials[key],0),m_AdditionalVertexStreams=ref(0)))
        p['material']=str(material_path(ART['frames'][p['frame_id']-1],p['variant'],scene,p['has_eye']).relative_to(ROOT))
        if p['has_eye']:
            data+=doc(1,i+4,'GameObject',go('Authored opening center - gaze and flashlight anchor',i+4,[i+5]))
            data+=doc(4,i+5,'Transform',tf(i+4,i+1,p['eye_local_position']))
    path.write_text(data);return meta(path)

def attach(scene,g,camera_transform,instance):
    p=ROOT/f'Assets/Scenes/{scene}.unity';s=p.read_text()
    # Remove only this author's prior insertion for repeatable offline authoring.
    s=re.sub(rf'^--- !u!1001 &{instance}\n.*?(?=^--- !u!|\Z)','',s,flags=re.M|re.S)
    s=re.sub(rf'^  - \{{fileID: {instance}\}}\n','',s,flags=re.M)
    modifications=[dict(target=ref(1140000,g),propertyPath='_Viewer',value='',objectReference=ref(camera_transform))]
    if scene=='Game': modifications.append(dict(target=ref(1140000,g),propertyPath='_BeamCamera',value='',objectReference=ref(24000026)))
    d=dict(m_ObjectHideFlags=0,serializedVersion=2,m_Modification=dict(serializedVersion=3,m_TransformParent=ref(0),m_Modifications=modifications,m_RemovedComponents=[],m_RemovedGameObjects=[],m_AddedGameObjects=[],m_AddedComponents=[]),m_SourcePrefab=ref(100100000,g))
    block=doc(1001,instance,'PrefabInstance',d)
    index=s.index('--- !u!1660057539 ');s=s[:index]+block+s[index:]
    roots=re.search(r'^--- !u!1660057539 .*?(?=^--- !u!|\Z)',s,re.M|re.S)
    s=s[:roots.start()]+roots[0].rstrip()+f'\n  - {{fileID: {instance}}}\n'+s[roots.end():]
    if scene=='Game':
        stripped=8141014012
        s=re.sub(rf'^--- !u!114 &{stripped} stripped\n.*?(?=^--- !u!|\Z)','',s,flags=re.M|re.S)
        block=f'--- !u!114 &{stripped} stripped\nMonoBehaviour:\n  m_CorrespondingSourceObject: {{fileID: 1140000, guid: {g}, type: 3}}\n  m_PrefabInstance: {{fileID: {instance}}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: 0}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {guid("Assets/Scripts/Presentation/WatchingFrames/WatchingFrameGallery.cs")}, type: 3}}\n  m_Name:\n  m_EditorClassIdentifier:\n'
        s+=block
        spotlight=re.search(r'^--- !u!114 &24000033\n.*?(?=^--- !u!|\Z)',s,re.M|re.S)
        text=re.sub(r'^  _WatchingFrames:.*\n','',spotlight[0],flags=re.M).rstrip()+f'\n  _WatchingFrames: {{fileID: {stripped}}}\n'
        s=s[:spotlight.start()]+text+s[spotlight.end():]
    p.write_text(s)

def attach_recording_stage(g):
    p=ROOT/'Assets/Resources/Tutorials/TutorialRecordingStage.prefab';s=p.read_text()
    instance,transform,controller=8141014100,8141014101,8141014112
    for i in [instance,transform,controller]:
        s=re.sub(rf'^--- !u!\d+ &{i}(?: stripped)?\n.*?(?=^--- !u!|\Z)','',s,flags=re.M|re.S)
    parent=6144801475636508509
    modifications=[dict(target=ref(1140000,g),propertyPath='_Viewer',value='',objectReference=ref(6821593003971969775)),dict(target=ref(1140000,g),propertyPath='_BeamCamera',value='',objectReference=ref(420571060765889963))]
    d=dict(m_ObjectHideFlags=0,serializedVersion=2,m_Modification=dict(serializedVersion=3,m_TransformParent=ref(parent),m_Modifications=modifications,m_RemovedComponents=[],m_RemovedGameObjects=[],m_AddedGameObjects=[],m_AddedComponents=[]),m_SourcePrefab=ref(100100000,g))
    s+=doc(1001,instance,'PrefabInstance',d)
    s+=f'--- !u!4 &{transform} stripped\nTransform:\n  m_CorrespondingSourceObject: {{fileID: 400000, guid: {g}, type: 3}}\n  m_PrefabInstance: {{fileID: {instance}}}\n  m_PrefabAsset: {{fileID: 0}}\n'
    s+=f'--- !u!114 &{controller} stripped\nMonoBehaviour:\n  m_CorrespondingSourceObject: {{fileID: 1140000, guid: {g}, type: 3}}\n  m_PrefabInstance: {{fileID: {instance}}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: 0}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {guid("Assets/Scripts/Presentation/WatchingFrames/WatchingFrameGallery.cs")}, type: 3}}\n  m_Name:\n  m_EditorClassIdentifier:\n'
    hallway=re.search(r'^--- !u!1001 &6144801475636508472\n.*?(?=^--- !u!|\Z)',s,re.M|re.S)
    addition=f'    - targetCorrespondingSourceObject: {{fileID: 101, guid: 32ba6258b9755b3991bf35e425189dec, type: 3}}\n      insertIndex: -1\n      addedObject: {{fileID: {transform}}}\n'
    text=hallway[0].replace(addition,'').replace('    m_AddedComponents:',addition+'    m_AddedComponents:')
    s=s[:hallway.start()]+text+s[hallway.end():]
    director=re.search(r'^--- !u!114 &242606801975031782\n.*?(?=^--- !u!|\Z)',s,re.M|re.S)
    text=re.sub(r'^  WatchingFrames:.*\n','',director[0],flags=re.M).rstrip()+f'\n  WatchingFrames: {{fileID: {controller}}}\n'
    s=s[:director.start()]+text+s[director.end():];p.write_text(s)

def main():
    a,b=lobby(),hallway();policies={'Lobby':apply_eye_selection(a),'Game':apply_eye_selection(b)};lg=prefab('Lobby',a);hg=prefab('Game',b)
    if '--no-scene-writes' not in sys.argv:
        attach('Lobby',lg,330585546,8141014001);attach('Game',hg,24000025,8141014002);attach_recording_stage(hg)
    keep={material_path(ART['frames'][p['frame_id']-1],p['variant'],p['scene'],p['has_eye']).name for p in a+b}
    for old in (OUT/'Materials').glob('*.mat'):
        if (old.name.startswith(('Lobby-Frame-','Game-Frame-')) or old.name=='WatchingEye.mat') and old.name not in keep:
            old.unlink();Path(str(old)+'.meta').unlink(missing_ok=True)
    report=dict(schema=3,eye_selection=policies,interior_color=dict(hex_srgb='#202020',srgb_rgba=INTERIOR_SRGB,linear_rgba=INTERIOR_LINEAR,material_property='_InteriorColor (explicit linear RGB Vector; no automatic Color conversion)',source='Exact uploaded swatch libfile_8941004c393081919e86231c0587e13b: 159x71, every pixel RGBA(32,32,32,255). See interior-colour-reference.json. Original textures remain unchanged.',composition='eyeAlpha=(outline.a+pupil.a*(1-outline.a))*eyeBounds; picture=eyeRGB+interiorRGB*(1-eyeAlpha); rgb=frameRGB*frame.a*(1-opening)+picture*opening+interiorRGB*backing*(1-frame.a)*(1-opening); alpha=frame.a+backing*(1-frame.a).'),coordinate_system='Unity world: x right, y up, z forward; quad UV bottom-left origin. Blender conversion (x,z,y).',provenance='Original RGBA sprite art; authored prefab geometry. Blender previews are source approximations, not native Unity output.',eye=dict(outline='Assets/Art/Frames/Eye/HALLWAY-Frame-Eye-outline.png',pupil='Assets/Art/Frames/Eye/HALLWAY-Frame-Eye.png',mask='Assets/Art/Frames/Eye/HALLWAY-Frame-Eye-interior-mask.png',canvas_size=[1654,853],uv_offset_limits=[.23,.12],hallway_flashlight=dict(source='GameMouseSpotlight scene component 24000033; local enabled fish screen beam',close_seconds=.16,open_seconds=.24,closure_shader='_Closure 0..1. Mask/outline sample vertical UV = (uv.y-.5)/max(.001,1-closure)+.5; pupil original UV stays shifted by gaze. Original upper/lower outlines converge. Last8% crossfades to anti-aliased central stroke sampled from original outline RGB at(.5,.985). Pupil alpha fades to0. Closed stroke half-width.006UV.',occlusion='Camera ScreenPointToRay to each eye, Physics.Raycast excluding final.03unit; ignore triggers.'),gain=.35,minimum_depth=4,projection='local direction = inverse eye world rotation * (camera - eye position). offset = .35 * direction.xy / max(4, abs(direction.z)), then clamp to ellipse (.23,.12). Sample pupil at uv-offset, alpha multiplied by mask alpha; outline source-over pupil. Opaque neutral-grey picture-frame opening. Height-first original eye sizing via per-frame eye_frame_uv_rect; complete eye including outline and closed lid multiplied by exact opening_mask. One composite quad, no stencil dependency.',default_viewer={'Lobby':[0,9.38,-6.89],'Game':[0,4,-20]}),prefabs={'Lobby':str((OUT/'LobbyWatchingFrames.prefab').relative_to(ROOT)),'Game':str((OUT/'HallwayWatchingFrames.prefab').relative_to(ROOT))},counts={'Lobby':len(a),'Lobby eyes':sum(p['has_eye'] for p in a),'Hallway':len(b),'Hallway eyes':sum(p['has_eye'] for p in b)},placements=a+b)
    (ROOT/'Tools/WatchingFrameTests/gallery-placement.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report['counts']))
if __name__=='__main__':main()
