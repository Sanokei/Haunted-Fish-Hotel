"""Render the lobby-intro city as three transparent depth slabs (far, mid, near).

Run with Blender's Python module:   python render_city_layers.py [--samples 48] [--scale 1.0]

The city is plain primitive blocks. Each wall is textured with a building facade tile (real photos named
`photo_*.jpg|png` in `facades/` are preferred; the procedural `placeholder_*` tiles are the fallback). Fog is
computed per pixel from the distance to the camera, so a heavily fogged block loses colour *and* opacity and
lets the existing night-sky layer show through, rather than becoming a flat cut-out.

Output (1600x900 RGBA, same overscan as the sky layer): Assets/Art/LobbyIntro/City/City{Far,Mid,Near}.png
"""
import argparse
import math
import random
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
OUT = ROOT / 'Assets/Art/LobbyIntro/City'
FACADES = HERE / 'facades'
ROOF_IMAGE = []   # one dark flat image shared by every roof

WIDTH, HEIGHT = 1600, 900
FOCAL_MM, SENSOR_MM = 30.0, 36.0
CAMERA_Z = 1.7
HORIZON_CANVAS_Y = -95          # canvas units (y up); block bases hide behind the opaque driveway
TILE_M = 8.0                    # one facade tile = 4 floors x 4 bays
FOG_COLOR = (0.085, 0.112, 0.158)   # matches the intro sky's horizon band (LobbyIntroWeather.shader)
FOG_COLOR_HIGH = (0.026, 0.036, 0.066)  # ...and its darker upper sky, reached by FOG_HIGH_M
FOG_HIGH_M = 170.0

# name, depth range (m), block height range, block width range, fog density, tower chance, seed
LAYERS = [
    ('Far',  (640, 1000), (80, 190), (26, 64), 0.0060, 0.22, 11),
    ('Mid',  (330, 560),  (40, 110), (22, 50), 0.0075, 0.16, 23),
    ('Near', (170, 270),  (24, 62),  (18, 40), 0.0085, 0.10, 37),
]
GROUND_FOG = 0.9        # share of light lost at ground level; clears with height so tower tops ghost through
GROUND_FOG_SCALE_M = 30.0


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
        for item in list(coll):
            coll.remove(item)


def load_facades():
    photos = sorted(p for p in FACADES.glob('photo_*') if p.suffix.lower() in ('.jpg', '.jpeg', '.png'))
    if photos:
        return [(bpy.data.images.load(str(p)), None) for p in photos], 'photos'
    tiles = []
    for color in sorted(FACADES.glob('placeholder_*.png')):
        if color.stem.endswith('_lit'):
            continue
        lit = FACADES / (color.stem + '_lit.png')
        tiles.append((bpy.data.images.load(str(color)), bpy.data.images.load(str(lit)) if lit.exists() else None))
    if not tiles:
        sys.exit('No facade tiles: run make_placeholder_facades.py or add photo_*.jpg files to facades/.')
    for image, lit in tiles:
        if lit:
            lit.colorspace_settings.name = 'Non-Color'
    return tiles, 'placeholders'


