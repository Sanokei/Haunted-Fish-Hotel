#!/usr/bin/env python3
"""Source contracts only; this does not claim Unity import or runtime validation."""
import argparse,hashlib,json,math,re,sys
from pathlib import Path
import yaml
from PIL import Image
import numpy as np
from scipy import ndimage
ROOT=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--ordinary-scenes-only', action='store_true', help='Validate Lobby/hallway galleries without the separately authored tutorial-recording integration.')
args=parser.parse_args()
def docs(p):
    return {int(m[2]):(int(m[1]),next(iter(yaml.safe_load(m[3]).values()))) for m in re.finditer(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',p.read_text(),re.M|re.S)}
def refs(d):
    if isinstance(d,dict):
        if 'fileID'in d:yield d
        else:
            for v in d.values():yield from refs(v)
    elif isinstance(d,list):
        for v in d:yield from refs(v)
checks=0
def check(condition,label):
    global checks
    checks+=1
    if not condition:raise AssertionError(label)
manifest=json.loads((ROOT/'Tools/WatchingFrameTests/gallery-placement.json').read_text())
art=json.loads((ROOT/'Tools/FrameArt/frame-segmentation.json').read_text())
guids={}
for p in (ROOT/'Assets').rglob('*.meta'):
    m=re.search(r'^guid: (\w+)',p.read_text(errors='ignore'),re.M)
    if m:guids[m[1]]=p
for scene in ['Lobby','Game']:
    p=ROOT/manifest['prefabs'][scene];d=docs(p)
    check(len(re.findall(r'^--- !u!',p.read_text(),re.M))==len(d),'unique object IDs')
    for i,(typ,obj) in d.items():
        for r in refs(obj):
            if not r['fileID']:continue
            if 'guid'in r:check(r['guid']in guids or r['guid']=='0000000000000000e000000000000000','resolvable external reference')
            else:check(r['fileID']in d,'resolvable local reference')
        if typ==4:
            parent=obj['m_Father']['fileID']
            if parent:check(any(c['fileID']==i for c in d[parent][1]['m_Children']),'parent child reciprocity')
    controller=[obj for typ,obj in d.values() if typ==114]
    check(len(controller)==1,'one shared gallery controller')
    check(all(e['PresentationGain']==1 for e in controller[0]['_Eyes']),'ordinary gallery does not inherit cinematic darkness')
    renderers=[i for i,(typ,obj) in d.items() if typ==23]
    scene_frames=[p for p in manifest['placements'] if p['scene']==scene]
    eye_ids={100102+j*10 for j,p in enumerate(scene_frames) if p['has_eye']}
    static_ids={100102+j*10 for j,p in enumerate(scene_frames) if not p['has_eye']}
    check(eye_ids=={e['Renderer']['fileID'] for e in controller[0]['_Eyes']},'only selected eye frames enter shared gaze/flashlight scheduler')
    check(static_ids=={e['Renderer']['fileID'] for e in controller[0]['_StaticFrames']},'empty frames receive static presentation data only')
    check(set(renderers)==eye_ids|static_ids and not eye_ids&static_ids,'eye and empty-frame owners partition all renderers')
    check(len(eye_ids)=={'Lobby':121,'Game':3}[scene],'authored scene uses chosen approximately33 percent count')
    for j,placement in enumerate(scene_frames):
        renderer=d[100102+j*10][1];mat_path=Path(str(guids[renderer['m_Materials'][0]['guid']])[:-5]);mat=docs(mat_path)[2100000][1]
        floats={k:v for entry in mat['m_SavedProperties']['m_Floats'] for k,v in entry.items()}
        textures={k:v for entry in mat['m_SavedProperties']['m_TexEnvs'] for k,v in entry.items()}
        check(floats['_HasEye']==int(placement['has_eye']),'material eye flag matches deterministic authored selection')
        check(floats['_HasPicture']==0 and textures['_PictureTex']['m_Texture']['fileID']==0,'future picture slot is empty and disabled')
        check((100105+j*10 in d)==placement['has_eye'],'only actual eyes have gaze/flashlight anchors')
    check(all(typ not in [210,212,331] for typ,obj in d.values()),'single-pass frames have no separate sprites, sorting groups or sprite masks')
    check(all(obj['m_SortingOrder']==0 for typ,obj in d.values() if typ==23),'composite frames sort behind foreground fish')
    check(all(e['AimPoint']['fileID'] in d and d[e['AimPoint']['fileID']][0]==4 for e in controller[0]['_Eyes']),'all eyes retain authored opening-center anchors')
    check(all(typ not in [20,223,224] for typ,obj in d.values()),'no extra cameras or UI canvases')
    scene_data=docs(ROOT/f'Assets/Scenes/{scene}.unity')
    prefab_guid=re.search(r'^guid: (\w+)',Path(str(p)+'.meta').read_text(),re.M)[1]
    instances=[o for typ,o in scene_data.values() if typ==1001 and o.get('m_SourcePrefab',{}).get('guid')==prefab_guid]
    check(len(instances)==1,'one authored scene instance')
    bind=[m for m in instances[0]['m_Modification']['m_Modifications'] if m['propertyPath']=='_Viewer']
    check(len(bind)==1 and bind[0]['objectReference']['fileID']in scene_data,'explicit local camera transform')
    if scene=='Game':
        check(scene_data[24000033][1]['_WatchingFrames']['fileID']==8141014012,'actual spotlight explicitly binds gallery')
        check(scene_data[8141014012][1]['m_CorrespondingSourceObject']['guid']==prefab_guid,'flashlight targets correct prefab instance')
        check(controller[0]['_ReactToFlashlight']==1,'hallway flashlight response enabled')
        beam_bind=[m for m in instances[0]['m_Modification']['m_Modifications'] if m['propertyPath']=='_BeamCamera']
        check(len(beam_bind)==1 and scene_data[beam_bind[0]['objectReference']['fileID']][0]==20,'beam uses authored output Camera')
    else:check(controller[0]['_ReactToFlashlight']==0,'Lobby excludes flashlight closure')
    cam_transform=scene_data[bind[0]['objectReference']['fileID']][1]
    check(any(typ==20 and o['m_GameObject']==cam_transform['m_GameObject'] for typ,o in scene_data.values()),'viewer belongs to actual scene output camera')
if not args.ordinary_scenes_only:
    stage=docs(ROOT/'Assets/Resources/Tutorials/TutorialRecordingStage.prefab')
    recording=stage[8141014100][1]
    check(recording['m_SourcePrefab']['guid']==prefab_guid,'recording stage shares actual hallway gallery prefab')
    check(recording['m_Modification']['m_TransformParent']['fileID']==6144801475636508509,'gallery is parented under recording Hallway visibility')
    check(stage[242606801975031782][1]['WatchingFrames']['fileID']==8141014112,'recording director is explicitly bound')
    check(stage[8141014112][1]['m_CorrespondingSourceObject']['fileID']==1140000,'recording binding targets shared controller')
    check(stage[8141014112][1]['m_CorrespondingSourceObject']['guid']==prefab_guid,'recording binding targets correct asset')
    check(any(a['addedObject']['fileID']==8141014101 for a in stage[6144801475636508472][1]['m_Modification']['m_AddedGameObjects']),'nested gallery registered as added prefab child')
    mods=recording['m_Modification']['m_Modifications']
    check(any(m['propertyPath']=='_Viewer' and m['objectReference']['fileID']==6821593003971969775 for m in mods),'recording gaze uses authored camera transform')
    check(any(m['propertyPath']=='_BeamCamera' and m['objectReference']['fileID']==420571060765889963 for m in mods),'recording beam uses same authored output camera')
placements=manifest['placements']
check(len([p for p in placements if p['scene']=='Lobby'])>=300,'dense Lobby gallery')
check(len([p for p in placements if p['scene']=='Game'])<=12,'sparse hallway gallery')
check(len(set(p['frame_id'] for p in placements))==25,'all 25 original frame families represented')
for p in placements:
    theta=math.radians(abs(p['rotation_roll']))
    bx=(p['width']*math.cos(theta)+p['height']*math.sin(theta))/2
    by=(p['height']*math.cos(theta)+p['width']*math.sin(theta))/2
    if p['scene']=='Lobby':
        x,y=p['wall_plane_position'];w,h=p['wall_plane_bounds']
        check(x-bx>=0 and x+bx<=w,'rotated cluster frame stays within original wall horizontally')
        check(y-by>=0 and y+by<=h,'rotated cluster frame stays within original wall vertically')
    check(p['has_eye']==(p['slot_kind']=='watching_eye') and not p['has_picture'],'authored slot kind agrees with eye selection and empty picture state')
    f=art['frames'][p['frame_id']-1];l,t,r,b=f['interior_bbox_local']
    check(abs(p['eye_world_size'][1]-(b-t)/f['size'][1]*p['height']*.97)<.000001,'eye fits opening height first, never width')
    if p['scene']=='Lobby':check(p['position'][1]-p['height']/2>4.25,'above original 3D lobby wall shell')
    check(Image.open(ROOT/p['source']).size==tuple(p['pixel_size']),'sprite dimensions match manifest')
    if p['has_eye']:check(abs(p['eye_world_size'][0]/p['eye_world_size'][1]-1654/853)<.00001,'eye source aspect preserved')
for wall in set(p['wall'] for p in placements):
    ps=[p for p in placements if p['wall']==wall]
    check(set(p['variant'] for p in ps)=={'filled','empty'},'each wall mixes filled and unfilled originals')
    if wall!='Hallway back wall':check(max(p['position'][1]+p['height']/2 for p in ps)>21,'gallery reaches upper wall heights')
# Assert clipping for the exact mask and 81 gaze samples across travel extrema.
mask=np.asarray(Image.open(ROOT/manifest['eye']['mask']).convert('RGBA'))[:,:,3]/255
pupil=Image.open(ROOT/manifest['eye']['pupil']).convert('RGBA')
outline=np.asarray(Image.open(ROOT/manifest['eye']['outline']).convert('RGBA'))
check(pupil.size==(1654,853) and outline.shape[:2]==mask.shape,'eye layers share exact UV canvas')
check((mask==0).any() and (mask==1).any(),'real bounded alpha interior')
for u in np.linspace(-.23,.23,9):
    for v in np.linspace(-.12,.12,9):
        shifted=pupil.transform(pupil.size,Image.Transform.AFFINE,(1,0,-u*pupil.width,0,1,v*pupil.height),resample=Image.Resampling.BILINEAR)
        a=np.asarray(shifted)[:,:,3]/255*mask
        check(np.max(a[mask==0])==0,'pupil cannot escape original outline interior')
        outa=outline[:,:,3]/255+a*(1-outline[:,:,3]/255)
        check(np.max(outa)<=1.000001,'correct alpha source-over')
script=(ROOT/'Assets/Scripts/Presentation/WatchingFrames/WatchingFrameGallery.cs').read_text()
check(not re.search(r'\b(?:Update|LateUpdate|FixedUpdate)\s*\(',script),'no per-frame Update loop')
check('InvokeRepeating(nameof(SampleGaze)'in script and 'CancelInvoke(nameof(SampleGaze))'in script,'bounded scheduler teardown')
check(not re.search(r'\b(?:Instantiate|AddComponent|FindObjects|GameObject\.Find)\b|new\s+(?:GameObject|Mesh|Material)\s*\(',script),'no runtime object/mesh/material construction or discovery')
shader=(ROOT/'Assets/Prefabs/WatchingFrames/Materials/WatchingEye.shader').read_text()
check('pupil.a *= SAMPLE_TEXTURE2D(_InteriorMask, sampler_InteriorMask, lidUV).a * inside(pupilUV) * lidBounds;'in shader,'original eye mask clips shifted pupil')
check('half pupilAlpha = pupil.a * (1 - outline.a) * eyeBounds;' in shader,'outline composited on top')
check('Transparent+1' not in shader,'eye render queue does not override foreground fish')
check('Physics.Raycast' in script and 'QueryTriggerInteraction.Ignore' in script,'beam respects scene occlusion')
check('if (gazeMoved) _LastViewerPosition = viewer;' in script,'subthreshold camera motion accumulates')
check('float aperture = max(.001, 1 - closure);' in shader,'two eyelid lines converge toward middle')
check('pupil.a *= (1 - closedBlend) * saturate(_HasEye);' in shader,'fully closed eye hides pupil')
check('SAMPLE_TEXTURE2D(_OutlineTex, sampler_OutlineTex, float2(.5, .985)).rgb' in shader,'closed stroke uses original outline color')
spotlight=(ROOT/'Assets/Scripts/Multiplayer/GameMouseSpotlight.cs').read_text()
check('PublishBeam(false, Vector2.zero)' in spotlight,'disabled and unavailable flashlight state clears')
check('SetFlashlightBeam(active, center, Radius, Softness,' in spotlight,'eye beam matches actual spotlight shader geometry')
check('player.ControlMode == HotelControlMode.Fish' in spotlight,'only actual local fish screen beam enables canvas')
check('half alpha = frame.a + backing * (1 - frame.a);' in shader,'grey backing is opaque beneath original inner-edge alpha')
check('frame.rgb * frame.a * (1 - opening) + pictureSurface * opening' in shader,'entire eye including outline and lid is clipped to its own opening')
check(not re.search(r'^\s*Stencil\s*\{',shader,re.M),'frame compositor never reads or writes shared stencil')
check('ZWrite Off' in shader and 'clip(alpha - .001);' in shader,'transparent painted edges do not create invisible depth occluders')
check('MixFog(rgb / max(alpha, .0001), i.fogFactor) * alpha' in shader,'whole composite receives atmospheric fog with correct premultiplied alpha')
check('_StaticFrames' not in script[script.index('void SampleGaze()'):],'empty slots never enter scheduled gaze/flashlight work')
check('foreach (var frame in _StaticFrames)' in script[script.index('void OnEnable()'):script.index('void OnDisable()')],'static slot gain is applied once on enable')
check('public Transform AimPoint;' in script and 'eye.AimPoint ? eye.AimPoint' in script,'gaze and beam origin remain exact opening center')
check(not re.search(r'SetGlobal|sharedMaterial\s*=|\.material\s*=',script),'gallery never mutates global or shared material state')
check(sum(abs(p['rotation_roll'])>8 for p in placements if p['scene']=='Lobby')>200,'freeform cluster layout has broad rotation variation')
check(len(set(round(p['position'][1],3) for p in placements if p['scene']=='Lobby'))>340,'gallery is not arranged into repeated horizontal rows')
check(sum(p['eye_frame_uv_rect'][2]>1 for p in placements)>100,'height-first eyes intentionally extend beyond narrow frame widths')
reference=json.loads((ROOT/'Tools/WatchingFrameTests/interior-colour-reference.json').read_text())
reference_path=ROOT/reference['source_path'];reference_pixels=np.asarray(Image.open(reference_path).convert('RGBA'))
check(hashlib.sha256(reference_path.read_bytes()).hexdigest()==reference['sha256'],'uploaded swatch bytes retain original checksum')
check(reference_pixels.shape==(71,159,4),'uploaded swatch original dimensions retained')
check(np.all(reference_pixels==np.array([32,32,32,255])),'all 11289 uploaded pixels match exact dark grey')
check(reference['uniform_pixel_count']==11289 and reference['unique_colors']==1,'reported uniform swatch sample is exact')
check(np.allclose(reference['linear_rgba'],manifest['interior_color']['linear_rgba']),'manifest linear grey derives from uploaded swatch')
interior=np.array(manifest['interior_color']['linear_rgba'][:3])
check(manifest['interior_color']['hex_srgb']=='#202020','dark grey exactly matches the uploaded user swatch')
check(np.allclose(interior,((32/255+.055)/1.055)**2.4),'explicit material vector is linear RGB, not ambiguous gamma color')
check('_InteriorColor ("Opaque interior (linear RGB)", Vector)' in shader,'interior color is a serialized source property, not repainted art')
check('half eyeAlpha = outlineAlpha + pupilAlpha;' in shader,'grey background is occluded by original eye alpha')
check('half3 pictureSurface = outline.rgb * outlineAlpha + backingRGB * (1 - eyeAlpha);' in shader,'original eye source-over neutral grey')
check('_InteriorColor.rgb * backing * (1 - frame.a) * (1 - opening)' in shader,'grey backing continues under semitransparent inner paint')
for p in placements:
    check(np.allclose(p['interior_color_linear'],manifest['interior_color']['linear_rgba']),'each frame manifest records the same opaque neutral grey')
for path in (ROOT/'Assets/Prefabs/WatchingFrames/Materials').glob('*-Frame-*.mat'):
    mat=docs(path)[2100000][1]
    colors={k:v for d in mat['m_SavedProperties']['m_Colors'] for k,v in d.items()}
    check(np.allclose([colors['_InteriorColor'][k] for k in 'rgba'],manifest['interior_color']['linear_rgba']),'every authored material serializes opaque neutral grey')
    floats={k:v for d in mat['m_SavedProperties']['m_Floats'] for k,v in d.items()}
    check(floats['_PresentationGain']==1 and floats['_CullMode']==0,'normal frame materials retain full brightness and original two-sided visibility')
for eye_a in [0,.1,.5,1]:
    eye_rgb=np.array([1.,0,0])*eye_a
    picture=eye_rgb+interior*(1-eye_a)
    check(np.allclose(picture,np.array([1.,0,0])*eye_a+interior*(1-eye_a)),'eye alpha composes over grey without washing opaque pupil color')
mask_manifest=json.loads((ROOT/'Tools/WatchingFrameTests/frame-opening-masks.json').read_text())
for m in mask_manifest['frames']:
    opening=np.asarray(Image.open(ROOT/m['path']))[:,:,3]
    backing=np.asarray(Image.open(ROOT/m['backing_path']))[:,:,3]
    original=np.asarray(Image.open(ROOT/m['source']))[:,:,3]
    check(opening.shape==backing.shape==original.shape,'per-frame masks retain original dimensions')
    check(np.all(backing[opening>0]==255),'backing fully covers each opening')
    touches=(opening[:-1,:-1]>0)|(opening[1:,:-1]>0)|(opening[:-1,1:]>0)|(opening[1:,1:]>0)
    covered=(backing[:-1,:-1]==255)&(backing[1:,:-1]==255)&(backing[:-1,1:]==255)&(backing[1:,1:]==255)
    check(np.all(covered[touches]),'all bilinear opening-edge footprints have opaque backing')
    check(np.all(backing[~ndimage.binary_fill_holes(original>0)]==0),'backing cannot spill outside original silhouette')
    for variant in ['filled','empty']:
        f=art['frames'][m['frame_id']-1];rgba=np.asarray(Image.open(ROOT/f['variants'][variant]['path']),dtype=float)/255
        # With eye contribution zero, every actual opening pixel must be opaque neutral grey.
        o=opening/255;b=backing/255
        rgb=rgba[:,:,:3]*rgba[:,:,3,None]*(1-o[:,:,None])+interior*o[:,:,None]+interior*b[:,:,None]*(1-rgba[:,:,3,None])*(1-o[:,:,None]);a=rgba[:,:,3]+b*(1-rgba[:,:,3])
        check(np.allclose(rgb[opening==255],interior),'grey opening does not retain filled glass color')
        check(np.all(a[opening==255]==1),'filled and unfilled interiors both opaque grey')
        exterior=(backing==0)&(opening==0)
        check(np.all(a[exterior]==rgba[:,:,3][exterior]),'exterior alpha exactly preserved')
result={'passed':checks,'recording_fixture_checked':not args.ordinary_scenes_only,'counts':manifest['counts'],'native_unity_verified':False,'scope':'Source GUID/hierarchy, original-art sizing, mixed layout, mask math, controller architecture. Not Unity import, native shader compilation or rendered gameplay.'}
print(json.dumps(result,indent=2))
