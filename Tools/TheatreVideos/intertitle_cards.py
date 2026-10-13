"""Original Haunted Intertitle silent-card authoring, shared by QA stills and video assembly.

Original font outlines and connected engraved flourishes follow the approved
reference direction; no reference-image pixels are used. This module never creates gameplay or video.
"""
from functools import lru_cache
import math
from pathlib import Path
import random
import re

from PIL import Image, ImageDraw, ImageFont
from intertitle_borders import draw_frame

ROOT = Path(__file__).resolve().parents[2]
FONT = ROOT / 'Assets/Art/Fonts/HauntedIntertitle-Regular.ttf'
SIZE = (1280, 720)
OPENINGS = {'Fish': 'Guide to all Fish', 'Ghost': 'Guide to the Ghost'}
IVORY = (225, 220, 204)
RULE = (151, 147, 132)
WORDS_PER_SECOND = 2.7  # Keep the established reading pace; shorten copy instead.
MIN_SECONDS = 3.5


def card_seconds(heading, body):
    # The one-second allowance covers the brief heading. Openings contain no body.
    return math.ceil(max(MIN_SECONDS, len(body.split()) / WORDS_PER_SECOND + 1.0) * 30) / 30


def duration_report(steps):
    cards = [{'heading': values[0], 'body_words': len(values[1].split()),
              'seconds': card_seconds(*values)}
             for kind, values in steps if kind == 'TutorialCaption']
    gameplay = sum(float(values[-1]) for kind, values in steps if kind == 'TutorialDemo')
    source = sum(float(values[-1]) for kind, values in steps if kind != 'TutorialCaption')
    reading = round(sum(card['seconds'] for card in cards), 3)
    return {'card_count': len(cards), 'body_words': sum(card['body_words'] for card in cards),
            'card_seconds': reading, 'gameplay_seconds': round(gameplay, 3),
            'estimated_finished_seconds': round(reading + gameplay, 3),
            'minimum_source_seconds': round(source, 3), 'cards': cards}


@lru_cache(maxsize=1)
def background():
    """Deterministic subtle original grain; no pixels copied from the preview."""
    rng = random.Random(112358)
    pixels = bytearray()
    for y in range(SIZE[1]):
        for x in range(SIZE[0]):
            radius = ((x - 640) / 740) ** 2 + ((y - 360) / 480) ** 2
            value = max(12, min(27, round(23 - 6 * min(1, radius) + rng.gauss(0, .7))))
            pixels.extend((value, value, max(0, value - 1)))
    return Image.frombytes('RGB', SIZE, bytes(pixels))


def wrap_body(text, font, max_width=1000):
    """Pixel-measured sentence lines, never a font fallback or smaller text."""
    if not text.strip():
        return []
    draw = ImageDraw.Draw(Image.new('RGB', (1, 1)))
    lines = []
    for sentence in re.split(r'(?<=[.!?])\s+|\n+', text.upper()):
        if not sentence:
            continue
        current = ''
        for word in sentence.split():
            if draw.textlength(word, font=font) > max_width:
                raise ValueError('A single word exceeds the card safe area: ' + word)
            proposed = (current + ' ' + word).strip()
            if current and draw.textlength(proposed, font=font) > max_width:
                lines.append(current)
                current = word
            else:
                current = proposed
        if current:
            lines.append(current)
    if len(lines) > 5:
        raise ValueError('Card has more than five readable lines; shorten its copy.')
    return lines


def render_card(role, index, heading, body):
    """Return a static 1280x720 card and inspectable layout bounds."""
    if role not in OPENINGS:
        raise ValueError('Unknown guide role: ' + role)
    opening = heading == OPENINGS[role]
    if heading.startswith('Guide to') and (not opening or index != 0):
        raise ValueError('Only the exact opening name is permitted, on card zero.')
    if opening and body:
        raise ValueError('The opening is title-only; put instructions on following cards.')
    scale = 2
    im = background().resize((2560, 1440), Image.Resampling.BICUBIC)
    draw = ImageDraw.Draw(im)
    bounds = []

    def font(size): return ImageFont.truetype(str(FONT), int(size * scale))
    def line(points, fill=RULE, width=1):
        draw.line([(int(x * scale), int(y * scale)) for x, y in points], fill=fill, width=max(1, round(width * scale)))
    def text(content, y, size):
        f = font(size)
        box = draw.textbbox((640 * scale, y * scale), content, font=f, anchor='mt')
        normalized = tuple(round(value / scale, 2) for value in box)
        if normalized[0] < 115 or normalized[2] > 1165 or normalized[1] < 100 or normalized[3] > 615:
            raise ValueError(f'Card text exceeds its safe area: {content!r}: {normalized}')
        bounds.append(normalized)
        draw.text((640 * scale, y * scale), content, fill=IVORY, font=f, anchor='mt')
    def ornament(y, half_width):
        line([(640 - half_width, y), (620, y)], width=.7)
        line([(660, y), (640 + half_width, y)], width=.7)
        line([(640, y - 4), (644, y), (640, y + 4), (636, y), (640, y - 4)], width=.8)
    border = draw_frame(im, opening, scale)
    if opening:
        text('Guide to', 170, 176)
        text('all Fish' if role == 'Fish' else 'the Ghost', 344, 212)
    else:
        text('Wowza' if heading == 'Wowza' else heading.upper(), 143, 60)
        ornament(219, 61)
        body_font = font(46)
        # Measure at the same 2x raster scale used for drawing.
        lines = wrap_body(body, body_font, 1000 * scale)
        for row, content in enumerate(lines):
            text(content, 265 + row * 56, 46)
    return im.resize(SIZE, Image.Resampling.LANCZOS), {'opening': opening, 'text_bounds': bounds,
                                                     'body_lines': [] if opening else lines,
                                                     'heading': heading, 'body': body, 'border': border}
