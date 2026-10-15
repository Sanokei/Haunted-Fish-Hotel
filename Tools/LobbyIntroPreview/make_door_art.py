#!/usr/bin/env python3
"""Door-scene art: a plaster wall and a green doormat with two gold inner borders (Assets/Art/LobbyIntro/Door)."""
import sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = Path(sys.argv[1]) if len(sys.argv) > 1 else Path('Assets/Art/LobbyIntro/Door')
OUT.mkdir(parents=True, exist_ok=True)
rng = np.random.default_rng(7)


def noise(w, h, sigma):
    n = Image.fromarray(rng.integers(0, 256, (h, w), dtype=np.uint8)).filter(ImageFilter.GaussianBlur(sigma))
    a = np.asarray(n, np.float32)
    return (a - a.mean()) / (a.std() + 1e-6)


def plaster(w=1392, h=590):
    n = .55 * noise(w, h, 40) + .3 * noise(w, h, 9) + .15 * noise(w, h, 1.5)
    streak = noise(w, h, 3)[:, :1] * 0 + noise(w, 1, 30)[:1, :] * 0  # keep shapes simple; trowel marks below
    base = np.array([201, 190, 170], np.float32)
    img = base[None, None, :] + n[..., None] * np.array([9, 9, 10], np.float32)
    img *= (1 - .10 * (np.linspace(0, 1, h)[:, None, None] ** 2))      # soft shade toward the floor
    return Image.fromarray(np.clip(img, 0, 255).astype(np.uint8))


def mat(w=520, h=120, s=4):
    big = Image.new('RGBA', (w * s, h * s), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    d.rounded_rectangle([0, 0, w * s - 1, h * s - 1], radius=10 * s, fill=(26, 66, 42, 255))          # binding
    d.rounded_rectangle([7 * s, 7 * s, (w - 8) * s, (h - 8) * s], radius=6 * s, fill=(38, 104, 64, 255))   # green pile
    gold = (201, 163, 76, 255)
    for inset in (16, 26):                                                                               # two gold inner borders
        d.rounded_rectangle([inset * s, inset * s, (w - inset - 1) * s, (h - inset - 1) * s], radius=4 * s, outline=gold, width=3 * s)
    img = big.resize((w, h), Image.LANCZOS)
    a = np.asarray(img, np.float32)
    pile = noise(w, h, .8)[..., None] * 5
    a[..., :3] = np.clip(a[..., :3] + pile, 0, 255)
    return Image.fromarray(a.astype(np.uint8), 'RGBA')


def boards(w=1024, h=1024):
    img = Image.new('RGB', (w, h)); d = ImageDraw.Draw(img)
    for i, y in enumerate(range(0, h, 128)):
        tone = int(46 + 10 * rng.random()); d.rectangle([0, y, w, y + 128], fill=(tone + 12, tone + 4, tone))
        d.line([0, y, w, y], fill=(18, 12, 10), width=3)
        for x in range((i * 97) % 300, w, 300): d.line([x, y, x, y + 128], fill=(18, 12, 10), width=3)
    return img


plaster().save(OUT / 'DoorWallPlaster.png')
boards().save(OUT / 'DoorFloorBoards.png')
mat(900, 350, 2).save(OUT / 'DoorMatGreen_topdown.png')
mat().save(OUT / 'DoorMatGreen.png')
print('wrote', OUT)
