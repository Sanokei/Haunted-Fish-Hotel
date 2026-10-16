"""Night storm sky for the opening city: FogSky.png (gradient) and CloudBank.png (graded storm deck).

Source cloud photo: "Clouds near thunderstorm from Maselakepark 2019-06-03 10" by Leonhard Lenz (GPSLeo),
CC0, https://commons.wikimedia.org/wiki/File:Clouds_near_thunderstorm_from_Maselakepark_2019-06-03_10.jpg

  python Tools/city/prepare_sky.py <path to the downloaded cloud photo>
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

OUT = Path(__file__).resolve().parents[2] / 'Assets/Art/LobbyIntro/City'
W, H = 1600, 900


def main(photo):
    y = np.linspace(0, 1, H)[:, None, None]
    top, horizon = np.array([.035, .045, .085]), np.array([.16, .15, .19])
    sky = top * (1 - y ** 1.6) + horizon * y ** 1.6
    sky = sky + np.random.default_rng(3).normal(0, .004, (H, W, 1))
    Image.fromarray((np.clip(np.broadcast_to(sky, (H, W, 3)), 0, 1) * 255).astype(np.uint8)).save(OUT / 'FogSky.png')

    src = Image.open(photo).convert('RGB')
    crop = src.crop((0, 0, src.width, int(src.width * H / W)))  # top of the frame: all storm deck
    rgb = np.asarray(crop.resize((W, H), Image.LANCZOS), np.float32) / 255
    lum = rgb @ np.array([.299, .587, .114])
    lum = (lum - lum.min()) / (lum.max() - lum.min())
    yy = np.linspace(0, 1, H)[:, None]
    # Moonlit blue-grey; the lower cloud bellies pick up a sodium-orange city glow.
    night = (np.power(lum, 1.35)[..., None] * np.array([.42, .47, .62]) +
             (np.power(lum, 2)[..., None] * yy[..., None] ** 2) * np.array([.22, .12, .05]))
    # The cloud layer draws above the city layers, so it must be gone before the rooftops.
    alpha = np.clip((0.42 - yy) / 0.22, 0, 1) * np.clip(.35 + lum * 1.2, 0, 1)
    out = np.dstack([np.clip(night, 0, 1), np.broadcast_to(alpha, (H, W))])
    Image.fromarray((out * 255).astype(np.uint8), 'RGBA').save(OUT / 'CloudBank.png')
    print('wrote FogSky.png and CloudBank.png')


if __name__ == '__main__':
    main(sys.argv[1])
