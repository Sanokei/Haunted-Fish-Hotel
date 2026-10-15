#!/usr/bin/env python3
"""Render static QA cards and estimate timing. Never reads or writes video."""
import argparse
import json
from pathlib import Path
from PIL import Image

from capture_contract import instructions, sha256
from intertitle_cards import ROOT, FONT, render_card, duration_report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output-dir', type=Path, default=ROOT / 'Tools/TheatreVideos/CardPreviews/HauntedIntertitle-v1')
    args = parser.parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    samples, reports = {}, {}
    for role in ('Fish', 'Ghost'):
        path = ROOT / f'Assets/Dialogue/TutorialRecording/{role}Recording.ink'
        steps = instructions(path)
        reports[role] = duration_report(steps)
        reports[role]['ink_sha256'] = sha256(path)
        for index, (_, (heading, body)) in enumerate(step for step in steps if step[0] == 'TutorialCaption'):
            picture, _ = render_card(role, index, heading, body)
            if index == 0 or role == 'Fish' and heading in ('A Gentleman Walks', 'Wowza', 'Dining cart'):
                name = role + ('-opening' if index == 0 else '-' + heading)
                picture.save(args.output_dir / (name + '.png'))
                samples[name] = picture
    sheet = Image.new('RGB', (2560, 1440))
    for name, position in [('Fish-opening', (0, 0)), ('Ghost-opening', (1280, 0)),
                           ('Fish-A Gentleman Walks', (0, 720)), ('Fish-Wowza', (1280, 720))]:
        sheet.paste(samples[name], position)
    sheet.save(args.output_dir / 'HauntedIntertitle-static-card-QA.png')
    report = {'status': 'source_estimates_and_static_cards_only', 'font': str(FONT.relative_to(ROOT)),
              'font_sha256': sha256(FONT), 'reading_words_per_second': 2.7,
              'minimum_card_seconds': 3.5, 'camera_travel_and_setup_cut': True,
              'native_import_and_capture': 'pending', 'roles': reports}
    (args.output_dir / 'estimated-timings.json').write_text(json.dumps(report, indent=2) + '\n')
    for role, values in reports.items():
        print(f"{role}: {values['card_count']} cards, {values['card_seconds']:.2f}s reading + "
              f"{values['gameplay_seconds']:.2f}s gameplay = {values['estimated_finished_seconds']:.2f}s estimated")
    print('Static card QA only. No footage or native gameplay evidence generated.')


if __name__ == '__main__':
    main()
