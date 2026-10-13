"""Run with Blender: blender -b --python Tools/StunEffectAuthoring/author_piranha_stun.py.
Derives all fish geometry/UVs from the repository piranha source; adds authored orbit arcs.
Does not construct objects at game runtime or edit source artwork.
"""
import bpy, math, struct, pathlib, re, json, uuid
from mathutils import Vector
ROOT=pathlib.Path(__file__).resolve().parents[2]
ART=ROOT/'Assets/Art/Effects/PiranhaStun'; ART.mkdir(parents=True,exist_ok=True)
SOURCE=ROOT/'Assets/Art/Fish/Piranha'
LAYOUT=json.loads((PathLayout:=pathlib.Path(__file__).with_name('orbit_layout.json')).read_text())
bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'piranhahigh.blend'))
src=bpy.data.objects['Piranha']; mesh=src.data.copy()
world=[src.matrix_world@v.co for v in mesh.vertices]
low=Vector(tuple(min(p[i] for p in world) for i in range(3))); high=Vector(tuple(max(p[i] for p in world) for i in range(3)))
center=(low+high)*.5; scale=LAYOUT['fish_length_m']/(high.y-low.y)
for v,p in zip(mesh.vertices,world): v.co=(p-center)*scale
for obj in list(bpy.data.objects): bpy.data.objects.remove(obj,do_unlink=True)
scene=bpy.context.scene
scene.frame_start=1; scene.frame_end=97; scene.render.fps=60
scene.world.color=(.025,.03,.04)
for mat in list(bpy.data.materials): bpy.data.materials.remove(mat)
def emission(name,color,texture=None):
 m=bpy.data.materials.new(name); m.use_nodes=True; n=m.node_tree.nodes; n.clear(); links=m.node_tree.links
 out=n.new('ShaderNodeOutputMaterial'); em=n.new('ShaderNodeEmission'); em.inputs['Color'].default_value=(*color,1); em.inputs['Strength'].default_value=1
 if texture:
  tex=n.new('ShaderNodeTexImage'); tex.image=bpy.data.images.load(str(texture),check_existing=True); tex.interpolation='Closest'; links.new(tex.outputs['Color'],em.inputs['Color'])
  trans=n.new('ShaderNodeBsdfTransparent'); mix=n.new('ShaderNodeMixShader'); links.new(tex.outputs['Alpha'],mix.inputs[0]); links.new(trans.outputs[0],mix.inputs[1]); links.new(em.outputs[0],mix.inputs[2]); links.new(mix.outputs[0],out.inputs[0])
 else: links.new(em.outputs[0],out.inputs[0])
 return m
fishmat=emission('Original piranha pixel texture',(1,1,1),SOURCE/'piranhahigh.png'); trailmat=emission('Warm ivory orbit trails',(.93,.87,.58))
mesh.materials.clear(); mesh.materials.append(fishmat)
def empty(name,parent=None):
 ob=bpy.data.objects.new(name,None);scene.collection.objects.link(ob);ob.parent=parent;return ob
