#!/usr/bin/env python3
"""Production-source/alpha-math regression checks, not native GPU rendering."""
from pathlib import Path
import json,re,hashlib,math
import yaml
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[2];checks=0
def check(value,msg):
 global checks
 checks+=1
 if not value:raise AssertionError(msg)
def docs(p):
 s=(ROOT/p).read_text();matches=list(re.finditer(r'^--- !u!(\d+) &(-?\d+).*?\n(.*?)(?=^--- !u!|\Z)',s,re.M|re.S))
 d={int(m[2]):(int(m[1]),next(iter(yaml.safe_load(m[3]).values()))) for m in matches}
 check(len(d)==len(matches),'unique object IDs '+p);return d
def floats(p):return {k:v for e in docs(p)[2100000][1]['m_SavedProperties']['m_Floats'] for k,v in e.items()}
base=ROOT/'Assets/Art/WorldLighting';lighting=(base/'ArtLighting.hlsl').read_text();sprite=(base/'ArtSpritePass.hlsl').read_text()
for token in ['GetMainLight(TransformWorldToShadowCoord(positionWS), positionWS','GetAdditionalLight(lightIndex, positionWS, half4(1,1,1,1))','source.shadowAttenuation','LIGHT_LOOP_BEGIN(lightCount)','URP_FP_DIRECTIONAL_LIGHTS_COUNT','#define _SURFACE_TYPE_TRANSPARENT 1','abs(dot(normalWS, source.direction))']:
 check(token in lighting,token)
for token in ['UNITY_SKINNED_VERTEX_COMPUTE(input)','UnityFlipSprite(input.positionOS, unity_SpriteProps.xy)','input.color * _Color * unity_SpriteColor','ArtSpriteSample(input.uv, input.color, input.rootLocalY).a','clip(alpha - _ShadowCutoff)','Varyings output = ArtSpriteVertex(input);','_CASTING_PUNCTUAL_LIGHT_SHADOW','normalize(_LightPosition - output.positionWS)','ApplyShadowBias','ApplyShadowClamping','texel.a *= smoothstep']:
 check(token in sprite,token)
for p in ['Assets/Art/Hallway/HallwaySpriteLit.shader','Assets/Art/Materials/LobbyRevision/LobbySpriteFog.shader','Assets/Art/WorldLighting/WorldSpriteLit.shader','Assets/Art/BossFight/BossArmVoidFade.shader']:
 s=(ROOT/p).read_text()
 for token in ['"LightMode"="ShadowCaster"','"LightMode"="UniversalForward"','_MAIN_LIGHT_SHADOWS_CASCADE','_ADDITIONAL_LIGHT_SHADOWS','_CLUSTER_LIGHT_LOOP','_CASTING_PUNCTUAL_LIGHT_SHADOW','ColorMask 0','Cull Off','ZWrite Off','ZWrite On']:
  check(token in s,p+' '+token)
 check('UsePass' not in s,'no rectangular borrowed opaque caster '+p)
 if 'BossArm' in p:check(s.count('#define ART_ARM_FADE 1')==2,'arm root fade applies to color and silhouette')
# Original alpha determines casting, including all transparent islands/holes, flip and tint.
sources=['Assets/Art/HotelFish/FISH-right.png','Assets/Art/HotelFish/FISH-tail.png','Assets/Art/Hallway/HALLWAY-sconce.png','Assets/Art/Traps/TRAP-Hotel-cart.png','Assets/Art/Traps/TRAP-Angler-fish.png','Assets/Art/Traps/TRAP-Chandelier-v3.png']
alpha_evidence=[]
for p in sources:
 alpha=np.asarray(Image.open(ROOT/p).convert('RGBA'))[:,:,3]/255.0;cast=alpha>=.2
 check(cast.any() and (~cast).any(),'nonrectangular original silhouette '+p)
 check(not cast[alpha==0].any(),'transparent canvas/holes cannot cast '+p)
 for axis in [0,1]:check(np.array_equal(np.flip(alpha,axis)>=.2,np.flip(cast,axis)),'flipped silhouette parity '+p)
 check(not (alpha*0>=.2).any(),'invisible sprite cannot cast '+p)
 alpha_evidence.append({'source':p,'sha256':hashlib.sha256((ROOT/p).read_bytes()).hexdigest(),'opaque_fraction':float(np.mean(cast)),'fully_transparent_pixels':int(np.sum(alpha==0))})
