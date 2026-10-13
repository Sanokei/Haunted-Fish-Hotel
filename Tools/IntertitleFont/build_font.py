#!/usr/bin/env python3
"""Rebuild Haunted Intertitle from original, human-readable glyph geometry.

No source font is read, sampled, transformed, or renamed. Glyphs are constructed
from the centre lines and filled serif outlines below. The approved generated
silent-film card is a visual design reference, not a traced glyph source.
Requires fonttools, skia-pathops, Pillow, numpy.
"""
from __future__ import annotations
import argparse, hashlib, json, math, re
from pathlib import Path
import numpy as np
import pathops
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.basePen import BasePen
from fontTools.pens.cu2quPen import Cu2QuPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.svgLib.path import parse_path
from fontTools.feaLib.builder import addOpenTypeFeaturesFromString
from fontTools.ttLib import newTable, TTFont
from fontTools.ttLib.tables._k_e_r_n import KernTable_format_0
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
OUTPUT = ROOT / 'Assets/Art/Fonts/HauntedIntertitle-Regular.ttf'
UPM, CAP, XHEIGHT, ASCENT, DESCENT, LINEGAP = 1000, 760, 520, 850, -250, 40
WIDTH_SCALE = 1.10  # Soften the narrow skeleton to the approved display proportions.
IVORY, BLACK = '#e4dcc7', '#151512'

class FlattenPen(BasePen):
    def __init__(self):
        super().__init__(None); self.lines=[]; self.line=[]
    def _moveTo(self,p):
        if self.line: self.lines.append(self.line)
        self.line=[np.array(p,dtype=float)]
    def _lineTo(self,p): self.line.append(np.array(p,dtype=float))
    def _curveToOne(self,a,b,c):
        p=self.line[-1]; a,b,c=map(lambda v:np.array(v,float),(a,b,c))
        n=max(8,int((np.linalg.norm(a-p)+np.linalg.norm(b-a)+np.linalg.norm(c-b))/9))
        for t in np.linspace(0,1,n+1)[1:]: self.line.append((1-t)**3*p+3*(1-t)**2*t*a+3*(1-t)*t*t*b+t**3*c)
    def _qCurveToOne(self,a,b):
        p=self.line[-1]; a,b=map(lambda v:np.array(v,float),(a,b))
        n=max(8,int((np.linalg.norm(a-p)+np.linalg.norm(b-a))/9))
        for t in np.linspace(0,1,n+1)[1:]: self.line.append((1-t)**2*p+2*(1-t)*t*a+t*t*b)
    def _closePath(self):
        if self.line: self.line.append(self.line[0]);self.lines.append(self.line);self.line=[]
    def _endPath(self):
        if self.line:self.lines.append(self.line);self.line=[]

class Glyph:
    def __init__(self, advance): self.advance=advance;self.path=pathops.Path()
    def fill(self,d):
        p=pathops.Path();parse_path(d,p.getPen());self.path.addPath(pathops.simplify(p,fix_winding=True,keep_starting_points=False));return self
    def poly(self,points):
        p=pathops.Path();p.moveTo(*points[0])
        for x,y in points[1:]:p.lineTo(float(x),float(y))
        p.close();self.path.addPath(pathops.simplify(p,fix_winding=True,keep_starting_points=False));return self
    def stroke(self,d,width=65,thin=23,taper=0.08):
        fp=FlattenPen();parse_path(d,fp)
        if fp.line:fp.lines.append(fp.line)
        for points in fp.lines:
            pts=np.array(points); sides=[[],[]]
            for j,p in enumerate(pts):
                if j==0:v=pts[1]-p
                elif j==len(pts)-1:v=p-pts[j-1]
                else:v=pts[j+1]-pts[j-1]
                n=np.array([-v[1],v[0]]);n/=max(1e-8,np.linalg.norm(n))
                variation=1+taper*math.sin(p[1]/155+0.4)
                a,b=width/2*variation,thin/2
                den=math.hypot(a*n[0],b*n[1])
                off=np.array([a*a*n[0],b*b*n[1]])/max(1e-8,den)
                sides[0].append(p+off);sides[1].append(p-off)
            self.poly(sides[0]+list(reversed(sides[1])))
            if np.linalg.norm(pts[0]-pts[-1]) > 0.01:
                # Elliptical-nib caps remove flat-cut spikes at curled terminals.
                for x,y in (pts[0],pts[-1]):
                    rx=width/2*(1+taper*math.sin(y/155+0.4));ry=thin/2;k=0.55228475
                    self.fill(f'M {x-rx},{y} C {x-rx},{y+k*ry} {x-k*rx},{y+ry} {x},{y+ry} C {x+k*rx},{y+ry} {x+rx},{y+k*ry} {x+rx},{y} C {x+rx},{y-k*ry} {x+k*rx},{y-ry} {x},{y-ry} C {x-k*rx},{y-ry} {x-rx},{y-k*ry} {x-rx},{y} Z')
        return self
    def dot(self,x,y,rx=25,ry=None):
        ry=ry or rx
        return self.fill(f'M {x-rx},{y} C {x-rx-2},{y+ry*.55} {x-rx*.28},{y+ry+3} {x+1},{y+ry} C {x+rx*.75},{y+ry-1} {x+rx+3},{y+ry*.38} {x+rx},{y-2} C {x+rx-1},{y-ry*.6} {x+rx*.22},{y-ry-2} {x-2},{y-ry} C {x-rx*.6},{y-ry+1} {x-rx-3},{y-ry*.3} {x-rx},{y} Z')
    def stem(self,x,top=CAP,bottom=0,foot=True,slant=0,top_serif=True):
        # Bent/tapered shoulders and bracketed, slightly asymmetric serifs.
        end=x+slant
        self.fill(f'M {x-30},{bottom+16} C {x-26},{bottom+150} {end-33},{top-150} {end-30},{top-18} L {end+29},{top-8} C {end+23},{top-195} {x+24},{bottom+160} {x+31},{bottom+18} Z')
        if foot:self.fill(f'M {x-31},{bottom+62} C {x-33},{bottom+21} {x-47},{bottom+18} {x-68},{bottom+10} Q {x-75},{bottom-1} {x-64},{bottom-2} Q {x},{bottom+3} {x+65},{bottom-1} Q {x+77},{bottom+6} {x+64},{bottom+13} C {x+43},{bottom+20} {x+33},{bottom+20} {x+30},{bottom+65} Z')
        if top_serif:
            if top>CAP:self.fill(f'M {end-31},{top-90} Q {end-32},{top-41} {end-70},{top-49} Q {end-81},{top-40} {end-63},{top-27} Q {end-5},{top-14} {end+16},{top+18} Q {end+31},{top+25} {end+31},{top+4} L {end+27},{top-87} Z')
            else:self.fill(f'M {end-32},{top-63} Q {end-31},{top-18} {end-62},{top-15} Q {end-76},{top-11} {end-62},{top+1} Q {end},{top-8} {end+57},{top+1} Q {end+71},{top-5} {end+59},{top-14} Q {end+28},{top-20} {end+27},{top-63} Z')
        return self
    def copy(self,advance=None,matrix=None):
        g=Glyph(advance or self.advance);g.path.addPath(self.path)
        if matrix:g.path=g.path.transform(*matrix)
        return g
    def finish(self):
        # Boolean union removes stroke/serif overlaps and normalizes winding.
        p=pathops.simplify(self.path,fix_winding=True,keep_starting_points=False)
        p=p.transform(WIDTH_SCALE,0,0,1,0,0)
        pen=TTGlyphPen(None);p.draw(Cu2QuPen(pen,max_err=0.7,reverse_direction=True))
        return pen.glyph()

