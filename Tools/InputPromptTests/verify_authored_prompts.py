#!/usr/bin/env python3
"""Serialized UI binding and pinned glyph checks. This is not a Unity render test."""
from pathlib import Path
import json,re,sys,yaml
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/ScenePresentationTests'))
from verify_scene_assets import records,syntax_issues,reference_issues
checks=0
def check(value,message):
 global checks;checks+=1
 assert value,message

def read(p):return (ROOT/p).read_text()
def docs(p):return {r.ident:next(iter(yaml.safe_load(r.body).values())) for r in records(read(p))}
CAT='8637790bc13a4245d40e97a5fa26c552';INLINE='46eb25c8bc896c7fea520e80685976d0';SLOT='b1e3ee40e82b591592d221ebed991260';SPRITES='57a166b6f5bc1abf4547f1c92949f12e'
paths=['Assets/Resources/FishRoundHowTo.prefab','Assets/Resources/GhostRoundHowTo.prefab','Assets/Scenes/Game.unity','Assets/Resources/MemberMenu.prefab','Assets/Scenes/theatre.unity','Assets/Resources/Theatre/TheatrePresentation.prefab','Assets/Resources/LobbyIntroductionUI.prefab']
actions=json.loads(read('Assets/Resources/HotelMultiplayerActions.inputactions'))
actionpaths={m['name']+'/'+a['name'] for m in actions['maps'] for a in m['actions']}
ghost=next(m for m in actions['maps'] if m['name']=='Ghost')
discard=next(a for a in ghost['actions'] if a['name']=='Dispose')
check(discard['type']=='Button','Discard is a real button action')
check(any(b['action']=='Dispose' and b['path']=='<Keyboard>/q' for b in ghost['bindings']),'Discard keeps its keyboard binding')
check(any(b['action']=='Dispose' and b['path']=='<Gamepad>/buttonNorth' for b in ghost['bindings']),'Discard has actual controller binding')
check(sum(b['path']=='<Gamepad>/buttonNorth' for b in ghost['bindings'])==1,'Discard controller binding conflicts with no other Ghost action')
controller=read('Assets/Scripts/Multiplayer/GhostPlacementController.cs')
check('_Dispose = _Map.FindAction("Dispose", true)' in controller and '_Dispose.WasPressedThisFrame()' in controller,'Discard gameplay consumes the same action')
check('(_Possessed || player.ControlledCube >= 0)' in controller and 'player.RequestDisposeTrap' in controller and '_World.Dispose' in controller,'Controller discard follows held/possessed network and offline disposal routes')
manifest=json.loads(read('Assets/Art/InputPrompts/glyph-manifest.json'))
catalog=docs('Assets/Resources/InputPrompts/HotelInputGlyphs.asset')[11400000]['_Glyphs']
check(len(catalog)==len(manifest['glyphs']),'all manifest mappings appear in authored catalog')
for entry,source in zip(catalog,manifest['glyphs']):
 check(entry['Family']==source['familyId'] and entry['ControlPath']==source['controlPath'],'catalog control identity')
 check(entry['Sprite']['guid']==source['spriteGuid'] and entry['SpriteName']==source['spriteName'],'catalog exact source sprite')
seen=[]
for path in paths:
 text=read(path);data=docs(path)
 check(not syntax_issues(text),path+' valid YAML');check(not reference_issues(text),path+' closed hierarchy/references')
 presenters=0
 for ident,d in data.items():
  script=d.get('m_Script',{}).get('guid')
  if script not in (INLINE,SLOT):continue
  presenters+=1;seen.append((path,ident))
  check(d['_Catalog']['guid']==CAT,path+' shared catalog')
  owner=data[d['m_GameObject']['fileID']]
  check({'component':{'fileID':ident}} in owner['m_Component'],path+' presenter attached to authored object')
  if script==SLOT:
   check(d['_Glyph']['fileID'] in data and d['_Fallback']['fileID'] in data,path+' authored image and fallback')
   for image in [d['_Glyph']]+d.get('_AdditionalGlyphs',[]):check(data[image['fileID']]['m_Script']['guid']=='fe87c0e1cc204ed48ad3b37840f39efc',path+' glyph is UI.Image')
   check(data[d['_Fallback']['fileID']]['m_Script']['guid']=='5f7201a12d95ffc409449d95f23cf332',path+' fallback is UI.Text')
   if d.get('_ActionPath'):check(d['_ActionPath'] in actionpaths and d['_ActionAsset']['fileID']!=0,path+' current action-map binding')
   else:check(d['_ActionAsset']['fileID']==0 and d.get('_KeyboardPath'),path+' fixed-control slots have no misleading action reference')
  else:
   check(d['_SpriteAsset']['guid']==SPRITES,path+' shared TMP sprite asset')
   target=data[d['_Text']['fileID']]
   check(target['m_Script']['guid']=='f4688fdb7df04437aeb418b961361dc5',path+' inline target is actual TMP text')
   check(target['m_spriteAsset']['guid']==SPRITES,path+' TMP asset is also authored on text')
   for token in re.findall(r'\{input:([^}]+)\}',d.get('_Template','')):check(token.startswith('<') or token in actionpaths,path+' valid template control')
 check(presenters>0,path+' uses shared prompts')
 check(not re.search(r'(E / X|Space / gamepad A|WASD /|CLICK TO SKIP)',text),path+' no simultaneous-platform control list')
# Actual controller owners inject their authored presenters.
game=docs(paths[2]);check(game[31000152]['_InputPrompts']['fileID']==51001004,'Ghost HUD presenter bound')
coordinator=next(d for d in game.values() if d.get('_Prompt',{}).get('fileID')==550014)
check(coordinator['_PromptGlyphs']['fileID']==51001010,'entrance presenter bound')
menu=docs(paths[3]);controller=next(d for d in menu.values() if '_Instructions' in d and '_FishContent' in d)
check(controller['_InputPrompts']['fileID']==51002000,'menu role-content presenter bound')
for role in ('Fish','Ghost'):
 content=docs('Assets/Resources/Tutorials/'+role+'TutorialContent.asset')[11400000]
 for text in [content['Summary']]+[i['Body'] for i in content['Instructions']]:
  check(not re.search(r'E / X|F / B|gamepad A|gamepad X|WASD /|A / D / stick',text),role+' no hardcoded cross-platform controls')
  for token in re.findall(r'\{input:([^}]+)\}',text):check(token.startswith('<') or token in actionpaths,role+' token resolves to real action')
for path in (ROOT/'Assets/Scripts/InputPrompts').glob('*.cs'):
 s=path.read_text();check(not re.search(r'\b(?:Update|LateUpdate|FixedUpdate)\s*\(|new GameObject|AddComponent|FindObject|Resources\.Load|Instantiate\s*\(',s),path.name+' event-driven, explicit dependencies, no runtime UI building')
check('InputPromptActions.Register(_GameMap)' in read('Assets/Scripts/Multiplayer/HotelPlayerMovement.cs'),'local cloned movement maps registered')
check('InputPromptActions.Register(_Map)' in read('Assets/Scripts/Multiplayer/GhostPlacementController.cs'),'local cloned ghost map registered')
print(f'PASS {checks} authored glyph catalog, UI binding, action-token, provider and no-runtime-UI checks across {len(seen)} prompt presenters.')
print('Source-only validation. Native import/render and controller hardware remain unverified.')