# Frame depth pass samples original paint + backing only, never eye/pupil or stencil.
eye=(ROOT/'Assets/Prefabs/WatchingFrames/Materials/WatchingEye.shader').read_text();shadow=eye[eye.index('Name "ShadowCaster"'):]
check('_PupilTex' not in shadow and '_PupilOffset' in shadow,'pupil parameter layout retained, but pupil not sampled by caster')
check('clip(paint + backing * (1 - paint) - _ShadowCutoff)' in shadow,'frame caster uses original exterior-alpha/backing union')
check('Stencil' not in re.sub(r'//[^\n]*','',eye),'no shared stencil interference')
check('half3 pupilRGB = pupil.rgb * pupilAlpha * saturate(_PupilIntensity);' in eye,'authored pupil display balance remains independent of light/shadow')
check('half3 rgb = litSurface + pupilRGB * opening;' in eye,'pupil is added after non-pupil lighting and before shared atmospheric attenuation')
check('rgb = MixFog(rgb / max(alpha, .0001), i.fogFactor) * alpha;' in eye,'pupils share scene fog after unlit compositing')
check('rgb *= max(heightVisibility, nearVisibility);' in eye,'nearby painted frame contour remains faintly readable while all distant art fades')
check('rgb * saturate(_PresentationGain)' in eye,'cinematic presentation gain remains independent of lights')
check('SAMPLE_TEXTURE2D(_InteriorMask, sampler_InteriorMask, lidUV)' in eye,'exact gaze/closure mask retained')
for opening in [0,.3,1]:
 for pupil_a in [0,.2,1]:
  pupil=np.array([.9,.025,.03])*pupil_a*opening
  for gain in [0,.4,1]:
   # Pupil RGB contribution is independent of light/shadow while cinematic depth gain is not.
   dark_pupil=pupil*gain;bright_pupil=pupil*gain
   check(np.array_equal(dark_pupil,bright_pupil),'unlit pupil invariance')
# The stronger intro paint visibility is balanced by its pupil-only display gain, never shadow response.
for distance in [0,9.6,14.8,20,25,31]:
 t=max(0,min(1,(distance-9.6)/(32-9.6)));falloff=1-t*t*(3-2*t)
 old_pupil=max(.012,.1*falloff);new_pupil=max(.012,.8*falloff)*.125
 check(new_pupil<=old_pupil+1e-12,'intro near-pupil energy never exceeds prior restrained profile')
 check(max(.012,.8*falloff)>=max(.012,.1*falloff),'near paint visibility raised without changing far walls')
check('Range(0,1)) = 1' in eye.split('_PupilIntensity')[1].split('\n')[0],'normal original pupils retain full display gain by default')
frame_masks=json.loads((ROOT/'Tools/WatchingFrameTests/frame-opening-masks.json').read_text())
for entry in frame_masks['frames']:
 original=np.asarray(Image.open(ROOT/entry['source']).convert('RGBA'))[:,:,3]/255.0
 backing=np.asarray(Image.open(ROOT/entry['backing_path']).convert('RGBA'))[:,:,3]/255.0
 union=original+backing*(1-original)
 check(not (union[(original==0)&(backing==0)]>=.2).any(),'original frame exterior stays transparent')
 check(np.all(union[backing==1]==1),'opaque grey opening casts as frame backing, independent of pupil')
# Every migrated renderer still carries serialized opt-in; nonphysical overlays remain exempt.
manifest=json.loads((ROOT/'Tools/WorldLightingTests/authoring-manifest.json').read_text());cache={}
for path,fid in manifest['renderer_updates']:
 d=cache.setdefault(path,docs(path)) if path not in cache else cache[path]
 kind,obj=d[int(fid)]
 check(obj['m_CastShadows']==2 and obj['m_ReceiveShadows']==1,'physical shadow flags '+path+':'+str(fid))
# Bounded authored lights, mobile transparent shadow gate, and atmospheric lining.
lobby=docs('Assets/Scenes/Lobby.unity');hall=docs('Assets/Resources/GameHallway.prefab');sconce=docs('Assets/Resources/HallwaySconce.prefab')
check(all(d['m_Shadows']['m_Type']==0 for k,d in sconce.values() if k==108),'all sixty inherited sconce point lights remain shadowless')
check(hall[981003][1]['m_Type']==1 and hall[981003][1]['m_Shadows']['m_Type']==2,'one inherited hallway directional shadow key')
check(len([d for k,d in hall.values() if k==108])==1,'no per-sprite or sixty-light shadow explosion')
for fid in [1998747292,981011,981021]:check(lobby[fid][1]['m_UsePipelineSettings']==0,'authored Lobby light bias is effective '+str(fid))
check(hall[981004][1]['m_UsePipelineSettings']==0,'authored Hallway light bias is effective')
for fid in [981011,981021]:check(lobby[fid][1]['m_AdditionalLightsShadowResolutionTier']==1,'bounded512px point-shadow tier')
for fid in [1555505994,1060684691,1998747293]:check(lobby[fid][1]['m_Shadows']['m_Type']==2,'bounded lobby source enabled '+str(fid))
check(len([d for k,d in lobby.values() if k==108 and d.get('m_Type')==2 and d['m_Shadows']['m_Type']>0])==2,'exactly two lobby point-light shadow maps')
boss=docs('Assets/Scenes/BossFight.unity');theatre=docs('Assets/Scenes/theatre.unity')
for scene,light,data,tier in [(boss,672667459,981041,2),(theatre,1297839878,981031,1)]:
 check(scene[data][1]['m_UsePipelineSettings']==0,'existing scene key honors explicit authored bias')
 check(scene[data][1]['m_AdditionalLightsShadowResolutionTier']==tier,'bounded existing scene key shadow resolution')
 check(scene[data][1]['m_GameObject']==scene[light][1]['m_GameObject'],'shadow settings belong to exact existing key')
 check(len([d for k,d in scene.values() if k==108 and d.get('m_Shadows',{}).get('m_Type',0)>0])==1,'exactly one existing shadow key in scene')
