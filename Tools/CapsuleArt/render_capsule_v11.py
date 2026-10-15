"""Capsule v11: the camera sees only the right wall and looks down into the dark of the hallway.

Out of the dark come tentacles and the angler (lit only by its own lamp); a chandelier swings overhead and the
service cart rolls right, straight at the oblivious bellhop. Original project art only, rendered in Blender (bpy).

    python render_capsule_v11.py [--samples 64]
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/Art'
OUT = ART / 'Capsule'
W, H = 1232, 706
HALL_W, HALL_H = 4.4, 3.2


def B_image(path):
    img = bpy.data.images.load(str(path))
    return img


def emission(name, image, gain=1.0, fog=0.0, tint=(1, 1, 1), alpha=True, repeat=False):
    """Self-lit material: texture * gain * exp(-distance * fog). Alpha cut from the texture."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    new, link = nt.nodes.new, nt.links.new
    tex = new('ShaderNodeTexImage'); tex.image = image
    tex.extension = 'REPEAT' if repeat else 'CLIP'
    geo = new('ShaderNodeNewGeometry')
    dist = new('ShaderNodeVectorMath'); dist.operation = 'DISTANCE'
    dist.inputs[1].default_value = (0.3, -1.0, 1.5)
    link(geo.outputs['Position'], dist.inputs[0])
    mul = new('ShaderNodeMath'); mul.operation = 'MULTIPLY'; mul.inputs[1].default_value = -fog
    link(dist.outputs['Value'], mul.inputs[0])
    att = new('ShaderNodeMath'); att.operation = 'POWER'; att.inputs[0].default_value = math.e
    link(mul.outputs['Value'], att.inputs[1])
    g = new('ShaderNodeMath'); g.operation = 'MULTIPLY'; g.inputs[1].default_value = gain
    link(att.outputs['Value'], g.inputs[0])
    col = new('ShaderNodeMix'); col.data_type = 'RGBA'; col.blend_type = 'MULTIPLY'; col.inputs[0].default_value = 1
    link(tex.outputs['Color'], col.inputs[6])
    tint_node = new('ShaderNodeRGB'); tint_node.outputs[0].default_value = (*tint, 1)
    link(tint_node.outputs[0], col.inputs[7])
    col2 = new('ShaderNodeMix'); col2.data_type = 'RGBA'; col2.blend_type = 'MULTIPLY'; col2.inputs[0].default_value = 1
    link(col.outputs[2], col2.inputs[6]); link(g.outputs['Value'], col2.inputs[7])
    emit = new('ShaderNodeEmission'); link(col2.outputs[2], emit.inputs['Color'])
    out = new('ShaderNodeOutputMaterial')
    if alpha:
        clear = new('ShaderNodeBsdfTransparent'); mix = new('ShaderNodeMixShader')
        link(tex.outputs['Alpha'], mix.inputs['Fac'])
        link(clear.outputs['BSDF'], mix.inputs[1]); link(emit.outputs['Emission'], mix.inputs[2])
        link(mix.outputs['Shader'], out.inputs['Surface'])
    else:
        link(emit.outputs['Emission'], out.inputs['Surface'])
    return mat


def plane(name, corners, material, uvs=((0, 0), (1, 0), (1, 1), (0, 1))):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(corners, [], [(0, 1, 2, 3)])
    uv = mesh.uv_layers.new(name='UV')
    for loop, c in zip(mesh.loops, uvs):
        uv.data[loop.index].uv = c
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def billboard(name, image, centre_x, y, height, bottom_z, material, aspect=None, tilt=0.0):
    w, h = image.size
    width = height * (aspect or w / h)
    x0, x1 = centre_x - width / 2, centre_x + width / 2
    return plane(name, [(x0, y, bottom_z), (x1, y, bottom_z), (x1, y, bottom_z + height), (x0, y, bottom_z + height)], material)


def solid(name, corners, colour):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree; nt.nodes.clear()
    e = nt.nodes.new('ShaderNodeEmission'); e.inputs['Color'].default_value = (*colour, 1)
    o = nt.nodes.new('ShaderNodeOutputMaterial'); nt.links.new(e.outputs[0], o.inputs[0])
    return plane(name, corners, mat)


def sprite_bbox_crop(path):
    im = Image.open(path).convert('RGBA')
    box = im.getchannel('A').getbbox()
    tmp = Path('/tmp') / ('cap_' + path.stem + '.png')
    im.crop(box).save(tmp)
    return tmp


