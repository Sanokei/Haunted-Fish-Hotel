"""Blender preview of the lobby introduction's world shots, ending in the crane + tilt into the frame tunnel.

    python render_lobby_preview.py --out frames_dir [--fps 24] [--width 960] [--samples 16] [--only-range 0 60]

A *source approximation* for review, not Unity output. It reads the same data Unity uses:
 * camera markers, fish markers, the tunnel camera, the dolly/tilt curves and the 60 degree lens from Lobby.unity
   (mirrored here as constants and checked against the scene by `check_scene_constants`);
 * every frame transform, opening mask, eye rectangle and depth gain from Tools/WatchingFrameTests/intro-tunnel-placement.json;
 * the real lobby blockout FBX for the room below 4.25 m, and the original frame/eye artwork.
The room above is the manifest's footprint extruded to Y80. Unity lighting, shaders and the real fish sprite
animation are not reproduced; the fish is a flat cut-out of FISH-right.png.

Unity (x, y, z) -> Blender (x, z, y), the handedness-preserving swap.
"""
import argparse
import json
import math
import re
import sys
from functools import lru_cache
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / 'Tools/WatchingFrameTests/intro-tunnel-placement.json'
CACHE = Path('/tmp/lobby_preview_frame_textures')

# --- Authored in Lobby.unity (verified by check_scene_constants) -------------------------------------------
ENTRY = ((0, 1.55, -4.8), (0.10911677, 0, 0, 0.9940289))           # Unity position, quaternion (x, y, z, w)
RECEPTION = ((0, 1.65, 3.2), (0.08478722, 0, 0, 0.9963991))
TUNNEL = ((0, 8.5, 3.2), (-0.7009093, 0, 0, 0.7132504))
FISH_START, FISH_END = (0, 0.85, -1.65), (1.7, 0.85, 2.5)
FOV_START, FOV_TUNNEL = 60.000004, 24.0
FBX_DEPTH_OFFSET = -2.95   # FBX floor spans z 0..12; the Unity lobby front plane sits at z -2.95
# Timeline from LobbyIntroduction.ink: entry 1.5, walk_away 4.0, caption 4.5, reception 4.0, tunnel 6.0, wait 1.0
T_ENTRY, T_WALK, T_LINE, T_RECEPTION, T_TUNNEL, T_END = 1.5, 4.0, 4.5, 4.0, 6.0, 1.0
CAPTION = 'Looks like nobody has checked into this place for years'
WALL_COLOUR = (0.64, 0.62, 0.55)  # HotelLobbyColor6.mat
SHELL_TOP = 4.25                  # the FBX shell height; the extension takes over above it


def check_scene_constants():
    """Fail loudly if the Lobby scene drifts from the constants above."""
    text = (ROOT / 'Assets/Scenes/Lobby.unity').read_text(encoding='utf-8')

    def block(fid):
        match = re.search(r'--- !u!\d+ &%d\n(.*?)(?=\n--- !u!)' % fid, text, re.S)
        return match.group(1)

    def vec(body, key):
        return [float(v) for v in re.search(key + r': \{([^}]*)\}', body).group(1).replace(' ', '').replace('x:', '').replace('y:', '').replace('z:', '').replace('w:', '').split(',')]

    for fid, (pos, rot) in {47000005: ENTRY, 47000011: RECEPTION, 47000022: TUNNEL}.items():
        body = block(fid)
        assert np.allclose(vec(body, 'm_LocalPosition'), pos, atol=1e-4), fid
        assert np.allclose(vec(body, 'm_LocalRotation'), rot, atol=1e-4), fid
    assert np.allclose(vec(block(47000007), 'm_LocalPosition'), FISH_START, atol=1e-4)
    assert np.allclose(vec(block(47000009), 'm_LocalPosition'), FISH_END, atol=1e-4)


def B(v):
    """Unity vector -> Blender vector."""
    return Vector((v[0], v[2], v[1]))


def unity_basis(q):
    """Right, up, forward of a Unity quaternion (x, y, z, w)."""
    quat = Quaternion((q[3], q[0], q[1], q[2]))
    return quat @ Vector((1, 0, 0)), quat @ Vector((0, 1, 0)), quat @ Vector((0, 0, 1))


def matrix_from_unity(position, q):
    """World matrix for a Unity object: Blender camera looks down -Z and a Unity Quad shows its face along -forward,
    so both use the columns [right, up, -forward]."""
    right, up, forward = unity_basis(q)
    m = Matrix((B(right), B(up), -B(forward))).transposed()
    m.resize_4x4()
    m.translation = B(position)
    return m


def smoothstep(t):
    t = min(1, max(0, t))
    return t * t * (3 - 2 * t)


