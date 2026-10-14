#!/usr/bin/env python3
"""Deterministically author shared glyph references; never creates runtime UI."""
from pathlib import Path
import json, re, sys
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/ScenePresentationTests'))
from verify_scene_assets import records
CAT='8637790bc13a4245d40e97a5fa26c552'
CAT_SCRIPT='67211a8fec0371c91028212e5985e767'
INLINE='46eb25c8bc896c7fea520e80685976d0'
SPRITES='57a166b6f5bc1abf4547f1c92949f12e'
ACTIONS='6b1b59be39e74c7fb572115019637e61'
ACTION_ID=-944628639613478452

def source(path): return (ROOT/path).read_text()
def field(body,key,value):
    line=f'  {key}: {value}'
    if re.search(r'^  '+re.escape(key)+r':.*$',body,re.M):return re.sub(r'^  '+re.escape(key)+r':.*$',line,body,flags=re.M)
    return body.rstrip()+'\n'+line+'\n'
def edit(path,fn):
    original=source(path);stripped={r.ident for r in records(original) if r.stripped};docs={r.ident:(r.kind,r.body) for r in records(original)};order=list(docs)
    fn(docs)
    (ROOT/path).write_text(original[:original.index('--- !u!')]+''.join(f'--- !u!{docs[i][0]} &{i}'+(' stripped' if i in stripped else '')+'\n'+docs[i][1] for i in order+sorted(set(docs)-set(order))))
def patch(docs,id,key,value):kind,b=docs[id];docs[id]=(kind,field(b,key,value))
def attach(docs,go,component):
    kind,b=docs[go];line=f'  - component: {{fileID: {component}}}\n'
    if line not in b:b=b.replace('  m_Layer:',line+'  m_Layer:',1)
    docs[go]=(kind,b)
def inline(go,textid,template):
    return f'''MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {INLINE}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  _Catalog: {{fileID: 11400000, guid: {CAT}, type: 2}}
  _SpriteAsset: {{fileID: 11400000, guid: {SPRITES}, type: 2}}
  _ActionAsset: {{fileID: {ACTION_ID}, guid: {ACTIONS}, type: 3}}
  _Text: {{fileID: {textid}}}
  _Template: {json.dumps(template)}
'''
def common(go):return f'''  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
'''
def add_text(docs,start,parent,template,tmp):
    docs[start]=(1,f'''GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {start+1}}}
  - component: {{fileID: {start+2}}}
  - component: {{fileID: {start+3}}}
  - component: {{fileID: {start+4}}}
  m_Layer: 5
  m_Name: Active device controls
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
''')
    docs[start+1]=(224,'RectTransform:\n'+common(start)+f'''  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {parent}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 0}}
  m_AnchoredPosition: {{x: 0, y: 25}}
  m_SizeDelta: {{x: -32, y: 44}}
  m_Pivot: {{x: 0.5, y: 0.5}}
''')
    docs[start+2]=(222,'CanvasRenderer:\n'+common(start)+'  m_CullTransparentMesh: 1\n')
    docs[start+3]=(114,field(tmp,'m_GameObject',f'{{fileID: {start}}}'))
    for key,value in {'m_text':json.dumps(''), 'm_fontSize':'24','m_fontSizeBase':'24','m_HorizontalAlignment':'2','m_VerticalAlignment':'512','m_enableAutoSizing':'0','m_TextWrappingMode':'0','m_spriteAsset':f'{{fileID: 11400000, guid: {SPRITES}, type: 2}}'}.items():patch(docs,start+3,key,value)
    docs[start+4]=(114,inline(start,start+3,template))
    kind,b=docs[parent];ref=f'  - {{fileID: {start+1}}}\n'
    if ref not in b:b=b.replace('  m_Father:',ref+'  m_Father:',1)
    docs[parent]=(kind,b)

manifest=json.loads(source('Assets/Art/InputPrompts/glyph-manifest.json'))
cat='''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
'''+f'''  m_Script: {{fileID: 11500000, guid: {CAT_SCRIPT}, type: 3}}
  m_Name: HotelInputGlyphs
  m_EditorClassIdentifier:
  _Glyphs:
'''
for g in manifest['glyphs']:
    cat+=f'''  - Family: {g['familyId']}
    ControlPath: {json.dumps(g['controlPath'])}
    Sprite: {{fileID: {g['spriteFileId']}, guid: {g['spriteGuid']}, type: 3}}
    SpriteName: {g['spriteName']}
'''
(ROOT/'Assets/Resources/InputPrompts/HotelInputGlyphs.asset').write_text(cat)
member={r.ident:(r.kind,r.body) for r in records(source('Assets/Resources/MemberMenu.prefab'))}
tmp='\n'.join(line.rstrip() for line in member[980000358][1].splitlines())+'\n'