G={}
def add(c,w):G[c]=Glyph(w);return G[c]
def oval(g,left,right,top=520,bottom=0,tilt=0,width=64):
    cx=(left+right)/2;cy=(top+bottom)/2; rx=(right-left)/2;ry=(top-bottom)/2
    # A narrow oval, not a geometrically perfect circle.
    return g.stroke(f'M {cx+tilt},{top} C {right+8},{top-4} {right+3},{cy+ry*.49} {right},{cy} C {right-4},{bottom+ry*.39} {right-22},{bottom-10} {cx-tilt},{bottom} C {left-6},{bottom+2} {left-5},{cy-ry*.43} {left},{cy} C {left+3},{cy+ry*.51} {left+20},{top+4} {cx+tilt},{top}',width=width)
def topser(g,x,y=520):return g.fill(f'M {x-32},{y-80} Q {x-28},{y-29} {x-61},{y-32} Q {x-79},{y-26} {x-59},{y-13} Q {x-9},{y-8} {x+19},{y+9} Q {x+32},{y+14} {x+29},{y-7} L {x+27},{y-78} Z')
def toe(g,x,y=0):return g.stroke(f'M {x},{y+83} C {x+1},{y+11} {x+24},{y-24} {x+59},{y+8} C {x+72},{y+21} {x+68},{y+41} {x+61},{y+41}',width=39,thin=20)
def shoulder(g,left,right,top=520,bottom=0):
    return g.stroke(f'M {left},{top-120} C {left+58},{top+51} {right-14},{top+42} {right},{top-130} C {right+8},{top-244} {right-12},{bottom+42} {right+2},{bottom+6}',width=61)
def serif_diag(g,x,y,w=49):return g.fill(f'M {x-10},{y+35} Q {x-12},{y+10} {x-w},{y+8} L {x-w},{y-4} Q {x},{y+2} {x+w},{y-3} L {x+w},{y+10} Q {x+9},{y+12} {x+10},{y+35} Z')