def timeline(time):
    """Camera (position, quaternion), fov and fish position at `time` seconds."""
    t0 = T_ENTRY
    t1 = t0 + T_WALK
    t2 = t1 + T_LINE
    t3 = t2 + T_RECEPTION
    t4 = t3 + T_TUNNEL
    lerp = lambda a, b, k: tuple(a[i] + (b[i] - a[i]) * k for i in range(3))
    q = lambda r: Quaternion((r[3], r[0], r[1], r[2]))
    fish = lerp(FISH_START, FISH_END, min(1, max(0, (time - t0) / T_WALK)))
    walking = t0 <= time < t1
    pos, rot, fov = ENTRY[0], ENTRY[1], FOV_START
    if t2 <= time < t3 + 1e-9:
        k = min(1, (time - t2) / T_RECEPTION)
        pos = lerp(ENTRY[0], RECEPTION[0], k)
        rot = tuple(q(ENTRY[1]).slerp(q(RECEPTION[1]), k)[i] for i in (1, 2, 3, 0))
    elif time > t3:
        k = min(1, (time - t3) / T_TUNNEL)
        dolly = smoothstep(k)                 # _TunnelDolly: ease in/out
        tilt = smoothstep((k - .2) / .8)      # _TunnelTilt: holds 20%, then ease
        pos = lerp(RECEPTION[0], TUNNEL[0], dolly)
        rot = tuple(q(RECEPTION[1]).slerp(q(TUNNEL[1]), tilt)[i] for i in (1, 2, 3, 0))
        fov = FOV_START + (FOV_TUNNEL - FOV_START) * tilt
    return pos, rot, fov, fish, walking


@lru_cache(maxsize=None)
def frame_texture(source, opening, backing, has_eye, eye_rect, gain_free=True):
    """Pre-composite one frame exactly as the authored shader does (see intro-tunnel-placement.json `eye.composition`)."""
    CACHE.mkdir(exist_ok=True, parents=True)
    key = CACHE / (Path(source).stem + ('-eye' if has_eye else '') + '.png')
    if key.exists():
        return str(key)
    f = np.asarray(Image.open(ROOT / source).convert('RGBA')).astype(np.float32) / 255
    h, w = f.shape[:2]
    op = np.asarray(Image.open(ROOT / opening).convert('RGBA').resize((w, h))).astype(np.float32)[..., 3] / 255
    bk = np.asarray(Image.open(ROOT / backing).convert('RGBA').resize((w, h))).astype(np.float32)[..., 3] / 255
    interior = np.array([32, 32, 32], np.float32) / 255
    eye_a = np.zeros((h, w), np.float32)
    eye_rgb = np.zeros((h, w, 3), np.float32)
    if has_eye:
        art = ROOT / 'Assets/Art/Frames/Eye'
        cx, cy, ew, eh = eye_rect
        size = (max(1, round(ew * w)), max(1, round(eh * h)))
        load = lambda n: np.asarray(Image.open(art / n).convert('RGBA').resize(size, Image.LANCZOS)).astype(np.float32) / 255
        pupil, outline, mask = load('HALLWAY-Frame-Eye.png'), load('HALLWAY-Frame-Eye-outline.png'), load('HALLWAY-Frame-Eye-interior-mask.png')
        pa = pupil[..., 3] * mask[..., 3]
        oa = outline[..., 3]
        a = oa + pa * (1 - oa)
        rgb = outline[..., :3] * oa[..., None] + pupil[..., :3] * (pa * (1 - oa))[..., None]
        x0 = round((cx - ew / 2) * w)
        y0 = round((1 - cy - eh / 2) * h)      # eye rect is centred, v up
        xs, ys = max(0, x0), max(0, y0)
        xe, ye = min(w, x0 + size[0]), min(h, y0 + size[1])
        eye_a[ys:ye, xs:xe] = a[ys - y0:ye - y0, xs - x0:xe - x0]
        eye_rgb[ys:ye, xs:xe] = rgb[ys - y0:ye - y0, xs - x0:xe - x0]
    picture = eye_rgb + interior * (1 - eye_a)[..., None]
    fa = f[..., 3]
    rgb = (f[..., :3] * (fa * (1 - op))[..., None] + picture * op[..., None]
           + interior * (bk * (1 - fa) * (1 - op))[..., None])
    alpha = np.clip(fa + bk * (1 - fa), 0, 1)
    out = np.dstack([np.clip(rgb, 0, 1), alpha])
    Image.fromarray((out * 255 + .5).astype(np.uint8), 'RGBA').save(key)
    return str(key)