def facade_material(name, color_image, lit_image, fog_k, mode='color'):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    new = nt.nodes.new
    link = nt.links.new
    uv = new('ShaderNodeTexCoord')
    tex = new('ShaderNodeTexImage')
    tex.image = color_image
    tex.extension = 'REPEAT'
    tex.interpolation = 'Linear'
    link(uv.outputs['UV'], tex.inputs['Vector'])
    # Per-block brightness variation so identical tiles do not read as copies.
    info = new('ShaderNodeObjectInfo')
    gain = new('ShaderNodeMath'); gain.operation = 'MULTIPLY_ADD'
    gain.inputs[1].default_value = 0.55; gain.inputs[2].default_value = 0.7
    link(info.outputs['Random'], gain.inputs[0])
    tint = new('ShaderNodeMix'); tint.data_type = 'RGBA'; tint.blend_type = 'MULTIPLY'
    tint.inputs['Factor'].default_value = 1.0
    # Mix node socket order for RGBA: A is input 6, B is input 7, Result is output 2.
    link(tex.outputs['Color'], tint.inputs[6])
    link(gain.outputs['Value'], tint.inputs[7])  # float -> grey colour
    diffuse = new('ShaderNodeBsdfDiffuse')
    link(tint.outputs[2], diffuse.inputs['Color'])
    shaded = diffuse.outputs['BSDF']
    if lit_image is not None:
        lit = new('ShaderNodeTexImage'); lit.image = lit_image; lit.extension = 'REPEAT'
        link(uv.outputs['UV'], lit.inputs['Vector'])
        glow = new('ShaderNodeEmission')
        glow.inputs['Color'].default_value = (1.0, 0.62, 0.26, 1.0)
        scale = new('ShaderNodeMath'); scale.operation = 'MULTIPLY'; scale.inputs[1].default_value = 2.0
        link(lit.outputs['Color'], scale.inputs[0])
        link(scale.outputs['Value'], glow.inputs['Strength'])
        add = new('ShaderNodeAddShader')
        link(shaded, add.inputs[0]); link(glow.outputs['Emission'], add.inputs[1])
        shaded = add.outputs['Shader']
    # Fog: exponential in camera distance, denser near the ground.
    geo = new('ShaderNodeNewGeometry')
    dist = new('ShaderNodeVectorMath'); dist.operation = 'DISTANCE'
    dist.inputs[1].default_value = (0.0, 0.0, CAMERA_Z)
    link(geo.outputs['Position'], dist.inputs[0])
    expo = new('ShaderNodeMath'); expo.operation = 'MULTIPLY'; expo.inputs[1].default_value = -fog_k
    link(dist.outputs['Value'], expo.inputs[0])
    atten = new('ShaderNodeMath'); atten.operation = 'POWER'; atten.inputs[0].default_value = math.e
    link(expo.outputs['Value'], atten.inputs[1])
    sep = new('ShaderNodeSeparateXYZ')
    link(geo.outputs['Position'], sep.inputs['Vector'])
    ground = new('ShaderNodeMath'); ground.operation = 'MULTIPLY'; ground.inputs[1].default_value = -1.0 / GROUND_FOG_SCALE_M
    link(sep.outputs['Z'], ground.inputs[0])
    ground_exp = new('ShaderNodeMath'); ground_exp.operation = 'POWER'; ground_exp.inputs[0].default_value = math.e
    link(ground.outputs['Value'], ground_exp.inputs[1])
    haze = new('ShaderNodeMath'); haze.operation = 'MULTIPLY'; haze.inputs[1].default_value = GROUND_FOG
    link(ground_exp.outputs['Value'], haze.inputs[0])
    inv_haze = new('ShaderNodeMath'); inv_haze.operation = 'SUBTRACT'; inv_haze.inputs[0].default_value = 1.0
    link(haze.outputs['Value'], inv_haze.inputs[1])
    clear_air = new('ShaderNodeMath'); clear_air.operation = 'MULTIPLY'
    link(atten.outputs['Value'], clear_air.inputs[0]); link(inv_haze.outputs['Value'], clear_air.inputs[1])
    fog = new('ShaderNodeMath'); fog.operation = 'SUBTRACT'; fog.inputs[0].default_value = 1.0
    link(clear_air.outputs['Value'], fog.inputs[1])
    fog.use_clamp = True
    # Fog colour follows the sky: horizon blue near the ground, darker night sky higher up.
    rise = new('ShaderNodeMapRange'); rise.inputs['From Min'].default_value = 0.0; rise.inputs['From Max'].default_value = FOG_HIGH_M
    rise.interpolation_type = 'SMOOTHSTEP'; rise.clamp = True
    link(sep.outputs['Z'], rise.inputs['Value'])
    fog_tint = new('ShaderNodeMix'); fog_tint.data_type = 'RGBA'
    fog_tint.inputs[6].default_value = (*FOG_COLOR, 1.0); fog_tint.inputs[7].default_value = (*FOG_COLOR_HIGH, 1.0)
    link(rise.outputs['Result'], fog_tint.inputs[0])
    fog_emit = new('ShaderNodeEmission')
    link(fog_tint.outputs[2], fog_emit.inputs['Color'])
    colour_mix = new('ShaderNodeMixShader')
    link(fog.outputs['Value'], colour_mix.inputs['Fac'])
    link(shaded, colour_mix.inputs[1]); link(fog_emit.outputs['Emission'], colour_mix.inputs[2])
    out = new('ShaderNodeOutputMaterial')
    if mode == 'color':
        # Blocks stay opaque inside a layer so fog hides what is behind them, as real fog does.
        link(colour_mix.outputs['Shader'], out.inputs['Surface'])
    else:
        # Opacity pass: how much of the layer survives the fog. Heavy fog fades to nothing so the sky shows through.
        fade = new('ShaderNodeMapRange'); fade.inputs['From Min'].default_value = 0.5; fade.inputs['From Max'].default_value = 1.0
        fade.interpolation_type = 'SMOOTHSTEP'; fade.clamp = True
        fade.inputs['To Min'].default_value = 1.0; fade.inputs['To Max'].default_value = 0.10
        link(fog.outputs['Value'], fade.inputs['Value'])
        grey = new('ShaderNodeEmission')
        combine = new('ShaderNodeCombineXYZ')
        for name_ in ('X', 'Y', 'Z'):
            link(fade.outputs['Result'], combine.inputs[name_])
        link(combine.outputs['Vector'], grey.inputs['Color'])
        link(grey.outputs['Emission'], out.inputs['Surface'])
    return mat


