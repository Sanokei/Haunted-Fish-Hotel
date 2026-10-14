#!/usr/bin/env python3
"""Author fixed role-guide prefab objects; no runtime hierarchy construction."""
from pathlib import Path
import re,json
ROOT=Path(__file__).resolve().parents[2]
FONT_REG='c36f36ab1733544a8cde4a7ae27f0672'
FONT_BOLD=FONT_REG
FONT_TITLE='d4e80c7736f5472abd2cace956a4fda6'
GLYPH='b1e3ee40e82b591592d221ebed991260'
CATALOG='8637790bc13a4245d40e97a5fa26c552'
ACTIONS='6b1b59be39e74c7fb572115019637e61'
PATTERN=r'^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)'
source=(ROOT/'Assets/Resources/FishRoundHowTo.prefab').read_text()
T={int(i):(int(k),b) for k,i,b in re.findall(PATTERN,source,re.M|re.S)}
def prop(b,key,value): return re.sub(r'(^  '+re.escape(key)+r':)[^\n]*',lambda m:m[1]+' '+str(value),b,flags=re.M)
def components(b,ids): return re.sub(r'  m_Component:\n(?:  - component:.*\n)*','  m_Component:\n'+''.join(f'  - component: {{fileID: {i}}}\n' for i in ids),b)
def children(b,ids): return re.sub(r'  m_Children:.*\n(?:  - \{fileID:.*\n)*','  m_Children:'+ ('\n'+''.join(f'  - {{fileID: {i}}}\n' for i in ids) if ids else ' []\n'),b)
def obj(i,name,parent,x,y,w,h,kind='text',copy='',font=24,face=FONT_REG,align=3,color=None,kids=(),emphasis=False):
 result=[];template=130 if kind=='text' else 120
 for offset in (0,1,2,3):
  k,b=T[template+offset];b=prop(b,'m_GameObject',f'{{fileID: {i}}}')
  if offset==0: b=components(prop(b,'m_Name',name),[i+1,i+2,i+3])
  if offset==1:
   b=prop(b,'m_Father',f'{{fileID: {parent}}}');b=prop(b,'m_AnchoredPosition',f'{{x: {x}, y: {y}}}');b=prop(b,'m_SizeDelta',f'{{x: {w}, y: {h}}}');b=children(b,kids)
  if offset==3:
   b=prop(b,'m_Color',color or ('{r: 0.97, g: 0.92, b: 0.78, a: 1}' if kind=='text' else '{r: 0.16, g: 0.11, b: 0.23, a: 1}'));b=prop(b,'m_RaycastTarget','0')
   if kind=='text':
    b=re.sub(r'    m_Font:.*',f'    m_Font: {{fileID: 12800000, guid: {face}, type: 3}}',b)
    for key,value in [('m_FontSize',font),('m_MaxSize',font),('m_Alignment',align),('m_FontStyle',1 if emphasis else 0),('m_LineSpacing',1),('m_AlignByGeometry',0)]:b=re.sub(rf'    {key}:.*',f'    {key}: {value}',b)
    b=b[:b.index('  m_Text:')]+'  m_Text: '+json.dumps(copy)+'\n'
   else:b=prop(b,'m_Sprite','{fileID: 0}')
  result.append((k,i+offset,b))
 return result

def group(i,name,parent,x,y,w,h,kids):
 rows=obj(i,name,parent,x,y,w,h,kids=kids)
 k,j,b=rows[0];rows[0]=(k,j,components(b,[i+1]));return rows[:2]

def prompt(component_id,go,glyph,fallback,path='',keyboard='',pad='',additional=()):
 b=T[107][1].split('  _Canvas:')[0];b=prop(b,'m_GameObject',f'{{fileID: {go}}}')
 b=re.sub(r'guid: [0-9a-f]{32}',f'guid: {GLYPH}',b)
 b+=f'''  _Catalog: {{fileID: 11400000, guid: {CATALOG}, type: 2}}
  _ActionAsset: {('{fileID: -944628639613478452, guid: '+ACTIONS+', type: 3}') if path else '{fileID: 0}'}
  _ActionPath: {path}
  _KeyboardPath: {keyboard}
  _GamepadPath: {pad}
  _Glyph: {{fileID: {glyph}}}
  _Fallback: {{fileID: {fallback}}}
'''
 b+='  _AdditionalGlyphs:'+ ('\n'+''.join(f'  - {{fileID: {i}}}\n' for i in additional) if additional else ' []\n')
 return (114,component_id,b)

