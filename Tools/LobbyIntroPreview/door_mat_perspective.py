#!/usr/bin/env python3
"""Blender visual QA for the door scene. Builds the real 3D set (plaster wall, door plane, floor, doormat lying on it)
with a pinhole camera matched to the intro's door panel (1392x590, door base at panel y=-250, 271 px per metre at the door),
renders it, and writes the screen-space corners of the mat quad so the flat UI sprite can be baked in true perspective.

    /tmp/bpyenv/bin/python door_mat_perspective.py --out /tmp/doorqa
"""
import argparse, json, math, sys
from pathlib import Path
import bpy
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
W, H = 1392, 590
PX_PER_M = 271.0
BASE_ROW = 295 + 250            # door base, in pixel rows from the panel top
CAM_DIST, CAM_H = 3.5, 1.5
MAT_W, MAT_D = 0.9, 0.35        # metres: width along the wall, depth toward the camera

a = argparse.ArgumentParser(); a.add_argument('--out', type=Path, required=True)
args = a.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:])
args.out.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.engine = 'CYCLES'; sc.cycles.samples = 48; sc.cycles.device = 'CPU'
sc.render.resolution_x, sc.render.resolution_y = W, H
sc.render.film_transparent = False


def tex_mat(name, path, alpha=False):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial'); em = nt.nodes.new('ShaderNodeEmission'); tx = nt.nodes.new('ShaderNodeTexImage')
    tx.image = bpy.data.images.load(str(path)); nt.links.new(tx.outputs['Color'], em.inputs['Color'])
    if alpha:
        tr = nt.nodes.new('ShaderNodeBsdfTransparent'); mix = nt.nodes.new('ShaderNodeMixShader')
        nt.links.new(tx.outputs['Alpha'], mix.inputs['Fac']); nt.links.new(tr.outputs[0], mix.inputs[1]); nt.links.new(em.outputs[0], mix.inputs[2]); nt.links.new(mix.outputs[0], out.inputs[0])
        m.blend_method = 'BLEND' if hasattr(m, 'blend_method') else None
    else:
        nt.links.new(em.outputs[0], out.inputs[0])
    return m


def plane(name, corners, mat):
    me = bpy.data.meshes.new(name); me.from_pydata(corners, [], [(0, 1, 2, 3)]); me.update()
    uv = me.uv_layers.new(); 
    for l, c in zip(me.loops, [(0, 0), (1, 0), (1, 1), (0, 1)]): uv.data[l.index].uv = c
    ob = bpy.data.objects.new(name, me); sc.collection.objects.link(ob); me.materials.append(mat); return ob


wall_w, wall_h = W / PX_PER_M, H / PX_PER_M
door = ROOT / 'Assets/Art/Hallway/HALLWAY-Door-full.png'
art = ROOT / 'Assets/Art/LobbyIntro/Door'
plane('wall', [(-6, 0, -1), (6, 0, -1), (6, 0, 4), (-6, 0, 4)], tex_mat('wall', art / 'DoorWallPlaster.png'))
# door sprite: 1172x1714 fitted to 440x644 px at 271 px/m, bottom on the floor
dw, dh = 440.3 / PX_PER_M, 644 / PX_PER_M
plane('door', [(-dw / 2, -.001, 0), (dw / 2, -.001, 0), (dw / 2, -.001, dh), (-dw / 2, -.001, dh)], tex_mat('door', door, True))
plane('floor', [(-6, 0, 0), (6, 0, 0), (6, -8, 0), (-6, -8, 0)], tex_mat('floor', art / 'DoorFloorBoards.png'))
mat_src = art / 'DoorMatGreen_topdown.png'
mat_obj = plane('mat', [(-MAT_W / 2, -.01, .003), (MAT_W / 2, -.01, .003), (MAT_W / 2, -.01 - MAT_D, .003), (-MAT_W / 2, -.01 - MAT_D, .003)], tex_mat('mat', mat_src))
mat_obj.data.uv_layers[0].data  # uv (0,0) is the far-left corner

cam_d = bpy.data.cameras.new('cam'); cam = bpy.data.objects.new('cam', cam_d); sc.collection.objects.link(cam); sc.camera = cam
f_px = PX_PER_M * CAM_DIST
cam_d.sensor_fit = 'HORIZONTAL'; cam_d.sensor_width = 36.0; cam_d.lens = f_px * 36.0 / W
horizon_row = BASE_ROW - CAM_H * PX_PER_M
cam_d.shift_y = (H / 2 - horizon_row) / W * -1
cam.location = (0, -CAM_DIST, CAM_H); cam.rotation_euler = (math.radians(90), 0, 0)
bpy.context.view_layer.update()

corners = {}
for label, v in {'far_left': (-MAT_W / 2, -.01, .003), 'far_right': (MAT_W / 2, -.01, .003), 'near_right': (MAT_W / 2, -.01 - MAT_D, .003), 'near_left': (-MAT_W / 2, -.01 - MAT_D, .003)}.items():
    p = world_to_camera_view(sc, cam, Vector(v)); corners[label] = [round(p.x * W, 2), round((1 - p.y) * H, 2)]
base = world_to_camera_view(sc, cam, Vector((0, 0, 0)))
corners['door_base_row'] = round((1 - base.y) * H, 2)
(args.out / 'mat_corners.json').write_text(json.dumps(corners, indent=1))
sc.render.filepath = str(args.out / 'door_qa.png'); bpy.ops.render.render(write_still=True)
print(json.dumps(corners))
