"""Grade the downloaded front-on facade photos into night facades for the opening city.

For each chosen facade:
  * sky above the roofline (bright regions touching the top edge) becomes transparent,
    so buildings have real silhouettes against the storm sky;
  * the photo is graded to moonlit night (dark, cool, keeps brick/cornice detail);
  * lit windows come from the emissive mask, keeping only window-sized blobs, painted
    warm while retaining the photo's mullions and texture.
Output: Assets/Art/LobbyIntro/CityFacades/Night/<name>.png (RGBA) + facades.json with
each facade's aspect, whether it tiles, and its rooftop row for prop placement.

  python Tools/city/prepare_facades.py
"""
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / 'Assets/Art/LobbyIntro/CityFacades/Facades'
OUT = ROOT / 'Assets/Art/LobbyIntro/CityFacades/Night'

# name: (tiles, storeys visible) - front-on elevations only; angled photos are left out.
FACADES = {
    'Tenement_2ndAve808_HABS': (False, 5),
    'Tenement_2ndAve863_HABS': (False, 5),
    'Tenement_2ndAve873_HABS': (False, 5),
    'Tenement_2ndAve867_FireEscape_HABS': (False, 5),
    'Tenement_2ndAve887_HABS': (False, 5),
    'CastIron_PerryBlock_HABS': (False, 4),
    'ArtDeco_FederalBldg_Monroe': (False, 4),
    'ACG_Facade012': (True, 12),
    'ACG_Facade013': (True, 12),
    'ACG_Facade014': (True, 12),
    'ACG_Facade015': (True, 12),
    'ACG_Facade016': (True, 12),
    'ACG_Facade018A': (True, 6),
    'ACG_Facade019A': (True, 6),
    'ACG_Facade020A': (True, 6),
}
NIGHT = np.array([0.30, 0.34, 0.48])   # moonlit multiplier
WARM = np.array([1.00, 0.70, 0.36])    # lamp-lit window colour


def sky_mask(rgb):
    lum = rgb @ np.array([0.299, 0.587, 0.114])
    bright = lum > 0.58
    labels, _ = ndimage.label(bright)
    top = np.unique(labels[0][labels[0] > 0])
    sky = np.isin(labels, top)
    sky = ndimage.binary_closing(sky, iterations=3)
    sky = ndimage.binary_fill_holes(sky | np.zeros_like(sky))
    # Only sky that connects to the top edge, and never the lower 40% of the frame.
    sky[int(sky.shape[0] * 0.6):] = False
    return sky


def windows(mask_img, shape, tiles):
    m = np.asarray(mask_img.convert('L').resize((shape[1], shape[0]))) > 128
    labels, count = ndimage.label(m)
    if count == 0:
        return np.zeros(shape, bool)
    area = ndimage.sum(m, labels, range(1, count + 1))
    boxes = ndimage.find_objects(labels)
    keep = np.zeros(count + 1, bool)
    total = shape[0] * shape[1]
    for i, (a, box) in enumerate(zip(area, boxes), 1):
        h = box[0].stop - box[0].start
        w = box[1].stop - box[1].start
        fill = a / max(1, h * w)
        lo, hi = (0.00002, 0.004) if tiles else (0.00008, 0.0025)
        if lo * total < a < hi * total and fill > 0.55 and 0.25 < w / max(1, h) < 2.5:
            keep[i] = True
    return keep[labels]


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    rng = np.random.default_rng(7)
    report = {}
    for name, (tiles, storeys) in FACADES.items():
        albedo = Image.open(SRC / f'{name}_Albedo.jpg').convert('RGB')
        rgb = np.asarray(albedo, dtype=np.float32) / 255
        alpha = np.ones(rgb.shape[:2], np.float32)
        roof_row = 0
        if not tiles:
            sky = sky_mask(rgb)
            alpha[sky] = 0
            alpha = ndimage.gaussian_filter(alpha, 1.2)
            solid_rows = np.where((alpha > 0.5).mean(axis=1) > 0.6)[0]
            roof_row = int(solid_rows[0]) if len(solid_rows) else 0
        night = np.power(rgb, 1.15) * NIGHT
        lit = windows(Image.open(SRC / f'{name}_EmissiveMask.png'), rgb.shape[:2], tiles)
        # Not every candidate window is lit; vary brightness so the city feels lived in.
        labels, count = ndimage.label(lit)
        level = np.concatenate([[0], rng.uniform(0.55, 1.0, count) * (rng.random(count) < 0.7)])
        glow = level[labels][..., None]
        detail = 0.55 + 0.45 * (rgb @ np.array([0.299, 0.587, 0.114]))[..., None]
        night = night * (1 - glow) + WARM * detail * glow
        out = np.dstack([np.clip(night, 0, 1), np.clip(alpha, 0, 1)])
        Image.fromarray((out * 255).astype(np.uint8), 'RGBA').save(OUT / f'{name}.png')
        report[name] = {'tiles': tiles, 'storeys': storeys, 'width': albedo.width, 'height': albedo.height,
                        'roofRow': roof_row, 'litWindows': int((level > 0).sum())}
        print(f'{name}: {report[name]}')
    (OUT / 'facades.json').write_text(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
