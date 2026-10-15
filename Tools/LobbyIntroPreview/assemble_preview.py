#!/usr/bin/env python3
"""Join the exterior crane frames and the Blender lobby frames into one review video plus a contact sheet.

    python assemble_preview.py --exterior DIR --lobby DIR --out Preview/

Exterior frames are 24 fps (compose_exterior.py); lobby frames are rendered at --lobby-fps (render_lobby_preview.py).
Adds the intro's caption during the world shot that carries it, and small labels saying what each half is. Needs ffmpeg.
"""
import argparse
import shutil
import subprocess
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
FONT = ROOT / 'Assets/Art/Fonts/HauntedIntertitle-Regular.ttf'
SIZE = (1280, 720)
CAPTION = 'Looks like nobody has checked into this place for years'
# seconds into the lobby sequence (LobbyIntroduction.ink): entry 1.5, walk 4.0, caption 4.5, reception 4.0, tunnel 6.0, wait 1.0
CAPTION_FROM, CAPTION_TO = 5.5, 10.0
SHOTS = [(0.0, 'entry'), (1.5, 'walk away'), (5.5, 'caption'), (10.0, 'reception dolly'), (14.0, 'crane + tilt into the frame tunnel'), (20.0, 'hold')]


def label(im, text, size=22, y=None):
    d = ImageDraw.Draw(im)
    f = ImageFont.truetype(str(FONT), size)
    box = d.textbbox((0, 0), text, font=f)
    x, y = 24, (SIZE[1] - 24 - (box[3] - box[1])) if y is None else y
    d.rectangle([x - 10, y - 6, x + box[2] - box[0] + 10, y + box[3] - box[1] + 10], fill=(0, 0, 0, 170))
    d.text((x, y), text, font=f, fill=(235, 228, 205))


def shot_name(t):
    name = SHOTS[0][1]
    for start, n in SHOTS:
        if t >= start:
            name = n
    return name


def annotate_lobby(src, dst, fps):
    dst.mkdir(parents=True, exist_ok=True)
    caption_font = ImageFont.truetype(str(FONT), 40)
    frames = sorted(src.glob('lobby_*.png'))
    for i, path in enumerate(frames):
        t = i / fps
        im = Image.open(path).convert('RGB').resize(SIZE, Image.LANCZOS).convert('RGBA')
        layer = Image.new('RGBA', SIZE, (0, 0, 0, 0))
        d = ImageDraw.Draw(layer)
        if CAPTION_FROM <= t < CAPTION_TO:
            w = d.textlength(CAPTION, font=caption_font)
            d.text(((SIZE[0] - w) / 2 + 3, 610 + 3), CAPTION, font=caption_font, fill=(0, 0, 0, 255))
            d.text(((SIZE[0] - w) / 2, 610), CAPTION, font=caption_font, fill=(246, 240, 220, 255))
        im = Image.alpha_composite(im, layer)
        label(im, f'LOBBY (Blender approximation)  ·  {shot_name(t)}')
        im.convert('RGB').save(dst / f'f_{i:04d}.png')
    return len(frames)


def annotate_exterior(src, dst):
    dst.mkdir(parents=True, exist_ok=True)
    frames = sorted(src.glob('ext_*.png'))
    for i, path in enumerate(frames):
        im = Image.open(path).convert('RGBA')
        label(im, 'EXTERIOR  ·  gate arrival crane, new fogged city behind the hotel')
        im.convert('RGB').save(dst / f'f_{i:04d}.png')
    return len(frames)


def sheet(images, path, cols=4):
    thumbs = [Image.open(p).convert('RGB').resize((480, 270), Image.LANCZOS) for p in images]
    rows = (len(thumbs) + cols - 1) // cols
    out = Image.new('RGB', (cols * 480, rows * 270))
    for i, t in enumerate(thumbs):
        out.paste(t, ((i % cols) * 480, (i // cols) * 270))
    out.save(path)


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--exterior', type=Path, required=True)
    p.add_argument('--lobby', type=Path, required=True)
    p.add_argument('--lobby-fps', type=int, default=15)
    p.add_argument('--out', type=Path, required=True)
    p.add_argument('--work', type=Path, default=Path('/tmp/lobby_preview_work'))
    a = p.parse_args()
    a.out.mkdir(parents=True, exist_ok=True)
    shutil.rmtree(a.work, ignore_errors=True)
    n_ext = annotate_exterior(a.exterior, a.work / 'ext')
    n_lob = annotate_lobby(a.lobby, a.work / 'lobby', a.lobby_fps)
    ext_len = n_ext / 24
    cmd = ['ffmpeg', '-y', '-loglevel', 'error',
           '-framerate', '24', '-i', str(a.work / 'ext/f_%04d.png'),
           '-framerate', str(a.lobby_fps), '-i', str(a.work / 'lobby/f_%04d.png'),
           '-filter_complex', f'[0:v]fps=24,format=yuv420p[a];[1:v]fps=24,format=yuv420p[b];[a][b]xfade=transition=fade:duration=0.6:offset={ext_len - 0.6:.3f}[v]',
           '-map', '[v]', '-c:v', 'libx264', '-crf', '20', '-preset', 'slow', '-pix_fmt', 'yuv420p', str(a.out / 'lobby-intro-preview.mp4')]
    subprocess.run(cmd, check=True)
    picks = [a.work / 'ext/f_0000.png', a.work / f'ext/f_{n_ext - 1:04d}.png']
    for t in (1.0, 3.0, 7.0, 12.0, 15.5, 17.5, 19.0, 20.8):
        picks.append(a.work / f'lobby/f_{min(n_lob - 1, int(t * a.lobby_fps)):04d}.png')
    sheet(picks, a.out / 'lobby-intro-contact-sheet.png')
    print('wrote', a.out / 'lobby-intro-preview.mp4', f'({ext_len + n_lob / a.lobby_fps - 0.6:.1f}s)')


if __name__ == '__main__':
    main()
