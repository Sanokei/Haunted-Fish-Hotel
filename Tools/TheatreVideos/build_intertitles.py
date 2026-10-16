"""Rebuild silent guides only from verified current Unity footage and mechanics Ink.

Requires Pillow and ffmpeg. Output preserves existing Unity asset GUIDs.
"""
from pathlib import Path
import argparse, json, shutil, subprocess
from capture_contract import validated_captures
from intertitle_cards import card_seconds, render_card, duration_report

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument('--capture-manifest', type=Path, required=True, help='Hashed current native recordings and observations; see README.md')
parser.add_argument('--plan', type=Path, default=ROOT / 'Tools/TheatreVideos/recording-plan.json')
parser.add_argument('--ffmpeg', default=shutil.which('ffmpeg') or 'ffmpeg')
parser.add_argument('--ffprobe', default=None)
args = parser.parse_args()
try:
    captures = validated_captures(ROOT, args.capture_manifest, args.plan)
except (ValueError, OSError, KeyError) as error:
    parser.error(str(error))
WORK = ROOT / 'Tools/TheatreVideos/Evidence'
UNDERCRANK = 1.2  # gameplay plays at 120%, like hand-cranked footage projected at speed
IRIS_SECONDS, IRIS_RADIUS = .45, 760  # radius covers the 1280x720 frame corner
WORK.mkdir(parents=True, exist_ok=True)


def run(arguments):
    subprocess.run([args.ffmpeg, '-hide_banner', '-loglevel', 'error', '-y', *arguments], check=True)


def card(role, index, heading, body):
    picture, _ = render_card(role, index, heading, body)
    path = WORK / f'{role}-card-{index:02}.png'
    picture.save(path)
    return path


manifest, replacements = {}, []
for role, capture in captures.items():
    source = capture['source']
    suffix = '.exe' if Path(args.ffmpeg).suffix.lower() == '.exe' else ''
    ffprobe = args.ffprobe or str(Path(args.ffmpeg).with_name('ffprobe' + suffix))
    source_duration = float(subprocess.check_output([ffprobe, '-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', str(source)]))
    instructions = capture['steps']
    required_duration = sum(float(values[-1]) for kind, values in instructions if kind != 'TutorialCaption')
    if source_duration + 1 / 30 < required_duration:
        raise ValueError(f'{role}: source ends at {source_duration}s before the complete {required_duration}s Ink sequence. Record the missing gameplay; do not replace it with cards.')
    timeline, pieces, chapters = 0.0, [], []
    for kind, values in instructions:
        if kind == 'TutorialCaption':
            heading, body = values
            seconds = card_seconds(heading, body)
            picture = card(role, len(chapters), heading, body)
            segment = WORK / f'{role}-segment-{len(pieces):02}.mp4'
            run(['-loop', '1', '-i', str(picture), '-t', str(seconds), '-r', '30', '-an', '-c:v', 'libx264', '-preset', 'fast', '-crf', '20', '-pix_fmt', 'yuv420p', str(segment)])
            chapters.append({'heading': heading, 'body': body, 'card_seconds': seconds})
            pieces.append(segment)
        else:
            seconds = float(values[-1])
            # Cards replace waits. Setup and camera travel remain in source offsets,
            # but only actual demonstrations are emitted as gameplay.
            if kind == 'TutorialDemo' and seconds > 0:
                segment = WORK / f'{role}-segment-{len(pieces):02}.mp4'
                # Captions are not drawn during capture, so the full gameplay frame is kept.
                # Silent-comedy presentation: undercranked so action plays a touch fast,
                # and each scene opens and closes on an iris like a Keaton two-reeler.
                shown = seconds / UNDERCRANK
                iris = (f"geq=lum='if(lt(hypot(X-W/2,Y-H/2),{IRIS_RADIUS}*min(1,min(T,{shown:.3f}-T)/{IRIS_SECONDS})),lum(X,Y),0)'"
                        f":cb='if(lt(hypot(X-W/2,Y-H/2),{IRIS_RADIUS}*min(1,min(T,{shown:.3f}-T)/{IRIS_SECONDS})),cb(X,Y),128)'"
                        f":cr='if(lt(hypot(X-W/2,Y-H/2),{IRIS_RADIUS}*min(1,min(T,{shown:.3f}-T)/{IRIS_SECONDS})),cr(X,Y),128)'")
                run(['-ss', str(timeline), '-i', str(source), '-t', str(seconds), '-vf', f'scale=1280:720,setsar=1,setpts=PTS/{UNDERCRANK},fps=30,format=yuv420p,{iris}', '-an', '-c:v', 'libx264', '-preset', 'fast', '-crf', '20', '-pix_fmt', 'yuv420p', str(segment)])
                pieces.append(segment)
            timeline += seconds
    listing = WORK / f'{role}-concat.txt'
    listing.write_text(''.join("file '"+p.as_posix()+"'\n" for p in pieces), encoding='utf-8')
    target = ROOT / f'Assets/TutorialVideos/{role}TutorialProduction.mp4'
    # Validate a complete temporary movie before replacing the asset.
    output = WORK / f'{role}Intertitles.mp4'
    run(['-f', 'concat', '-safe', '0', '-i', str(listing), '-c', 'copy', '-movflags', '+faststart', str(output)])
    run(['-i', str(output), '-f', 'null', '-'])
    replacements.append((target, output))
    manifest[role] = {'source': str(source), 'source_seconds': source_duration, 'chapters': chapters, 'duration_estimate': duration_report(instructions), 'segments': len(pieces), 'output': str(target), 'capture_manifest': str(args.capture_manifest), 'note': 'Current hashed native capture; full Ink sequence present.'}
# Do not replace either shipped asset until both complete outputs have decoded.
for target, output in replacements:
    target.write_bytes(output.read_bytes())
(WORK / 'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
print('Built and fully decoded both silent intertitle guides. Native theatre playback review is still required.')
