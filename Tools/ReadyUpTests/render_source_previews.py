#!/usr/bin/env python3
"""Render actual prefab rectangles, TTFs and device glyph art; not a Unity screenshot."""
import argparse
from PIL import Image,ImageDraw,ImageFont
from guide_source import *


def render(role,destination,state,family,width,height):
 source,docs=guide(role);catalog=GlyphCatalog();glyphs,fallbacks,_=resolved_slots(docs,family,catalog)
 supersample=3
 page=Image.new('RGBA',(1280*supersample,720*supersample),(10,6,18,255));draw=ImageDraw.Draw(page)
 def paint(rect_id):
  rect=docs[rect_id];go=docs[rect['m_GameObject']['fileID']]
  for ref in go['m_Component']:
   ident=ref['component']['fileID'];component=docs[ident]
   if'm_Color'not in component:continue
   x,y=offset(docs,rect_id);w,h=rect['m_SizeDelta']['x'],rect['m_SizeDelta']['y']
   if w<=0 or h<=0:continue
   x,y=640+x-w/2,360-y-h/2
   color=tuple(round(component['m_Color'][axis]*255)for axis in 'rgba')
   if ident in glyphs:
    path=glyphs[ident]
    if path:
     sprite=Image.open(path).convert('RGBA');scale=min(w*supersample/sprite.width,h*supersample/sprite.height);size=(max(1,round(sprite.width*scale)),max(1,round(sprite.height*scale)))
     sprite=sprite.resize(size,Image.Resampling.LANCZOS);page.alpha_composite(sprite,(round((x+w/2)*supersample-size[0]/2),round((y+h/2)*supersample-size[1]/2)))
    continue
   if'm_FontData'not in component:
    draw.rectangle((round(x*supersample),round(y*supersample),round((x+w)*supersample),round((y+h)*supersample)),fill=color);continue
   copy=fallbacks.get(ident,component['m_Text'])
   if not copy:continue
   if state=='sent' and ident in(193,203):copy='1 / 4 PLAYERS READY'if ident==193 else'READY SENT'
   if state=='ready' and ident in(193,203):copy='3 / 4 PLAYERS READY'if ident==193 else'YOU ARE READY'
   fd=component['m_FontData'];font=font_for(component,supersample);lines=wrap(copy,font,w*supersample);ascent,descent=font.getmetrics();step=(ascent+descent)*fd['m_LineSpacing'];full=(len(lines)-1)*step+ascent+descent
   yy=y*supersample
   if fd['m_Alignment']in(3,4,5):yy+=(h*supersample-full)/2
   for line in lines:
    xx=x*supersample+(w*supersample-font.getlength(line))/2 if fd['m_Alignment']%3==1 else x*supersample
    # A short, matching-face authored FontStyle.Bold is previewed with a light
    # half-pixel emphasis. Native font rasterization remains unverified.
    stroke=1 if fd['m_FontStyle']==1 else 0
    draw.text((xx,yy+ascent),line,font=font,fill=color,anchor='ls',stroke_width=stroke,stroke_fill=color);yy+=step
  for child in rect.get('m_Children',[]):paint(child['fileID'])
 paint(221)
 scale=min(width/1280,height/720);page=page.resize((round(1280*scale),round(720*scale)),Image.Resampling.LANCZOS)
 image=Image.new('RGBA',(width,height+34),(10,6,18,255));image.alpha_composite(page,((width-page.width)//2,(height-page.height)//2));draw=ImageDraw.Draw(image)
 note_font=ImageFont.truetype(str(ROOT/'Assets/TextMesh Pro/Fonts/LiberationSans.ttf'),max(13,round(width/1280*18)))
 name=('PC','Xbox','PlayStation')[family]
 note=f'Serialized layout + actual font/glyph assets. {name}, {width} x {height}. Not a Unity screenshot.'
 if state!='authored':note=f'Illustrative {state} state and count, not live players. {name}. Source proof, not a Unity screenshot.'
 draw.text((10,height+8),note,font=note_font,fill=(220,220,220,255))
 destination.mkdir(parents=True,exist_ok=True);suffix=''if state=='authored'else'-'+state
 target=destination/f'{role.lower()}-guide-{name.lower()}-{width}x{height}{suffix}-source.png';image.convert('RGB').save(target);print(target)

if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--output',type=Path,required=True);parser.add_argument('--state',choices=['authored','sent','ready'],default='authored');parser.add_argument('--family',choices=['PC','Xbox','PlayStation'],default='PC');parser.add_argument('--width',type=int,default=1280);parser.add_argument('--height',type=int,default=720)
 args=parser.parse_args()
 for role in('Fish','Ghost'):render(role,args.output,args.state,('PC','Xbox','PlayStation').index(args.family),args.width,args.height)
