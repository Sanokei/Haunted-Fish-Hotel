#!/usr/bin/env python3
"""Build Unity-ready opening-city layer PNGs from the sourced CC0 photos.

No Blender: uses PIL only. Writes 1600x900 PNGs into Assets/Art/LobbyIntro/City/,
.meta files with deterministic uuid5 GUIDs (same namespace as author_lobby_intro),
and Tools/LobbyIntroArt/opening_city_guids.json for author_opening_city.py.
"""
import json
import re
import uuid
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
NS = uuid.UUID("10f3b273-cebc-4390-9877-a27297dc344f")
FACADES = ROOT / "Tools/LobbyIntroPreview/city/facades"
CLOUDS = ROOT / "Tools/LobbyIntroPreview/city/clouds"
OUT = ROOT / "Assets/Art/LobbyIntro/City"
W, H = 1600, 900

# stem -> source photo (spread across the 7 facades for variety)
CITY_SOURCES = {
    "OpeningCityFar": "photo_02.jpg",
    "OpeningCityMid": "photo_04.jpg",
    "OpeningCityBack": "photo_06.jpg",
    "OpeningCityNear": "photo_01.jpg",
}
CLOUD_SOURCE = "cloud_01.jpg"


def guid_for(stem):
    return uuid.uuid5(NS, f"Assets/Art/LobbyIntro/City/{stem}.png").hex


def cover(img, w, h):
    """Center-crop to fill w x h."""
    iw, ih = img.size
    scale = max(w / iw, h / ih)
    img = img.resize((int(iw * scale) + 1, int(ih * scale) + 1), Image.LANCZOS)
    x = (img.width - w) // 2
    y = (img.height - h) // 2
    return img.crop((x, y, x + w, y + h))


def make_fog_sky():
    top = (107, 122, 141)      # grey-blue
    bottom = (158, 168, 180)
    img = Image.new("RGB", (W, H))
    px = img.load()
    for y in range(H):
        t = y / (H - 1)
        px_row = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3))
        for x in range(W):
            px[x, y] = px_row
    return img


def make_cloud_bank():
    img = cover(Image.open(CLOUDS / CLOUD_SOURCE).convert("RGB"), W, H)
    rgba = img.convert("RGBA")
    px = rgba.load()
    for y in range(H):
        for x in range(W):
            r, g, b, _ = px[x, y]
            blue = b - max(r, g)
            # sky is blue-dominant; clouds are near-white. Soft key.
            if blue > 24:
                a = 0
            elif blue > 8:
                a = int(255 * (blue - 8) / 16)
                a = 255 - a
            else:
                a = 255
            px[x, y] = (r, g, b, a)
    return rgba


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    template = (OUT / "CityFar.png.meta").read_text()

    layers = {}
    layers["FogSky"] = make_fog_sky()
    for stem, photo in CITY_SOURCES.items():
        layers[stem] = cover(Image.open(FACADES / photo).convert("RGB"), W, H)
    layers["CloudBank"] = make_cloud_bank()

    guids = {}
    for stem, img in layers.items():
        png = OUT / f"{stem}.png"
        img.save(png)
        g = guid_for(stem)
        guids[stem] = g
        meta = re.sub(r"^guid: [0-9a-f]{32}$", f"guid: {g}", template, count=1, flags=re.M)
        assert f"guid: {g}" in meta, f"guid substitution failed for {stem}"
        (OUT / f"{stem}.png.meta").write_text(meta)
        print("wrote", png.name, g)

    (ROOT / "Tools/LobbyIntroArt/opening_city_guids.json").write_text(json.dumps(guids, indent=2) + "\n")
    print("wrote opening_city_guids.json")


if __name__ == "__main__":
    main()