root=empty('PiranhaStun'); orbit=empty('Orbit',root);orbit.location.z=LAYOUT['orbit_lift_m']
fish=[]; trails=[]; fish_number=0
for group in LAYOUT['planes']:
 plane=empty(group['name'],orbit)
 x=math.radians(group['tilt_x'])/2;z=math.radians(group['tilt_z'])/2
 # Unity Qx * Qz, mapped (x,y,z) -> Blender (x,-z,y).
 q=(math.sin(x)*math.cos(z),-math.sin(x)*math.sin(z),math.cos(x)*math.sin(z),math.cos(x)*math.cos(z))
 plane.rotation_mode='QUATERNION';plane.rotation_quaternion=(q[3],q[0],-q[2],q[1])
 spin=empty('Spin',plane); sign=group['direction'];radius=group['radius']
 for degrees in group['fish_angles']:
  angle=math.radians(degrees);fish_number+=1
  ob=bpy.data.objects.new('Piranha%02d'%fish_number,mesh);scene.collection.objects.link(ob);ob.parent=spin
  ob.location=(radius*math.cos(angle),radius*math.sin(angle),0);ob.rotation_euler.z=angle+(math.pi if sign>0 else 0)
  fish.append(ob)
  for frame in [1,25,49,73,97]:
   ob.location.z=LAYOUT['bob_amplitude_m']*math.sin((frame-1)/96*2*math.pi+angle)
   ob.keyframe_insert(data_path='location',frame=frame)
  # Match Unity's analytic Hermite bob and its continuous seam in the editable source.
  for fc in ob.animation_data.action.fcurves:
   for key in fc.keyframe_points:
    frame,value=key.co;velocity=LAYOUT['bob_amplitude_m']*2*math.pi/96*math.cos((frame-1)/96*2*math.pi+angle) if fc.array_index==2 else 0
    key.handle_left_type='FREE';key.handle_right_type='FREE'
    key.handle_left=(frame-8,value-velocity*8);key.handle_right=(frame+8,value+velocity*8)
   fc.modifiers.new('CYCLES')
 for frame,angle in [(1,0),(97,sign*2*math.pi)]:spin.rotation_euler.z=angle;spin.keyframe_insert(data_path='rotation_euler',frame=frame)
 for fc in spin.animation_data.action.fcurves:
  for key in fc.keyframe_points:key.interpolation='LINEAR'
  fc.modifiers.new('CYCLES')
 # Main and accent strokes can visibly cross strokes on the other tilted planes.
 verts=[];faces=[]
 for degrees in group['fish_angles']:
  angle=math.radians(degrees)
  for arc in range(2):
   start=angle-sign*math.radians(150 if arc==0 else 110);end=angle-sign*math.radians(26 if arc==0 else 40)
   radius=group['radius']+(.032 if arc else 0);segs=18 if arc==0 else 12;sides=5;first=len(verts)
   for j in range(segs+1):
    t=j/segs;a=start+(end-start)*t;thickness=(.0075 if arc==0 else .0043)*(.23+.77*math.sin(math.pi*t)**.45)
    z=-.01+.012*math.sin(a*2)+(0 if arc==0 else -.016)
    for k in range(sides):
     b=2*math.pi*k/sides
     verts.append(((radius+thickness*math.cos(b))*math.cos(a),(radius+thickness*math.cos(b))*math.sin(a),z+thickness*math.sin(b)))
   for j in range(segs):
    for k in range(sides):
     a=first+j*sides+k;b=first+j*sides+(k+1)%sides;c=b+sides;d=a+sides;faces.append((a,b,c,d))
   faces.append(tuple(first+k for k in reversed(range(sides))));faces.append(tuple(first+segs*sides+k for k in range(sides)))
 arcs=bpy.data.meshes.new(group['mesh']);arcs.from_pydata(verts,[],faces);arcs.update();arcs.materials.append(trailmat)
 ob=bpy.data.objects.new('CurvedTrails',arcs);scene.collection.objects.link(ob);ob.parent=spin;trails.append(ob)
 for poly in arcs.polygons:poly.use_smooth=True
