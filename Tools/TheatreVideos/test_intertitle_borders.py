"""Checks for real renderer artwork, separate from native Unity verification."""
import unittest

from PIL import Image, ImageChops

from capture_contract import instructions
from intertitle_borders import ART_VERSION, draw_frame
from intertitle_cards import ROOT, OPENINGS, render_card


class IntertitleBorderTests(unittest.TestCase):
    def frame(self, opening):
        image = Image.new('RGB', (2560, 1440))
        contract = draw_frame(image, opening, scale=2)
        return image, contract

    def test_straight_double_rule_and_no_rounded_ui_frame(self):
        image, contract = self.frame(True)
        self.assertEqual(contract['outer_rule'], [43,43,1237,677])
        self.assertEqual(contract['inner_rule'], [49,49,1231,671])
        self.assertEqual(contract['version'], ART_VERSION)
        # Both complete engraved rails remain connected at the midpoint of each side.
        for x, y in ((43,360),(49,360),(1237,360),(1231,360),
                     (640,43),(640,49),(400,677),(400,671)):
            self.assertNotEqual(image.getpixel((x*2,y*2)), (0,0,0))

    def test_generous_central_text_safe_area_has_no_artwork(self):
        for opening in (True, False):
            image, contract = self.frame(opening)
            x0,y0,x1,y1 = contract['text_safe_area']
            self.assertIsNone(image.crop((x0*2,y0*2,x1*2,y1*2)).getbbox())

    def test_instruction_corners_use_same_lighter_vocabulary(self):
        opening, a = self.frame(True)
        instruction, b = self.frame(False)
        self.assertEqual(a['corner_scale'],1)
        self.assertEqual(b['corner_scale'],.62)
        self.assertGreater(sum(opening.convert('L').histogram()[1:]),
                           sum(instruction.convert('L').histogram()[1:]))
        # No large center engraving on an instruction card.
        self.assertIsNone(instruction.crop((450*2,600*2,830*2,669*2)).getbbox())
        self.assertIsNotNone(opening.crop((450*2,600*2,830*2,669*2)).getbbox())

    def test_all_actual_card_text_bounds_clear_the_vector_artwork(self):
        frames = {opening: self.frame(opening)[0] for opening in (True,False)}
        for role in OPENINGS:
            steps = instructions(ROOT / f'Assets/Dialogue/TutorialRecording/{role}Recording.ink')
            captions = [values for kind, values in steps if kind == 'TutorialCaption']
            for index, (heading, body) in enumerate(captions):
                _, layout = render_card(role,index,heading,body)
                for box in layout['text_bounds']:
                    # Include two pixels of breathing room beyond text's actual ink bounds.
                    x0,y0,x1,y1 = box
                    crop = frames[layout['opening']].crop((int((x0-2)*2),int((y0-2)*2),
                                                           int((x1+2)*2),int((y1+2)*2)))
                    self.assertIsNone(crop.getbbox(), f'{role} {heading}: ornament/text collision')

    def test_authored_art_is_deterministic(self):
        a,_ = self.frame(True)
        b,_ = self.frame(True)
        self.assertIsNone(ImageChops.difference(a,b).getbbox())


if __name__ == '__main__':
    unittest.main()
