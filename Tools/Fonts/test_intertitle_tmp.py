"""Independent source/serialization/atlas checks; these are not Unity import tests."""
import json
import math
from pathlib import Path
import re
import tempfile
import unittest

from fontTools.ttLib import TTFont
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import distance_transform_edt
import yaml

from build_intertitle_tmp import ROOT, SOURCE, OUTPUT, BASENAME, POINT_SIZE, PADDING, ATLAS_SIZE, guid, sha256, build


def unity_document(path, root):
    source = path.read_text()
    source = re.sub(r'^%.*\n', '', source, flags=re.M)
    source = re.sub(r'^--- !u!\d+ &\d+', '---', source, flags=re.M)
    return yaml.safe_load(source)[root]


class IntertitleTMPTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.font = TTFont(SOURCE)
        cls.asset = unity_document(OUTPUT / (BASENAME + '.asset'), 'MonoBehaviour')
        cls.material = unity_document(OUTPUT / (BASENAME + '.mat'), 'Material')
        cls.atlas = np.asarray(Image.open(OUTPUT / (BASENAME + ' Atlas.png')))[:, :, 3]
        cls.cmap = cls.font.getBestCmap()
        cls.glyphs = {r['m_Index']: r for r in cls.asset['m_GlyphTable']}
        cls.chars = {r['m_Unicode']: r['m_GlyphIndex'] for r in cls.asset['m_CharacterTable']}
        cls.report = json.loads((OUTPUT / 'HauntedIntertitle-SDF-build.json').read_text())

    def test_source_and_every_generated_asset_match_manifest(self):
        self.assertEqual(self.report['source_sha256'], sha256(SOURCE))
        self.assertEqual(self.report['family'], 'Haunted Intertitle')
        self.assertEqual(self.report['status'], 'cpu_authored_static_sdf_native_import_pending')
        self.assertIn('NOT RUN', self.report['native_validation'])
        for filename, digest in self.report['files'].items():
            self.assertEqual(sha256(OUTPUT / filename), digest, filename)

    def test_exact_complete_cmap_without_system_or_fallback_font(self):
        self.assertEqual(self.chars, {c: self.font.getGlyphID(g) for c, g in self.cmap.items()})
        self.assertTrue(set(range(32, 127)) <= set(self.chars))
        self.assertTrue(all(index != 0 for index in self.chars.values()))
        self.assertEqual(len(self.glyphs), len(set(self.chars.values())))
        self.assertEqual(self.asset['m_FallbackFontAssetTable'], [])
        self.assertEqual(self.asset['m_AtlasPopulationMode'], 0)
        self.assertEqual(self.asset['m_AtlasTextureIndex'], 0)
        self.assertEqual(self.asset['m_SourceFontFileGUID'], guid(SOURCE))
        self.assertEqual(self.asset['m_SourceFontFile_EditorRef'], {'fileID': 12800000, 'guid': guid(SOURCE), 'type': 3})
        self.assertEqual(self.asset['m_SourceFontFile'], {'fileID': 0})

    def test_correct_script_shader_material_atlas_and_import_references(self):
        self.assertEqual(self.asset['m_Script']['guid'], '71c1514a6bd24e1e882cebbe1904ce04')
        self.assertEqual(self.material['m_Shader']['guid'], guid(ROOT / 'Assets/TextMesh Pro/Shaders/TMP_SDF.shader'))
        self.assertEqual(self.asset['material']['guid'], guid(OUTPUT / (BASENAME + '.mat')))
        atlas_ref = {'fileID': 2800000, 'guid': guid(OUTPUT / (BASENAME + ' Atlas.png')), 'type': 3}
        self.assertEqual(self.asset['m_AtlasTextures'], [atlas_ref])
        properties = self.material['m_SavedProperties']
        self.assertEqual(properties['m_TexEnvs'][0]['_MainTex']['m_Texture'], atlas_ref)
        values = {k: v for entry in properties['m_Floats'] for k, v in entry.items()}
        self.assertEqual(values['_GradientScale'], PADDING + 1)
        self.assertEqual(values['_TextureWidth'], ATLAS_SIZE)
        self.assertEqual(values['_TextureHeight'], ATLAS_SIZE)
        self.assertEqual(self.atlas.shape, (ATLAS_SIZE, ATLAS_SIZE))
        importer = yaml.safe_load((OUTPUT / (BASENAME + ' Atlas.png.meta')).read_text())['TextureImporter']
        self.assertEqual(importer['mipmaps']['sRGBTexture'], 0)
        self.assertEqual(importer['mipmaps']['enableMipMap'], 0)
        self.assertEqual(importer['alphaIsTransparency'], 0)
        self.assertEqual(importer['platformSettings'][0]['textureCompression'], 0)
        self.assertEqual(importer['platformSettings'][0]['textureFormat'], 4)
        self.assertEqual(importer['textureSettings']['filterMode'], 1)
        self.assertEqual(importer['textureSettings']['wrapU'], 1)

    def test_face_and_glyph_metrics_match_true_type(self):
        scale = POINT_SIZE / self.font['head'].unitsPerEm
        face = self.asset['m_FaceInfo']
        self.assertEqual(face['m_UnitsPerEM'], self.font['head'].unitsPerEm)
        self.assertAlmostEqual(face['m_AscentLine'], self.font['hhea'].ascent * scale)
        self.assertAlmostEqual(face['m_DescentLine'], self.font['hhea'].descent * scale)
        self.assertAlmostEqual(face['m_CapLine'], self.font['OS/2'].sCapHeight * scale)
        for name in set(self.cmap.values()):
            metrics = self.glyphs[self.font.getGlyphID(name)]['m_Metrics']
            outline = self.font['glyf'][name]
            outline.recalcBounds(self.font['glyf'])
            self.assertAlmostEqual(metrics['m_HorizontalAdvance'], self.font['hmtx'].metrics[name][0] * scale, places=5)
            if outline.numberOfContours:
                self.assertEqual(metrics['m_HorizontalBearingX'], math.floor(outline.xMin * scale))
                self.assertEqual(metrics['m_HorizontalBearingY'], math.ceil(outline.yMax * scale))
                self.assertEqual(metrics['m_Width'], math.ceil(outline.xMax * scale) - math.floor(outline.xMin * scale))
                self.assertEqual(metrics['m_Height'], math.ceil(outline.yMax * scale) - math.floor(outline.yMin * scale))

    def test_rectangles_and_padding_do_not_overlap(self):
        occupied = np.zeros((ATLAS_SIZE, ATLAS_SIZE), dtype=np.bool_)
        for glyph in self.glyphs.values():
            rect = glyph['m_GlyphRect']
            x, y, width, height = [rect['m_' + k] for k in ('X', 'Y', 'Width', 'Height')]
            if not width or not height:
                continue
            pad = PADDING + 1
            x0, y0, x1, y1 = x - pad, y - pad, x + width + pad, y + height + pad
            self.assertTrue(0 <= x0 < x1 < ATLAS_SIZE)
            self.assertTrue(0 <= y0 < y1 < ATLAS_SIZE)
            region = occupied[y0:y1, x0:x1]
            self.assertFalse(region.any(), str(glyph['m_Index']))
            region[:] = True
            bitmap = self.atlas[ATLAS_SIZE-y1:ATLAS_SIZE-y0, x0:x1]
            self.assertTrue(np.any(bitmap > 128), str(glyph['m_Index']))
            # At a guard texel's center, up to half a texel of the signed
            # distance ramp remains; this is <=5/255 for spread13.
            edge_ceiling = math.ceil(255 / (4 * pad))
            self.assertLessEqual(int(bitmap[0].max()), edge_ceiling)
            self.assertLessEqual(int(bitmap[-1].max()), edge_ceiling)
            self.assertLessEqual(int(bitmap[:, 0].max()), edge_ceiling)
            self.assertLessEqual(int(bitmap[:, -1].max()), edge_ceiling)

    def test_atlas_outline_matches_ttf_and_is_not_vertically_flipped(self):
        # Independent FreeType mask at 4x (builder uses 8x). Reconstruct from the
        # serialized bottom-left glyph rectangles, not builder's private records.
        raster = ImageFont.truetype(str(SOURCE), POINT_SIZE * 4)
        for codepoint, index in self.chars.items():
            glyph = self.glyphs[index]
            r, m = glyph['m_GlyphRect'], glyph['m_Metrics']
            w, h, x, y = [r['m_' + k] for k in ('Width', 'Height', 'X', 'Y')]
            if not w or not h:
                continue
            pad = PADDING + 1
            mask = Image.new('L', ((w + 2 * pad) * 4, (h + 2 * pad) * 4))
            ImageDraw.Draw(mask).text(((pad - m['m_HorizontalBearingX']) * 4,
                                     (pad + m['m_HorizontalBearingY']) * 4), chr(codepoint),
                                    font=raster, fill=255, anchor='ls')
            expected = np.asarray(mask.resize((w + 2 * pad, h + 2 * pad), Image.Resampling.BOX)) >= 128
            actual = self.atlas[ATLAS_SIZE-y-h-pad:ATLAS_SIZE-y+pad, x-pad:x+w+pad] >= 128
            difference = actual ^ expected
            self.assertTrue(actual.any(), chr(codepoint))
            intersection = np.count_nonzero(actual & expected)
            union = np.count_nonzero(actual | expected)
            # An independent 4x mask can differ by one thresholded row
            # on a 4px underline or an 8px-wide curly quote. Keep contour tolerance equally strict
            # for all shapes; use area ratio appropriate to thin marks.
            self.assertGreater(intersection / union, .70 if min(w, h) < 6 else .80 if min(w, h) < 20 else .86, f'U+{codepoint:04X}')
            # Every contour discrepancy must remain within one raster pixel.
            distance = np.where(expected, distance_transform_edt(expected), distance_transform_edt(~expected))
            self.assertLessEqual(float(distance[difference].max()) if difference.any() else 0, 1.42, f'U+{codepoint:04X}')

    def test_authored_kerning_preserved(self):
        expected = {}
        if 'kern' in self.font:
            for table in self.font['kern'].kernTables:
                if table.version == 0 and table.coverage & 1:
                    for (a, b), value in table.kernTable.items():
                        key = (self.font.getGlyphID(a), self.font.getGlyphID(b))
                        expected[key] = expected.get(key, 0) + value
        actual = {}
        for pair in self.asset['m_FontFeatureTable']['m_GlyphPairAdjustmentRecords']:
            a, b = pair['m_FirstAdjustmentRecord'], pair['m_SecondAdjustmentRecord']
            actual[a['m_GlyphIndex'], b['m_GlyphIndex']] = a['m_GlyphValueRecord']['m_XAdvance']
        expected = {k: v * POINT_SIZE / self.font['head'].unitsPerEm for k, v in expected.items() if v}
        self.assertEqual(actual.keys(), expected.keys())
        for key in expected:
            self.assertAlmostEqual(actual[key], expected[key], places=5)

    def test_caption_coverage_and_guide_selection(self):
        import sys
        sys.path.insert(0, str(ROOT / 'Tools/TheatreVideos'))
        from capture_contract import instructions
        from intertitle_cards import FONT
        self.assertEqual(FONT, SOURCE)
        for role in ('Fish', 'Ghost'):
            for kind, values in instructions(ROOT / f'Assets/Dialogue/TutorialRecording/{role}Recording.ink'):
                if kind != 'TutorialCaption':
                    continue
                text = ' '.join(values)
                for char in text + text.upper():
                    if not char.isspace():
                        self.assertIn(ord(char), self.chars, repr(char))
        self.assertIn(guid(SOURCE), (ROOT / 'Assets/Resources/Tutorials/TutorialRecordingStage.prefab').read_text())

    def test_rebuild_is_byte_identical_and_preserves_guids(self):
        with tempfile.TemporaryDirectory(prefix='intertitle-sdf-check-') as folder:
            build(SOURCE, Path(folder))
            for filename in self.report['files']:
                self.assertEqual(sha256(Path(folder) / filename), sha256(OUTPUT / filename), filename)
                self.assertEqual(guid(Path(folder) / filename), guid(OUTPUT / filename))


if __name__ == '__main__':
    unittest.main()