def glow_texture(path, colour, size=512, power=2.2):
    yy, xx = np.mgrid[0:size, 0:size]
    d = np.sqrt(((xx - size / 2) / (size / 2)) ** 2 + ((yy - size / 2) / (size / 2)) ** 2)
    a = np.clip(1 - d, 0, 1) ** power
    arr = np.zeros((size, size, 4), np.uint8)
    arr[..., 0], arr[..., 1], arr[..., 2] = [int(c * 255) for c in colour]
    arr[..., 3] = (a * 255).astype(np.uint8)
    Image.fromarray(arr, 'RGBA').save(path)
    return path


def swung(name, image, centre_x, y, height, pivot_z, angle_deg, material):
    """A billboard hanging from (centre_x, pivot_z) rotated about that pivot in the view plane."""
    w = height * image.size[0] / image.size[1]
    a = math.radians(angle_deg)
    pts = [(-w / 2, -height), (w / 2, -height), (w / 2, 0), (-w / 2, 0)]
    corners = [(centre_x + px * math.cos(a) - pz * math.sin(a), y, pivot_z + px * math.sin(a) + pz * math.cos(a)) for px, pz in pts]
    return plane(name, corners, material)


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    L = 60.0
    wall = emission('WallR', B_image(ART / 'Hallway/HALLWAY-Wall-v1-2.png'), gain=1.0, fog=.085, tint=(.92, 1, 1), alpha=False, repeat=True)
    reps = (L + 14) / 7.2
    # right wall only: the left side of the frame is the dark mouth of the hallway
    plane('RightWall', [(HALL_W / 2, L, 0), (HALL_W / 2, -14, 0), (HALL_W / 2, -14, HALL_H), (HALL_W / 2, L, HALL_H)], wall, ((0, 0), (reps, 0), (reps, 1), (0, 1)))
    solid('Floor', [(-HALL_W / 2 - 4, -14, 0), (HALL_W / 2, -14, 0), (HALL_W / 2, L, 0), (-HALL_W / 2 - 4, L, 0)], (.03, .012, .016))
    carpet = bpy.data.materials.new('Carpet'); carpet.use_nodes = True
    nt = carpet.node_tree; nt.nodes.clear()
    geo = nt.nodes.new('ShaderNodeNewGeometry'); sep = nt.nodes.new('ShaderNodeSeparateXYZ'); nt.links.new(geo.outputs['Position'], sep.inputs[0])
    mulf = nt.nodes.new('ShaderNodeMath'); mulf.operation = 'MULTIPLY'; mulf.inputs[1].default_value = -.1; nt.links.new(sep.outputs['Y'], mulf.inputs[0])
    att = nt.nodes.new('ShaderNodeMath'); att.operation = 'POWER'; att.inputs[0].default_value = math.e; nt.links.new(mulf.outputs[0], att.inputs[1])
    gain = nt.nodes.new('ShaderNodeMath'); gain.operation = 'MULTIPLY'; gain.inputs[1].default_value = .3; nt.links.new(att.outputs[0], gain.inputs[0])
    col = nt.nodes.new('ShaderNodeMix'); col.data_type = 'RGBA'; col.blend_type = 'MULTIPLY'; col.inputs[0].default_value = 1
    col.inputs[6].default_value = (.62, .20, .27, 1); nt.links.new(gain.outputs[0], col.inputs[7])
    em = nt.nodes.new('ShaderNodeEmission'); nt.links.new(col.outputs[2], em.inputs[0])
    o = nt.nodes.new('ShaderNodeOutputMaterial'); nt.links.new(em.outputs[0], o.inputs[0])
    plane('Carpet', [(-.9, -14, .002), (.9, -14, .002), (.9, L, .002), (-.9, L, .002)], carpet)
    # wall furniture: sconces and two watching frames, all on the right wall
    sconce = B_image(sprite_bbox_crop(ART / 'Hallway/HALLWAY-sconce.png'))
    for y in (5.0, 10.5, 17.0):
        mat = emission(f'Sconce{y}', sconce, gain=1.0, fog=.07)
        w = .62 * sconce.size[0] / sconce.size[1]
        x = HALL_W / 2 - .03
        plane(f'Sconce{y}', [(x, y + w / 2, 1.55), (x, y - w / 2, 1.55), (x, y - w / 2, 1.55 + .62), (x, y + w / 2, 1.55 + .62)], mat)
    eye = Image.open(ART / 'Frames/Eye/HALLWAY-Frame-Eye.png').convert('RGBA'); outline = Image.open(ART / 'Frames/Eye/HALLWAY-Frame-Eye-outline.png').convert('RGBA')
    for name, y, z, height, has_eye in (('Frame-Filled-09', 7.6, 2.05, .95, True), ('Frame-Filled-14', 13.5, 1.95, 1.0, True)):
        f = Image.open(ART / f'Frames/Segmented Frames/{name}.png').convert('RGBA')
        back = Image.new('RGBA', f.size, (32, 32, 32, 255)); back.alpha_composite(f)
        e = Image.alpha_composite(eye, outline).resize((int(f.width * .8), int(f.height * .5)), Image.LANCZOS)
        back.alpha_composite(e, ((f.width - e.width) // 2, (f.height - e.height) // 2))
        back.putalpha(f.getchannel('A').point(lambda a: 255 if a > 8 else 0).filter(ImageFilter.MaxFilter(3)))
        tmp = Path('/tmp') / f'cap11_{name}.png'; back.save(tmp)
        w = height * f.width / f.height; x = HALL_W / 2 - .02
        plane(name, [(x, y + w / 2, z - height / 2), (x, y - w / 2, z - height / 2), (x, y - w / 2, z + height / 2), (x, y + w / 2, z + height / 2)], emission('F' + name, B_image(tmp), gain=.9, fog=.07))
    # the dark mouth: tentacles reaching out of it
    arms = [('left', 1, -3.4, 15.0, 5.0, .8), ('right', 2, -.6, 20.0, 6.4, .7), ('left', 3, -2.0, 12.5, 3.6, .85), ('right', 4, 0.4, 17.0, 5.2, .65),
            ('left', 2, -5.2, 22.0, 7.2, .6), ('right', 1, -2.6, 28.0, 8.6, .55), ('left', 4, -.4, 24.0, 7.4, .5), ('right', 3, -4.4, 18.0, 5.6, .7)]
    for side, n, x, y, h, g in arms:
        im = B_image(sprite_bbox_crop(ART / f'Boss/BOSS-v1v2-arm-{side}-{n}.png'))
        billboard(f'Tentacle{side}{n}{y}', im, x, y, h, 0.0, emission(f'T{side}{n}{y}', im, gain=g, fog=.03, tint=(.8, .95, 1.0)))
    # the angler, lit only by its own lamp: bright sprite, warm-green pool of light on floor, wall and nearby tentacles
    angler = B_image(sprite_bbox_crop(ART / 'Traps/TRAP-Angler-fish-glow.png'))
    glow = B_image(glow_texture('/tmp/cap11_glow.png', (.78, 1.0, .72)))
    plane('AnglerGlow', [(-6.6, 13.9, -1.2), (.8, 13.9, -1.2), (.8, 13.9, 4.6), (-6.6, 13.9, 4.6)], emission('AnglerGlowM', glow, gain=.85, fog=0.0))
    billboard('Angler', angler, -2.9, 13.6, 2.6, 0.3, emission('AnglerM', angler, gain=1.0, fog=0.0))
    # chandelier above, mid-swing, with a fainter trailing ghost to read the motion
    chand = B_image(sprite_bbox_crop(ART / 'Traps/TRAP-Chandelier-v2.png'))
    swung('ChandGhost', chand, .55, 9.1, 1.9, HALL_H, 16, emission('ChandG', chand, gain=.28, fog=.05))
    swung('Chand', chand, .55, 9.0, 1.9, HALL_H, 9, emission('ChandM', chand, gain=.95, fog=.05))
    # the cart barrels down the hallway toward us and the bellhop, nose up and wheels off the carpet: about to go airborne
    cart = B_image(sprite_bbox_crop(ART / 'Traps/TRAP-Hotel-cart.png'))
    heading = (0.80, -0.60)                      # horizontal direction of travel: down the hall, a little toward the right wall
    pitch, lift, cx, cy, ch = math.radians(14), .34, -.55, 6.4, 1.55
    cw = ch * cart.size[0] / cart.size[1]
    def cart_point(s_, t_):
        # s along the heading (nose = +s), t vertical; nose-up pitch about the horizontal axis across the heading
        h = s_ * math.cos(pitch) - t_ * math.sin(pitch)
        v = s_ * math.sin(pitch) + t_ * math.cos(pitch)
        return (cx + heading[0] * h, cy + heading[1] * h, lift + v)
    plane('Cart', [cart_point(-cw / 2, 0), cart_point(cw / 2, 0), cart_point(cw / 2, ch), cart_point(-cw / 2, ch)],
          emission('CartM', cart, gain=.95, fog=.05, tint=(.92, .97, 1.0)))
    # soft contact shadow on the carpet, offset because the cart is off the ground
    shadow = B_image(glow_texture('/tmp/cap11_shadow.png', (0, 0, 0), power=1.2))
    sx, sy = cx + heading[0] * -.1, cy + heading[1] * -.1
    ux, uy = heading
    plane('CartShadow', [(sx - ux * cw * .6 + uy * .45, sy - uy * cw * .6 - ux * .45, .006), (sx + ux * cw * .6 + uy * .45, sy + uy * cw * .6 - ux * .45, .006),
                         (sx + ux * cw * .6 - uy * .45, sy + uy * cw * .6 + ux * .45, .006), (sx - ux * cw * .6 - uy * .45, sy - uy * cw * .6 + ux * .45, .006)],
          emission('CartShadowM', shadow, gain=1.0, fog=0.0))
    # the oblivious bellhop in the right foreground, facing away from the cart
    fish = B_image(sprite_bbox_crop(ART / 'HotelFish/FISH-right.png'))
    billboard('Fish', fish, 1.25, 2.9, 1.95, 0.0, emission('FishM', fish, gain=1.0, fog=0.0))
    cam = bpy.data.cameras.new('Cam'); cam.lens = 24; cam.sensor_width = 36; cam.clip_end = 200
    ob = bpy.data.objects.new('Cam', cam); ob.location = (-.5, -3.4, 1.45); ob.rotation_euler = (math.radians(90.5), 0, math.radians(-20))
    sc.collection.objects.link(ob); sc.camera = ob
    sc.world = bpy.data.worlds.new('W'); sc.world.use_nodes = True
    sc.world.node_tree.nodes['Background'].inputs['Color'].default_value = (0, 0, 0, 1)
    return sc


def render(samples, path):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = samples; sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = W, H
    sc.render.film_transparent = False
    sc.view_settings.view_transform = 'Standard'
    sc.render.image_settings.file_format = 'PNG'
    sc.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def finish(art_path, title_path):
    im = Image.open(art_path).convert('RGB')
    # vignette + a little tint so the corners fall to black
    a = np.asarray(im).astype(np.float32)
    yy, xx = np.mgrid[0:H, 0:W]
    r = np.sqrt(((xx - W * .55) / (W * .62)) ** 2 + ((yy - H * .5) / (H * .75)) ** 2)
    a *= (1 - .55 * np.clip(r - .45, 0, 1) ** 1.4)[..., None]
    im = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
    im.save(art_path)
    t = im.copy().convert('RGBA')
    font = ROOT / 'Assets/Art/Fonts/HauntedIntertitle-Regular.ttf'
    big, small = ImageFont.truetype(str(font), 150), ImageFont.truetype(str(font), 104)
    layer = Image.new('RGBA', t.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    y = 40
    for text, f, yy_ in (('HAUNTED', big, y), ('FISH HOTEL', small, y + 132)):
        x = W - 64 - d.textlength(text, font=f)
        d.text((x + 5, yy_ + 6), text, font=f, fill=(0, 0, 0, 255))
        d.text((x, yy_), text, font=f, fill=(250, 243, 222, 255))
    shadow = layer.filter(ImageFilter.GaussianBlur(10))
    t = Image.alpha_composite(t, Image.merge('RGBA', (*[Image.new('L', t.size, 0)] * 3, shadow.getchannel('A').point(lambda v: min(255, v * 2)))))
    t = Image.alpha_composite(t, layer)
    t.convert('RGB').save(title_path)


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
    p = argparse.ArgumentParser()
    p.add_argument('--samples', type=int, default=64)
    p.add_argument('--out', type=Path, default=OUT)
    args = p.parse_args(argv)
    build()
    art = args.out / 'Haunted-Fish-Hotel-Main-Art-Only.png'
    render(args.samples, art)
    finish(art, args.out / 'Haunted-Fish-Hotel-Main.png')
    print('wrote', art)


main()
