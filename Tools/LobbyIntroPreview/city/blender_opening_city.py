#!/usr/bin/env python3
"""Blender build of the intro's opening shot: a front-on, fog-drowned city of primitive blocks (photo facades on the
fronts, chimneys on the roofs) plus a volumetric cumulus bank. Everything is rendered as separate transparent layers so
the Unity prefab can stack them (sky / far / mid / back / near / clouds).

    /tmp/bpyenv/bin/python blender_opening_city.py --out Assets/Art/LobbyIntro/City [--scale 1.0] [--samples 24]

Facades come from facades/photo_*.jpg when present (real building photos), otherwise facades/placeholder_*.png.
The camera looks straight down +Y from rooftop height: no yaw, no pitch, no roll. Perspective is plain and symmetric.
"""
import argparse, math, random, sys
from pathlib import Path
import bpy

HERE = Path(__file__).resolve().parent
argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
ap = argparse.ArgumentParser()
ap.add_argument('--out', type=Path, required=True); ap.add_argument('--scale', type=float, default=1.0)
ap.add_argument('--samples', type=int, default=24); ap.add_argument('--only', default='')
a = ap.parse_args(argv)
a.out.mkdir(parents=True, exist_ok=True)
W, H = int(1600 * a.scale), int(900 * a.scale)

FOG = (0.44, 0.53, 0.64, 1.0)
CAM_Z = 24.0
# name, distance (m), block width range, block height range, chimney chance, fog start/end (m), count seed
ROWS = [('Far', 300, (16, 34), (16, 34), .4, (60, 330), 11),
        ('Mid', 190, (14, 30), (14, 30), .5, (40, 300), 23),
        ('Back', 120, (12, 26), (12, 26), .55, (30, 280), 37),
        ('Near', 70, (10, 22), (10, 22), .65, (20, 260), 41)]

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = a.samples
sc.cycles.use_denoising = True
sc.render.resolution_x, sc.render.resolution_y = W, H
sc.render.film_transparent = True
sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGBA'

cam_d = bpy.data.cameras.new('cam'); cam_d.lens = 28; cam_d.sensor_width = 36
cam = bpy.data.objects.new('cam', cam_d); sc.collection.objects.link(cam); sc.camera = cam
cam.location = (0, 0, CAM_Z); cam.rotation_euler = (math.radians(90), 0, 0)      # straight down +Y, level
cam_d.shift_y = -0.10                                                                  # sky gets the top third; no tilt


def facade_images():
    photos = sorted((HERE / 'facades').glob('photo_*.jpg'))
    files = photos or [p for p in sorted((HERE / 'facades').glob('placeholder_*.png')) if not p.stem.endswith('_lit')]
    return [bpy.data.images.load(str(p)) for p in files]


IMAGES = facade_images()


def facade_mat(i, image, fog0, fog1, row, flat=None):
    m = bpy.data.materials.new(f'{row}_facade_{i}'); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear(); n = nt.nodes.new
    out, em, tx, mp = n('ShaderNodeOutputMaterial'), n('ShaderNodeEmission'), n('ShaderNodeTexImage'), n('ShaderNodeMapping')
    tc, sep, comb = n('ShaderNodeTexCoord'), n('ShaderNodeSeparateXYZ'), n('ShaderNodeCombineXYZ')
    if image is not None:
        tx.image = image
    tx.extension = 'REPEAT'
    mp.inputs['Scale'].default_value = (1 / 12, 1 / 12, 1 / 12)                    # one facade tile per 12 m
    nt.links.new(tc.outputs['Object'], sep.outputs and sep.inputs[0])
    nt.links.new(sep.outputs['X'], comb.inputs['X']); nt.links.new(sep.outputs['Z'], comb.inputs['Y'])
    nt.links.new(comb.outputs[0], mp.inputs['Vector']); nt.links.new(mp.outputs[0], tx.inputs['Vector'])
    dark = n('ShaderNodeMix'); dark.data_type = 'RGBA'; dark.inputs[0].default_value = .45
    dark.inputs[6].default_value = (.12, .15, .20, 1)                                                               # pull facade toward ink
    if image is not None:
        nt.links.new(tx.outputs['Color'], dark.inputs[7])
    else:
        dark.inputs[0].default_value = 0; dark.inputs[6].default_value = flat
    depth = n('ShaderNodeCameraData') if False else n('ShaderNodeObjectInfo')
    cd = n('ShaderNodeNewGeometry'); vz = n('ShaderNodeVectorMath'); vz.operation = 'DISTANCE'
    cp = n('ShaderNodeCombineXYZ'); cp.inputs['X'].default_value = 0; cp.inputs['Y'].default_value = 0; cp.inputs['Z'].default_value = CAM_Z
    nt.links.new(cd.outputs['Position'], vz.inputs[0]); nt.links.new(cp.outputs[0], vz.inputs[1])
    mr = n('ShaderNodeMapRange'); mr.inputs['From Min'].default_value = fog0; mr.inputs['From Max'].default_value = fog1
    mr.inputs['To Min'].default_value = .18; mr.inputs['To Max'].default_value = .86; mr.clamp = True
    nt.links.new(vz.outputs['Value'], mr.inputs['Value'])
    fogmix = n('ShaderNodeMix'); fogmix.data_type = 'RGBA'; fogmix.inputs[7].default_value = FOG
    nt.links.new(mr.outputs[0], fogmix.inputs[0]); nt.links.new(dark.outputs[2], fogmix.inputs[6])
    nt.links.new(fogmix.outputs[2], em.inputs['Color']); nt.links.new(em.outputs[0], out.inputs[0])
    return m


