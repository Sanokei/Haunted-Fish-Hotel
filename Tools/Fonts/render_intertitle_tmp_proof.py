#!/usr/bin/env python3
"""Reconstruct a CPU proof using committed TMP glyph tables and SDF alpha.

This explicitly does not run Unity or claim native shader/render validation.
"""
import argparse
from pathlib import Path
import re
import numpy as np
from PIL import Image
import yaml

from build_intertitle_tmp import ROOT, OUTPUT, BASENAME


def read_asset(path):
    text = re.sub(r'^%.*\n', '', path.read_text(), flags=re.M)
    text = re.sub(r'^--- !u!\d+ &\d+', '---', text, flags=re.M)
    return yaml.safe_load(text)['MonoBehaviour']


def render(output):
    asset = read_asset(OUTPUT / (BASENAME + '.asset'))
    alpha = Image.open(OUTPUT / (BASENAME + ' Atlas.png')).getchannel('A')
    glyphs = {g['m_Index']: g for g in asset['m_GlyphTable']}
    chars = {c['m_Unicode']: c['m_GlyphIndex'] for c in asset['m_CharacterTable']}
    pairs = {}
    for pair in asset['m_FontFeatureTable']['m_GlyphPairAdjustmentRecords']:
        a, b = pair['m_FirstAdjustmentRecord'], pair['m_SecondAdjustmentRecord']
        pairs[a['m_GlyphIndex'], b['m_GlyphIndex']] = a['m_GlyphValueRecord']['m_XAdvance']
    image = Image.new('RGB', (1600, 1000), (22, 22, 21))
    spread = asset['m_AtlasPadding'] + 1
    atlas_height = alpha.height

    def line(content, baseline, size):
        scale = size / asset['m_FaceInfo']['m_PointSize']
        indices = [chars[ord(c)] for c in content]
        advances = [glyphs[index]['m_Metrics']['m_HorizontalAdvance'] +
                    (pairs.get((index, indices[i + 1]), 0) if i + 1 < len(indices) else 0)
                    for i, index in enumerate(indices)]
        x = (image.width - sum(advances) * scale) / 2
        for index, advance in zip(indices, advances):
            g = glyphs[index]
            r, m = g['m_GlyphRect'], g['m_Metrics']
            rx, ry, w, h = [r['m_' + k] for k in ('X', 'Y', 'Width', 'Height')]
            if w and h:
                tile = alpha.crop((rx - spread, atlas_height - ry - h - spread,
                                   rx + w + spread, atlas_height - ry + spread))
                tile = tile.resize((round(tile.width * scale), round(tile.height * scale)), Image.Resampling.BILINEAR)
                coverage = np.clip((np.asarray(tile).astype(float) / 255 - .5) * 2 * spread * scale + .5, 0, 1)
                mask = Image.fromarray(np.rint(coverage * 255).astype(np.uint8))
                xy = (round(x + (m['m_HorizontalBearingX'] - spread) * scale),
                      round(baseline - (m['m_HorizontalBearingY'] + spread) * scale))
                image.paste((225, 220, 204), xy + (xy[0] + mask.width, xy[1] + mask.height), mask)
            x += advance * scale
    line('Haunted Intertitle', 125, 114)
    line('CPU reconstruction from the static TMP SDF atlas', 186, 43)
    line('Guide to', 337, 144)
    line('all Fish', 520, 186)
    line('the Ghost   Wowza!', 680, 100)
    line('ABCDEFGHIJKLMNOPQRSTUVWXYZ', 785, 60)
    line('abcdefghijklmnopqrstuvwxyz', 858, 65)
    line('0123456789 .,:;!? “quotes” — ← ↑ → ↓', 935, 56)
    output.parent.mkdir(parents=True, exist_ok=True)
    image.save(output)
    print(str(output))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=ROOT / 'Tools/Fonts/Proofs/HauntedIntertitle-TMP-CPU-proof.png')
    render(parser.parse_args().output)
