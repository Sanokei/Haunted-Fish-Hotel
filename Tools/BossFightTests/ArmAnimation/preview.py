"""Contact sheet / GIF of the boss poses, built from the real sprites and the same pose functions the clips use.

    python preview.py --sheet out.png            # every clip at four moments
    python preview.py --gif Clip out.gif         # one clip as an animation
Preview only: sprite compositing in PIL, no Unity rendering, no void shader.
"""
import argparse
import math
from functools import lru_cache
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

import poses
import rig_data

PX = 70           # pixels per world unit
W, H = 1000, 640
CX, CY = W // 2, 250   # world origin on the sheet (rig root)
TINT = (0.62, 0.82, 0.68)   # the scene tints every segment this green


@lru_cache(maxsize=None)
def arms():
    return rig_data.load(1)[0]


@lru_cache(maxsize=None)
def sprite(path):
    return Image.open(path).convert('RGBA')


@lru_cache(maxsize=None)
def head_info():
    return rig_data.load(1)[1]


@lru_cache(maxsize=None)
def head():
    return Image.open(rig_data.ART / 'BOSS-v1-neutral-head.png').convert('RGBA')


def paste_centered(canvas, im, centre, rot_deg, scale_xy=(1.0, 1.0)):
    k = PX / rig_data.PPU
    im = im.resize((max(1, round(im.width * k * scale_xy[0])), max(1, round(im.height * k * scale_xy[1]))), Image.BILINEAR)
    r, g, b, a = im.split()
    im = Image.merge('RGBA', (r.point(lambda v: int(v * TINT[0])), g.point(lambda v: int(v * TINT[1])), b.point(lambda v: int(v * TINT[2])), a))
    if rot_deg:
        im = im.rotate(rot_deg, resample=Image.BICUBIC, expand=True)
    x = round(CX + centre[0] * PX - im.width / 2)
    y = round(CY - centre[1] * PX - im.height / 2)
    layer = Image.new('RGBA', canvas.size, (0, 0, 0, 0))
    layer.paste(im, (x, y))
    return Image.alpha_composite(canvas, layer)


def render(clip, t):
    canvas = Image.new('RGBA', (W, H), (14, 14, 20, 255))
    items = []
    for name, a in arms().items():
        rot, dx, dy = poses.sample(clip, name, t)
        sx, sy = a['shoulder'][0] * a['scale'][0], a['shoulder'][1] * a['scale'][1]
        a0, a1 = math.radians(a['rest_rot']), math.radians(a['rest_rot'] + rot)
        rx = lambda ang: (sx * math.cos(ang) - sy * math.sin(ang), sx * math.sin(ang) + sy * math.cos(ang))
        (x0, y0), (x1, y1) = rx(a0), rx(a1)
        pos = (a['rest'][0] + x0 - x1 + dx, a['rest'][1] + y0 - y1 + dy)
        items.append((a['order'], sprite(a['path']), pos, a['rest_rot'] + rot, a['scale']))
    for _, im, pos, rot, sc in sorted(items, key=lambda i: i[0]):
        canvas = paste_centered(canvas, im, pos, rot, sc)
    rot, dx, dy = poses.sample(clip, 'Head', t)
    hd = head_info()
    canvas = paste_centered(canvas, head(), (hd['rest'][0] + dx, hd['rest'][1] + dy), rot, hd['scale'])
    return canvas.convert('RGB')


def sheet(path):
    cols = 4
    clips = list(poses.CLIPS)
    cell_w, cell_h = W // 2, H // 2
    out = Image.new('RGB', (cell_w * cols, cell_h * len(clips)), (0, 0, 0))
    font = ImageFont.load_default()
    for r, clip in enumerate(clips):
        length = poses.CLIPS[clip][1]
        for c in range(cols):
            t = length * (c + .5) / cols
            im = render(clip, t).resize((cell_w, cell_h), Image.LANCZOS)
            ImageDraw.Draw(im).text((8, 6), f'{clip}  t={t:.2f}s', fill=(255, 255, 255), font=font)
            out.paste(im, (c * cell_w, r * cell_h))
    out.save(path)


def gif(clip, path, fps=20):
    length = poses.CLIPS[clip][1]
    frames = [render(clip, i / fps).resize((W // 2, H // 2), Image.LANCZOS) for i in range(int(length * fps))]
    frames[0].save(path, save_all=True, append_images=frames[1:], duration=int(1000 / fps), loop=0)


if __name__ == '__main__':
    p = argparse.ArgumentParser()
    p.add_argument('--sheet')
    p.add_argument('--gif', nargs=2)
    a = p.parse_args()
    if a.sheet:
        sheet(a.sheet)
    if a.gif:
        gif(*a.gif)
