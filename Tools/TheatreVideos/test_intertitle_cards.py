import json
import math
from pathlib import Path
import unittest

from capture_contract import instructions, sha256
from intertitle_cards import ROOT, FONT, OPENINGS, card_seconds, duration_report, render_card, wrap_body
from PIL import ImageFont


class IntertitleCardTests(unittest.TestCase):
    def steps(self, role):
        return instructions(ROOT / f'Assets/Dialogue/TutorialRecording/{role}Recording.ink')

    def test_original_font_and_provenance(self):
        from fontTools.ttLib import TTFont
        font = TTFont(FONT)
        self.assertEqual(font['name'].getDebugName(1), 'Haunted Intertitle')
        self.assertEqual(FONT.name, 'HauntedIntertitle-Regular.ttf')
        manifest = json.loads(FONT.with_name('HauntedIntertitle-SDF-build.json').read_text())
        self.assertEqual(sha256(FONT), manifest['source_sha256'])
        self.assertTrue(set(range(32, 127)) <= set(font.getBestCmap()))

    def test_all_current_cards_fit_without_shrinking(self):
        count = 0
        for role in OPENINGS:
            captions = [v for k, v in self.steps(role) if k == 'TutorialCaption']
            self.assertEqual(captions[0], [OPENINGS[role], ''])
            self.assertEqual(sum(h.startswith('Guide to') for h, _ in captions), 1)
            for index, (heading, body) in enumerate(captions):
                with self.subTest(role=role, heading=heading):
                    image, layout = render_card(role, index, heading, body)
                    self.assertEqual(image.size, (1280, 720))
                    self.assertEqual(layout['heading'], heading)
                    self.assertLessEqual(len(layout['body_lines']), 5)
                    for x0, y0, x1, y1 in layout['text_bounds']:
                        self.assertTrue(115 <= x0 <= x1 <= 1165)
                        self.assertTrue(100 <= y0 <= y1 <= 615)
                    count += 1
        self.assertEqual(count, 26)

    def test_exact_openings_and_no_repeated_role_cards(self):
        plan = json.loads((ROOT / 'Tools/TheatreVideos/recording-plan.json').read_text())
        self.assertEqual(plan['presentation']['opening_titles'], OPENINGS)
        for args in [('Ghost', 0, 'Guide to all Ghost', ''), ('Fish', 1, 'Guide to all Fish', '')]:
            with self.assertRaisesRegex(ValueError, 'exact opening'):
                render_card(*args)

    def test_opening_is_title_only(self):
        with self.assertRaisesRegex(ValueError, 'title-only'):
            render_card('Fish', 0, OPENINGS['Fish'], 'Extra text')

    def test_wowza_case_and_no_role_label(self):
        _, layout = render_card('Ghost', 5, 'Wowza', '')
        self.assertFalse(layout['opening'])
        self.assertEqual(layout['heading'], 'Wowza')
        self.assertEqual(len(layout['text_bounds']), 1)

    def test_reading_pace_has_not_increased(self):
        for words in range(80):
            body = ' '.join(['word'] * words)
            self.assertEqual(card_seconds('Heading', body), math.ceil(max(3.5, words / 2.7 + 1) * 30) / 30)

    def test_camera_setup_and_waits_cut_but_source_offsets_preserved(self):
        report = duration_report([('TutorialCaption', ['Title', '']), ('TutorialShot', ['wide', 2]),
                                  ('TutorialSetup', ['setup', 1.2]), ('Wait', [4]), ('TutorialDemo', ['real', 3])])
        self.assertEqual(report['minimum_source_seconds'], 10.2)
        self.assertEqual(report['gameplay_seconds'], 3)
        self.assertEqual(report['estimated_finished_seconds'], 6.5)

    def test_every_demonstration_retains_original_order_and_duration(self):
        expected = {
            'Fish': [('fish-walk', 3), ('fish-run', 2), ('fish-jump', 3), ('fish-obstacle', 1.6),
                     ('tablecart-hit', 5), ('danger-band', 2), ('fish-light', 5), ('chandelier-sway', 3),
                     ('fall', 2.5), ('angler-place', 1), ('angler-light', 3), ('angler-sweep', 3.5)],
            'Ghost': [('ghost-fly', 3), ('package', 3), ('carry', 3), ('invalid', 4), ('place', 2),
                      ('possess', 2), ('danger-band', 2), ('cart-move', 2), ('tablecart-hit', 5),
                      ('unpossess', 2), ('chandelier', 3), ('chandelier-sway', 3), ('fall', 2.5),
                      ('reset', 2.5), ('click', 2.5), ('dispose', 2), ('angler-place', 1),
                      ('angler-light', 3), ('angler-sweep', 3.5)]}
        for role, demos in expected.items():
            self.assertEqual([tuple(v) for k, v in self.steps(role) if k == 'TutorialDemo'], demos)

    def test_estimates_current_and_under_target(self):
        saved = json.loads((ROOT / 'Tools/TheatreVideos/CardPreviews/HauntedIntertitle-v1/estimated-timings.json').read_text())
        self.assertEqual(saved['native_import_and_capture'], 'pending')
        for role, target in [('Fish', 90), ('Ghost', 120)]:
            report = duration_report(self.steps(role))
            self.assertLessEqual(report['estimated_finished_seconds'], target)
            self.assertEqual(saved['roles'][role]['ink_sha256'], sha256(ROOT / f'Assets/Dialogue/TutorialRecording/{role}Recording.ink'))
            for key, value in report.items(): self.assertEqual(saved['roles'][role][key], value)

    def test_long_copy_fails_instead_of_getting_tiny(self):
        with self.assertRaisesRegex(ValueError, 'shorten'):
            wrap_body('Long sentence. ' * 20, ImageFont.truetype(str(FONT), 46))


if __name__ == '__main__': unittest.main()