def game(d):
    add_text(d,51001000,31000053,'{input:Ghost/Move} Fly   {input:Ghost/Confirm} Open nearby package',tmp)
    patch(d,31000152,'_InputPrompts','{fileID: 51001004}')
    patch(d,31000059,'m_Text',json.dumps('Fly to the package conveyor above the hallway'))
    patch(d,31000057,'m_AnchorMin','{x: 0, y: 1}');patch(d,31000057,'m_AnchorMax','{x: 1, y: 1}')
    patch(d,31000057,'m_AnchoredPosition','{x: 0, y: -20}');patch(d,31000057,'m_SizeDelta','{x: -32, y: 30}')
    coordinator=next(i for i,(_,b) in d.items() if '_Prompt: {fileID: 550014}' in b)
    patch(d,coordinator,'_PromptGlyphs','{fileID: 51001010}')
    # Reuse the entrance's already-authored text/rect; TMP inline glyphs preserve its layout.
    d[550014]=(114,field(tmp,'m_GameObject','{fileID: 550011}'))
    for key,val in {'m_text':json.dumps('Challenge the ghost at air hockey'),'m_fontSize':'27','m_fontSizeBase':'27','m_HorizontalAlignment':'2','m_VerticalAlignment':'512','m_TextWrappingMode':'0','m_spriteAsset':f'{{fileID: 11400000, guid: {SPRITES}, type: 2}}'}.items():patch(d,550014,key,val)
    attach(d,550011,51001010);d[51001010]=(114,inline(550011,550014,'{input:Game/Interact} Challenge the ghost at air hockey'))
edit('Assets/Scenes/Game.unity',game)

def menu(d):
    go=int(re.search(r'm_GameObject: \{fileID: (\d+)\}',d[980000358][1])[1])
    attach(d,go,51002000);d[51002000]=(114,inline(go,980000358,''))
    component=next(i for i,(_,b) in d.items() if '_FishContent:' in b and '_Instructions:' in b)
    patch(d,component,'_InputPrompts','{fileID: 51002000}')
    patch(d,980000358,'m_spriteAsset',f'{{fileID: 11400000, guid: {SPRITES}, type: 2}}')
edit('Assets/Resources/MemberMenu.prefab',menu)

def theatre(d):
    for id,(kind,b) in list(d.items()):
        if kind!=114 or '  m_text:' not in b or not ('LEFT: LOBBY EXIT' in b or 'Walk left to return to the lobby' in b):continue
        go=int(re.search(r'm_GameObject: \{fileID: (\d+)\}',b)[1])
        patch(d,id,'m_text',json.dumps('Walk left to return to the lobby'))
        patch(d,id,'m_spriteAsset',f'{{fileID: 11400000, guid: {SPRITES}, type: 2}}')
        attach(d,go,51003000);d[51003000]=(114,inline(go,id,'{input:<Keyboard>/a;<Keyboard>/d} Walk left to return to the lobby'))
for p in ['Assets/Scenes/theatre.unity','Assets/Resources/Theatre/TheatrePresentation.prefab']:edit(p,theatre)
print('Authored shared catalog and existing Game, MemberMenu and Theatre prompt bindings.')

def intro(d):
    go=7852033194056368440;text=5398190371629490443;rect=7386572765746047887
    kind,b=d[text]
    # Replace the old multi-line YAML scalar as a unit before normal field updates.
    b=re.sub(r"  m_text: 'CLICK TO SKIP\n\n    or hold SPACE'",'  m_text: "Skip introduction"',b)
    d[text]=(kind,b)
    patch(d,text,'m_spriteAsset',f'{{fileID: 11400000, guid: {SPRITES}, type: 2}}')
    patch(d,rect,'m_SizeDelta','{x: 320, y: 60}')
    patch(d,rect,'m_AnchoredPosition','{x: -255, y: 65}')
    attach(d,go,51004000);d[51004000]=(114,inline(go,text,'Skip: {input:<Mouse>/leftButton}\nHold to skip: {input:<Keyboard>/space}'))
edit('Assets/Resources/LobbyIntroductionUI.prefab',intro)
