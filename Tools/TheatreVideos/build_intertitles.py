"""Rebuild silent guides from archived Unity footage and complete mechanics Ink.

Requires Pillow and ffmpeg. Output preserves existing Unity asset GUIDs.
"""
from pathlib import Path
import argparse, json, math, re, subprocess, textwrap
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument('--sources', type=Path, default=Path('C:/Users/wkeif/Documents/Codex/2026-10-11/task-4/TutorialEvidence'))
parser.add_argument('--ffmpeg', default='C:/ffmpeg/bin/ffmpeg.exe')
args = parser.parse_args()
WORK = ROOT / 'Tools/TheatreVideos/Evidence'
WORK.mkdir(parents=True, exist_ok=True)
CREAM = (248, 234, 205)
heading_font = ImageFont.truetype(str(ROOT / 'Assets/Art/Fonts/Modak-Regular.ttf'), 54)
body_font = ImageFont.truetype('C:/Windows/Fonts/georgia.ttf', 37)

def run(arguments):
    subprocess.run([args.ffmpeg, '-hide_banner', '-loglevel', 'error', '-y', *arguments], check=True)

def card(role, index, heading, body):
    im = Image.new('RGB', (1280, 720), (19, 19, 22))
    d = ImageDraw.Draw(im)
    for inset, width in [(34, 2), (45, 1)]:
        d.rectangle((inset, inset, 1279-inset, 719-inset), outline=CREAM, width=width)
    for x, sx in [(66, 1), (1214, -1)]:
        for y, sy in [(66, 1), (654, -1)]:
            d.line([(x, y+sy*35), (x, y), (x+sx*35, y)], fill=CREAM, width=3)
            d.ellipse((x-4, y-4, x+4, y+4), outline=CREAM, width=2)
    lines = textwrap.wrap(body, width=49)
    assert len(lines) <= 7, body
    # Measure every line to guarantee it stays inside the frame.
    assert max(d.textlength(line, font=body_font) for line in lines) < 1100
    title_lines = textwrap.wrap(heading.upper(), width=29)
    y = 170 if len(title_lines) == 1 else 130
    for line in title_lines:
        d.text((640, y), line, font=heading_font, fill=CREAM, anchor='mt')
        y += 68
    d.line((480, y+22, 800, y+22), fill=CREAM, width=2)
    y += 62
    for line in lines:
        d.text((640, y), line, font=body_font, fill=CREAM, anchor='mt')
        y += 47
    assert y < 645, (heading, y)
    path = WORK / f'{role}-card-{index:02}.png'
    im.save(path)
    return path

manifest = {}
for role, filename in [('Fish', 'FishTutorial-20261011-195827.mp4'), ('Ghost', 'GhostTutorial-20261011-195910.mp4')]:
    source = args.sources / filename
    assert source.exists(), source
    source_duration = float(subprocess.check_output([str(Path(args.ffmpeg).with_name('ffprobe.exe')), '-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', str(source)]))
    ink = (ROOT / f'Assets/Dialogue/TutorialRecording/{role}Recording.ink').read_text(encoding='utf-8-sig')
    instructions = []
    for line in ink.splitlines():
        match = re.match(r'~ (\w+)\((.*)\)', line)
        if match:
            instructions.append((match[1], json.loads('['+match[2]+']')))
    timeline, pieces, chapters = 0.0, [], []
    for kind, values in instructions:
        if kind == 'TutorialCaption':
            heading, body = values
            seconds = max(3.5, len(body.split()) / 2.7 + 1.0)
            seconds = math.ceil(seconds * 30) / 30
            picture = card(role, len(chapters), heading, body)
            segment = WORK / f'{role}-segment-{len(pieces):02}.mp4'
            run(['-loop', '1', '-i', str(picture), '-t', str(seconds), '-r', '30', '-an', '-c:v', 'libx264', '-preset', 'fast', '-crf', '20', '-pix_fmt', 'yuv420p', str(segment)])
            chapters.append({'heading': heading, 'body': body, 'card_seconds': seconds})
            pieces.append(segment)
        else:
            seconds = float(values[-1])
            # Waits previously held explanatory overlays. Their replacement is the card.
            if kind != 'Wait' and seconds > 0 and timeline + seconds <= source_duration - .02:
                segment = WORK / f'{role}-segment-{len(pieces):02}.mp4'
                # Strip the baked lower captions and upper role label. Retain the
                # complete gameplay viewport, letterboxed without stretching it.
                run(['-ss', str(timeline), '-i', str(source), '-t', str(seconds), '-vf', 'crop=1280:460:0:68,pad=1280:720:0:130:black,setsar=1,fps=30', '-an', '-c:v', 'libx264', '-preset', 'fast', '-crf', '20', '-pix_fmt', 'yuv420p', str(segment)])
                pieces.append(segment)
            timeline += seconds
    listing = WORK / f'{role}-concat.txt'
    listing.write_text(''.join("file '"+p.as_posix()+"'\n" for p in pieces), encoding='utf-8')
    target = ROOT / f'Assets/TutorialVideos/{role}TutorialProduction.mp4'
    # Validate a complete temporary movie before replacing the asset.
    output = WORK / f'{role}Intertitles.mp4'
    run(['-f', 'concat', '-safe', '0', '-i', str(listing), '-c', 'copy', '-movflags', '+faststart', str(output)])
    run(['-i', str(output), '-f', 'null', '-'])
    target.write_bytes(output.read_bytes())
    manifest[role] = {'source': str(source), 'source_seconds': source_duration, 'chapters': chapters, 'segments': len(pieces), 'output': str(target), 'note': 'Archived Ghost footage ends during drop; later mechanics are explained by cards.' if role == 'Ghost' else ''}
(WORK / 'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
print('Built and fully decoded both silent intertitle guides.')