check(theatre[1297839878][1]['m_Shadows']['m_Type']==2,'theatre physical cards can cast via existing spotlight')
check(boss[672667459][1]['m_Shadows']['m_Type']==1,'Boss original hard shadow character retained')
check(lobby[330585545][1]['m_ClearFlags']==2 and lobby[330585545][1]['m_BackGroundColor']['r']==0,'no visible skybox through staircase gap')
for name in ['PC_Renderer','Mobile_Renderer']:check('m_ShadowTransparentReceive: 1' in (ROOT/f'Assets/Settings/{name}.asset').read_text(),'transparent reception enabled '+name)
check('m_AdditionalLightShadowsSupported: 0' in (ROOT/'Assets/Settings/Mobile_RPAsset.asset').read_text(),'mobile punctual shadow budget unchanged')
# Intro-only visibility uses the actual upper-room viewing distances; gameplay profile stays unchanged.
for mat in (ROOT/'Assets/Prefabs/WatchingFrames/IntroTunnel/Materials').glob('*Frame-*.mat'):
 check(floats(str(mat.relative_to(ROOT)))['_NearArtDistance']==32,'intro-only32m visibility profile '+mat.name)
 check(floats(str(mat.relative_to(ROOT)))['_NearArtVisibility']==.8,'intro-only near painted frame visibility '+mat.name)
 check(floats(str(mat.relative_to(ROOT)))['_PupilIntensity']==.125,'intro-only restrained pupil energy '+mat.name)
 check(floats(str(mat.relative_to(ROOT)))['_Ambient']==.8,'intro-only painted rim readability '+mat.name)
for mat in (ROOT/'Assets/Prefabs/WatchingFrames/Materials').glob('*-Frame-*.mat'):
 for key in ['_NearArtDistance','_NearArtVisibility','_PupilIntensity']:
  check(key not in floats(str(mat.relative_to(ROOT))),'normal galleries keep ordinary shader default '+key+' '+mat.name)
f=floats('Assets/Art/WorldLighting/LobbyUpperRoomAtmosphere.mat')
def height(y):
 t=max(0,min(1,(y-f['_DarknessStart'])/(f['_DarknessEnd']-f['_DarknessStart'])));return 1+(f['_DarknessFloor']-1)*t*t*(3-2*t)
for y in [0,2,4,4.25]:check(height(y)==1,'playable height retains local illumination')
for y in [12,21.9,80]:check(math.isclose(height(y),.012,abs_tol=1e-9),'upper walls disappear into atmosphere')
back=docs('Assets/Prefabs/LobbyAtmosphere/LobbyDepthDarkness.prefab')
check(len([d for k,d in back.values() if k==23])==3,'three saved staircase backstops')
check(all(d['m_CastShadows']==0 for k,d in back.values() if k==23),'black backstops do not shadow playable room')
check(not any(k in [65,64,114] for k,d in back.values()),'no runtime components or movement-blocking colliders')
# Check each referenced installed URP method against the actual editor package source where available.
urp=ROOT.parent/'unity-cloud-qa/editor-6000.3.6f1/Editor/Data/Resources/PackageManager/BuiltInPackages/com.unity.render-pipelines.universal'
if urp.exists():
 h='\n'.join((urp/'ShaderLibrary'/n).read_text() for n in ['Core.hlsl','Shadows.hlsl','RealtimeLights.hlsl'])
 for token in ['ApplyShadowBias','ApplyShadowClamping','GetMainLight','GetAdditionalLight','LIGHT_LOOP_BEGIN']:
  check(token in h,'installed URP 17.3 symbol '+token)
report={'passed':checks,'physical_renderer_blocks':len(manifest['renderer_updates']),'alpha_evidence':alpha_evidence,'native_unity_verified':False,'scope':'Source contracts and CPU alpha/composition checks; not GPU shader import, rendered gameplay or performance.'}
(ROOT/'Tools/WorldLightingTests/source-validation.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps({k:v for k,v in report.items() if k!='alpha_evidence'},indent=2))
