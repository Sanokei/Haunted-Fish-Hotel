#!/usr/bin/env python3
"""Record a verified source checkpoint without staging shared work or claiming native QA."""
from pathlib import Path
import hashlib,json,re
import yaml
ROOT=Path(__file__).resolve().parents[2];D=ROOT/'Tools/WorldLightingTests'
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 core=json.loads((D/'source-freeze.json').read_text());paths={x['path'] for x in core['files']}
 paths.update(str(p.relative_to(ROOT)) for p in (ROOT/'Assets/Art/WorldLighting').glob('*') if p.is_file())
 paths.update(['Assets/Art/Hallway/HallwaySpriteLit.mat','Assets/Art/Materials/LobbyRevision/LobbySpriteFog0.mat','Assets/Art/BossFight/BossArmVoidFade.mat'])
 for p in list(paths):
  if (ROOT/(p+'.meta')).is_file():paths.add(p+'.meta')
 core=dict(scope='Stable authored world-art shaders/materials and atmospheric assets, including approved intro contrast. Native GPU validation is not established.',files=[dict(path=p,sha256=digest(ROOT/p)) for p in sorted(paths)])
 (D/'source-freeze.json').write_text(json.dumps(core,indent=2)+'\n')
 replay=json.loads((ROOT/'Tools/WatchingFrameTests/eye-distribution-idempotence.json').read_text());files={x['path']:x['sha256'] for x in core['files']}
 for p,h in replay['sha256'].items():
  assert digest(ROOT/p)==h,'Generator replay hash drift: '+p
  files[p]=h
 # Include all frame-material metadata and dormant authored variants, not only current bindings.
 extra=list((ROOT/'Assets/Prefabs/WatchingFrames').rglob('*.mat'))+list((ROOT/'Assets/Prefabs/WatchingFrames').rglob('*.mat.meta'))
 extra += [ROOT/p for p in ['Assets/Scripts/Presentation/WatchingFrames/WatchingFrameGallery.cs','Assets/Scripts/Presentation/WatchingFrames/WatchingFrameGallery.cs.meta','Tools/WatchingFrameTests/author_frame_galleries.py','Tools/WatchingFrameTests/author_intro_frame_tunnel.py']]
 extra += list(D.glob('*.py'))+list(D.glob('*.sh'))+[D/'README.md',D/'room-atmosphere.json',D/'existing-key-shadows.json',D/'authoring-manifest.json']
 for p in extra:files[str(p.relative_to(ROOT))]=digest(p)
 manifest=json.loads((D/'authoring-manifest.json').read_text());cache={};rows=[]
 for p,ident in manifest['renderer_updates']:
  if p not in cache:cache[p]={int(m[2]):next(iter(yaml.safe_load(m[3]).values())) for m in re.finditer(r'^--- !u!(\d+) &(-?\d+).*?\n(.*?)(?=^--- !u!|\Z)',(ROOT/p).read_text(),re.M|re.S)}
  d=cache[p][int(ident)];rows.append(dict(path=p,file_id=int(ident),lighting_owned_fields={k:d[k] for k in ['m_CastShadows','m_ReceiveShadows','m_Materials']}))
 scene_paths=['Assets/Scenes/Lobby.unity','Assets/Scenes/BossFight.unity','Assets/Scenes/theatre.unity','Assets/Resources/GameHallway.prefab']
 report=dict(scope='Verified lighting plus deterministic one-third eye galleries. Shared gameplay assets are represented only by the three owned renderer fields below; do not publish their unrelated WIP. Scene hashes are integrated snapshot evidence, not an instruction to include unrelated changes.',pending_visual_signoff=False,contrast_review='Root approved final still0096: intro near visibility0.8, ambient0.8, distance32m, pupil intensity0.125. Renderer owns motion proof.',native_gpu_validation=False,files=dict(sorted(files.items())),integrated_scene_snapshot_sha256={p:digest(ROOT/p) for p in scene_paths},frame_counts={'Lobby':dict(frames=365,eyes=121),'Hallway':dict(frames=8,eyes=3),'IntroExtension':dict(frames=553,eyes=182)},owned_renderer_fields=rows,scene_lighting_recipe=['Tools/WorldLightingTests/room-atmosphere.json','Tools/WorldLightingTests/existing-key-shadows.json'],proof_recipe='Tools/WorldLightingTests/render_shadow_source_proof.py',regression_logs=['latest-source-check.txt','unity-api-validation.txt','gallery-regression.txt','extension-regression.txt','sconce-regression.txt','gaze-api-regression.txt','aggregate-scene-validation.txt','final-authoring-replay.txt','existing-key-replay.txt'])
 (D/'joint-lighting-gallery-freeze.json').write_text(json.dumps(report,indent=2)+'\n')
 print('Verified frozen files',len(files),'renderer field records',len(rows))
 print('Joint manifest SHA256',digest(D/'joint-lighting-gallery-freeze.json'))
 print('WatchingEye SHA256',digest(ROOT/'Assets/Prefabs/WatchingFrames/Materials/WatchingEye.shader'))
if __name__=='__main__':main()