def emission_material(name, image_path, gain_from_object=True, flat=None):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    new, link = nt.nodes.new, nt.links.new
    out = new('ShaderNodeOutputMaterial')
    tex = new('ShaderNodeTexImage')
    tex.image = bpy.data.images.load(image_path)
    tex.extension = 'CLIP'
    emit = new('ShaderNodeEmission')
    gain = new('ShaderNodeObjectInfo')
    mul = new('ShaderNodeMix'); mul.data_type = 'RGBA'; mul.blend_type = 'MULTIPLY'; mul.inputs[0].default_value = 1.0
    link(tex.outputs['Color'], mul.inputs[6]); link(gain.outputs['Color'], mul.inputs[7])
    link(mul.outputs[2], emit.inputs['Color'])
    clear = new('ShaderNodeBsdfTransparent')
    mix = new('ShaderNodeMixShader')
    link(tex.outputs['Alpha'], mix.inputs['Fac'])
    link(clear.outputs['BSDF'], mix.inputs[1]); link(emit.outputs['Emission'], mix.inputs[2])
    link(mix.outputs['Shader'], out.inputs['Surface'])
    return mat


def quad(name, width, height, matrix):
    mesh = bpy.data.meshes.new(name)
    hw, hh = width / 2, height / 2
    mesh.from_pydata([(-hw, -hh, 0), (hw, -hh, 0), (hw, hh, 0), (-hw, hh, 0)], [], [(0, 1, 2, 3)])
    uv = mesh.uv_layers.new(name='UV')
    for loop, coord in zip(mesh.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
        uv.data[loop.index].uv = coord
    obj = bpy.data.objects.new(name, mesh)
    obj.matrix_world = matrix
    bpy.context.scene.collection.objects.link(obj)
    return obj


def build_room(manifest):
    pts = manifest['footprint_xz']
    top = 80.0
    verts, faces = [], []
    for x, _, z in pts:
        verts.append(B((x, SHELL_TOP, z)))
    for x, _, z in pts:
        verts.append(B((x, top, z)))
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, n + j, n + i))
    mesh = bpy.data.meshes.new('UpperRoom')
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    # Make every wall face the room's centre.
    centre = Vector((0, sum(p[2] for p in pts) / n, 0))
    for poly in mesh.polygons:
        if (poly.center - Vector((centre.x, centre.y, poly.center.z))).dot(poly.normal) > 0:
            poly.flip()
    obj = bpy.data.objects.new('UpperRoom', mesh)
    bpy.context.scene.collection.objects.link(obj)
    mat = bpy.data.materials.new('UpperPlaster')
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    new, link = nt.nodes.new, nt.links.new
    geo = new('ShaderNodeNewGeometry')
    sep = new('ShaderNodeSeparateXYZ'); link(geo.outputs['Position'], sep.inputs['Vector'])
    # gain = exp(-max(0, worldY - 25) * 0.10), the same extinction the authored overlays use.
    sub = new('ShaderNodeMath'); sub.operation = 'SUBTRACT'; sub.inputs[1].default_value = 25.0; link(sep.outputs['Z'], sub.inputs[0])
    clampz = new('ShaderNodeMath'); clampz.operation = 'MAXIMUM'; clampz.inputs[1].default_value = 0.0; link(sub.outputs['Value'], clampz.inputs[0])
    scale = new('ShaderNodeMath'); scale.operation = 'MULTIPLY'; scale.inputs[1].default_value = -0.10; link(clampz.outputs['Value'], scale.inputs[0])
    gain = new('ShaderNodeMath'); gain.operation = 'POWER'; gain.inputs[0].default_value = math.e; link(scale.outputs['Value'], gain.inputs[1])
    colour = new('ShaderNodeRGB'); colour.outputs[0].default_value = (*(c ** 2.2 for c in WALL_COLOUR), 1)
    mul = new('ShaderNodeMix'); mul.data_type = 'RGBA'; mul.blend_type = 'MULTIPLY'; mul.inputs[0].default_value = 1.0
    link(colour.outputs[0], mul.inputs[6]); link(gain.outputs['Value'], mul.inputs[7])
    diffuse = new('ShaderNodeBsdfDiffuse'); link(mul.outputs[2], diffuse.inputs['Color'])
    out = new('ShaderNodeOutputMaterial'); link(diffuse.outputs['BSDF'], out.inputs['Surface'])
    mesh.materials.append(mat)
    # The authored shaft ends in an opaque black cap at Y80.
    cap = bpy.data.meshes.new('BlackCap')
    cap.from_pydata([B((x, top, z)) for x, _, z in pts], [], [tuple(range(n))])
    black = bpy.data.materials.new('Black')
    black.use_nodes = True
    black.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0, 0, 0, 1)
    black.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = 1
    cap.materials.append(black)
    bpy.context.scene.collection.objects.link(bpy.data.objects.new('BlackCap', cap))