def box(name, x0, x1, y, z0, z1, depth, mat, coll):
    verts = [(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1), (x0, y + depth, z0), (x1, y + depth, z0), (x1, y + depth, z1), (x0, y + depth, z1)]
    faces = [(0, 1, 2, 3), (4, 5, 1, 0), (1, 5, 6, 2), (5, 4, 7, 6), (4, 0, 3, 7), (3, 2, 6, 7)]
    me = bpy.data.meshes.new(name); me.from_pydata(verts, [], faces); me.update(); me.materials.append(mat)
    ob = bpy.data.objects.new(name, me); coll.objects.link(ob); return ob


import bmesh
from mathutils import Matrix


def tilted_slab(name, x, y, z0, x1, z1, thick, mat, coll):
    """A thin slab from (x, z0) up/down to (x1, z1) in the XZ plane, `thick` deep in Y: one stair flight."""
    dx, dz = x1 - x, z1 - z0; ln = math.hypot(dx, dz); w = .28
    nx, nz = -dz / ln * w / 2, dx / ln * w / 2
    v = [(x - nx, y, z0 - nz), (x + nx, y, z0 + nz), (x1 + nx, y, z1 + nz), (x1 - nx, y, z1 - nz)]
    verts = v + [(a_, y + thick, c_) for a_, _, c_ in v]
    me = bpy.data.meshes.new(name); me.from_pydata(verts, [], [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]); me.update(); me.materials.append(mat)
    ob = bpy.data.objects.new(name, me); coll.objects.link(ob); return ob


def fire_escape(name, x, y, top, mat, coll, width=4.6, floors=4, storey=3.4):
    for f in range(floors):
        z = top - 1.0 - f * storey
        box(f'{name}_landing_{f}', x, x + width, y, z - .15, z, 1.5, mat, coll)
        box(f'{name}_post_a{f}', x, x + .12, y - .02, z, z + 1.0, .1, mat, coll); box(f'{name}_post_b{f}', x + width - .12, x + width, y - .02, z, z + 1.0, .1, mat, coll)
        box(f'{name}_toprail_{f}', x, x + width, y - .02, z + .95, z + 1.05, .1, mat, coll)
        if f < floors - 1:                                                                         # stair flight to the landing below, alternating sides
            a_, b_ = (x + .4, x + width - .6) if f % 2 == 0 else (x + width - .6, x + .4)
            tilted_slab(f'{name}_stair_{f}', a_, y + .2, z - .15, b_, z - storey, 1.0, mat, coll)


def water_tank(name, cx, cy, top, mat, coll, radius=2.2, height=4.2, legs=3.0):
    for i, (dx, dy) in enumerate([(-1.5, -1.5), (1.5, -1.5), (-1.5, 1.5), (1.5, 1.5)]):
        box(f'{name}_leg_{i}', cx + dx - .15, cx + dx + .15, cy + dy - .15, top, top + legs, .3, mat, coll)
    box(f'{name}_deck', cx - 2.2, cx + 2.2, cy - 2.2, top + legs - .1, top + legs + .1, 4.4, mat, coll)
    for kind, r1, r2, depth, zc in (('body', radius, radius, height, top + legs + .1 + height / 2), ('cap', radius + .15, .15, 1.6, top + legs + .1 + height + .8)):
        bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=18, radius1=r1, radius2=r2, depth=depth, matrix=Matrix.Translation((cx, cy, zc)))
        me = bpy.data.meshes.new(f'{name}_{kind}'); bm.to_mesh(me); bm.free(); me.materials.append(mat)
        coll.objects.link(bpy.data.objects.new(f'{name}_{kind}', me))