# Lowercase: the reference's open counters, stretched ascenders and curls.
g=add('a',342);oval(g,71,241,top=306,bottom=0,width=63);g.stroke('M 240,21 C 251,141 242,296 246,396 C 250,531 104,577 76,475 C 63,428 102,422 122,449',width=60);g.dot(119,455,18);toe(g,247)
g=add('b',347);g.stem(80,780,foot=False);oval(g,80,263,top=523,bottom=0,width=65);g.fill('M 46,-5 L 50,91 L 110,35 Z')
g=add('c',295);g.stroke('M 247,430 C 241,521 182,544 126,504 C 67,461 56,354 56,251 C 54,93 97,-21 173,5 C 222,20 244,67 255,108',width=64);g.dot(241,432,19,26)
g=add('d',356);oval(g,69,244,top=515,bottom=0,width=64);g.stem(256,780,foot=False);toe(g,256)
g=add('e',302);g.stroke('M 65,252 L 249,349 C 244,442 216,529 164,521 C 95,519 56,397 57,257 C 58,81 106,-23 185,9 C 231,26 252,70 253,98 C 251,128 231,128 223,112',width=62);g.dot(231,111,12,17)
g=add('f',246);g.stroke('M 103,-176 C 111,30 94,409 102,605 C 109,793 217,846 233,723 C 233,684 205,683 197,704',width=59);g.dot(200,708,17);g.stroke('M 40,510 Q 128,502 215,516',width=27,thin=24);g.fill('M 72,80 L 73,-115 Q 69,-174 41,-192 L 46,-205 Q 109,-178 127,-193 L 132,83 Z')
g=add('g',351);oval(g,72,248,top=519,bottom=0,width=64);g.stroke('M 253,509 C 248,301 260,99 252,-62 C 245,-234 76,-249 64,-148 C 64,-99 103,-96 125,-116',width=60);g.dot(120,-116,18);topser(g,253)
g=add('h',362);g.stem(82,780);shoulder(g,92,263);g.stroke('M 265,115 C 264,25 258,-46 295,-60 Q 316,-71 332,-43',width=50,thin=21)
g=add('i',177);g.stem(86,510,top_serif=False);topser(g,86,520);g.dot(87,668,27,34)
g=add('j',192);g.stroke('M 111,498 C 119,283 123,37 111,-109 C 103,-229 -7,-247 -15,-164 Q -16,-129 9,-136',width=61);topser(g,113,520);g.dot(117,668,27,34);g.dot(9,-139,15)
g=add('k',355);g.stem(81,780);g.stroke('M 87,222 Q 196,379 281,506',width=39,thin=20);g.stroke('M 170,336 Q 220,154 303,7',width=69,thin=26);serif_diag(g,294,0);g.fill('M 234,502 L 320,516 L 320,501 Q 277,490 252,456 Z')
g=add('l',177);g.stem(86,780)
g=add('m',523);g.stem(79,508,top_serif=False);topser(g,79);shoulder(g,89,247);shoulder(g,251,434);g.stem(438,240,top_serif=False)
g=add('n',349);g.stem(79,508,top_serif=False);topser(g,79);shoulder(g,88,258);g.stem(262,200,top_serif=False)
g=add('o',331);oval(g,69,261,top=521,bottom=-2,width=65)
g=add('p',348);g.stem(81,511,bottom=-218,top_serif=False);topser(g,81);oval(g,82,266,top=520,bottom=5,width=65)
g=add('q',354);oval(g,70,252,top=520,bottom=4,width=65);g.stem(258,516,bottom=-218,top_serif=False);topser(g,258)
g=add('r',268);g.stem(81,508,top_serif=False);topser(g,81);g.stroke('M 91,358 C 119,486 173,556 218,496 Q 247,449 213,436',width=47,thin=21);g.dot(218,443,22,28)
g=add('s',299);g.stroke('M 232,437 C 250,517 177,550 121,505 C 38,441 81,349 155,283 C 233,214 271,129 227,49 C 185,-22 69,-29 56,72 C 49,145 119,167 143,122 Q 153,99 135,94',width=60,thin=22);g.dot(233,441,18,24);g.dot(135,98,16)
g=add('t',244);g.stroke('M 103,658 C 116,473 87,211 109,74 C 122,-28 202,-21 214,52 Q 222,94 197,98',width=59,thin=23);g.dot(198,91,15,22);g.fill('M 46,520 Q 91,547 109,664 L 129,669 L 130,518 L 209,525 L 213,491 L 126,491 L 56,491 Z')
g=add('u',349);g.stroke('M 77,501 C 76,365 63,191 80,89 C 105,-65 225,0 254,105',width=61);topser(g,77);g.stroke('M 261,505 Q 253,227 262,38',width=61);topser(g,261);toe(g,260)
g=add('v',328);g.stroke('M 61,505 C 85,362 122,131 166,0',width=64,thin=24);g.stroke('M 269,507 Q 225,204 168,0',width=29,thin=24);serif_diag(g,61,500,43);serif_diag(g,269,500,38)
g=add('w',468);g.stroke('M 53,505 Q 79,224 127,0 M 238,495 Q 266,214 318,0',width=61,thin=22);g.stroke('M 224,505 Q 171,197 127,0 M 418,507 Q 378,203 318,0',width=29,thin=23);serif_diag(g,53,500,40);serif_diag(g,418,500,35)
g=add('x',329);g.stroke('M 58,505 Q 168,251 280,6',width=63,thin=25);g.stroke('M 274,509 Q 180,302 52,0',width=29,thin=21);serif_diag(g,58,500,43);serif_diag(g,274,500,36);serif_diag(g,56,0,42);serif_diag(g,277,0,42)
g=add('y',332);g.stroke('M 53,508 Q 90,220 172,0',width=64,thin=24);g.stroke('M 280,505 C 249,278 165,-92 108,-180 Q 54,-242 36,-164',width=29,thin=22);g.dot(39,-165,19,25);serif_diag(g,53,500,40);serif_diag(g,280,500,35)
g=add('z',299);g.stroke('M 59,491 Q 156,507 254,499 L 52,12 Q 167,4 253,26',width=55,thin=23);g.fill('M 49,439 L 54,538 L 69,538 Q 64,497 106,497 L 97,477 Q 63,478 62,439 Z M 207,13 Q 253,25 252,91 L 266,91 L 261,-9 L 196,1 Z')

