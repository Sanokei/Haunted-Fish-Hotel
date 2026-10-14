"""Read-only serialized UI/sprite/source validation. Never edits image pixels."""
from pathlib import Path
import re, hashlib, subprocess
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
checks=0
def check(value,message):
 global checks
 assert value,message
 checks+=1
def read(p):return (ROOT/p).read_text()
def docs(text):
 result={}
 for m in re.finditer(r'(?m)^--- !u!(\d+) &(-?\d+)(?: stripped)?\n([\s\S]*?)(?=^--- !u!|\Z)',text):
  check(m[2] not in result,'unique fileID '+m[2]);result[m[2]]=m[3]
 return result
def refs(text,documents):
 for ref in re.findall(r'{fileID: (-?\d+)}',text):check(ref=='0' or ref in documents,'resolved local fileID '+ref)
ui='Assets/Resources/BossFight/SceneObjects/BossUI.prefab'
for path in [ui,'Assets/Resources/BossFight/BossFightArena.prefab']:
 text=read(path);d=docs(text);refs(text,d)
 hud=next(v for v in d.values() if 'Assembly-CSharp::HauntedFish.BossFight.BossHudPresentation' in v)
 check('_FishLife: {fileID: 910200054}' in hud,'fish icon serialized')
 check(all('{fileID: '+str(k)+'}' in hud for k in [910200064,910200074,910200084]),'three ghost images serialized')
 label=d['910200024'];check('m_text: "Lives:"' in label and 'm_RaycastTarget: 0' in label,'literal noninteractive Lives label')
 check('guid: 7b418d1c9a165c578a62c1d026aab649' in label and 'm_fontSize: 44' in label,'HauntedIntertitle TMP font')
 check('m_AnchoredPosition: {x: 32, y: -24}' in d['910200012'] and 'm_AnchorMin: {x: 0, y: 1}' in d['910200012'],'top-left authored inset')
 check('m_Children:\n  - {fileID: 910200092}' in d['910200012'],'readability backing is first sibling beneath label/icons')
 check('m_AnchoredPosition: {x: -8, y: 8}' in d['910200092'] and 'm_SizeDelta: {x: 436, y: 112}' in d['910200092'],'readability backing has eight-pixel padding')
 check('m_Color: {r: 0, g: 0, b: 0, a: 0.85}' in d['910200094'] and 'm_RaycastTarget: 0' in d['910200094'],'readability backing is translucent black and noninteractive')
 check('m_AnchorMax: {x: 1, y: 1}' in d['910200002'],'safe area stretches full canvas')
 check('_Content: {fileID: 910200002}' in d['910200003'],'safe-area component wired')
 for name in ['Lives','Lives meter backing','Control and result hint']:
  g=next(v for v in d.values() if '  m_Name: '+name+'\n' in v);check('m_IsActive: 0' in g,'removed bottom element stays disabled: '+name)
 check('m_IsActive: 0' in d['910200031'] and 'm_IsActive: 0' in d['910200041'],'role rows start hidden')
 for i in [910200054,910200064,910200074,910200084]:
  check('m_PreserveAspect: 1' in d[str(i)] and 'm_RaycastTarget: 0' in d[str(i)],'aspect-preserved noninteractive icon')
 for i in [910200064,910200074,910200084]:check('fileID: 21300000, guid: 3d80936c6f444eb183ee1d1ba756125c' in d[str(i)],'all ghost heads use separate neutral boss life art')
 canvas=d[re.search(r'_Canvas: {fileID: (\d+)}',hud)[1]];check('m_SortingOrder: 20' in canvas and 'm_Enabled: 0' in canvas,'HUD below menus, hidden before viewer selection')
scene=read('Assets/Scenes/BossFight.unity');d=docs(scene);refs(scene,d)
check('_Hud: {fileID: 1119873192}' in scene,'active scene manager binds scene-owned presenter')
for k in [24,31,41,54,64,74,84]:
 check(f'{{fileID: {910300000+k}}}' in d['1119873192'],'active presenter binds each authored UI reference')
 check(f'm_CorrespondingSourceObject: {{fileID: {910200000+k}, guid: bcb5deadf28128d44a966a8f6e99ad2b' in d[str(910300000+k)],'scene stripped reference points to BossUI source')
meta=read('Assets/Art/Boss/BOSS-v1-neutral-head.png.meta')
check('internalID: 322984378' in meta and '322984379' not in meta,'segmented boss sprite metadata stays original')
original_boss=ROOT/'Assets/Art/Boss/BOSS-v1-neutral-head.png'
check(hashlib.sha256(original_boss.read_bytes()).hexdigest()=='dee7cea2c8e04c03ffa268c0d2299960a0e7e0cedb0976033bda4817551b1ff1','gameplay boss raster unchanged')
check(hashlib.sha256((ROOT/'Assets/Art/HotelFish/FISH-right.png').read_bytes()).hexdigest()=='f8c6cd40901e2f4f4877f6453ab6685d9ebf148045863b96409b146e9e10db60','gameplay fish raster unchanged')
cut_samples={}
for name,fractions,minimum_drop in [('Player-fish-life-head.png',(.3,.55,.8),100),('Boss-neutral-life-head.png',(.3,.5,.7),40)]:
 im=Image.open(ROOT/'Assets/Art/Boss'/name);a=im.getchannel('A')
 check(im.mode=='RGBA' and a.getextrema()==(0,255),'real transparent alpha: '+name)
 bottom=lambda x:max(y for y in range(im.height) if a.getpixel((x,y))>200)
 columns=[int(im.width*f) for f in fractions];edges=[bottom(x) for x in columns]
 check(edges[1]>edges[0]+minimum_drop and edges[1]>edges[2]+minimum_drop,'convex lower cut has center BELOW both sides: '+name)
 cut_samples[name]=dict(zip(columns,edges))
manager=read('Assets/Scripts/BossFight/BossArenaManager.cs')
check('PresentScore();\n            SnapshotChanged?.Invoke' in manager,'authority publishes score then refreshes HUD')
check('PresentScore();\n            return true;' in manager,'remote accepted snapshot refreshes HUD')
check('s.Revision <= _ObservedRevision' in manager,'existing stale/repeated snapshot gate retained')
check('void RefreshViewer()' in manager and '_Hud.Present(' in manager,'role/practice selection uses same presentation path')
for path in ['Assets/Scripts/BossFight/BossHudPresentation.cs','Assets/Scripts/BossFight/BossHudSafeArea.cs']:
 s=read(path);check(not re.search(r'\b(Update|LateUpdate)\s*\(',s),'no monolithic frame loop: '+path)
 check(not re.search(r'new GameObject|Instantiate\(|AddComponent<|Sprite.Create|Texture2D',s),'no runtime HUD construction: '+path)
check('m_SortingOrder: 32767' in read('Assets/Resources/MemberMenu.prefab'),'existing menu order remains above HUD')
print(f'{checks} authored scene/prefab/icon/source checks passed (read-only source inspection; not native Unity).')
for name,samples in cut_samples.items():
 print(name+' SHA256 '+hashlib.sha256((ROOT/'Assets/Art/Boss'/name).read_bytes()).hexdigest())
 print('Lower opaque edge y, image coordinates: '+str(samples))
