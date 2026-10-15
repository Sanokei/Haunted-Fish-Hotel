#!/usr/bin/env python3
"""Generate PLACEHOLDER building-facade tiles for the foggy block city.

These are procedural stand-ins, NOT photographs. The cloud sandbox that produced them could not reach any
photo-hosting site. To use real building photos instead, save them in `facades/` as `photo_*.jpg` or
`photo_*.png` (any size, ideally a front-on crop that tiles vertically and horizontally); the renderer
prefers `photo_*` files automatically and only falls back to these tiles when none exist.

Each tile covers 4 floors x 4 window bays (12.8 m square). A matching `*_lit.png` mask marks windows that
glow warm; photos have no lit mask, so the renderer lights a small random share of their bright pixels instead.
"""
import random
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = Path(__file__).resolve().parent / 'facades'
SIZE = 1024
SS = 2  # supersample for clean edges
CELL = SIZE // 4


def grain(rng, base, amount, size=SIZE):
    noise = rng.normal(0, amount, (size, size, 1))
    return np.clip(np.array(base, dtype=float) + noise * 255, 0, 255)


def finish(img, lit, name, blur=0.6):
    img = img.resize((SIZE, SIZE), Image.LANCZOS).filter(ImageFilter.GaussianBlur(blur))
    lit = lit.resize((SIZE, SIZE), Image.LANCZOS)
    img.save(OUT / f'{name}.png')
    lit.convert('L').save(OUT / f'{name}_lit.png')


def canvas(rng, wall, amount=.035):
    arr = grain(rng, wall, amount, SIZE * SS)
    return Image.fromarray(arr.astype('uint8'), 'RGB')


