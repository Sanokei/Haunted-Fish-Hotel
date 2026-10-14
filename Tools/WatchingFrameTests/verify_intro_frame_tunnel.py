#!/usr/bin/env python3
"""Source, geometry and visibility contracts; not native Unity shader/render validation."""
import json,math,re,sys
from pathlib import Path
import numpy as np,yaml
from scipy.spatial.transform import Rotation
ROOT=Path(__file__).resolve().parents[2]
M=json.loads((ROOT/'Tools/WatchingFrameTests/intro-tunnel-placement.json').read_text())
def docs(path):
    return {int(x[2]):(int(x[1]),next(iter(yaml.safe_load(x[3]).values()))) for x in re.finditer(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',path.read_text(),re.M|re.S)}
def refs(x):
    if isinstance(x,dict):
        if 'fileID' in x:yield x
        else:
            for v in x.values():yield from refs(v)
    elif isinstance(x,list):
        for v in x:yield from refs(v)
checks=0
def check(x,label):
    global checks;checks+=1
    if not x:raise AssertionError(label)
d=docs(ROOT/M['prefab']);root=d[100000][1];controller=d[1140000][1]
check(root['m_IsActive']==0,'intro-only root is authored inactive')
check(controller['_ReactToFlashlight']==0,'tunnel is camera-gaze only')
check(controller['_SamplesPerSecond']==20,'one shared scheduled owner, no per-eye updates')
check(len(controller['_Eyes'])==M['counts']['eyes']==182 and M['counts']['frames']==553,'182 of553 extension frames have eyes')
check(len(controller['_StaticFrames'])==M['counts']['empty_slots']==371,'remaining371 slots have static presentation only')
check(len([1 for t,o in d.values() if t==114])==1,'one controller for all tunnel eyes')
check(all(t not in (20,210,212,223,224,331) for t,o in d.values()),'no runtime camera/UI/sprite masking stack')
guids={re.search(r'^guid: (\w+)',p.read_text(errors='ignore'),re.M)[1] for p in (ROOT/'Assets').rglob('*.meta') if re.search(r'^guid: (\w+)',p.read_text(errors='ignore'),re.M)}
for ident,(typ,obj) in d.items():
    for r in refs(obj):
        if not r['fileID']:continue
        check((r.get('guid') in guids or r.get('guid')=='0000000000000000e000000000000000') if 'guid' in r else r['fileID'] in d,'all prefab references resolve')
    if typ==4 and obj['m_Father']['fileID']:
        check(any(c['fileID']==ident for c in d[obj['m_Father']['fileID']][1]['m_Children']),'parent-child reciprocity')
check(set(p['wall'] for p in M['placements'])=={'Left','Right','Back','Front'},'frames cover every inward side')
check(len(set(p['frame_id'] for p in M['placements']))==25,'all original frame families represented')
rotation=Rotation.from_quat(M['root_rotation_quaternion']);origin=np.array(M['root_position'])
normal={w['name']:np.array(w['inward_normal']) for w in M['wall_planes']}
eye_by_renderer={e['Renderer']['fileID']:e for e in controller['_Eyes']}
static_by_renderer={e['Renderer']['fileID']:e for e in controller['_StaticFrames']}
for j,p in enumerate(M['placements']):
    rid=100102+j*10;e=(eye_by_renderer if p['has_eye'] else static_by_renderer)[rid]
    check((rid in eye_by_renderer)==p['has_eye'] and (rid in static_by_renderer)!=p['has_eye'],'only selected frames run gaze and beam updates')
    check((100105+j*10 in d)==p['has_eye'],'empty extension slots have no gaze anchor')
    check(np.allclose(p['position'],rotation.apply(p['local_position'])+origin),'world manifest matches authored root/local transforms')
    face=Rotation.from_quat(p['local_rotation_quaternion']).apply([0,0,-1])
    check(np.dot(face,normal[p['wall']])>.99999,'frame faces inward, never behind its tunnel wall')
    x,y=p['wall_plane_position'];w,h=p['wall_plane_bounds'];t=math.radians(p['rotation_roll']);bx=(abs(math.cos(t))*p['width']+abs(math.sin(t))*p['height'])/2;by=(abs(math.sin(t))*p['width']+abs(math.cos(t))*p['height'])/2
    check(x-bx>=0 and x+bx<=w and y-by>=0 and y+by<=h,'rolled original frame fits its side panel')
    check(M['frame_height_interval'][0]<=p['world_height']<=M['frame_height_interval'][1],'black throat stays free of frame planes')
    check(np.isclose(p['presentation_gain'],math.exp(-max(0,p['world_height']-25)*.10)),'authored depth fade monotonically approaches black')
    check(e['PresentationGain']==p['presentation_gain'],'controller fade matches source manifest')
    check(np.allclose(p['interior_color_linear'],[.014443843596092545]*3+[1]),'exact uploaded #202020 base grey retained')
    check(abs(p['eye_world_size'][0]/p['eye_world_size'][1]-1654/853)<1e-6,'eye retains height-first original aspect')
for wall in normal:
    subset=[p for p in M['placements'] if p['wall']==wall]
    check(len(subset)>=45,'each side is densely filled')
    check(set(p['variant'] for p in subset)=={'filled','empty'},'each side preserves both original artwork variants')
for relative in sorted(set(p['material'] for p in M['placements'])):
    path=ROOT/relative
    mat=docs(path)[2100000][1];fs={k:v for x in mat['m_SavedProperties']['m_Floats'] for k,v in x.items()};cs={k:v for x in mat['m_SavedProperties']['m_Colors'] for k,v in x.items()}
    expected=next(p['has_eye'] for p in M['placements'] if p['material']==relative)
    check(fs['_HasEye']==int(expected) and fs['_HasPicture']==0,'extension eye/empty material state matches authored slots')
    check(fs['_CullMode']==2,'tunnel frame art is inward-only backface culled')
    check(np.allclose([cs['_InteriorColor'][k] for k in 'rgba'],M['interior_color']['linear_rgba']),'tunnel material uses exact sampled dark grey')
black=(ROOT/Path(M['prefab']).parent/'Materials/IntroTunnelBlack.shader').read_text()
check('Cull Back ZWrite On' in black and 'half4(0,0,0,1)' in black,'inward opaque black panels and cap conceal the outside')
check(np.allclose(M['root_position'],[0,0,0]) and np.allclose(M['root_rotation_quaternion'],[0,0,0,1]),'extension uses identity root, never a small shifted portal')
matched=[w for w in M['surfaces'] if w['kind']=='matched_wall'];overlays=[w for w in M['surfaces'] if w['kind']=='height_darkness']
check(len(matched)==4 and len(overlays)==4 and len(M['surfaces'])==9,'four continuous walls, four smooth overlays and one cap')
check(M['surfaces'][-1]['position'][1]==80 and M['surfaces'][-1]['inward_normal']==[0,-1,0],'black cap is high above the real room')
check(all(w['vertical_range'][0]>=21.8998 for w in M['surfaces']),'new wall geometry never intrudes below the original upper rim')
scene=docs(ROOT/'Assets/Scenes/Lobby.unity')
for w in matched:
    n=np.array(w['inward_normal']);point=np.array(w['plane_point'])
    for endpoint in w['endpoints']:check(abs(np.dot(np.array(endpoint)-point,n))<1e-7,'footprint edge exactly lies on original plane')
    check(np.dot(Rotation.from_quat(w['rotation_quaternion']).apply([0,0,-1]),n)>.99999,'extension wall front faces the room interior')
    check(w['material']=='Assets/Art/WorldLighting/LobbyUpperRoomAtmosphere.mat','lower seam reuses the same authored upper-room atmospheric finish')
    if w['source_transform']:
        tf=scene[w['source_transform']][1];parent=scene[tf['m_Father']['fileID']][1]
        center=np.array([tf['m_LocalPosition'][k]+parent['m_LocalPosition'][k] for k in 'xyz'])
        q=Rotation.from_quat([tf['m_LocalRotation'][k] for k in 'xyzw']);sign=1 if w['name']=='Left' else -1
        expected_n=q.apply([sign,0,0]);expected_point=center+expected_n*tf['m_LocalScale']['x']/2
        check(np.allclose(n,expected_n,atol=1e-7),'inner normal derived independently from original scene transform')
        check(abs(np.dot(point-expected_point,n))<1e-7,'new inner wall is exactly coplanar with old inner face')
        check(abs(w['vertical_range'][0]-(center[1]+tf['m_LocalScale']['y']/2))<1e-7,'new wall starts at exact old upper edge without gaps or a lip')
for w in overlays:
    check(w['sorting_order']<0,'black wall overlay sorts before frame quads')
    check(w['alpha']=='1-exp(-max(0,worldY-25)*0.10)','wall shade continuously complements frame height gain')
check(all(w['vertical_range'][0]<25 for w in matched),'material seam stays unchanged before the darkness gradient')
footprint=np.array(M['footprint_xz'])
check(np.ptp(footprint[:,0])>15 and np.ptp(footprint[:,2])>11,'extension follows full tapered room footprint rather than narrow shaft')
# Final source camera edge rays must read all four actual polygon walls above the existing rim.
cam=np.array(M['endpoint']['position']);view=Rotation.from_euler('x',M['endpoint']['pitch'],degrees=True);tangent=math.tan(math.radians(M['endpoint']['fov']/2));hit_names=[]
for x,y in [(-tangent*16/9,0),(tangent*16/9,0),(0,-tangent),(0,tangent)]:
    ray=view.apply([x,y,1]);hits=[]
    for w in matched:
        n=np.array(w['inward_normal']);point=np.array(w['plane_point']);den=np.dot(ray,n)
        if abs(den)<1e-10:continue
        distance=np.dot(point-cam,n)/den
        if distance<=0:continue
        hit=cam+distance*ray
        if all(np.dot(hit-np.array(other['plane_point']),np.array(other['inward_normal']))>=-1e-6 for other in matched):hits.append((distance,w,hit))
    check(bool(hits),'endpoint edge ray meets convex room perimeter')
    _,w,hit=min(hits,key=lambda x:x[0]);hit_names.append(w['name'])
    check(w['vertical_range'][0]<hit[1]<M['frame_height_interval'][1],'endpoint edge ray lands on framed extension, not old wall or cap')
check(set(hit_names)=={'Left','Right','Back','Front'},'all four inward sides are visible at the ending')
forward=view.apply([0,0,1]);cap_point=cam+forward*((80-cam[1])/forward[1])
check(all(np.dot(cap_point-np.array(w['plane_point']),np.array(w['inward_normal']))>0 for w in matched),'center ray reaches deep black cap inside full room perimeter')
world=(ROOT/'Assets/Scripts/Multiplayer/Lobby/LobbyIntroductionWorldSequence.cs').read_text()
check('_EndingWallOccluder' not in world and '_EndingGalleryOccluder' not in world,'ordinary walls and gallery are never hidden for the ending')
check('BeginEndingVisibility();' in world[world.index('public IEnumerator Prepare()'):world.index('void EndLine()')],'extension activates during preparation before first world view')
check('BeginEndingVisibility();' not in world[world.index('public IEnumerator Play('):world.index('public IEnumerator Prepare()')],'no timed activation or portal-pop in the camera shot')
shader=(ROOT/'Assets/Prefabs/WatchingFrames/Materials/WatchingEye.shader').read_text()
check('rgb * saturate(_PresentationGain)' in shader,'cinematic depth gain affects RGB, not alpha or masks')
check('Range(0,1)) = 1' in shader and 'Cull [_CullMode]' in shader,'ordinary galleries retain default presentation')
script=(ROOT/'Assets/Scripts/Presentation/WatchingFrames/WatchingFrameGallery.cs').read_text()
check('public float PresentationGain = 1' in script and '_Properties.SetFloat(GainId, Mathf.Clamp01(eye.PresentationGain))' in script,'authored gain is renderer-local in existing shared scheduler')
scene=docs(ROOT/'Assets/Scenes/Lobby.unity');instances=[o for t,o in scene.values() if t==1001 and o.get('m_SourcePrefab',{}).get('guid')==M['prefab_guid']]
if '--require-scene' in sys.argv:check(len(instances)==1,'one explicitly installed intro tunnel scene instance')
if instances:
    check(len(instances)==1,'no duplicate scene installation')
    mods=instances[0]['m_Modification']['m_Modifications']
    check(any(x['propertyPath']=='_Viewer' and x['objectReference']['fileID']==330585546 for x in mods),'intro tunnel gaze explicitly binds output camera')
print(json.dumps(dict(passed=checks,counts=M['counts'],scene_installed=bool(instances),native_unity_verified=False),indent=2))