def control(i,label,path,y,keyboard=''):
 movement=path.endswith('/Move')
 extra=1 if path=='Game/Move' else 3 if movement else 0
 kids=[i+11,i+21,i+31]+[i+41+n*10 for n in range(extra)]
 rows=group(i,label+' control row',221,-282,y,494,42,kids)
 k,j,b=rows[0];rows[0]=(k,j,components(b,[i+1,i+2]))
 rows.append(prompt(i+2,i,i+13,i+23,path,keyboard,additional=[i+43+n*10 for n in range(extra)]))
 icon=obj(i+10,label+' active-device glyph',i+1,-227 if movement else -208,0,32 if movement else 70,36 if movement else 42,'image',color='{r: 1, g: 1, b: 1, a: 1}')
 k,j,b=icon[3];icon[3]=(k,j,prop(b,'m_PreserveAspect','1'));rows+=icon
 for n in range(extra):
  icon=obj(i+40+n*10,label+' composite glyph '+str(n+2),i+1,-190+n*37,0,32,36,'image',color='{r: 1, g: 1, b: 1, a: 1}')
  k,j,b=icon[3];icon[3]=(k,j,prop(b,'m_PreserveAspect','1'));rows+=icon
 rows+=obj(i+20,label+' binding fallback',i+1,-172 if movement else -205,0,150 if movement else 90,42,copy='',font=18,align=4)
 rows+=obj(i+30,label+' action',i+1,75,0,320,42,copy=label,font=28,emphasis=label in ('Jump','Interact','Drop chandelier','Reset trap'))
 return rows