def window(draw, lit_draw, rng, x0, y0, x1, y1, glass, frame, lit_chance, arch=False, mullion=True):
    box = [x0, y0, x1, y1]
    draw.rectangle([x0 - 6 * SS, y0 - 6 * SS, x1 + 6 * SS, y1 + 6 * SS], fill=frame)
    lit = rng.random() < lit_chance
    shade = rng.randint(-10, 10)
    color = tuple(max(0, min(255, c + shade)) for c in glass)
    if arch:
        draw.rectangle([x0, y0 + (x1 - x0) // 2, x1, y1], fill=color)
        draw.pieslice([x0, y0, x1, y0 + (x1 - x0)], 180, 360, fill=color)
    else:
        draw.rectangle(box, fill=color)
    if mullion:
        mx = (x0 + x1) // 2
        draw.line([mx, y0, mx, y1], fill=frame, width=4 * SS)
        my = y0 + (y1 - y0) * 2 // 5
        draw.line([x0, my, x1, my], fill=frame, width=3 * SS)
    if lit:
        warm = (255, rng.randint(170, 205), rng.randint(90, 130))
        draw.rectangle(box, fill=tuple(int(c * .55 + w * .45) for c, w in zip(color, warm)))
        lit_draw.rectangle(box, fill=255)
        if mullion:
            mx = (x0 + x1) // 2
            lit_draw.line([mx, y0, mx, y1], fill=0, width=4 * SS)
    # sill
    draw.rectangle([x0 - 10 * SS, y1 + 6 * SS, x1 + 10 * SS, y1 + 14 * SS], fill=tuple(min(255, c + 25) for c in frame))


def tenement(rng):
    img = canvas(rng, (92, 62, 54))
    lit = Image.new('L', img.size, 0)
    d, l = ImageDraw.Draw(img), ImageDraw.Draw(lit)
    for y in range(0, SIZE * SS, 14 * SS):  # brick courses
        d.line([0, y, SIZE * SS, y], fill=(70, 46, 40), width=2)
    for r in range(4):
        for c in range(4):
            x, y = c * CELL * SS, r * CELL * SS
            window(d, l, rng, x + 70 * SS, y + 60 * SS, x + 186 * SS, y + 200 * SS, (22, 28, 38), (50, 38, 34), .22)
    return img, lit


def concrete_office(rng):
    img = canvas(rng, (112, 114, 116), .03)
    lit = Image.new('L', img.size, 0)
    d, l = ImageDraw.Draw(img), ImageDraw.Draw(lit)
    for r in range(4):
        y = r * CELL * SS
        d.rectangle([0, y + 70 * SS, SIZE * SS, y + 190 * SS], fill=(30, 40, 52))  # ribbon glazing
        for c in range(16):
            x = c * (CELL // 4) * SS
            d.line([x, y + 70 * SS, x, y + 190 * SS], fill=(88, 90, 92), width=5 * SS)
            if rng.random() < .16:
                d.rectangle([x + 4 * SS, y + 76 * SS, x + (CELL // 4) * SS - 4 * SS, y + 184 * SS], fill=(214, 170, 104))
                l.rectangle([x + 4 * SS, y + 76 * SS, x + (CELL // 4) * SS - 4 * SS, y + 184 * SS], fill=255)
        d.line([0, y + 190 * SS, SIZE * SS, y + 190 * SS], fill=(82, 84, 86), width=10 * SS)
    return img, lit


def art_deco(rng):
    img = canvas(rng, (128, 118, 98), .03)
    lit = Image.new('L', img.size, 0)
    d, l = ImageDraw.Draw(img), ImageDraw.Draw(lit)
    for c in range(4):
        x = c * CELL * SS
        d.rectangle([x + 10 * SS, 0, x + 40 * SS, SIZE * SS], fill=(150, 140, 118))  # piers
        for r in range(4):
            y = r * CELL * SS
            window(d, l, rng, x + 76 * SS, y + 40 * SS, x + 180 * SS, y + 214 * SS, (26, 30, 36), (96, 88, 72), .2)
    for r in range(4):
        d.line([0, r * CELL * SS + 8 * SS, SIZE * SS, r * CELL * SS + 8 * SS], fill=(160, 148, 120), width=6 * SS)
    return img, lit


def glass_curtain(rng):
    img = canvas(rng, (34, 46, 62), .02)
    lit = Image.new('L', img.size, 0)
    d, l = ImageDraw.Draw(img), ImageDraw.Draw(lit)
    n = 8
    step = SIZE * SS // n
    for r in range(n):
        for c in range(n):
            x, y = c * step, r * step
            tone = rng.randint(-8, 10)
            d.rectangle([x + 3 * SS, y + 3 * SS, x + step - 3 * SS, y + step - 3 * SS], fill=(40 + tone, 62 + tone, 84 + tone))
            if rng.random() < .1:
                d.rectangle([x + 6 * SS, y + 6 * SS, x + step - 6 * SS, y + step - 6 * SS], fill=(228, 196, 140))
                l.rectangle([x + 6 * SS, y + 6 * SS, x + step - 6 * SS, y + step - 6 * SS], fill=255)
    return img, lit


def warehouse(rng):
    img = canvas(rng, (84, 78, 70), .04)
    lit = Image.new('L', img.size, 0)
    d, l = ImageDraw.Draw(img), ImageDraw.Draw(lit)
    for x in range(0, SIZE * SS, 12 * SS):  # corrugation
        d.line([x, 0, x, SIZE * SS], fill=(70, 64, 58), width=3 * SS)
    for r in range(4):
        for c in range(4):
            x, y = c * CELL * SS, r * CELL * SS
            if (r + c) % 2 == 0:
                window(d, l, rng, x + 90 * SS, y + 90 * SS, x + 166 * SS, y + 150 * SS, (24, 30, 34), (60, 56, 50), .3, mullion=False)
    return img, lit


def balconies(rng):
    img = canvas(rng, (118, 100, 84), .035)
    lit = Image.new('L', img.size, 0)
    d, l = ImageDraw.Draw(img), ImageDraw.Draw(lit)
    for r in range(4):
        for c in range(4):
            x, y = c * CELL * SS, r * CELL * SS
            window(d, l, rng, x + 80 * SS, y + 36 * SS, x + 176 * SS, y + 170 * SS, (22, 28, 36), (66, 56, 48), .24, arch=(r % 2 == 0))
            d.rectangle([x + 46 * SS, y + 184 * SS, x + 210 * SS, y + 196 * SS], fill=(54, 48, 44))
            for px in range(x + 50 * SS, x + 210 * SS, 18 * SS):
                d.line([px, y + 196 * SS, px, y + 232 * SS], fill=(54, 48, 44), width=3 * SS)
    return img, lit


STYLES = [tenement, concrete_office, art_deco, glass_curtain, warehouse, balconies]


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for i, make in enumerate(STYLES, 1):
        rng = random.Random(7000 + i)
        np_rng = np.random.default_rng(7000 + i)
        img, lit = make(_Rng(rng, np_rng))
        finish(img, lit, f'placeholder_{i:02d}_{make.__name__}')
        print('wrote', f'placeholder_{i:02d}_{make.__name__}')


class _Rng:
    """Adapter exposing both stdlib `random` and numpy `normal` calls on one deterministic source."""
    def __init__(self, r, n):
        self._r, self._n = r, n

    def random(self):
        return self._r.random()

    def randint(self, a, b):
        return self._r.randint(a, b)

    def normal(self, mean, sd, shape):
        return self._n.normal(mean, sd, shape)


if __name__ == '__main__':
    main()
