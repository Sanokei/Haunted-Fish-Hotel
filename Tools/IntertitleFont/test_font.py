#!/usr/bin/env python3
"""Regression checks on the authored TTF, independent of the TMP atlas."""
import hashlib
import re
import struct
import unittest
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.pens.areaPen import AreaPen
from fontTools.pens.recordingPen import RecordingPen
from PIL import ImageFont

ROOT=Path(__file__).resolve().parents[2]
FONT=ROOT/'Assets/Art/Fonts/HauntedIntertitle-Regular.ttf'

class HauntedIntertitleTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.font=TTFont(FONT,checkChecksums=2);cls.cmap=cls.font.getBestCmap()
    def test_identity_is_original_family(self):
        names=self.font['name']
        self.assertEqual(names.getDebugName(1),'Haunted Intertitle')
        self.assertEqual(names.getDebugName(6),'HauntedIntertitle-Regular')
        self.assertNotIn('Smythe',names.getDebugName(1))
        self.assertIn('No third-party font outlines',names.getDebugName(10))
    def test_complete_english_caption_coverage(self):
        self.assertFalse(set(range(32,127))-self.cmap.keys())
        for path in (ROOT/'Assets/Dialogue/TutorialRecording').glob('*Recording.ink'):
            for a,b in re.findall(r'TutorialCaption\("([^"\n]*)",\s*"([^"\n]*)"\)',path.read_text()):
                self.assertFalse({ord(c) for c in a+b}-self.cmap.keys(),path)
                self.assertFalse({ord(c) for c in (a+b).upper()}-self.cmap.keys(),path)
        for c in '\u00a0‘’“”–—…•←↑→↓↔©®°×÷−':
            self.assertIn(ord(c),self.cmap)
    def test_stable_metrics_and_bounds(self):
        f=self.font
        self.assertEqual(f['head'].unitsPerEm,1000)
        self.assertEqual((f['hhea'].ascent,f['hhea'].descent,f['hhea'].lineGap),(850,-250,40))
        self.assertEqual((f['OS/2'].sCapHeight,f['OS/2'].sxHeight),(760,520))
        self.assertEqual(f['OS/2'].version,4)
        self.assertEqual(f['OS/2'].fsType,0)
        self.assertTrue(f['OS/2'].fsSelection&(1<<7))
        for name in f.getGlyphOrder():
            g=f['glyf'][name]
            if not g.numberOfContours:continue
            self.assertFalse(g.isComposite())
            self.assertLessEqual(g.yMax,850,name)
            self.assertGreaterEqual(g.yMin,-250,name)
            coordinates,endpoints,flags=g.getCoordinates(f['glyf'])
            self.assertEqual(len(coordinates),endpoints[-1]+1)
            self.assertTrue(all(a<b for a,b in zip([-1]+list(endpoints),endpoints)))
    def test_truetype_winding_and_no_tiny_contours(self):
        glyphset=self.font.getGlyphSet()
        for name in self.font.getGlyphOrder():
            rec=RecordingPen();glyphset[name].draw(rec)
            areas=[];pen=AreaPen(None)
            for op,args in rec.value:
                getattr(pen,op)(*args)
                if op=='closePath':
                    areas.append(pen.value);pen=AreaPen(None)
            if areas:
                self.assertLess(sum(areas),0,name)  # Clockwise filled outlines.
                self.assertTrue(all(abs(a)>8 for a in areas),name)
        rec=RecordingPen();glyphset['o'].draw(rec);areas=[];pen=AreaPen(None)
        for op,args in rec.value:
            getattr(pen,op)(*args)
            if op=='closePath':areas.append(pen.value);pen=AreaPen(None)
        self.assertEqual(len(areas),2)
        self.assertLess(areas[0],0)
        self.assertGreater(areas[1],0)  # Counter is counterclockwise.
    def test_tabular_digits(self):
        widths={self.font['hmtx'][self.cmap[ord(c)]][0] for c in '0123456789'}
        self.assertEqual(len(widths),1)
    def test_legacy_and_opentype_kerning_match(self):
        legacy=self.font['kern'].kernTables[0].kernTable
        pairs={}
        for lookup in self.font['GPOS'].table.LookupList.Lookup:
            self.assertEqual(lookup.LookupType,2)
            for sub in lookup.SubTable:
                self.assertEqual(sub.Format,1)
                for first,pairset in zip(sub.Coverage.glyphs,sub.PairSet):
                    for record in pairset.PairValueRecord:
                        pairs[(first,record.SecondGlyph)]=record.Value1.XAdvance
        self.assertEqual(legacy,pairs)
        self.assertEqual(len(pairs),67)
        for pair in ('AV','To','Wa','Yo'):
            self.assertLess(pairs[tuple(self.cmap[ord(c)] for c in pair)],0)
    def test_visible_supported_glyphs_at_small_sizes(self):
        for size in (24,36,48,96):
            font=ImageFont.truetype(str(FONT),size)
            for cp in self.cmap:
                mask=font.getmask(chr(cp))
                if cp in (32,160):self.assertIsNone(mask.getbbox())
                else:self.assertIsNotNone(mask.getbbox(),(size,cp))
    def test_sfnt_checksum(self):
        data=FONT.read_bytes();data+=b'\0'*((-len(data))%4)
        self.assertEqual(sum(struct.unpack('>%dI'%(len(data)//4),data))&0xffffffff,0xB1B0AFBA)
    def test_provenance_and_stable_unity_guid(self):
        provenance=FONT.with_name('HauntedIntertitle-SOURCE.txt').read_text()
        self.assertIn('original',provenance.lower())
        self.assertIn('No third-party font',provenance)
        meta=FONT.with_suffix('.ttf.meta').read_text()
        self.assertIn('guid: c36f36ab1733544a8cde4a7ae27f0672',meta)

if __name__=='__main__':unittest.main(verbosity=2)