# Capitals, with deliberately selective decoration so instructions remain clear.
g=add('A',420);g.stroke('M 44,8 Q 128,376 214,756',width=30,thin=22);g.stroke('M 214,754 Q 282,378 370,7',width=71,thin=24);g.stroke('M 119,306 Q 225,319 299,310',width=29,thin=25);serif_diag(g,49,0);serif_diag(g,369,0)
g=add('B',405);g.stem(102);g.stroke('M 105,741 C 352,815 395,436 117,401 C 402,481 435,-34 104,14',width=67,thin=25)
g=add('C',391);g.stroke('M 329,637 C 343,724 278,797 205,751 C 105,722 59,548 58,362 C 55,126 126,-39 235,6 Q 311,28 343,142',width=72,thin=24);g.dot(329,639,24,31)
g=add('D',424);g.stem(102);g.stroke('M 105,745 C 340,816 369,525 371,389 C 373,139 300,-34 103,12',width=72,thin=24)
g=add('E',365);g.stem(106);g.stroke('M 100,746 Q 207,739 301,759 M 103,409 Q 216,425 269,423 M 104,10 Q 230,6 318,33',width=28,thin=28);g.fill('M 264,753 Q 304,743 308,683 L 325,683 L 317,786 L 304,786 L 293,753 Z M 249,422 Q 272,429 269,483 L 284,483 L 278,362 L 265,362 Q 269,402 247,410 Z M 281,20 Q 316,36 323,112 L 340,112 L 330,-7 L 281,8 Z')
g=add('F',385);g.stem(109);g.stroke('M 55,745 C 166,735 247,764 312,767',width=36,thin=27);g.stroke('M 310,767 C 369,789 381,657 338,648 C 312,640 301,674 323,683',width=29,thin=22);g.dot(321,681,12,15);g.stroke('M 105,429 Q 217,440 264,444',width=24,thin=27);g.fill('M 230,432 Q 260,444 261,518 L 279,519 L 279,362 L 263,358 Q 263,420 231,416 Z')
g=add('G',409);g.stroke('M 289,765 C 143,794 61,615 58,378 C 54,136 108,-19 207,2 C 279,20 307,108 307,310',width=74,thin=24);g.stroke('M 287,765 C 337,778 358,670 323,645 C 301,629 284,661 308,674',width=29,thin=22);g.dot(306,673,12,14);g.fill('M 220,367 Q 286,352 356,365 L 357,338 Q 320,338 321,298 L 281,298 Q 291,341 222,339 Z');g.stroke('M 307,347 Q 309,167 317,12',width=59,thin=23);g.fill('M 284,35 Q 309,12 330,-6 Q 337,-15 338,4 L 336,50 Z')
g=add('H',445);g.stem(100);g.stem(343);g.stroke('M 106,390 Q 229,404 341,398',width=27,thin=28)
g=add('I',206);g.stem(103)
g=add('J',302);g.stem(211,bottom=150,foot=False);g.stroke('M 210,232 C 213,52 160,-39 85,13 C 38,48 45,106 71,116',width=67,thin=25);g.dot(69,115,20,28)
g=add('K',428);g.stem(101);g.stroke('M 101,312 Q 245,565 356,752',width=33,thin=24);g.stroke('M 203,485 Q 275,220 377,5',width=75,thin=25);serif_diag(g,377,0);serif_diag(g,355,746,43)
g=add('L',356);g.stem(106);g.stroke('M 103,10 C 185,18 277,-11 318,37 Q 337,68 329,122',width=36,thin=26)
g=add('M',544);g.stem(83,foot=True,top_serif=False);g.stroke('M 81,749 Q 174,415 259,80',width=71,thin=25);g.stroke('M 447,748 Q 335,375 260,81',width=30,thin=22);g.stem(453,top_serif=False);g.fill('M 25,742 L 28,758 L 106,747 L 102,710 L 51,727 Z M 425,748 L 518,758 L 518,742 L 477,728 Z')
g=add('N',457);g.stem(84,top_serif=False);g.stroke('M 89,746 Q 221,364 367,8',width=72,thin=26);g.stroke('M 366,745 Q 362,399 369,0',width=30,thin=22);serif_diag(g,367,746,49);g.fill('M 27,742 L 27,759 L 112,751 L 120,710 Z');serif_diag(g,367,0,44)
g=add('O',424);oval(g,65,358,top=764,bottom=-3,width=76)
g=add('P',393);g.stem(102);g.stroke('M 107,742 C 389,822 407,345 105,411',width=72,thin=26)
g=add('Q',432);oval(g,64,359,top=764,bottom=-3,width=75);g.stroke('M 225,101 C 264,12 319,-113 375,-117 Q 414,-124 413,-70',width=53,thin=23)
g=add('R',420);g.stem(102);g.stroke('M 106,742 C 390,821 402,347 106,407',width=71,thin=25);g.stroke('M 202,413 C 258,309 294,93 352,17 Q 380,-15 393,22',width=69,thin=25)
g=add('S',377);g.stroke('M 312,650 C 321,765 220,800 146,732 C 41,637 87,482 185,393 C 293,300 357,173 293,55 C 232,-51 65,-16 58,112 C 55,176 105,194 128,155 Q 143,128 121,121',width=71,thin=24);g.dot(312,650,23,31);g.dot(122,125,18)
g=add('T',402);g.stem(206,top=746,top_serif=False);g.stroke('M 43,745 Q 203,732 357,756',width=32,thin=28);g.fill('M 35,776 L 54,777 Q 55,745 110,743 L 104,724 Q 61,728 50,651 L 31,648 Z M 301,745 Q 345,749 350,803 L 368,803 L 367,665 L 351,664 Q 351,731 301,724 Z')
g=add('U',429);g.stroke('M 84,745 C 79,509 73,255 94,134 C 119,-49 315,-56 341,149 C 354,309 344,552 343,748',width=64,thin=25);serif_diag(g,85,746,50);serif_diag(g,343,746,47)
g=add('V',401);g.stroke('M 53,745 Q 106,347 202,0',width=73,thin=25);g.stroke('M 345,748 Q 273,321 202,0',width=31,thin=24);serif_diag(g,54,746,45);serif_diag(g,345,746,44)
g=add('W',594);g.stroke('M 53,748 Q 97,354 163,0 M 305,747 Q 355,329 422,0',width=72,thin=25);g.stroke('M 297,748 Q 225,315 163,0 M 544,748 Q 489,319 422,0',width=29,thin=25);serif_diag(g,54,746,45);serif_diag(g,545,746,43)
g=add('X',403);g.stroke('M 54,746 Q 202,369 348,7',width=70,thin=25);g.stroke('M 342,745 Q 210,370 52,7',width=30,thin=24);serif_diag(g,55,746,46);serif_diag(g,343,746,44);serif_diag(g,55,0,47);serif_diag(g,348,0,47)
g=add('Y',407);g.stroke('M 53,746 Q 123,525 206,319',width=73,thin=26);g.stroke('M 350,747 Q 279,521 206,319',width=31,thin=24);g.stem(206,top=346,top_serif=False);serif_diag(g,54,746,45);serif_diag(g,350,746,46)
g=add('Z',363);g.stroke('M 53,739 Q 185,751 311,749 L 50,13 Q 181,10 311,21',width=70,thin=25);g.fill('M 43,783 L 59,783 Q 59,747 112,740 L 106,720 Q 61,729 56,648 L 39,648 Z M 259,15 Q 308,18 316,121 L 334,121 L 326,-6 L 251,4 Z')

