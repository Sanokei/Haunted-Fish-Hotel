#!/usr/bin/env python3
"""Render the complete glyph inventory from the shipped TTF for visual audit."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from fontTools.ttLib import TTFont
ROOT=Path(__file__).resolve().parents[2]
FONT=ROOT/'Assets/Art/Fonts/HauntedIntertitle-Regular.ttf'
OUT=Path(__file__).resolve().parent/'Proofs/HauntedIntertitle-glyph-inventory.png'
cps=sorted(TTFont(FONT).getBestCmap())
cols=10;cell_w=144;cell_h=168;rows=(len(cps)+cols-1)//cols
im=Image.new('RGB',(cols*cell_w,rows*cell_h+90),'#151512');draw=ImageDraw.Draw(im)
font=ImageFont.truetype(str(FONT),100);label=ImageFont.truetype(str(FONT),23);title=ImageFont.truetype(str(FONT),46)
draw.text((32,18),'Haunted Intertitle • Complete glyph inventory',font=title,fill='#e4dcc7')
for i,cp in enumerate(cps):
 x=(i%cols)*cell_w;y=(i//cols)*cell_h+90
 draw.rectangle((x,y,x+cell_w-1,y+cell_h-1),outline='#36342e')
 baseline=y+113
 draw.line((x+10,baseline,x+cell_w-10,baseline),fill='#3d3c33')
 text=chr(cp);length=draw.textlength(text,font=font)
 draw.text((x+(cell_w-length)/2,baseline),text,font=font,anchor='ls',fill='#e4dcc7')
 desc='space' if cp==32 else 'NBSP' if cp==160 else 'U+'+format(cp,'04X')
 draw.text((x+12,y+137),desc,font=label,fill='#a39c89')
im.save(OUT)
print(OUT)
