#!/usr/bin/env python3
"""Verify real serialized ready guides, font metrics and authored active-device glyph slots."""
from pathlib import Path
import json,re,sys
from guide_source import *
checks=0

def check(value,message):
 global checks
 checks+=1
 assert value,message

def main():
 catalog=GlyphCatalog()
 report={'kind':'Serialized source/font/glyph proof only; no native Unity rendering or network pass','roles':{},'displays':[]}
 all_text=''
 for role in('Fish','Ghost'):
  source,docs=guide(role);all_text+=source
  check(not assets.syntax_issues(source),role+' valid YAML');check(not assets.reference_issues(source),role+' local references/hierarchy resolve')
  check('VideoPlayer:'not in source and 'm_VideoClip:'not in source,role+' no video replacement')
  check(sum(r.kind==223 for r in assets.records(source))==1,role+' exactly one canvas')
  check(source.count('guid: 4e29b1a8efbd4b44bb3f3716e73f07ff')==1,role+' exactly one Ready action')
  check(docs[105]['m_ScreenMatchMode']==1,role+' expand scaling');check(docs[133]['m_Text']=='YOU ARE THE '+role.upper(),role+' role heading')
  check(docs[253]['m_Text']==('Traps to avoid' if role=='Fish' else 'TRAPS'),role+' current role-specific trap-section title')
  check(docs[133]['m_FontData']['m_FontStyle']==0,role+' accepted title is not fake-bolded')
  check(docs[193]['m_Text']=='0 / 0 PLAYERS READY',role+' concise group counter')
  check(docs[184]['m_Interactable']==0,role+' Ready begins disabled')
  presenters=prompts(docs);check(len(presenters)==(6 if role=='Fish'else 7),role+' every control and Ready has an authored glyph presenter')
  check(len({c['m_FontData']['m_Font']['guid']for c in docs.values()if'm_FontData'in c})==2,role+' only two coherent font families')
  labels=[];boxes=[]
  for ident,component in docs.items():
   if'm_FontData'not in component:continue
   font=font_for(component);fd=component['m_FontData'];go=component['m_GameObject']['fileID'];rect=docs[go+1];width,height=rect['m_SizeDelta']['x'],rect['m_SizeDelta']['y'];copy=component['m_Text']
   check(fd['m_BestFit']==0,role+' no unpredictable text shrinking: '+docs[go]['m_Name'])
   if not copy:continue
   extra=1 if fd['m_FontStyle']==1 else 0
   measured=max(font.getlength(line)for line in copy.split('\n'))+extra
   used=sum(font.getmetrics())*len(copy.split('\n'))+extra
   check(measured<=width,role+' copy fits width: '+copy);check(used<=height,role+' copy fits line-height: '+copy)
   box=rect_bounds(docs,go+1);check(inside(box,(-600,-336,600,336)),role+' content stays inside page')
   boxes.append((ident,box));labels.append({'file_id':ident,'text':copy,'font':FONTS[fd['m_Font']['guid']],'font_size':fd['m_FontSize'],'short_bold_emphasis':fd['m_FontStyle']==1,'width':round(measured,2),'line_height':used,'bounds':box})
  for n,(ident,box)in enumerate(boxes):
   for other,otherbox in boxes[n+1:]:check(not overlap(box,otherbox),f'{role} non-overlapping text {ident}/{other}')
  for caption in('Dining cart','Chandelier','Angler'):
   check(any(c.get('m_Text')==caption for c in docs.values()),role+' current trap name '+caption)
  check(not re.search('tablecart',source,re.I),role+' no obsolete cart name')
  combined=' '.join(item['text']for item in labels)
  check(not re.search(r'WASD|A / D|Space /|E / X|F / B|Xbox|PlayStation|gamepad',combined,re.I),role+' prose does not list competing platform bindings')
  check(len(combined.split())<=110,role+' concise one-page copy')
  check(sum(item['short_bold_emphasis'] for item in labels)<len(labels)/2,role+' selective emphasis leaves most copy regular')
  for family,name in((0,'PC'),(1,'Xbox'),(2,'PlayStation')):
   glyphs,fallbacks,details=resolved_slots(docs,family,catalog)
   for detail in details:
    prompt=docs[detail['component']];check(bool(detail['glyphs'])or detail['fallback']in('Mouse required','Keyboard required'),role+' truthful '+name+' glyph/fallback')
    if detail['glyphs']:
     for path in detail['glyphs']:
      check(('Keyboard & Mouse'in path)if family==0 else ('Xbox'in path)if family==1 else ('PlayStation'in path),role+' only active '+name+' glyph family')
   if role=='Ghost' and family in (1,2):
    discard=next(item for item in details if item['component']==1402)
    check(discard['paths']==['<Gamepad>/buttonNorth'] and discard['glyphs'] and not discard['fallback'],name+' Discard resolves its real Y/Triangle binding')
   for ident,copy in fallbacks.items():
    if not copy:continue
    component=docs[ident];go=component['m_GameObject']['fileID'];rect=docs[go+1];font=font_for(component);lines=wrap(copy,font,rect['m_SizeDelta']['x'])
    check(max(font.getlength(line)for line in lines)<=rect['m_SizeDelta']['x'],role+' fallback width '+copy)
    check(sum(font.getmetrics())*len(lines)<=rect['m_SizeDelta']['y'],role+' fallback height '+copy)
   report['roles'][role+' '+name+' inputs']=details
  label_font=font_for(docs[203]);status_font=font_for(docs[193])
  for label in re.findall(r'return "([A-Z ,]+)";',raw('Assets/Scripts/Multiplayer/RoundHowToState.cs')):
   check(label_font.getlength(label)+1<=docs[201]['m_SizeDelta']['x'],role+' dynamic button label fits: '+label)
  check(status_font.getlength('2147483647 / 2147483647 PLAYERS READY')<=docs[191]['m_SizeDelta']['x'],role+' maximum group counter fits')
  report['roles'][role]=labels
 for width,height,safe in [(1280,720,(0,0,1280,720)),(1920,1080,(0,0,1920,1080)),(1280,800,(0,0,1280,800)),(1024,768,(0,0,1024,768)),(3440,1440,(0,0,3440,1440)),(2340,1080,(108,40,2124,1000))]:
  sx,sy,sw,sh=safe;canvas=min(width/1280,height/720);scale=min(1,sw/canvas/1280,sh/canvas/720);pixel=canvas*scale;cx,cy=sx+sw/2,sy+sh/2;bounds=(cx-640*pixel,cy-360*pixel,cx+640*pixel,cy+360*pixel)
  check(inside(bounds,(sx,sy,sx+sw,sy+sh)),str((width,height))+' full guide inside safe display');report['displays'].append({'pixels':[width,height],'safe_area':safe,'page_pixel_bounds':bounds,'body_pixels':27*pixel})
 check('!_Gate.Complete || !_ReadyGate.Complete'in raw('Assets/Scripts/Multiplayer/GameSceneController.cs'),'All-ready authoritative barrier retained')
 runtime=raw('Assets/Scripts/Multiplayer/RoundHowToPage.cs')+raw('Assets/Scripts/Multiplayer/RoundHowToSafeArea.cs')+raw('Assets/Scripts/InputPrompts/InputGlyphPrompt.cs')
 check(all(token not in runtime for token in('new GameObject','Instantiate(','AddComponent')),'No runtime hierarchy construction')
 report['checks']=checks
 if'--report'in sys.argv:
  path=Path(sys.argv[sys.argv.index('--report')+1]);path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(report,indent=2)+'\n')
 print(f'Passed {checks} authored guide, active-family glyph, font-fit, concise-copy and safe-area checks. No native render/network pass claimed.')

def raw(path):return(ROOT/path).read_text()
if __name__=='__main__':main()
