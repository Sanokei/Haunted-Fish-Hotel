#!/usr/bin/env python3
"""Idempotent offline asset migration. No runtime creation; preserve unrelated serialized fields."""
import hashlib,json,re,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/WatchingFrameTests'))
import author_frame_galleries as a
D=ROOT/'Assets/Art/WorldLighting';REPORT=ROOT/'Tools/WorldLightingTests'
def guid(p):return a.guid(str(p.relative_to(ROOT)))
def meta(p):return a.meta(p,'NativeFormatImporter')
def float_props(p,values):
 s=p.read_text()
 for k,v in values.items():
  pattern=rf'(?m)^    - {re.escape(k)}: .*$'
  if re.search(pattern,s):s=re.sub(pattern,f'    - {k}: {v}',s)
  else:s=s.replace('    m_Floats:\n','    m_Floats:\n'+f'    - {k}: {v}\n')
 p.write_text(s)
COMMON={'_Ambient':.48,'_LightResponse':.85,'_LightLimit':1.1,'_ShadowCutoff':.2,'_DarknessStart':10000,'_DarknessEnd':10001,'_DarknessFloor':1}
LOBBY=dict(COMMON,_Ambient=.32,_DarknessStart=4.25,_DarknessEnd=12,_DarknessFloor=.012)
HALL=dict(COMMON,_Ambient=.52,_LightResponse=.72)
INTRO=dict(LOBBY,_Ambient=.8,_NearArtDistance=32,_NearArtVisibility=.8,_PupilIntensity=.125)
def main():
 hallway=ROOT/'Assets/Art/Hallway/HallwaySpriteLit.mat';float_props(hallway,HALL)
 lobby=ROOT/'Assets/Art/Materials/LobbyRevision/LobbySpriteFog0.mat';float_props(lobby,LOBBY)
 p=D/'WorldSpriteLit.mat'
 s=hallway.read_text().replace('m_Name: Hallway original art - real point light response','m_Name: World physical artwork - alpha-shaped shadows').replace(guid(ROOT/'Assets/Art/Hallway/HallwaySpriteLit.shader'),guid(D/'WorldSpriteLit.shader'));p.write_text(s);meta(p);float_props(p,COMMON)
 float_props(ROOT/'Assets/Art/BossFight/BossArmVoidFade.mat',dict(COMMON,_Ambient=.6))
 for p in (ROOT/'Assets/Prefabs/WatchingFrames').rglob('*.mat'):
  if 'Frame-' not in p.name:continue
  float_props(p,INTRO if p.name.startswith('IntroTunnel-') else LOBBY if p.name.startswith('Lobby-') else HALL)
 worldmat=guid(D/'WorldSpriteLit.mat');hallmat=guid(hallway)
 paths=list((ROOT/'Assets/Resources').rglob('*.prefab'))+list((ROOT/'Assets/Scenes').glob('*.unity'))+list((ROOT/'Assets/Prefabs').rglob('*.prefab'))
 changes=[];excluded=[]
 for p in paths:
  rel=str(p.relative_to(ROOT))
  # Existing storm projection cards are nonphysical feedback; preserve original file encoding too.
  if '/Storm/' in rel:continue
  s=p.read_text()
  def patch(m):
   block=m[0];kind=m[1];fid=m[2]
   if kind=='212' and 'm_Materials:' in block:
    # Atmosphere, cursor/placement previews and animated music waves are graphics/VFX, not physical cards.
    if any(v in rel for v in ['StormAtmosphere','StormHallwaySurfaces']) or any(v in block for v in ['ec0036378fdc5757b9ededc8136ab981']):
     excluded.append([rel,fid,'Weather or placement/cursor overlay']);return block
    g=re.search(r'm_Sprite: \{[^}]*guid: (\w+)',block);sg=g[1] if g else ''
    # Material selection operates only on known world-art materials; never Canvas/UI components.
    if not any(v in block for v in ['9dfc825aed78fcd4ba02077103263b40',guid(hallway),guid(lobby),worldmat]):return block
    # Original BOSS cursor and angler glow are intentionally nonphysical world-space feedback.
    # Lobby music waves already bind WorldFeedbackUnlitFog, so neither their color nor shadow state migrates.
    from_sprite={a.guid(q) for q in ['Assets/Art/BOSS-cursor.png','Assets/Art/Traps/TRAP-Angler-fish-glow.png','Assets/Art/Music-layer-1.png','Assets/Art/Music-layer-2.png','Assets/Art/Music-layer-3.png','Assets/Art/Music-layer-4.png']}
    if sg in from_sprite:
     excluded.append([rel,fid,'Nonphysical feedback artwork']);return block
    block=block.replace('9dfc825aed78fcd4ba02077103263b40',hallmat if ('Hallway' in rel or 'Sconce' in rel) else worldmat)
    block=re.sub(r'm_CastShadows: \d+', 'm_CastShadows: 2',block)
    block=re.sub(r'm_ReceiveShadows: \d+', 'm_ReceiveShadows: 1',block)
   elif kind=='23' and 'WatchingFrames' in rel and 'm_Materials:' in block:
    # Eye-frame quads only. Black throat / dark atmospheric panels are not shadow-casting masks.
    g=re.search(r'm_Materials:\n\s+- \{fileID: 2100000, guid: (\w+)',block)
    mats={a.guid(str(q.relative_to(ROOT))) for q in (ROOT/'Assets/Prefabs/WatchingFrames').rglob('*Frame-*.mat')}
    if not g or g[1] not in mats:return block
    block=re.sub(r'm_CastShadows: \d+', 'm_CastShadows: 2',block)
    block=re.sub(r'm_ReceiveShadows: \d+', 'm_ReceiveShadows: 1',block)
   if block!=m[0]:changes.append([rel,fid])
   return block
  ns=re.sub(r'^--- !u!(\d+) &(-?\d+).*?\n.*?(?=^--- !u!|\Z)',patch,s,flags=re.M|re.S)
  if ns!=s:p.write_text(ns)
 report={'migration':'Authored 3D-lit sprite cards and silhouette shadow casters','renderer_updates_this_run':changes,'nonphysical_exceptions':excluded,'material_parameters':{'world':COMMON,'hallway':HALL,'lobby':LOBBY,'intro':INTRO},'limitations':['Native Unity shader import/render not run; source and real C# API checks are separate.','Screen-space Canvas/Image graphics have no 3D shadow meaning and are deliberately untouched.','Pupil RGB is unlit; frame/backing silhouette casts independently of pupil position.']}
 previous=REPORT/'authoring-manifest.json'
 if previous.exists():report['renderer_updates']=sorted({tuple(v) for v in json.loads(previous.read_text()).get('renderer_updates',[])+changes})
 else:report['renderer_updates']=changes
 previous.write_text(json.dumps(report,indent=2)+'\n');print('Updated renderer blocks:',len(changes))
if __name__=='__main__':main()