# Lining figures with old-film proportions. Digits are tabular for control lists.
for c in '0123456789':add(c,348)
oval(G['0'],65,282,top=735,bottom=0,width=67)
G['1'].stem(185,top=729);G['1'].stroke('M 80,652 Q 144,681 189,733',width=31,thin=23)
G['2'].stroke('M 65,610 C 65,766 275,804 280,612 C 285,443 164,292 63,33 Q 170,36 284,44',width=63,thin=24);G['2'].dot(66,608,20,27);G['2'].fill('M 52,61 L 52,-3 L 296,4 L 304,95 L 289,98 Q 283,44 249,41 Z')
G['3'].stroke('M 70,635 C 103,766 276,793 277,603 C 278,474 203,409 137,397 C 297,430 338,50 194,4 C 119,-23 64,35 65,107',width=63,thin=25);G['3'].dot(69,634,18,24);G['3'].dot(67,109,20,26)
G['4'].stroke('M 257,729 L 62,222 L 306,226',width=31,thin=26);G['4'].stem(239,top=729);G['4'].fill('M 211,749 L 269,749 L 252,680 L 223,667 Z')
G['5'].stroke('M 288,722 Q 180,710 89,721 L 85,424 C 296,528 333,68 200,9 C 126,-29 54,40 60,120',width=62,thin=24);G['5'].dot(63,119,20,25);G['5'].fill('M 76,744 L 80,684 L 279,703 L 297,766 L 282,770 Q 278,732 239,726 Z')
G['6'].stroke('M 284,674 C 237,822 60,724 60,331 C 60,93 100,-19 190,2 C 310,25 328,403 202,427 C 144,439 93,386 67,315',width=65,thin=24);G['6'].dot(281,674,18,25)
G['7'].stroke('M 57,720 Q 170,722 292,733 C 201,466 151,197 126,7',width=64,thin=25);G['7'].fill('M 44,772 L 61,772 Q 59,731 118,725 L 112,706 Q 60,716 56,648 L 39,648 Z');serif_diag(G['7'],128,0,55)
oval(G['8'],76,272,top=735,bottom=378,width=62);oval(G['8'],60,285,top=391,bottom=0,width=68)
G['9'].stroke('M 68,58 C 112,-94 287,7 289,401 C 289,638 248,755 159,736 C 34,707 22,329 151,307 C 207,294 258,349 283,421',width=65,thin=24);G['9'].dot(71,60,18,25)