def roof_material(fog_k):
    return facade_material('Roof', ROOF_IMAGE[0], None, fog_k, 'color')


def mask_material(fog_k):
    return facade_material('RoofMask', ROOF_IMAGE[0], None, fog_k, 'alpha')


def block(coll, name, cx, cy, w, d, h, material, roof):
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.new('UV')
    hx, hy = w / 2, d / 2
    base = [bm.verts.new(v) for v in ((cx - hx, cy - hy, 0), (cx + hx, cy - hy, 0), (cx + hx, cy + hy, 0), (cx - hx, cy + hy, 0))]
    top = [bm.verts.new(v) for v in ((cx - hx, cy - hy, h), (cx + hx, cy - hy, h), (cx + hx, cy + hy, h), (cx - hx, cy + hy, h))]
    shift = random.random()
    for i in range(4):
        j = (i + 1) % 4
        face = bm.faces.new((base[i], base[j], top[j], top[i]))
        length = math.dist(base[i].co[:2], base[j].co[:2])
        coords = ((0, 0), (length / TILE_M, 0), (length / TILE_M, h / TILE_M), (0, h / TILE_M))
        for loop, (u, v) in zip(face.loops, coords):
            loop[uv_layer].uv = (u + shift, v)
        face.material_index = 0
    roof_face = bm.faces.new(top)
    roof_face.material_index = 1
    bm.normal_update()
    # Walls must face outward for correct lighting.
    for face in bm.faces:
        centre = face.calc_center_median()
        if face.normal.dot(centre - Vector((cx, cy, h / 2))) < 0:
            face.normal_flip()
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    obj.data.materials.append(material)
    obj.data.materials.append(roof)
    coll.objects.link(obj)
    return obj


def build_layer(name, depth, heights, widths, fog_k, tower_chance, seed, tiles, hfov):
    rng = random.Random(seed)
    random.seed(seed)
    coll = bpy.data.collections.new('City' + name)
    bpy.context.scene.collection.children.link(coll)
    roof = roof_material(fog_k)
    mask_roof = mask_material(fog_k)
    mats = [facade_material(f'{name}_{i}', color, lit, fog_k, 'color') for i, (color, lit) in enumerate(tiles)]
    mask_mats = [facade_material(f'{name}_mask_{i}', color, lit, fog_k, 'alpha') for i, (color, lit) in enumerate(tiles)]
    half = lambda d: d * math.tan(hfov / 2) * 1.12 + 40
    d = depth[0]
    count = 0
    while d < depth[1]:
        thickness = rng.uniform(26, 64)
        cy = d + thickness / 2
        x = -half(cy)
        while x < half(cy):
            w = rng.uniform(*widths)
            h = rng.uniform(*heights)
            if rng.random() < tower_chance:
                h *= rng.uniform(1.5, 2.1)        # the odd tower
            if rng.random() < 0.08:
                x += rng.uniform(w * .2, w * .7)  # a gap
            index = rng.randrange(len(mats))
            obj = block(coll, f'{name}_{count}', x + w / 2, cy, w, thickness, h, mats[index], roof)
            obj['tile'] = index
            x += w + rng.uniform(0, 5)
            count += 1
        d += thickness * rng.uniform(.55, .9)
    return coll, count, mats, mask_mats, roof, mask_roof