layers = {}
for name, dist, bw, bh, chim, (f0, f1), seed in ROWS:
    rnd = random.Random(seed); coll = bpy.data.collections.new(name); sc.collection.children.link(coll); layers[name] = coll
    mats = [facade_mat(i, im, f0, f1, name) for i, im in enumerate(IMAGES)]
    flat_mats = {'wood': facade_mat(90, None, f0, f1, name, (.22, .17, .14, 1)), 'iron': facade_mat(91, None, f0, f1, name, (.07, .08, .10, 1))}
    half = dist * math.tan(math.atan(18 / 28)) * 1.25                                    # a little wider than the frame
    x = -half; n_ = 0
    while x < half:
        w, h = rnd.uniform(*bw), rnd.uniform(*bh); m = rnd.choice(mats)
        top = h + rnd.uniform(0, 4)
        box(f'{name}_block_{n_}', x, x + w, dist + rnd.uniform(-4, 4), -30, top, rnd.uniform(14, 30), m, coll)
        if rnd.random() < chim:                                                          # water tank on stilts: barrel with a cone cap
            water_tank(f'{name}_tank_{n_}', x + rnd.uniform(w * .25, w * .75), dist + 6, top, flat_mats['wood'], coll)
        if rnd.random() < .7:                                                            # iron fire escape zig-zagging down the face
            fire_escape(f'{name}_escape_{n_}', x + rnd.uniform(1.5, max(1.6, w - 6)), dist - .2, top, flat_mats['iron'], coll)
        x += w - rnd.uniform(0, 2); n_ += 1
    print(name, n_, 'blocks')

# soft world so the fog colour shows through gaps in the Mid layers and the sky layer has a gradient
world = bpy.data.worlds.new('w'); sc.world = world; world.use_nodes = True
nt = world.node_tree; nt.nodes.clear(); n = nt.nodes.new
bg, out, ramp, tc, sep = n('ShaderNodeBackground'), n('ShaderNodeOutputWorld'), n('ShaderNodeValToRGB'), n('ShaderNodeTexCoord'), n('ShaderNodeSeparateXYZ')
nt.links.new(tc.outputs['Window'], sep.inputs[0]); nt.links.new(sep.outputs['Y'], ramp.inputs[0])
ramp.color_ramp.elements[0].position, ramp.color_ramp.elements[0].color = .30, (.66, .73, .81, 1)
ramp.color_ramp.elements[1].position, ramp.color_ramp.elements[1].color = .95, (.28, .36, .48, 1)
nt.links.new(ramp.outputs[0], bg.inputs['Color']); nt.links.new(bg.outputs[0], out.inputs[0])


def render(name, show):
    for cname, coll in layers.items(): 
        for ob in coll.objects: ob.hide_render = cname not in show
    for ob in clouds.objects: ob.hide_render = 'Clouds' not in show
    sc.render.film_transparent = name != 'FogSky'
    sc.render.filepath = str(a.out / f'{name}.png'); bpy.ops.render.render(write_still=True)