# Punctuation and practical UI symbols. Every mapped mark has its own outline.
add(' ',190);add('\u00a0',190)
add('.',157).dot(79,29,26,30)
add(',',158).stroke('M 87,44 C 110,-21 76,-82 44,-95',width=29,thin=22).dot(83,33,28,32)
add(':',166).dot(81,31,24,29).dot(82,401,24,29)
add(';',166).dot(82,401,24,29).stroke('M 89,44 C 109,-17 81,-73 47,-96',width=28,thin=22).dot(83,33,27,32)
add('!',176).fill('M 49,746 Q 87,758 126,748 L 99,182 L 74,182 Z').dot(87,30,29,33)
add('?',320).stroke('M 54,603 C 64,774 274,793 275,611 C 272,469 152,426 151,237 L 152,190',width=62,thin=24).dot(58,605,20,28).dot(152,30,28,32)
add("'",127).fill('M 42,761 Q 74,770 99,756 L 67,555 L 47,554 Z')
add('"',231).fill('M 39,761 Q 68,770 92,756 L 62,555 L 44,554 Z M 143,761 Q 172,770 196,756 L 167,555 L 148,554 Z')
add('-',262).fill('M 45,284 Q 133,295 218,289 L 214,253 Q 121,252 43,252 Z')
add('\u2013',446).fill('M 36,285 Q 222,293 409,286 L 407,253 L 35,253 Z')
add('\u2014',672).fill('M 31,283 Q 337,292 640,286 L 638,253 L 30,253 Z')
add('_',430).fill('M 23,-110 L 407,-110 L 407,-145 L 23,-145 Z')
add('/',316).stroke('M 37,-62 Q 160,371 276,791',width=31,thin=25)
add('\\',316).stroke('M 37,791 Q 161,371 277,-62',width=31,thin=25)
add('(',237).stroke('M 183,812 C 88,725 58,509 58,335 C 57,128 105,-94 183,-161',width=49,thin=22)
add(')',237).stroke('M 54,812 C 149,725 179,509 179,335 C 180,128 132,-94 54,-161',width=49,thin=22)
add('[',234).fill('M 178,806 L 68,806 L 68,-149 L 178,-149 L 178,-122 L 113,-122 L 113,779 L 178,779 Z')
add(']',234).fill('M 56,806 L 166,806 L 166,-149 L 56,-149 L 56,-122 L 121,-122 L 121,779 L 56,779 Z')
add('{',273).stroke('M 221,800 C 79,831 157,493 77,388 Q 59,369 33,361 Q 123,327 119,166 C 112,16 96,-170 221,-143',width=45,thin=23)
add('}',273).stroke('M 52,800 C 194,831 116,493 196,388 Q 214,369 240,361 Q 150,327 154,166 C 161,16 177,-170 52,-143',width=45,thin=23)
add('+',425).fill('M 191,585 L 232,585 L 232,345 L 383,345 L 383,308 L 232,308 L 232,66 L 191,66 L 191,308 L 42,308 L 42,345 L 191,345 Z')
add('=',425).fill('M 42,438 L 383,438 L 383,400 L 42,400 Z M 42,246 L 383,246 L 383,208 L 42,208 Z')
add('<',370).stroke('M 309,601 L 61,336 L 308,80',width=33,thin=29)
add('>',370).stroke('M 61,601 L 309,336 L 62,80',width=33,thin=29)
add('|',144).fill('M 56,814 L 88,814 L 88,-161 L 56,-161 Z')
add('~',409).stroke('M 47,320 C 117,484 274,195 361,356',width=36,thin=32)
add('`',167).fill('M 31,803 L 72,816 L 130,611 L 108,603 Z')
add('^',364).stroke('M 54,508 L 181,773 L 310,509',width=35,thin=27)
add('*',320).stroke('M 159,769 L 159,451 M 34,682 L 285,535 M 35,534 L 286,681',width=27,thin=24)
add('#',431).stroke('M 159,764 L 98,0 M 326,764 L 265,0 M 55,499 L 382,513 M 39,251 L 366,262',width=33,thin=30)
add('%',506).stroke('M 63,-20 L 447,755',width=29,thin=24);oval(G['%'],76,196,top=735,bottom=414,width=38);oval(G['%'],310,430,top=322,bottom=1,width=38)
add('$',375).stroke('M 311,650 C 321,765 220,800 146,732 C 41,637 87,482 185,393 C 293,300 357,173 293,55 C 232,-51 65,-16 58,112',width=63,thin=24).stroke('M 211,829 L 168,-86',width=24,thin=20)
add('&',467).stroke('M 391,435 C 348,268 260,-42 128,10 C 19,59 51,236 164,379 C 268,510 341,721 235,745 C 149,765 87,618 153,455 C 209,313 307,23 427,4',width=61,thin=24);G['&'].stroke('M 322,445 L 451,445',width=22,thin=24)
add('@',631);oval(G['@'],63,559,top=744,bottom=0,width=37);oval(G['@'],234,408,top=534,bottom=151,width=45);G['@'].stroke('M 413,532 L 402,245 C 390,85 543,148 557,340 M 551,110 C 418,-14 246,-26 142,55',width=41,thin=24)
add('\u2019',146).stroke('M 64,757 C 94,742 101,655 43,612',width=32,thin=20).dot(63,743,24,30)
add('\u2018',146).stroke('M 87,770 C 34,729 38,650 69,631',width=32,thin=20).dot(73,640,24,30)
for c,base in [('“','‘'),('”','’')]:
    g=add(c,260);g.path.addPath(G[base].path);g.path.addPath(G[base].path.transform(1,0,0,1,113,0))
add('…',458).dot(73,29,25,29).dot(229,29,25,29).dot(385,29,25,29)
add('•',267).dot(134,343,47,55)
add('°',229);oval(G['°'],60,169,top=761,bottom=524,width=29)
add('×',400).stroke('M 61,531 L 341,94 M 61,94 L 341,531',width=36,thin=31)
add('÷',421).stroke('M 48,328 L 373,328',width=34,thin=34).dot(210,525,23,28).dot(210,132,23,28)
for c in '←→↔':
    g=add(c,650).stroke('M 61,337 L 589,337',width=29,thin=31)
    if c!='→':g.stroke('M 240,543 Q 171,407 61,337 Q 176,260 240,134',width=34,thin=26)
    if c!='←':g.stroke('M 410,543 Q 479,407 589,337 Q 474,260 410,134',width=34,thin=26)
for c in '↑↓':
    g=add(c,430).stroke('M 214,42 L 214,737',width=33,thin=26)
    if c=='↑':g.stroke('M 53,511 Q 157,611 214,737 Q 271,611 375,511',width=33,thin=26)
    else:g.stroke('M 53,268 Q 157,168 214,42 Q 271,168 375,268',width=33,thin=26)
for c,letter in [('©','C'),('®','R')]:
    g=add(c,545);oval(g,49,495,top=758,bottom=0,width=30);g.path.addPath(G[letter].path.transform(.58,0,0,.61,152,145))