def setup_scene(samples, scale):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.render.resolution_x = int(WIDTH * scale)
    sc.render.resolution_y = int(HEIGHT * scale)
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    sc.render.image_settings.color_depth = '8'
    sc.view_settings.view_transform = 'Standard'
    sc.view_settings.look = 'None'
    sc.world = bpy.data.worlds.new('Night')
    sc.world.use_nodes = True
    bg = sc.world.node_tree.nodes['Background']
    bg.inputs['Color'].default_value = (0.05, 0.07, 0.11, 1)
    bg.inputs['Strength'].default_value = 0.8
    cam_data = bpy.data.cameras.new('Camera')
    cam_data.lens = FOCAL_MM
    cam_data.sensor_width = SENSOR_MM
    cam_data.sensor_fit = 'HORIZONTAL'
    cam_data.clip_end = 5000
    cam_data.shift_y = -HORIZON_CANVAS_Y / WIDTH      # positive shift lowers the horizon in frame
    cam = bpy.data.objects.new('Camera', cam_data)
    cam.location = (0, 0, CAMERA_Z)
    cam.rotation_euler = (math.pi / 2, 0, 0)
    sc.collection.objects.link(cam)
    sc.camera = cam
    sun_data = bpy.data.lights.new('Moon', 'SUN')
    sun_data.energy = 0.4
    sun_data.color = (0.62, 0.72, 1.0)
    sun_data.angle = math.radians(8)
    sun = bpy.data.objects.new('Moon', sun_data)
    sun.rotation_euler = (math.radians(58), 0, math.radians(-35))
    sc.collection.objects.link(sun)
    return 2 * math.atan(SENSOR_MM / (2 * FOCAL_MM))


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
    parser = argparse.ArgumentParser()
    parser.add_argument('--samples', type=int, default=48)
    parser.add_argument('--scale', type=float, default=1.0)
    parser.add_argument('--out', type=Path, default=OUT)
    parser.add_argument('--only', default='')
    args = parser.parse_args(argv)
    clear()
    hfov = setup_scene(args.samples, args.scale)
    roof = bpy.data.images.new('RoofTile', 4, 4)
    roof.pixels = [0.035, 0.04, 0.05, 1.0] * 16
    ROOF_IMAGE.append(roof)
    tiles, kind = load_facades()
    print('facade source:', kind, len(tiles), 'tiles')
    args.out.mkdir(parents=True, exist_ok=True)
    collections = {}
    for name, depth, heights, widths, fog_k, tower_chance, seed in LAYERS:
        collections[name] = build_layer(name, depth, heights, widths, fog_k, tower_chance, seed, tiles, hfov)
        print(name, 'blocks:', collections[name][1])
    sc = bpy.context.scene
    for name in collections:
        if args.only and name != args.only:
            continue
        coll, _, mats, mask_mats, roof, mask_roof = collections[name]
        for other, entry in collections.items():
            for obj in entry[0].objects:
                obj.hide_render = other != name
        sc.render.filepath = str(args.out / f'_{name}_color.png')
        sc.view_settings.view_transform = 'Standard'
        bpy.ops.render.render(write_still=True)
        for obj in coll.objects:
            obj.material_slots[0].material = mask_mats[obj['tile']]
            obj.material_slots[1].material = mask_roof
        sc.render.filepath = str(args.out / f'_{name}_mask.png')
        sc.view_settings.view_transform = 'Raw'
        sc.cycles.samples = max(4, args.samples // 4)
        bpy.ops.render.render(write_still=True)
        sc.cycles.samples = args.samples
        final = args.out / f'City{name}.png'
        combine(args.out / f'_{name}_color.png', args.out / f'_{name}_mask.png', final)
        print('wrote', final)


def combine(color_path, mask_path, final):
    from PIL import Image
    import numpy as np
    color = np.asarray(Image.open(color_path).convert('RGBA')).astype(np.float32) / 255
    mask = np.asarray(Image.open(mask_path).convert('RGBA')).astype(np.float32) / 255
    out = color.copy()
    out[..., 3] = color[..., 3] * np.clip(mask[..., 0], 0, 1)
    Image.fromarray((out * 255 + .5).astype(np.uint8), 'RGBA').save(final, optimize=True)
    color_path.unlink(); mask_path.unlink()


main()