def import_lobby():
    bpy.ops.import_scene.fbx(filepath=str(ROOT / 'Assets/Art/Haunted_Fish_Hotel_Lobby_Revised_Unity.fbx'))
    for obj in [o for o in bpy.data.objects if o.parent is None]:
        obj.location.y += FBX_DEPTH_OFFSET
    for mat in bpy.data.materials:
        if not mat.use_nodes:
            continue
        # Blockout materials from the FBX: give them a plain warm-plaster/dark-wood read.
        bsdf = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
        if bsdf:
            bsdf.inputs['Roughness'].default_value = 1.0


def add_lights():
    for x, z in ((-3.5, 0.5), (3.5, 0.5), (-3.0, 5.5), (3.0, 5.5), (0, 8.0)):
        data = bpy.data.lights.new('Lamp', 'POINT')
        data.energy = 150
        data.color = (1.0, 0.78, 0.55)
        data.shadow_soft_size = 0.4
        obj = bpy.data.objects.new('Lamp', data)
        obj.location = B((x, 3.9, z))
        bpy.context.scene.collection.objects.link(obj)


def build_frames(manifest):
    mats = {}
    for p in manifest['placements']:
        eye = p['has_eye']
        tex = frame_texture(p['source'], p['opening_mask'], p['backing_mask'], eye,
                            tuple(p['eye_frame_uv_rect']) if eye else None)
        if tex not in mats:
            mats[tex] = emission_material(Path(tex).stem, tex)
        m = matrix_from_unity(p['position'], p['rotation_quaternion'])
        obj = quad(p['name'], p['width'], p['height'], m)
        obj.data.materials.append(mats[tex])
        g = p['presentation_gain']
        obj.color = (g, g, g, 1)


def build_fish():
    mat = emission_material('Fish', str(ROOT / 'Assets/Art/HotelFish/FISH-right.png'))
    obj = quad('Fish', 1.7, 1.7, Matrix.Identity(4))
    obj.data.materials.append(mat)
    obj.color = (.75, .75, .75, 1)
    return obj


def setup_render(width, samples):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.render.resolution_x = width
    sc.render.resolution_y = width * 9 // 16
    sc.render.image_settings.file_format = 'PNG'
    sc.view_settings.view_transform = 'Standard'
    sc.world = bpy.data.worlds.new('W')
    sc.world.use_nodes = True
    bg = sc.world.node_tree.nodes['Background']
    bg.inputs['Color'].default_value = (0.004, 0.005, 0.008, 1)
    bg.inputs['Strength'].default_value = 1.0
    cam_data = bpy.data.cameras.new('Cam')
    cam_data.sensor_fit = 'VERTICAL'
    cam_data.clip_start, cam_data.clip_end = 0.01, 400
    cam = bpy.data.objects.new('Cam', cam_data)
    sc.collection.objects.link(cam)
    sc.camera = cam
    return cam


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--fps', type=int, default=24)
    parser.add_argument('--width', type=int, default=960)
    parser.add_argument('--samples', type=int, default=16)
    parser.add_argument('--only-range', type=int, nargs=2, default=None)
    args = parser.parse_args(argv)
    check_scene_constants()
    manifest = json.load(open(MANIFEST))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    cam = setup_render(args.width, args.samples)
    import_lobby()
    build_room(manifest)
    add_lights()
    build_frames(manifest)
    fish = build_fish()
    args.out.mkdir(parents=True, exist_ok=True)
    total = T_ENTRY + T_WALK + T_LINE + T_RECEPTION + T_TUNNEL + T_END
    count = round(total * args.fps)
    lo, hi = args.only_range or (0, count)
    for i in range(lo, min(hi, count)):
        time = i / args.fps
        pos, rot, fov, fish_pos, walking = timeline(time)
        cam.matrix_world = matrix_from_unity(pos, rot)
        cam.data.angle_y = math.radians(fov)
        bob = .06 * abs(math.sin(time * 9)) if walking else 0
        # Billboard the fish toward the entry camera plane (Unity -z).
        upright = Matrix(((1, 0, 0), (0, 0, -1), (0, 1, 0))).to_4x4()   # local X->X, Y->up, face normal -> -Y (toward the entry camera)
        fish.matrix_world = Matrix.Translation(B((fish_pos[0], fish_pos[1] + bob, fish_pos[2]))) @ upright
        bpy.context.scene.render.filepath = str(args.out / f'lobby_{i:04d}.png')
        bpy.ops.render.render(write_still=True)
        print(f'{i + 1}/{count} t={time:.2f}s fov={fov:.1f}', flush=True)


main()
