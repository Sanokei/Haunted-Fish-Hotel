#!/usr/bin/env python3
"""Independent authored eye/empty policy checks; no Unity runtime claim."""
import copy,hashlib,json,random,sys
from collections import Counter
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).parent))
import author_frame_galleries as a
G=json.loads((ROOT/'Tools/WatchingFrameTests/gallery-placement.json').read_text())
T=json.loads((ROOT/'Tools/WatchingFrameTests/intro-tunnel-placement.json').read_text())
checks=0
def check(value,label):
    global checks;checks+=1
    if not value:raise AssertionError(label)
report=[]
for scene,frames,policy in [('Lobby',[p for p in G['placements'] if p['scene']=='Lobby'],G['eye_selection']['Lobby']),('Game',[p for p in G['placements'] if p['scene']=='Game'],G['eye_selection']['Game']),('IntroTunnel',T['placements'],T['eye_selection'])]:
    selected={p['name'] for p in frames if p['has_eye']}
    check(len(selected)=={'Lobby':121,'Game':3,'IntroTunnel':182}[scene],'chosen approx33 percent count')
    check(all(not p['has_picture'] and p['slot_kind']==('watching_eye' if p['has_eye'] else 'empty_picture_slot') for p in frames),'remaining slots genuinely empty')
    for seed in (1,2,3):
        shuffled=copy.deepcopy(frames);random.Random(seed).shuffle(shuffled);again=a.apply_eye_selection(shuffled)
        check({p['name'] for p in shuffled if p['has_eye']}==selected,'selection is independent of serialization order')
        check(again==policy,'policy manifest regenerates exactly')
    for wall,quota in policy['wall_quotas'].items():
        ps=[p for p in frames if p['wall']==wall];ids=[i for i,p in enumerate(ps) if p['has_eye']]
        check(len(ids)==quota,'wall quotas retain spatial coverage')
        check(abs(quota/len(ps)-len(selected)/len(frames))<.025,'per-wall ratio remains balanced')
        expected=sorted(ps,key=lambda p:hashlib.sha256(f'watching-slot:52:{scene}:{wall}:{p["name"]}'.encode()).hexdigest())[:quota]
        check({p['name'] for p in expected}=={p['name'] for p in ps if p['has_eye']},'selection matches independent SHA256 rank')
        if len(ids)>3:
            check(len({i%3 for i in ids})==3,'not an every-third sequential pattern')
            check(len({b-c for c,b in zip(ids,ids[1:])})>2,'irregular spacing in authored order')
    report.append(dict(scene=scene,frames=len(frames),eyes=len(selected),empty_slots=len(frames)-len(selected),percent=100*len(selected)/len(frames),wall_quotas=policy['wall_quotas'],selected_frames=sorted(selected)))
hall=[p for p in G['placements'] if p['scene']=='Game' and p['has_eye']]
check([p['position'][0] for p in hall]==[-13.55,5.5,44.76],'hallway scatters across length and preserves authored flashlight demonstration')
check(sum(r['eyes'] for r in report)==306 and sum(r['empty_slots'] for r in report)==620,'all authored galleries total306 eyes and620 empty slots')
print(json.dumps(dict(passed=checks,policy='Approximately33 percent of frame objects, balanced by wall and hash-scattered; no every-Nth layout. Future picture slots remain disabled.',counts=report,native_unity_verified=False),indent=2))
