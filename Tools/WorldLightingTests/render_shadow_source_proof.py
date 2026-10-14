"""Run with Blender --background --python this.py -- /absolute/output/directory.
Diagnostic source-art shadow study, explicitly NOT Unity rendering. No project asset edits.
"""
import bpy,math,json,sys,hashlib
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
OUT=Path(args[0]) if args else ROOT.parent/'artwork_shadow_proof_20261014';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=64;s.cycles.use_denoising=False;s.cycles.seed=8173
s.render.resolution_x=1280;s.render.resolution_y=720;s.render.resolution_percentage=100
s.render.image_settings.file_format='PNG';s.world.color=(.03,.03,.03);s.view_settings.view_transform='Standard';s.view_settings.look='Medium High Contrast';s.view_settings.exposure=0
s.render.use_stamp=True;s.render.use_stamp_note=True;s.render.stamp_note_text='SOURCE SHADOW STUDY | Original artwork, Blender Cycles approximation. NOT Unity GPU validation.';s.render.stamp_font_size=15
for p in ['date','time','render_time','frame','memory','hostname','camera','lens','scene','marker','filename','sequencer_strip']:
 if hasattr(s.render,'use_stamp_'+p):setattr(s.render,'use_stamp_'+p,False)
def principled(name,color):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True;n=m.node_tree.nodes.get('Principled BSDF');n.inputs['Base Color'].default_value=(*color,1);n.inputs['Roughness'].default_value=.9;return m
def cube(name,pos,size,mat):
 bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.name=name;o.scale=size;o.data.materials.append(mat);return o
floor=principled('Diagnostic neutral receiving floor',(.31,.28,.26));wall=principled('Diagnostic blue grey receiving wall',(.19,.22,.26))
cube('Receiving floor',(0,1,-.13),(15,12,.2),floor);cube('Receiving backdrop',(0,1.75,2.7),(15,.15,6),wall)
# A visible physical slat intercepts the same lamp and casts across the fish, testing reception.
cube('Physical shadow-casting slat',(-2.05,-.9,3.25),(.2,.2,3.4),principled('Slat',(.16,.12,.09)))
data=bpy.data.lights.new('Directional approximation of authored key','SUN');data.energy=2.2;data.angle=math.radians(2);light=bpy.data.objects.new(data.name,data);s.collection.objects.link(light);light.rotation_euler=(math.radians(40),math.radians(-25),math.radians(-22))
# Extra bounded point source demonstrates a second source-shaped shadow, rather than a painted blob.
data=bpy.data.lights.new('Punctual approximation','AREA');data.energy=160;data.shape='DISK';data.size=.3;o=bpy.data.objects.new(data.name,data);s.collection.objects.link(o);o.location=(3,-4,5);o.rotation_euler=(Vector((0,0,1.2))-o.location).to_track_quat('-Z','Y').to_euler()
# Alpha bounds measured from the originals, not replacement silhouettes.
items=[('Fish','Assets/Art/HotelFish/FISH-right.png',(2048,2048),(586,45,1417,1737),-2.65,2.6),('Sconce','Assets/Art/Hallway/HALLWAY-sconce.png',(1173,1714),(423,521,733,992),0,2.1),('Dining cart','Assets/Art/Traps/TRAP-Hotel-cart.png',(2048,2048),(201,721,1860,1813),3,2.1)]
objects=[];materials=[]
for name,path,wh,bb,x,height in items:
 w,h=wh;planeh=height/((bb[3]-bb[1])/h);planew=planeh*w/h;bottom=.08-(1-bb[3]/h)*planeh
 # Center only the visible original extent; preserve the full imported image UV canvas.
 center=x-( (bb[0]+bb[2])/2/w-.5)*planew
 verts=[(center-planew/2,0,bottom),(center+planew/2,0,bottom),(center+planew/2,0,bottom+planeh),(center-planew/2,0,bottom+planeh)]
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],[(0,1,2,3)]);mesh.uv_layers.new(name='Original full canvas')
 for poly in mesh.polygons:
  for li,uv in zip(poly.loop_indices,[(0,0),(1,0),(1,1),(0,1)]):mesh.uv_layers[0].data[li].uv=uv
 obj=bpy.data.objects.new(name,mesh);s.collection.objects.link(obj);objects.append(obj)
 mat=bpy.data.materials.new(name+' original alpha');mat.use_nodes=True;n=mat.node_tree.nodes;n.clear();l=mat.node_tree.links
 tex=n.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(ROOT/path));tex.interpolation='Linear'
 trans=n.new('ShaderNodeBsdfTransparent');em=n.new('ShaderNodeEmission');l.new(tex.outputs['Color'],em.inputs['Color']);em.inputs['Strength'].default_value=1
 diffuse=n.new('ShaderNodeBsdfDiffuse');l.new(tex.outputs['Color'],diffuse.inputs['Color']);diffuse.inputs['Roughness'].default_value=1
 add=n.new('ShaderNodeAddShader');l.new(em.outputs[0],add.inputs[0]);l.new(diffuse.outputs[0],add.inputs[1])
 lightpath=n.new('ShaderNodeLightPath');cut=n.new('ShaderNodeMath');cut.operation='GREATER_THAN';cut.inputs[1].default_value=.2;l.new(tex.outputs['Alpha'],cut.inputs[0])
 alphamix=n.new('ShaderNodeMix');alphamix.data_type='FLOAT';l.new(lightpath.outputs['Is Shadow Ray'],alphamix.inputs[0]);l.new(tex.outputs['Alpha'],alphamix.inputs[2]);l.new(cut.outputs[0],alphamix.inputs[3])
 mix=n.new('ShaderNodeMixShader');l.new(alphamix.outputs[0],mix.inputs[0]);l.new(trans.outputs[0],mix.inputs[1]);l.new(em.outputs[0],mix.inputs[2]);out=n.new('ShaderNodeOutputMaterial');l.new(mix.outputs[0],out.inputs[0]);obj.data.materials.append(mat);materials.append((mat,em,add,mix))
camd=bpy.data.cameras.new('Shared diagnostic camera');cam=bpy.data.objects.new(camd.name,camd);s.collection.objects.link(cam);cam.location=(7,-15,7);target=Vector((.25,.6,1.25));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();camd.type='ORTHO';camd.ortho_scale=10.9;s.camera=cam
for state in ['before-unlit-no-caster','after-lit-alpha-caster']:
 after=state.startswith('after')
 for obj,(mat,em,add,mix) in zip(objects,materials):
  obj.visible_shadow=after;em.inputs['Strength'].default_value=.48 if after else 1
  mat.node_tree.links.new(add.outputs[0] if after else em.outputs[0],mix.inputs[2])
 s.render.stamp_note_text=('AFTER: lit + original-alpha shadows' if after else 'BEFORE: unlit + no artwork shadows')+' | SOURCE STUDY, Blender Cycles; NOT Unity GPU evidence.'
 s.render.filepath=str(OUT/(state+'.png'));bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'original-art-shadow-study.blend'))
(OUT/'proof-provenance.json').write_text(json.dumps({'source_assets':[{'path':x[1],'sha256':hashlib.sha256((ROOT/x[1]).read_bytes()).hexdigest()} for x in items],'purpose':'Identical diagnostic camera/geometry/light before and after original-art alpha casting/reception. Uses supplied image pixels directly.','shader_parameters':{'ambient_after':.48,'shadow_alpha_cutoff':.2},'limitation':'Cycles source approximation; does not import/compile URP, validate native point/directional shadow maps, final scene exposure or performance.','script':'Tools/WorldLightingTests/render_shadow_source_proof.py'},indent=2)+'\n')