add('\u2212',425).stroke('M 42,328 L 383,328',width=34,thin=34)

# A visible missing-glyph marker; it is not mapped to supported characters.
NOTDEF=Glyph(420).fill('M 42,0 L 42,744 L 378,744 L 378,0 Z M 75,33 L 345,33 L 345,711 L 75,711 Z')

KERN={
'AV':-42,'AW':-35,'AY':-44,'AT':-30,'VA':-43,'WA':-34,'YA':-46,
'Ta':-35,'Te':-31,'To':-33,'Tu':-24,'Ty':-22,'Tr':-22,'Ti':-13,
'Va':-30,'Ve':-25,'Vo':-29,'Wa':-20,'We':-17,'Wo':-21,
'Ya':-37,'Ye':-34,'Yo':-36,'Yu':-30,'FA':-30,'Fa':-24,'Fo':-17,
'Pa':-15,'Po':-14,'LA':-6,'LT':-25,'LV':-25,'LW':-19,'LY':-29,
'AT':-28,'Ac':-11,'Ao':-10,'Av':-19,'Aw':-13,'Ay':-17,'Fi':-12,
'GT':-7,'Gu':-5,'Wo':-22,'T.':-45,'T,':-43,'V.':-37,'V,':-35,
'Y.':-48,'Y,':-47,'F.':-35,'F,':-32,'P.':-32,'P,':-30,
'fe':-9,'fo':-11,'fa':-9,'ff':-9,'rt':-4,'rv':-5,'ry':-7,
'va':-12,'ve':-11,'vo':-11,'wa':-8,'we':-6,'wo':-7,'ya':-11,'yo':-11,
}

def glyph_name(c):
    from fontTools.agl import UV2AGL
    return UV2AGL.get(ord(c),f'uni{ord(c):04X}')

def build():
    order=['.notdef']+[glyph_name(c) for c in sorted(G,key=ord)]
    glyphs={'.notdef':NOTDEF.finish()};metrics={'.notdef':(round(420*WIDTH_SCALE),round(42*WIDTH_SCALE))}
    for c,g in G.items():
        name=glyph_name(c);glyph=g.finish();glyphs[name]=glyph
        if glyph.numberOfContours:
            glyph.recalcBounds(None);lsb=glyph.xMin
        else:lsb=0
        metrics[name]=(round(g.advance*WIDTH_SCALE),lsb)
    fb=FontBuilder(UPM,isTTF=True)
    fb.setupGlyphOrder(order);fb.setupCharacterMap({ord(c):glyph_name(c) for c in G})
    fb.setupGlyf(glyphs);fb.setupHorizontalMetrics(metrics)
    fb.setupHorizontalHeader(ascent=ASCENT,descent=DESCENT,lineGap=LINEGAP)
    fb.setupNameTable({'familyName':'Haunted Intertitle','styleName':'Regular','uniqueFontIdentifier':'Haunted Intertitle Regular 1.000','fullName':'Haunted Intertitle Regular','psName':'HauntedIntertitle-Regular','version':'Version 1.000','description':'Original generated-outline typeface for Haunted Fish Hotel, inspired by the approved silent-film title-card reference. Reproducible source accompanies this font. No third-party font outlines were used.','licenseDescription':'Original project-authored font asset. No third-party font outlines are included. Use and redistribution follow the Haunted Fish Hotel project owner’s chosen terms; no separate third-party font license is asserted.'})
    fb.setupOS2(version=4,sTypoAscender=ASCENT,sTypoDescender=DESCENT,sTypoLineGap=LINEGAP,usWinAscent=ASCENT,usWinDescent=-DESCENT,sxHeight=XHEIGHT,sCapHeight=CAP,usWeightClass=400,usWidthClass=3,ySuperscriptXSize=650,ySuperscriptYSize=650,ySuperscriptYOffset=350,ySubscriptXSize=650,ySubscriptYSize=650,ySubscriptYOffset=200,yStrikeoutSize=30,yStrikeoutPosition=280,fsSelection=0x40|0x80,fsType=0)
    fb.setupPost(underlinePosition=-130,underlineThickness=30)
    fb.setupMaxp();fb.font['head'].created=fb.font['head'].modified=3864211200
    fb.font['head'].fontRevision=1.0
    pairs={(glyph_name(p[0]),glyph_name(p[1])):round(v*WIDTH_SCALE) for p,v in KERN.items()}
    # Both formats: modern shaping and classic/Pillow/legacy consumers.
    kern=newTable('kern');kern.version=0;sub=KernTable_format_0();sub.version=0;sub.coverage=1;sub.kernTable=pairs;kern.kernTables=[sub];fb.font['kern']=kern
    features='languagesystem DFLT dflt;\nlanguagesystem latn dflt;\nfeature kern {\n'+''.join(f'pos {a} {b} {v};\n' for (a,b),v in sorted(pairs.items()))+'} kern;'
    addOpenTypeFeaturesFromString(fb.font,features)
    fb.font.recalcTimestamp=False
    OUTPUT.parent.mkdir(parents=True,exist_ok=True);fb.font.save(OUTPUT,reorderTables=True)
    return fb.font


