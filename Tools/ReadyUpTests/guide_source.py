"""Read serialized guide assets for deterministic source proofs. Never runs Unity."""
from pathlib import Path
import re,sys,yaml,json
from PIL import ImageFont
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/ScenePresentationTests'))
import verify_scene_assets as assets
FONTS={'d4e80c7736f5472abd2cace956a4fda6':'Assets/Art/Fonts/Modak-Regular.ttf','c36f36ab1733544a8cde4a7ae27f0672':'Assets/Art/Fonts/HauntedIntertitle-Regular.ttf'}
GLYPH_SCRIPT='b1e3ee40e82b591592d221ebed991260'

def documents(path):
 source=Path(path).read_text()
 return source,{r.ident:next(iter(yaml.safe_load(r.body).values())) for r in assets.records(source)}
def font_for(component,scale=1):return ImageFont.truetype(str(ROOT/FONTS[component['m_FontData']['m_Font']['guid']]),round(component['m_FontData']['m_FontSize']*scale))
def guide(role):return documents(ROOT/f'Assets/Resources/{role}RoundHowTo.prefab')
def offset(docs,ident):
 rect=docs[ident];x,y=rect['m_AnchoredPosition']['x'],rect['m_AnchoredPosition']['y'];parent=rect['m_Father']['fileID']
 if parent and ident!=221:
  px,py=offset(docs,parent);x+=px;y+=py
 return x,y

def rect_bounds(docs,ident):
 rect=docs[ident];x,y=offset(docs,ident);w,h=rect['m_SizeDelta']['x'],rect['m_SizeDelta']['y'];return x-w/2,y-h/2,x+w/2,y+h/2

def inside(inner,outer):return inner[0]>=outer[0]-.01 and inner[1]>=outer[1]-.01 and inner[2]<=outer[2]+.01 and inner[3]<=outer[3]+.01

def overlap(a,b):return min(a[2],b[2])-max(a[0],b[0])>.1 and min(a[3],b[3])-max(a[1],b[1])>.1

def wrap(text,font,width):
 lines=[]
 for paragraph in text.split('\n'):
  current=''
  for word in paragraph.split(' '):
   candidate=(current+' '+word).strip()
   if current and font.getlength(candidate)>width:lines.append(current);current=word
   else:current=candidate
  lines.append(current)
 return lines

def prompts(docs):return {i:c for i,c in docs.items() if c.get('m_Script',{}).get('guid')==GLYPH_SCRIPT}

def input_paths(prompt,family):
 """Default authored binding selection for proof only; production C# resolver is separately tested."""
 keyboard=family==0
 def matches(path):return path and (path.startswith(('<Keyboard>','<Mouse>','<Pointer>')) if keyboard else path.startswith('<Gamepad>'))
 if prompt['_ActionAsset']['fileID']:
  map_name,action=prompt['_ActionPath'].split('/')
  maps=json.loads((ROOT/'Assets/Resources/HotelMultiplayerActions.inputactions').read_text())['maps'];bindings=next(m['bindings'] for m in maps if m['name']==map_name)
  relevant=[b for b in bindings if b['action']==action]
  for index,binding in enumerate(relevant):
   if binding.get('isPartOfComposite'):continue
   if binding.get('isComposite'):
    parts={}
    for part in relevant[index+1:]:
     if not part.get('isPartOfComposite'):break
     if matches(part['path']) and part['name'] not in parts:parts[part['name']]=part['path']
    if parts:return [parts[key] for key in ('up','left','down','right','negative','positive') if key in parts],''
   elif matches(binding['path']):return[binding['path']],''
  mouse=any(b['path'].startswith(('<Mouse>','<Pointer>')) for b in relevant)
  return [],('Mouse required' if mouse else 'Keyboard required') if not keyboard else 'Unbound'
 path=prompt.get('_KeyboardPath' if keyboard else '_GamepadPath')
 if path:return[path],''
 prior=prompt.get('_KeyboardPath')or''
 return [],('Mouse required' if prior.startswith(('<Mouse>','<Pointer>')) else 'Keyboard required') if prior else 'Unbound'

class GlyphCatalog:
 def __init__(self):
  _,docs=documents(ROOT/'Assets/Resources/InputPrompts/HotelInputGlyphs.asset')
  self.entries=next(c['_Glyphs']for c in docs.values() if '_Glyphs'in c)
  self.paths={}
  for meta in (ROOT/'Assets/Art/InputPrompts').rglob('*.png.meta'):
   match=re.search(r'^guid: ([a-f0-9]+)',meta.read_text(),re.M)
   if match:self.paths[match[1]]=Path(str(meta)[:-5])
 def find(self,family,path):
  key=path if family==0 else path.split('/',1)[-1]
  for entry in self.entries:
   if entry['Family']==family and entry['ControlPath'].lower()==key.lower():return self.paths[entry['Sprite']['guid']]
  if family in(1,2):return self.find(3,path)
  return None

def resolved_slots(docs,family,catalog):
 glyphs={};fallbacks={};details=[]
 for ident,prompt in prompts(docs).items():
  paths,reason=input_paths(prompt,family)
  slots=[prompt['_Glyph']['fileID']]+[r['fileID']for r in prompt.get('_AdditionalGlyphs',[])]
  pictures=[catalog.find(family,path)for path in paths]
  drawable=bool(paths)and len(pictures)<=len(slots)and all(pictures)
  for n,slot in enumerate(slots):glyphs[slot]=pictures[n]if drawable and n<len(pictures)else None
  fallbacks[prompt['_Fallback']['fileID']]=''if drawable else reason or ' / '.join(paths)
  details.append({'component':ident,'paths':paths,'glyphs':[str(p.relative_to(ROOT))for p in pictures if p]if drawable else [],'fallback':fallbacks[prompt['_Fallback']['fileID']]})
 return glyphs,fallbacks,details
