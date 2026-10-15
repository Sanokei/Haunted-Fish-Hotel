#!/usr/bin/env python3
"""Four transparent rooftop layers (far -> near) for the opening fog shot: flat blocks and chimneys, each layer
tinted toward the fog colour by depth. Optional facade photos in facades/photo_*.jpg are multiplied onto block faces.

    python make_opening_city.py --out ../../../Assets/Art/LobbyIntro/City
"""
import argparse, random
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

W, H = 1600, 900
FOG = (118, 138, 160)
# name, base y, block width range, block height range, chimney chance, ink colour, fog mix, blur
LAYERS = [('Far', 470, (120, 260), (150, 330), .35, (92, 108, 128), .72, 3.0),
          ('Mid', 560, (150, 300), (110, 240), .45, (74, 90, 110), .55, 2.0),
          ('Back', 660, (180, 340), (90, 190), .5, (56, 70, 88), .38, 1.2),
          ('Near', 800, (220, 420), (70, 150), .6, (40, 52, 68), .22, .6)]


def facade(w, h, rnd, photos):
    if photos:
        return Image.open(rnd.choice(photos)).convert('RGB').resize((w, h))
    im = Image.new('RGB', (w, h), (rnd.randint(110, 150),) * 3)
    d = ImageDraw.Draw(im)
    for y in range(8, h - 8, 22):
        for x in range(8, w - 8, 20):
            if rnd.random() < .7:
                d.rectangle([x, y, x + 8, y + 12], fill=(60, 66, 76))
    return im


def build(name, base, bw, bh, chimney, ink, mix, blur, seed, photos):
    rnd = random.Random(seed)
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    x = -rnd.randint(0, 80)
    while x < W:
        w, h = rnd.randint(*bw), rnd.randint(*bh)
        top = base - h
        face = facade(w, h, rnd, photos)
        shade = Image.blend(Image.new('RGB', (w, h), ink), face, .22)
        img.paste(shade.convert('RGBA'), (x, top))
        d = ImageDraw.Draw(img)
        if rnd.random() < .5:                         # a sloped roof wedge on one side
            rise = rnd.randint(14, 34)
            d.polygon([(x, top), (x + w, top), (x + w, top - rise)], fill=tuple(int(c * .85) for c in ink) + (255,))
        if rnd.random() < chimney:
            cw, ch = rnd.randint(22, 44), rnd.randint(50, 130)
            cx = x + rnd.randint(8, max(9, w - cw - 8))
            d.rectangle([cx, top - ch, cx + cw, top], fill=ink + (255,))
            d.rectangle([cx - 4, top - ch - 8, cx + cw + 4, top - ch], fill=tuple(int(c * .8) for c in ink) + (255,))
        x += w - rnd.randint(0, 20)
    d = ImageDraw.Draw(img)
    d.rectangle([0, base, W, H], fill=ink + (255,))
    # fog: blend colours toward FOG, thicker toward the bottom (ground fog) and by depth
    rgb, a = img.convert('RGB'), img.getchannel('A')
    fogged = Image.blend(rgb, Image.new('RGB', (W, H), FOG), mix)
    grad = Image.linear_gradient('L').resize((W, H)).point(lambda v: int(v * .5))
    fogged = Image.composite(Image.new('RGB', (W, H), FOG), fogged, grad)
    out = fogged.convert('RGBA'); out.putalpha(a.filter(ImageFilter.GaussianBlur(blur)))
    return out


def main():
    p = argparse.ArgumentParser(); p.add_argument('--out', type=Path, required=True)
    a = p.parse_args(); a.out.mkdir(parents=True, exist_ok=True)
    photos = sorted((Path(__file__).parent / 'facades').glob('photo_*.jpg'))
    for i, (n, *rest) in enumerate(LAYERS):
        build(n, *rest, seed=11 + i * 7, photos=photos).save(a.out / f'OpeningCity{n}.png')
        print('wrote', n)


if __name__ == '__main__':
    main()
