#!/usr/bin/env python3
"""Procedural photographic-style cumulus bank (fractal noise, lit from above) for the opening shot. Offline stand-in
for a licensed cloud photo: drop a cloud photo-cutout over Assets/Art/LobbyIntro/City/CloudBank.png to replace it."""
import sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageFilter

W, H = 1500, 456
rng = np.random.default_rng(21)


def layer(sigma, w=W, h=H):
    n = Image.fromarray(rng.integers(0, 256, (h // 4, w // 4), dtype=np.uint8)).resize((w, h), Image.BICUBIC).filter(ImageFilter.GaussianBlur(sigma))
    a = np.asarray(n, np.float32); return (a - a.mean()) / a.std()


f = sum(layer(s) * a for s, a in [(70, 1.0), (32, .6), (14, .35), (6, .2)])
yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
env = np.clip(1 - ((xx / W - .5) / .55) ** 2 - ((yy / H - .45) / .5) ** 2, 0, 1)
dens = np.clip((f * .35 + env * 1.6 - .55), 0, 1) ** .8
alpha = np.clip(dens * 1.7, 0, 1)
# light from above: a point is lit when little cloud sits above it, shaded when thick cloud is stacked over it
blur = lambda x, s: np.asarray(Image.fromarray((np.clip(x, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(s)), np.float32) / 255
above = blur(np.roll(dens, 28, 0), 14)
shade = np.clip(1.0 - .42 * above + .1 * (1 - yy / H), .5, 1)
rgb = np.stack([shade * 247 + (1 - shade) * 70, shade * 250 + (1 - shade) * 88, shade * 255 + (1 - shade) * 112], -1)
alpha = blur(alpha, 2.0)
img = Image.fromarray(np.dstack([rgb, alpha * 255]).astype(np.uint8), 'RGBA')
out = Path(sys.argv[1]) if len(sys.argv) > 1 else Path('Assets/Art/LobbyIntro/City/CloudBank.png')
img.save(out); print('wrote', out)