for role in ('Fish','Ghost'):
 controls=[('Move','Game/Move',70),('Run','Game/Run',20),('Jump','Game/Jump',-30),('Interact','Game/Interact',-80),('Aim spotlight','',-130)] if role=='Fish' else [('Fly / drive','Ghost/Move',70),('Open / place / possess','Ghost/Confirm',28),('Drop chandelier','Ghost/TrapActivate',-14),('Reset trap','Ghost/Reset',-56),('Discard trap','Ghost/Dispose',-98),('Aim placement','Ghost/TrapPoint',-140)]
 root_children=[121,151,231,131,141,241,251,171,301,311,321,331,341,351,261,191,181]+[1001+i*100 for i in range(len(controls))]
 rows=[]
 for i in (100,101,104,105,106,107,109,110,111,112,113,210,211,220,221):
  k,b=T[i]
  if i==100:b=prop(b,'m_Name',role+'RoundHowTo')
  if i==221:b=children(b,root_children)
  rows.append((k,i,b))
 rows+=obj(120,'Single-page instruction board',221,0,0,1200,672,'image',color='{r: 0.1, g: 0.063, b: 0.153, a: 1}')
 rows+=obj(150,'Controls card',221,-282,-11,542,320,'image')
 rows+=obj(230,'Trap rules card',221,282,-11,542,320,'image')
 rows+=obj(130,'Bubbly role heading',221,0,266,1100,90,copy='YOU ARE THE '+role.upper(),font=56,face=FONT_TITLE,align=4)
 objective='Reach the stairwell. Challenge the ghost.' if role=='Fish' else 'Use packages and traps to stop the fish.'
 rows+=obj(140,'Role objective',221,0,192,1100,52,copy=objective,font=30,align=4)
 rows+=obj(240,'Controls heading',221,-282,120,494,50,copy='CONTROLS',font=30,face=FONT_TITLE)
 rows+=obj(250,'Trap rules heading',221,282,120,494,50,copy='Traps to avoid' if role=='Fish' else 'TRAPS',font=30,face=FONT_TITLE)
 facts=[('Dining cart','Knockback + stun. No damage.'),('Chandelier','Watch for the drop.'),('Angler','Lethal on contact.')] if role=='Fish' else [('Dining cart','Drive to stun fish. No damage.'),('Chandelier','Drop, then reset when clear.'),('Angler','Rush through fish. Every hit is lethal.')]
 for index,((name,body),top) in enumerate(zip(facts,(74,-5,-82))):
  heading_id=(170,310,330)[index];body_id=(300,320,340)[index]
  rows+=obj(heading_id,name+' important name',221,282,top,494,34,copy=name,font=28,emphasis=True)
  rows+=obj(body_id,name+' short rule',221,282,top-35,494,34,copy=body,font=27,emphasis=index==2)
 note='STARTING HALLWAY: TRAP-FREE' if role=='Fish' else 'PLACE CLEAR. KEEP STARTING HALLWAY SAFE.'
 rows+=obj(350,'Placement safety rule',221,282,-155,494,24,copy=note,font=20,emphasis=True)
 rows+=obj(260,'Everyone-ready countdown rule',221,0,-204,1100,42,copy='Everyone ready. 10-second ghost setup. Then fish move.',font=28,align=4)
 rows+=obj(190,'Live participant readiness',221,-275,-272,540,70,copy='0 / 0 PLAYERS READY',font=26,align=4)
 button=obj(180,'Ready up button',221,280,-270,480,62,'image',color='{r: 0.35, g: 0.2, b: 0.47, a: 1}',kids=[201,281,291])
 k,j,b=button[0];button[0]=(k,j,components(b,[181,182,183,184,185]));k,j,b=button[3];button[3]=(k,j,prop(b,'m_RaycastTarget','1'));rows+=button
 rows.append((114,184,prop(T[184][1],'m_Interactable','0')))
 rows.append(prompt(185,180,283,293,keyboard='<Keyboard>/enter',pad='<Gamepad>/buttonSouth'))
 rows+=obj(200,'Ready up label',181,32,0,380,58,copy='READ, THEN READY UP',font=28,align=4,emphasis=True)
 icon=obj(280,'Ready active-device glyph',181,-207,0,44,44,'image',color='{r: 1, g: 1, b: 1, a: 1}');k,j,b=icon[3];icon[3]=(k,j,prop(b,'m_PreserveAspect','1'));rows+=icon
 rows+=obj(290,'Ready binding fallback',181,-207,0,58,44,copy='',font=18,align=4)
 for index,(label,path,y) in enumerate(controls): rows+=control(1000+index*100,label,path,y,keyboard='<Pointer>/position' if label.startswith('Aim') and not path else '')
 out='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+''.join(f'--- !u!{k} &{i}\n{b}' for k,i,b in rows)
 target=ROOT/f'Assets/Resources/{role}RoundHowTo.prefab'
 target.write_text('\n'.join(line.rstrip() for line in out.splitlines())+'\n')
 # Serialize the PC default glyph art as well. The shared presenter switches these
 # same existing slots when the active family or binding changes.
 from guide_source import documents, resolved_slots, GlyphCatalog
 _,authored=documents(target)
 glyphs,_,_=resolved_slots(authored,0,GlyphCatalog())
 final=target.read_text()
 for ident,picture in glyphs.items():
  if not picture: continue
  sprite_guid=re.search(r'^guid: ([a-f0-9]+)',Path(str(picture)+'.meta').read_text(),re.M)[1]
  pattern=rf'(^--- !u!114 &{ident}\n)(.*?)(?=^--- !u!|\Z)'
  final=re.sub(pattern,lambda m:m[1]+prop(m[2],'m_Sprite',f'{{fileID: 21300000, guid: {sprite_guid}, type: 3}}'),final,flags=re.M|re.S)
 target.write_text('\n'.join(line.rstrip() for line in final.splitlines())+'\n')
