#!/usr/bin/env python3
"""Offline preview of the lobby-intro exterior crane (gate arrival), including the foggy city layers.

This is a *layout preview*, not Unity output. It uses the project's real sprites and rig data, applies the same
parallax projection as StoryAnimationMath.Project/Magnification, and ports the sky, wet-driveway, rain and lamp
modes of LobbyIntroWeather.shader to numpy. Rain/ripple animation is approximate. Native Unity rendering is
still the reference; nothing here is evidence of how the Unity build looks.

    python compose_exterior.py --out frames_dir [--fps 24] [--seconds 5] [--hold-start 0.8] [--hold-end 1.6]
"""
import argparse
import functools
import re
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/Art/LobbyIntro'
W, H = 1280, 720          # reference canvas
SHADER_SCALE = 2          # shader layers are evaluated at 1/2 resolution then scaled


def frac(x):
    return x - np.floor(x)


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def hash2(x, y):
    return frac(np.sin(x * 127.1 + y * 311.7) * 43758.5453)


def noise(x, y):
    ix, iy = np.floor(x), np.floor(y)
    fx, fy = x - ix, y - iy
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    a, b = hash2(ix, iy), hash2(ix + 1, iy)
    c, d = hash2(ix, iy + 1), hash2(ix + 1, iy + 1)
    return (a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fy


def cloud_noise(x, y):
    return noise(x, y) * .56 + noise(x * 2.03 + 5.7, y * 2.03 + 5.7) * .28 + noise(x * 4.11 + 13.1, y * 4.11 + 13.1) * .16


def grid(w, h):
    u = (np.arange(w) + .5) / w
    v = 1 - (np.arange(h) + .5) / h
    return np.meshgrid(u, v)


def sky(w, h, t):
    u, v = grid(w, h)
    clouds = cloud_noise(u * 4.3 + t * .012, v * 3.0)
    horizon = (1 - v) ** 1.7
    low, high = np.array([.018, .027, .051]), np.array([.075, .10, .14])
    rgb = low + (high - low) * horizon[..., None]
    rgb += np.array([.025, .035, .049]) * smoothstep(.34, .85, clouds)[..., None]
    rgb *= (.72 + .28 * (1 - smoothstep(.25, .75, np.abs(u - .5))))[..., None]
    return np.dstack([rgb, np.ones_like(u)])


def ripple(u, v, t, seed=0.0):
    px, py = u * 15.0, v * 5.0
    cx, cy = np.floor(px), np.floor(py)
    n = hash2(cx + seed, cy + seed)
    phase = frac(t * .47 + n * 7.0)
    qx = frac(px) - (.3 + n * .4)
    qy = frac(py) - (.28 + hash2(cx + 31.0, cy + 31.0) * .44)
    radius = phase * .54
    ring = 1 - smoothstep(.012, .045, np.abs(np.sqrt(qx ** 2 + (qy * 2.6) ** 2) - radius))
    return ring * (1 - phase) ** 3 * (n >= .67) * smoothstep(.05, .4, v)


def wet_ground(w, h, t, opacity=.42):
    u, v = grid(w, h)
    lerp = lambda a, b, k: a + (b - a) * k
    path = 1 - smoothstep(lerp(.44, .09, v), lerp(.49, .14, v), np.abs(u - .5))
    grain = cloud_noise(u * 11, v * 27)
    low, high = np.array([.018, .026, .031]), np.array([.068, .077, .089])
    k = (path * (.58 + grain * .42))[..., None]
    rgb = low + (high - low) * k
    wet = noise(u * 13, v * 53) * noise(u * 4, v * 17)
    rgb += np.array([.040, .052, .062]) * (wet * path)[..., None]
    rgb += np.array([.22, .25, .26]) * (ripple(u, v, t) * path * opacity)[..., None]
    return np.dstack([rgb, 1 - smoothstep(.82, 1.0, v)])


def lamp_visibility(u, v, light):
    a = np.exp(-(((u - .15) * 6) ** 2 + ((v - .58) * 4) ** 2))
    b = np.exp(-(((u - .85) * 6) ** 2 + ((v - .58) * 4) ** 2))
    s = np.exp(-(((u - .56) * 2.5) ** 2 + ((v - .78) * 4) ** 2))
    return np.clip(.10 + (a + b) * .70 * light + s * .42, 0, 1)


def rain(w, h, t, aspect, density, speed, wind, seed, opacity, length, width, light):
    u, v = grid(w, h)
    px, py = u * aspect + v * wind, v
    px, py = px * density, py * density
    column = np.floor(px)
    py = py + t * speed * density * (.77 + hash2(column, seed) * .46) + hash2(column, 9.2 + seed) * 17.0
    cx, cy = np.floor(px), np.floor(py)
    n = hash2(cx + seed, cy + seed)
    n2 = hash2(cy + seed + 19.3, cx + seed + 19.3)
    qx = frac(px) - (.2 + n * .6)
    qy = frac(py) - .5
    ln = length * (.6 + .85 * n2)
    along = qy / ln
    taper = np.clip(1 - np.abs(along) * 2.0, 0, 1)
    wd = width * (.6 + .9 * n) * np.sqrt(taper + .015)
    aa = max(1.4 * density / w * .7, .004)
    cross = 1 - smoothstep(wd * .15, wd + aa, np.abs(qx))
    body = cross * taper ** .85
    breakup = .35 + .65 * noise(qy * 39.0, n * 71.0)
    head = np.exp(-((qx / (wd * 1.8 + aa)) ** 2 + (qy / (ln * .11)) ** 2)) * .22
    amount = np.clip(body * breakup + head, 0, 1) * (.25 + .7 * n2) * (n >= .19)
    amount = amount * lamp_visibility(u, v, light) * opacity
    warm = np.clip((1 - np.abs(v - .58) * 4) * (1 - np.abs(np.abs(u - .5) - .35) * 7), 0, 1) * light
    rgb = np.array([.46, .61, .72]) * (1 - warm[..., None] * .55) + np.array([.74, .67, .48]) * (warm[..., None] * .55)
    return np.dstack([rgb, amount])


def lamp_glow(w, h, opacity=.13):
    u, v = grid(w, h)
    qx, qy = (u - .5) * 2, (v - .5) * 2
    a = np.exp(-(qx ** 2 + qy ** 2) * 4.8) * opacity
    return np.dstack([np.full_like(a, 1.0), np.full_like(a, .73), np.full_like(a, .33), a])


def to_image(arr):
    return Image.fromarray((np.clip(arr, 0, 1) * 255 + .5).astype(np.uint8), 'RGBA')


@functools.lru_cache(maxsize=None)
def sprite(path, size, tint):
    im = Image.open(path).convert('RGBA').resize(size, Image.LANCZOS)
    a = np.asarray(im).astype(np.float32) / 255
    a[..., :3] *= np.array(tint, dtype=np.float32)
    return to_image(a)


@functools.lru_cache(maxsize=None)
def city_layer(name):
    return Image.open(ART / 'City' / f'{name}.png').convert('RGBA')


@functools.lru_cache(maxsize=None)
def glow_sprite():
    return to_image(lamp_glow(118, 128)).resize((235, 255))


def guid_of(png):
    return re.search(r'guid: (\w+)', (png.parent / (png.name + '.meta')).read_text()).group(1)


def tree_files():
    """Foliage sprites are referenced by guid in the prefab; resolve them from .meta files."""
    return {guid_of(p): p for p in ART.glob('EXT-*.png')}


class Layer:
    def __init__(self, depth, rest, image, size, children=()):
        self.depth, self.rest, self.image, self.size, self.children = depth, rest, image, size, list(children)


def build_layers(t, wait=False):
    trees = tree_files()
    lay = []
    # Use the new FogSky instead of generated sky
    fog_sky = Image.open(ART / 'City/FogSky.png').convert('RGBA')
    lay.append(Layer(12, (0, 0), fog_sky, (1600, 900)))
    # Use the new OpeningCity layers with facade photos
    for name, depth in (('OpeningCityFar', 9), ('OpeningCityMid', 7.5), ('OpeningCityBack', 6.5), ('OpeningCityNear', 4.6)):
        lay.append(Layer(depth, (0, 0), city_layer(name), (1600, 900)))
    # CloudBank above the city
    cloud_bank = Image.open(ART / 'City/CloudBank.png').convert('RGBA')
    lay.append(Layer(11, (0, -100), cloud_bank, (1600, 900)))
    lay.append(Layer(2.2, (0, -190), to_image(wet_ground(1280 // SHADER_SCALE, 340 // SHADER_SCALE, t)), (1280, 340)))
    far = rain(1600 // SHADER_SCALE, 900 // SHADER_SCALE, t, 1600 / 900, 67, .38, .13, 2.3, .35, .21, .018, .4)
    lay.append(Layer(7, (0, 0), to_image(far), (1600, 900)))
    hotel = Layer(3.5, (0, 35), None, (0, 0), [((0, 15), sprite(ART / 'EXT-Hotel-filled.png', (580, 580), (.73, .8, .91)))])
    lay.append(hotel)
    mid = rain(1600 // SHADER_SCALE, 900 // SHADER_SCALE, t, 1600 / 900, 39, .66, .16, 17.1, .44, .32, .026, 1.0)
    lay.append(Layer(2.7, (0, 0), to_image(mid), (1600, 900)))
    left = next(p for g, p in trees.items() if p.name == 'EXT-Tree.png')
    right = next(p for g, p in trees.items() if p.name == 'EXT-Tree-darker.png')
    lay.append(Layer(1.9, (0, 0), None, (0, 0), [
        ((-475, 30), sprite(left, (770, 770), (.40, .44, .54))),
        ((500, 10), sprite(right, (810, 810), (.31, .36, .47)))]))
    glow = glow_sprite()
    gate = sprite(ART / 'EXT-Gate.png', (1420, 594), (.71, .73, .76))
    lay.append(Layer(1.5, (0, -90), None, (0, 0), [((-456, 55), glow), ((456, 55), glow), ((0, 0), gate)]))
    near = rain(1600 // SHADER_SCALE, 900 // SHADER_SCALE, t, 1600 / 900, 23, .98, .2, 39.7, .3, .46, .031, .65)
    lay.append(Layer(1.3, (0, 0), to_image(near), (1600, 900)))
    return lay


def place(canvas, image, centre, size):
    """Paste `image` resized to `size` (canvas units) centred on `centre` (canvas units, y up)."""
    w, h = max(1, round(size[0])), max(1, round(size[1]))
    x, y = round(W / 2 + centre[0] - w / 2), round(H / 2 - centre[1] - h / 2)
    tmp = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    tmp.paste(image.resize((w, h), Image.BILINEAR), (x, y))
    return Image.alpha_composite(canvas, tmp)


def project(position, camera, depth, push):
    mag = depth / (depth - push)
    return ((position[0] - camera[0] / depth) * mag, (position[1] - camera[1] / depth) * mag), mag


def frame(layers, camera, push):
    canvas = Image.new('RGBA', (W, H), (0, 0, 0, 255))
    for layer in layers:
        centre, mag = project(layer.rest, camera, layer.depth, push)
        if layer.image is not None:
            canvas = place(canvas, layer.image, centre, (layer.size[0] * mag, layer.size[1] * mag))
        for offset, child in layer.children:
            canvas = place(canvas, child, (centre[0] + offset[0] * mag, centre[1] + offset[1] * mag),
                           (child.size[0] * mag, child.size[1] * mag))
    return canvas.convert('RGB')


def smoother(t):
    t = min(1, max(0, t))
    return t * t * t * (t * (t * 6 - 15) + 10)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--fps', type=int, default=24)
    parser.add_argument('--seconds', type=float, default=5.0)
    parser.add_argument('--hold-start', type=float, default=.8)
    parser.add_argument('--hold-end', type=float, default=1.6)
    parser.add_argument('--clock', type=float, default=37.0, help='shader time offset (s)')
    args = parser.parse_args()
    args.out.mkdir(parents=True, exist_ok=True)
    total = args.hold_start + args.seconds + args.hold_end
    count = round(total * args.fps)
    for i in range(count):
        now = i / args.fps
        p = smoother((now - args.hold_start) / args.seconds)
        layers = build_layers(args.clock + now)
        frame(layers, (0, -140 * p), .4 * p).save(args.out / f'ext_{i:04d}.png')
        if i % 12 == 0:
            print(f'{i + 1}/{count}', flush=True)


if __name__ == '__main__':
    main()