# ---- cloud bank: a noise volume in a flattened box, lit by a sun from above-left
clouds = bpy.data.collections.new('Clouds'); sc.collection.children.link(clouds)
vol = bpy.data.objects.new('cloud_volume', bpy.data.meshes.new('cv'))
bm_v = [(-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1), (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)]
vol.data.from_pydata(bm_v, [], [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]); vol.data.update()
import bmesh
_b = bmesh.new(); _b.from_mesh(vol.data); bmesh.ops.recalc_face_normals(_b, faces=_b.faces); _b.to_mesh(vol.data); _b.free()
vol.location = (-150, 380, CAM_Z + 52); vol.scale = (150, 55, 30); clouds.objects.link(vol)
vm = bpy.data.materials.new('cloud_vol'); vm.use_nodes = True; vt = vm.node_tree; vt.nodes.clear(); n = vt.nodes.new
vo, pv, tcv, nz1, nz2, mul, gr, sepv, mr2 = n('ShaderNodeOutputMaterial'), n('ShaderNodeVolumePrincipled'), n('ShaderNodeTexCoord'), n('ShaderNodeTexNoise'), n('ShaderNodeTexNoise'), n('ShaderNodeMath'), n('ShaderNodeMath'), n('ShaderNodeSeparateXYZ'), n('ShaderNodeMapRange')
nz1.inputs['Scale'].default_value = 1.8; nz1.inputs['Detail'].default_value = 9; nz1.inputs['Roughness'].default_value = .62
nz2.inputs['Scale'].default_value = 5.5; nz2.inputs['Detail'].default_value = 6
vt.links.new(tcv.outputs['Object'], nz1.inputs['Vector']); vt.links.new(tcv.outputs['Object'], nz2.inputs['Vector'])
add = n('ShaderNodeMath'); add.operation = 'ADD'; vt.links.new(nz1.outputs['Fac'], add.inputs[0]); vt.links.new(nz2.outputs['Fac'], add.inputs[1]); add.use_clamp = False
mulh = n('ShaderNodeMath'); mulh.operation = 'MULTIPLY'; mulh.inputs[1].default_value = .5; vt.links.new(add.outputs[0], mulh.inputs[0])
vt.links.new(tcv.outputs['Object'], sepv.inputs[0])
sq = n('ShaderNodeMath'); sq.operation = 'POWER'; sq.inputs[1].default_value = 2; vt.links.new(sepv.outputs['X'], sq.inputs[0])
sqz = n('ShaderNodeMath'); sqz.operation = 'POWER'; sqz.inputs[1].default_value = 2; vt.links.new(sepv.outputs['Z'], sqz.inputs[0])
fall = n('ShaderNodeMath'); fall.operation = 'ADD'; vt.links.new(sq.outputs[0], fall.inputs[0]); vt.links.new(sqz.outputs[0], fall.inputs[1])
inv = n('ShaderNodeMath'); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1.0; vt.links.new(fall.outputs[0], inv.inputs[1]); inv.use_clamp = True
dens = n('ShaderNodeMapRange'); dens.inputs['From Min'].default_value = .42; dens.inputs['From Max'].default_value = .62; dens.clamp = True; vt.links.new(mulh.outputs[0], dens.inputs['Value'])
env = n('ShaderNodeMath'); env.operation = 'MULTIPLY'; vt.links.new(dens.outputs['Result'], env.inputs[0]); vt.links.new(inv.outputs[0], env.inputs[1])
fin = n('ShaderNodeMath'); fin.operation = 'MULTIPLY'; fin.inputs[1].default_value = .04; fin.use_clamp = True; vt.links.new(env.outputs[0], fin.inputs[0])
vt.links.new(fin.outputs[0], pv.inputs['Density']); pv.inputs['Color'].default_value = (.95, .97, 1, 1); pv.inputs['Anisotropy'].default_value = .35; pv.inputs['Emission Strength'].default_value = .5; pv.inputs['Emission Color'].default_value = (.92, .95, 1, 1)
vt.links.new(pv.outputs[0], vo.inputs['Volume']); vol.data.materials.append(vm)
for i, (cx, cz, sx, sz) in enumerate([(60, 70, 130, 26), (230, 48, 120, 30)]):        # sibling puffs share mesh and material
    v2 = bpy.data.objects.new(f'cloud_volume_{i}', vol.data); v2.location = (cx, 380, CAM_Z + cz); v2.scale = (sx, 55, sz); v2.rotation_euler = (0, 0, math.radians(37 * (i + 1))); clouds.objects.link(v2)
sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN')); sun.data.energy = 14.0; sun.rotation_euler = (math.radians(52), 0, math.radians(-28)); sc.collection.objects.link(sun)
sc.cycles.volume_bounces = 3; sc.cycles.volume_max_steps = 256

jobs = [('FogSky', set()), ('OpeningCityFar', {'Far'}), ('OpeningCityMid', {'Mid'}), ('OpeningCityBack', {'Back'}), ('OpeningCityNear', {'Near'}), ('CloudBank', {'Clouds'})]
for name, show in jobs:
    if a.only and a.only != name: continue
    render(name, show)
print('done')
