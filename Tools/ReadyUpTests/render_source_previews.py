#!/usr/bin/env python3
"""Draw the checked-in prefab rectangles and actual TTFs. This does not run Unity."""
import argparse
from pathlib import Path
import sys
import yaml
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Tools/ScenePresentationTests'))
import verify_scene_assets as assets


def render(role, destination, state):
    source = (ROOT / f'Assets/Resources/{role}RoundHowTo.prefab').read_text()
    docs = {r.ident: next(iter(yaml.safe_load(r.body).values())) for r in assets.records(source)}
    image = Image.new('RGB', (1280, 750), (10, 6, 18))
    draw = ImageDraw.Draw(image)
    # The actual authored hierarchy/draw order, flattened within the fixed page.
    order = [120, 150, 230, 130, 140, 240, 160, 250, 170, 260, 190, 180, 200, 270]
    for ident in order:
        rect = docs[ident + 1]; component = docs[ident + 3]
        px = 640 + rect['m_AnchoredPosition']['x']; py = 360 - rect['m_AnchoredPosition']['y']
        if ident == 200:  # The label is a child of the existing Ready button.
            px += docs[181]['m_AnchoredPosition']['x']; py -= docs[181]['m_AnchoredPosition']['y']
        width, height = rect['m_SizeDelta']['x'], rect['m_SizeDelta']['y']
        x, y = px - width / 2, py - height / 2
        color = tuple(round(component['m_Color'][axis] * 255) for axis in 'rgb')
        if 'm_FontData' not in component:
            draw.rectangle((x, y, x + width, y + height), fill=color)
            continue
        data = component['m_FontData']
        font_path = ('Assets/Art/Fonts/Modak-Regular.ttf' if data['m_Font']['guid'] == 'd4e80c7736f5472abd2cace956a4fda6'
                     else 'Assets/TextMesh Pro/Fonts/LiberationSans.ttf')
        font = ImageFont.truetype(str(ROOT / font_path), data['m_FontSize'])
        copy = component['m_Text']
        if state == 'sent' and ident in (190, 200): copy = '1 / 4 PLAYERS READY' if ident == 190 else 'READY SENT'
        if state == 'ready' and ident in (190, 200): copy = '3 / 4 PLAYERS READY' if ident == 190 else 'YOU ARE READY'
        lines = copy.split('\n'); ascent, descent = font.getmetrics()
        step = (ascent + descent) * data['m_LineSpacing']
        full_height = (len(lines) - 1) * step + ascent + descent
        if data['m_Alignment'] in (3, 4, 5): y += (height - full_height) / 2
        for line in lines:
            text_x = x + (width - font.getlength(line)) / 2 if data['m_Alignment'] % 3 == 1 else x
            draw.text((text_x, y + ascent), line, font=font, fill=color, anchor='ls')
            y += step
    note_font = ImageFont.truetype(str(ROOT / 'Assets/TextMesh Pro/Fonts/LiberationSans.ttf'), 18)
    note = 'Serialized prefab + actual TTF source preview. Not a Unity screenshot.'
    if state != 'authored': note = 'Source preview, not Unity. Illustrative ' + state + ' state/count; not live players.'
    draw.text((12, 726), note, font=note_font, fill=(220, 220, 220))
    destination.mkdir(parents=True, exist_ok=True)
    suffix = '' if state == 'authored' else '-' + state
    target = destination / (role.lower() + '-guide' + suffix + '-source.png')
    image.save(target)
    print(target)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--state', choices=['authored', 'sent', 'ready'], default='authored')
    args = parser.parse_args()
    for role in ('Fish', 'Ghost'): render(role, args.output, args.state)