scene.frame_set(1)
# Direct native mesh assets use stable file IDs and don't require a Blender install in Unity.
def unityvec(v): return (v[0],v[2],-v[1])
def vec(v):return '{'+', '.join(k+': '+format(float(x),'.8g') for k,x in zip('xyz',v))+'}'
def native_mesh(ob,name):
 me=ob.data; me.calc_loop_triangles(); uv=me.uv_layers.active.data if me.uv_layers.active else None
 vv=[]; ii=[]; seen={}
 for tri in me.loop_triangles:
  ids=[]
  for li in tri.loops:
   lp=me.loops[li]; p=unityvec(me.vertices[lp.vertex_index].co); normal=unityvec(me.corner_normals[li].vector); tex=tuple(uv[li].uv) if uv else (0,0)
   key=tuple(round(float(v),7) for v in (*p,*normal,*tex))
   if key not in seen:seen[key]=len(vv);vv.append(key)
   ids.append(seen[key])
  ii.extend(reversed(ids))
 lo=[min(v[i] for v in vv) for i in range(3)]; hi=[max(v[i] for v in vv) for i in range(3)]; cen=[(a+b)*.5 for a,b in zip(lo,hi)]; ext=[(b-a)*.5 for a,b in zip(lo,hi)]
 template=(ROOT/'Assets/Art/Materials/LobbyRevision/LobbyCarpetSurface.asset').read_text()
 header=template[:template.index('  m_SubMeshes:')].replace('m_Name: LobbyCarpetSurface','m_Name: '+name)
 sub='  m_SubMeshes:\n  - serializedVersion: 2\n    firstByte: 0\n    indexCount: '+str(len(ii))+'\n    topology: 0\n    baseVertex: 0\n    firstVertex: 0\n    vertexCount: '+str(len(vv))+'\n    localAABB:\n      m_Center: '+vec(cen)+'\n      m_Extent: '+vec(ext)+'\n'
 middle=template[template.index('  m_Shapes:'):template.index('  m_IndexBuffer:')].replace('m_IsReadable: 1','m_IsReadable: 0')
 data=b''.join(struct.pack('<8f',*v) for v in vv); idx=struct.pack('<'+'H'*len(ii),*ii)
 vertex='  m_IndexBuffer: '+idx.hex()+'\n  m_VertexData:\n    serializedVersion: 3\n    m_VertexCount: '+str(len(vv))+'\n    m_Channels:\n'
 for c in range(14):
  dim={0:3,1:3,4:2}.get(c,0);offset={0:0,1:12,4:24}.get(c,0)
  vertex+='    - stream: 0\n      offset: '+str(offset)+'\n      format: 0\n      dimension: '+str(dim)+'\n'
 vertex+='    m_DataSize: '+str(len(data))+'\n    _typelessdata: '+data.hex()+'\n'
 tail=template[template.index('  m_CompressedMesh:'):template.index('  m_MeshLodInfo:')]
 tail=re.sub(r'(m_LocalAABB:\n    m_Center: )\{[^}]+\}\n    m_Extent: \{[^}]+\}',lambda m:m[1]+vec(cen)+'\n    m_Extent: '+vec(ext),tail)
 tail+='  m_MeshLodInfo:\n    serializedVersion: 2\n    m_LodSelectionCurve:\n      serializedVersion: 1\n      m_LodSlope: 0\n      m_LodBias: 0\n    m_NumLevels: 1\n    m_SubMeshes:\n    - serializedVersion: 2\n      m_Levels:\n      - serializedVersion: 1\n        m_IndexStart: 0\n        m_IndexCount: '+str(len(ii))+'\n'
 (ART/(name+'.asset')).write_text(header+sub+middle+vertex+tail)
 return {'mesh':name,'vertices':len(vv),'triangles':len(ii)//3,'bounds_center':cen,'bounds_extent':ext}
stats=[native_mesh(fish[0],'PiranhaMini')]+[native_mesh(ob,group['mesh']) for ob,group in zip(trails,LAYOUT['planes'])]
# Portable mesh source in addition to directly bound native assets.
bpy.ops.object.select_all(action='DESELECT')
for ob in fish+trails:ob.select_set(True)
bpy.context.view_layer.objects.active=fish[0]
bpy.ops.export_scene.fbx(filepath=str(ART/'PiranhaStunMeshes.fbx'),use_selection=True,object_types={'MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,path_mode='STRIP')
# Keep the editable Blender file in Unity's ignored source directory.
for im in bpy.data.images:
 if im.source=='FILE':
  try:im.pack()
  except RuntimeError:pass
scene.frame_set(1)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'Source~'/'PiranhaStun.blend'))
# Deliberately labelled asset preview, separate from the game and the imported FBX.
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=False
scene.render.resolution_x=1400;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.view_settings.view_transform='Standard';scene.view_settings.look='None';scene.view_settings.exposure=0
scene.world.use_nodes=True;scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.018,.025,.037,1);scene.world.node_tree.nodes.get('Background').inputs[1].default_value=.4
camdata=bpy.data.cameras.new('Asset preview camera');cam=bpy.data.objects.new('Asset preview camera',camdata);scene.collection.objects.link(cam)
cam.location=(1.1,-1.8,2.6);target=Vector((0,0,LAYOUT['orbit_lift_m']+.02));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();camdata.type='ORTHO';camdata.ortho_scale=1.85;scene.camera=cam
labelmat=emission('Preview title ivory',(.78,.85,.88));submat=emission('Preview label muted blue',(.40,.54,.62))
def label(text,x,y,size,mat):
 cu=bpy.data.curves.new('Preview caption','FONT');cu.body=text;cu.align_x='LEFT';cu.size=size;cu.extrude=0
 ob=bpy.data.objects.new(text,cu);scene.collection.objects.link(ob);ob.rotation_euler=cam.rotation_euler
 ob.location=cam.matrix_world@Vector((x,y,-2.8));cu.materials.append(mat)
# Camera matrix must be evaluated after assignment.
bpy.context.view_layer.update()
label('PIRANHA STUN',-.81,.48,.065,labelmat)
label('BLENDER ASSET PREVIEW  /  NOT A UNITY CAPTURE',-.81,.405,.022,submat)
label('Original piranha mesh + pixel texture  |  5 fish / 10 crossing trails',-.81,-.48,.025,labelmat)
label('Three tilted orbits  /  reusable 1.6 s loop, no runtime construction',-.81,-.525,.022,submat)
scene.frame_set(12)
preview=ROOT/'Tools/StunEffectAuthoring/PiranhaStun_BlenderPreview.png';scene.render.filepath=str(preview);bpy.ops.render.render(write_still=True)
(ART/'Source~'/'mesh_report.json').write_text(json.dumps({'source':'Assets/Art/Fish/Piranha/piranhahigh.blend','texture':'Assets/Art/Fish/Piranha/piranhahigh.png','fish_length_m':LAYOUT['fish_length_m'],'fish_count':len(fish),'plane_count':len(trails),'orbit_lift_m':LAYOUT['orbit_lift_m'],'loop_seconds':LAYOUT['loop_seconds'],'meshes':stats,'effect_triangles':stats[0]['triangles']*len(fish)+sum(x['triangles'] for x in stats[1:])},indent=2)+'\n')
print(json.dumps(stats))