def proof(reference=None):
    out=HERE/'Proofs';out.mkdir(exist_ok=True)
    im=Image.new('RGB',(1700,1970),BLACK);d=ImageDraw.Draw(im)
    def line(text,y,size,center=False):
        f=ImageFont.truetype(str(OUTPUT),size);x=(1700-d.textlength(text,font=f))/2 if center else 72
        d.text((x,y),text,font=f,fill=IVORY,anchor='lt',stroke_width=0)
    line('Haunted Intertitle',50,116,True)
    line('Original typeface • Regular • 1000 units per em',178,40,True)
    d.line((70,255,1630,255),fill='#766f60',width=1)
    line('Guide to',304,171,True);line('all Fish',475,235,True)
    line('Guide to the Ghost',747,100,True)
    for s,y,size in [('ABCDEFGHIJKLMNOPQRSTUVWXYZ',904,64),('abcdefghijklmnopqrstuvwxyz',1000,74),('0123456789  !?  .,:;  “quotes”  ‘apostrophe’',1106,58),('() [] {} / \\ + − × ÷ = % & @ # $',1190,55),('← ↑ → ↓ ↔   – — … •   © ® °',1273,54)]:line(s,y,size)
    d.line((70,1360,1630,1360),fill='#766f60',width=1)
    line('Read The Danger Bands',1405,83,True)
    line('Ghost markers bound the animated ghost-fish danger band.',1520,49,True)
    line('A / D / arrows: walk. Shift: run. Gamepad: stick walks; click runs.',1606,44,True)
    line('Damaging traps drain health. Bodies remain; souls rise.',1690,47,True)
    line('Fixed base, swinging body. Possession lets it drop.',1775,47,True)
    line('THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG.',1860,40,True)
    im.save(out/'HauntedIntertitle-proof.png')
    # The reference crop is never embedded in or used to create glyph outlines.
    if reference and reference.exists():
        ref=Image.open(reference).convert('RGB').crop((525,210,1165,695))
        target=Image.new('RGB',(640,485),BLACK);td=ImageDraw.Draw(target)
        for text,y,size in [('Guide to',16,196),('all Fish',225,245)]:
            font=ImageFont.truetype(str(OUTPUT),size);box=td.textbbox((0,0),text,font=font)
            td.text(((640-td.textlength(text,font=font))/2,y),text,font=font,fill=IVORY,anchor='lt')
        compare=Image.new('RGB',(1320,560),BLACK);compare.paste(ref,(10,55));compare.paste(target,(670,55))
        cd=ImageDraw.Draw(compare);label=ImageFont.truetype(str(OUTPUT),32)
        cd.text((30,12),'Approved visual reference',font=label,fill=IVORY);cd.text((690,12),'Actual TTF rendering',font=label,fill=IVORY)
        compare.save(out/'HauntedIntertitle-reference-comparison.png')


def validate():
    f=TTFont(OUTPUT);cmap=f.getBestCmap();all_text='';captions=[]
    for file in (ROOT/'Assets/Dialogue/TutorialRecording').glob('*Recording.ink'):
        for match in re.finditer(r'TutorialCaption\("([^"\n]*)",\s*"([^"\n]*)"\)',file.read_text()):
            captions.extend(match.groups());all_text+=''.join(match.groups())
    missing=sorted(set(all_text)-set(map(chr,cmap)))
    assert not missing, missing
    assert set(range(32,127))<=set(cmap)
    bounds=[];contours=0
    for name in f.getGlyphOrder():
        glyph=f['glyf'][name]
        if not glyph.numberOfContours:continue
        glyph.recalcBounds(f['glyf']);contours+=glyph.numberOfContours
        assert glyph.yMax<=ASCENT and glyph.yMin>=DESCENT,(name,glyph.yMin,glyph.yMax)
        assert glyph.xMin>=-70 and glyph.xMax<=f['hmtx'][name][0]+70,(name,glyph.xMin,glyph.xMax)
        assert glyph.numberOfContours>0
        coords,endpts,flags=glyph.getCoordinates(f['glyf'])
        assert len(coords)>0 and endpts[-1]+1==len(coords)
        bounds.append((glyph.xMin,glyph.yMin,glyph.xMax,glyph.yMax))
    # FreeType renders all codepoints, full guide strings, and small body sizes.
    for size in (24,36,48,96,240):
        font=ImageFont.truetype(str(OUTPUT),size)
        for text in captions+[''.join(G)]:
            font.getmask(text)
    results={'family':'Haunted Intertitle','font':str(OUTPUT.relative_to(ROOT)),'sha256':hashlib.sha256(OUTPUT.read_bytes()).hexdigest(),'glyph_count':len(f.getGlyphOrder()),'unicode_count':len(cmap),'guide_caption_strings':len(captions),'missing_guide_characters':missing,'units_per_em':UPM,'cap_height':CAP,'x_height':XHEIGHT,'ascender':ASCENT,'descender':DESCENT,'line_gap':LINEGAP,'kern_pairs':len(KERN),'contour_count':contours,'bounds':[min(x[0] for x in bounds),min(x[1] for x in bounds),max(x[2] for x in bounds),max(x[3] for x in bounds)],'validation':['Complete printable ASCII','All current guide caption characters','All contours serialize and reload','All glyphs within vertical metrics','FreeType renders at 24/36/48/96/240 px','Boolean-unioned overlaps; normalized winding'],'provenance':'Original parametric outlines, no source font input. Inspired by approved generated reference; not an exact traced reconstruction.'}
    (HERE/'Proofs/validation.json').write_text(json.dumps(results,indent=2)+'\n');print(json.dumps(results,indent=2))

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference',type=Path,help='Optional approved card image, used only in the comparison proof.')
    args=parser.parse_args()
    build();proof(args.reference);validate()
