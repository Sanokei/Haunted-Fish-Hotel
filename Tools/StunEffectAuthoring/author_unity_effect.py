"""Author the reusable serialized Unity effect and its production-player binding.
No runtime construction; rerunning retains stable GUIDs and does not edit other assets.
"""
from pathlib import Path
import re, uuid, math, json
LAYOUT=json.loads(Path(__file__).with_name('orbit_layout.json').read_text())
ROOT=Path(__file__).resolve().parents[2]; ART=ROOT/'Assets/Art/Effects/PiranhaStun'; PREFAB=ROOT/'Assets/Prefabs/Effects/PiranhaStun.prefab'
def meta(p,importer=None,fileid=None,folder=False):
 m=Path(str(p)+'.meta')
 if m.exists():return re.search(r'^guid: (\w+)',m.read_text(),re.M)[1]
 g=uuid.uuid5(uuid.NAMESPACE_URL,'haunted-fish-hotel/piranha-stun/'+str(p.relative_to(ROOT))).hex
 s='fileFormatVersion: 2\nguid: '+g+'\n'
 if folder:s+='folderAsset: yes\n'
 s+=(importer or 'DefaultImporter')+':\n  externalObjects: {}\n'
 if fileid:s+='  mainObjectFileID: '+str(fileid)+'\n'
 s+='  userData: \n  assetBundleName: \n  assetBundleVariant: \n';m.write_text(s);return g
for p in [ART.parent,ART,PREFAB.parent]:meta(p,folder=True)
fishguid=meta(ART/'PiranhaMini.asset','NativeFormatImporter',4300000)
trailguids={p['mesh']:meta(ART/(p['mesh']+'.asset'),'NativeFormatImporter',4300000) for p in LAYOUT['planes']}
shader=ART/'PiranhaStunUnlit.shader';shader.write_text('''Shader "HauntedFish/Effects/PiranhaStunUnlit"
{
    Properties
    {
        _MainTex ("Original piranha texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        ZWrite On
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Cutoff;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                clip(color.a - _Cutoff);
                return half4(color.rgb, 1);
            }
            ENDHLSL
        }
    }
}
''');shaderguid=meta(shader,'ShaderImporter')
textureguid=re.search(r'guid: (\w+)',(ROOT/'Assets/Art/Fish/Piranha/piranhahigh.png.meta').read_text())[1]
def material(name,color,texture=None):
 p=ART/(name+'.mat');p.write_text('''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: '''+name+'''
  m_Shader: {fileID: 4800000, guid: '''+shaderguid+''', type: 3}
  m_Parent: {fileID: 0}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {}
  disabledShaderPasses: []
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _MainTex:
        m_Texture: '''+('{fileID: 2800000, guid: '+texture+', type: 3}' if texture else '{fileID: 0}')+'''
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    m_Ints: []
    m_Floats:
    - _Cutoff: 0.5
    m_Colors:
    - _Color: {r: '''+str(color[0])+', g: '+str(color[1])+', b: '+str(color[2])+''', a: 1}
  m_BuildTextureStacks: []
''');return meta(p,'NativeFormatImporter',2100000)
fishmat=material('PiranhaMini',(1,1,1),textureguid);trailmat=material('PiranhaCurvedTrails',(.97,.94,.70))
HEADER='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
def vec(v,keys='xyz'):return '{'+', '.join(k+': '+format(float(x),'.8g') for k,x in zip(keys,v))+'}'
def curvekeys(keys):
 return ''.join('      - serializedVersion: 3\n        time: '+format(t,'.8g')+'\n        value: '+vec(v)+'\n        inSlope: '+vec(s)+'\n        outSlope: '+vec(s)+'\n        tangentMode: 0\n        weightedMode: 0\n        inWeight: {x: 0.33333334, y: 0.33333334, z: 0.33333334}\n        outWeight: {x: 0.33333334, y: 0.33333334, z: 0.33333334}\n' for t,v,s in keys)
def curve(path,keys):return '  - curve:\n      serializedVersion: 2\n      m_Curve:\n'+curvekeys(keys)+'      m_PreInfinity: 2\n      m_PostInfinity: 2\n      m_RotationOrder: 4\n    path: '+path+'\n'
clip=ART/'PiranhaStunOrbit.anim'
s=HEADER+'''--- !u!74 &7400000
AnimationClip:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: PiranhaStunOrbit
  serializedVersion: 7
  m_Legacy: 1
  m_Compressed: 0
  m_UseHighQualityCurve: 1
  m_RotationCurves: []
  m_CompressedRotationCurves: []
  m_EulerCurves:
'''
for group in LAYOUT['planes']:
 sign=group['direction'];s+=curve('Orbit/'+group['name']+'/Spin',[(0,(0,0,0),(0,sign*225,0)),(1.6,(0,sign*360,0),(0,sign*225,0))])
s+='  m_PositionCurves:\n'
fish_number=0
for group in LAYOUT['planes']:
 for degrees in group['fish_angles']:
  fish_number+=1;angle=math.radians(degrees);x=group['radius']*math.cos(angle);z=-group['radius']*math.sin(angle);keys=[]
  for j in range(5):
   t=j*.4;theta=t/1.6*2*math.pi+angle;amp=LAYOUT['bob_amplitude_m']
   keys.append((t,(x,amp*math.sin(theta),z),(0,amp*2*math.pi/1.6*math.cos(theta),0)))
  s+=curve('Orbit/'+group['name']+'/Spin/Piranha%02d'%fish_number,keys)
s+='''  m_ScaleCurves: []
  m_FloatCurves: []
  m_PPtrCurves: []
  m_SampleRate: 60
  m_WrapMode: 2
  m_Bounds:
    m_Center: {x: 0, y: 0.22, z: 0}
    m_Extent: {x: 0.65, y: 0.35, z: 0.65}
  m_ClipBindingConstant:
    genericBindings: []
    pptrCurveMapping: []
  m_AnimationClipSettings:
    serializedVersion: 2
    m_AdditiveReferencePoseClip: {fileID: 0}
    m_AdditiveReferencePoseTime: 0
    m_StartTime: 0
    m_StopTime: 1.6
    m_OrientationOffsetY: 0
    m_Level: 0
    m_CycleOffset: 0
    m_HasAdditiveReferencePose: 0
    m_LoopTime: 1
    m_LoopBlend: 0
    m_LoopBlendOrientation: 0
    m_LoopBlendPositionY: 0
    m_LoopBlendPositionXZ: 0
    m_KeepOriginalOrientation: 1
    m_KeepOriginalPositionY: 1
    m_KeepOriginalPositionXZ: 1
    m_HeightFromFeet: 0
    m_Mirror: 0
  m_EditorCurves: []
  m_EulerEditorCurves: []
  m_HasGenericRootTransform: 0
  m_HasMotionFloatCurves: 0
  m_Events: []
''';clip.write_text(s);clipguid=meta(clip,'NativeFormatImporter',7400000)
common='  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
def node(n,name,parent,children,pos=(0,0,0),rot=(0,0,0,1),active=True,renderer=False,animation=False):
 comps=[n+1]+([n+2] if animation else [])+([n+3,n+4] if renderer else [])
 go='--- !u!1 &'+str(n)+'\nGameObject:\n'+common+'  serializedVersion: 6\n  m_Component:\n'+''.join('  - component: {fileID: '+str(c)+'}\n' for c in comps)+'  m_Layer: 0\n  m_Name: '+name+'\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: '+str(int(active))+'\n'
 tf='--- !u!4 &'+str(n+1)+'\nTransform:\n'+common+'  m_GameObject: {fileID: '+str(n)+'}\n  serializedVersion: 2\n  m_LocalRotation: '+vec(rot,'xyzw')+'\n  m_LocalPosition: '+vec(pos)+'\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children:'+(' []\n' if not children else '\n'+''.join('  - {fileID: '+str(c)+'}\n' for c in children))+'  m_Father: {fileID: '+str(parent)+'}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n'
 return go+tf
pref=HEADER+node(1000,'PiranhaStun',0,[1101],active=False,animation=True)
pref+='--- !u!111 &1002\nAnimation:\n'+common+'''  m_GameObject: {fileID: 1000}
  m_Enabled: 1
  serializedVersion: 3
  m_Animation: {fileID: 7400000, guid: '''+clipguid+''', type: 2}
  m_Animations:
  - {fileID: 7400000, guid: '''+clipguid+''', type: 2}
  m_WrapMode: 2
  m_PlayAutomatically: 0
  m_AnimatePhysics: 0
  m_CullingType: 0
'''
pref+=node(1100,'Orbit',1001,[g['id']+1 for g in LAYOUT['planes']],pos=(0,LAYOUT['orbit_lift_m'],0))
base=(ROOT/'Assets/Prefabs/LobbyAtmosphere/LobbySurfaceFinishes.prefab').read_text()
render=re.search(r'--- !u!23 &\d+\n(.*?)(?=--- !u!)',base,re.S)[1]
def renderer_node(n,name,parent,pos,rot,mg,mat):
 out=node(n,name,parent,[],pos,rot,renderer=True)
 out+='--- !u!33 &'+str(n+3)+'\nMeshFilter:\n'+common+'  m_GameObject: {fileID: '+str(n)+'}\n  m_Mesh: {fileID: 4300000, guid: '+mg+', type: 2}\n'
 r=re.sub(r'm_GameObject: \{fileID: \d+\}','m_GameObject: {fileID: '+str(n)+'}',render)
 r=re.sub(r'm_Materials:\n  - \{[^}]+\}','m_Materials:\n  - {fileID: 2100000, guid: '+mat+', type: 2}',r)
 for field,val in [('m_CastShadows',0),('m_ReceiveShadows',0),('m_LightProbeUsage',0),('m_ReflectionProbeUsage',0),('m_DynamicOccludee',0),('m_SortingOrder',5)]:r=re.sub(r'('+field+r': )[^\n]+',lambda m:m[1]+str(val),r)
 return out+'--- !u!23 &'+str(n+4)+'\n'+r
fish_number=0
for group in LAYOUT['planes']:
 n=group['id'];x=math.radians(group['tilt_x'])/2;z=math.radians(group['tilt_z'])/2
 q=(math.sin(x)*math.cos(z),-math.sin(x)*math.sin(z),math.cos(x)*math.sin(z),math.cos(x)*math.cos(z))
 pref+=node(n,group['name'],1101,[n+101],rot=q)
 fishids=[n+200+i*100 for i in range(len(group['fish_angles']))];trailid=n+200+100*len(fishids)
 pref+=node(n+100,'Spin',n+1,[i+1 for i in fishids]+[trailid+1])
 for fishid,degrees in zip(fishids,group['fish_angles']):
  fish_number+=1;a=math.radians(degrees);yaw=a+(math.pi if group['direction']>0 else 0)
  rot=(0,math.sin(yaw/2),0,math.cos(yaw/2));pos=(group['radius']*math.cos(a),LAYOUT['bob_amplitude_m']*math.sin(a),-group['radius']*math.sin(a))
  pref+=renderer_node(fishid,'Piranha%02d'%fish_number,n+101,pos,rot,fishguid,fishmat)
 pref+=renderer_node(trailid,'CurvedTrails',n+101,(0,0,0),(0,0,0,1),trailguids[group['mesh']],trailmat)
PREFAB.write_text(pref);prefguid=meta(PREFAB,'PrefabImporter')
fbx=ART/'PiranhaStunMeshes.fbx'; fbmeta=Path(str(fbx)+'.meta')
if not fbmeta.exists():
 src=(SOURCE_META:=ROOT/'Assets/Art/Fish/Piranha/piranhahigh.blend.meta').read_text();src=re.sub(r'^guid: \w+', 'guid: '+uuid.uuid5(uuid.NAMESPACE_URL,'haunted-fish-hotel/piranha-stun/fbx').hex,src,flags=re.M)
 for field in ['materialImportMode','importAnimation','importCameras','importLights']:
  src=re.sub(r'('+field+r': )\d+',r'\g<1>0',src)
 fbmeta.write_text(src)
# Bind a single nested reusable prefab in the real network player; cinematic preview actors are untouched.
player=ROOT/'Assets/Resources/HotelNetworkPlayer.prefab';p=player.read_text()
if '_StunEffect:' not in p:
 blocks=re.split(r'(?=--- !u!)',p);fishblock=next(b for b in blocks if 'abc3383cc5bc80046b19b7bfdfbfe4a3' in b)
 p=p.replace(fishblock,fishblock.rstrip()+'\n  _StunEffect: {fileID: 883400003}\n  _StunAnimation: {fileID: 883400004}\n')
 transform=next(b for b in blocks if b.startswith('--- !u!4 &21000015\n'))
 p=p.replace(transform,transform.replace('  m_Children:\n','  m_Children:\n  - {fileID: 883400002}\n'))
 for ident in [883400001,883400002,883400003,883400004]:assert not re.search(r'^--- !u!\d+ &'+str(ident)+r'\b',p,re.M)
 p+='--- !u!1001 &883400001\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n  m_Modification:\n    serializedVersion: 3\n    m_TransformParent: {fileID: 21000015}\n    m_Modifications:\n'
 for field,val in [('m_LocalPosition.x',0),('m_LocalPosition.y',12.5),('m_LocalPosition.z',0),('m_LocalRotation.x',math.sin(math.radians(10))),('m_LocalRotation.y',0),('m_LocalRotation.z',0),('m_LocalRotation.w',math.cos(math.radians(10))),('m_LocalScale.x',10),('m_LocalScale.y',10),('m_LocalScale.z',10)]:
  p+='    - target: {fileID: 1001, guid: '+prefguid+', type: 3}\n      propertyPath: '+field+'\n      value: '+format(val,'.8g')+'\n      objectReference: {fileID: 0}\n'
 p+='    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n    m_AddedGameObjects: []\n    m_AddedComponents: []\n  m_SourcePrefab: {fileID: 100100000, guid: '+prefguid+', type: 3}\n'
 for kind,ident,source,name in [(4,883400002,1001,'Transform'),(1,883400003,1000,'GameObject'),(111,883400004,1002,'Animation')]:
  p+='--- !u!'+str(kind)+' &'+str(ident)+' stripped\n'+name+':\n  m_CorrespondingSourceObject: {fileID: '+str(source)+', guid: '+prefguid+', type: 3}\n  m_PrefabInstance: {fileID: 883400001}\n  m_PrefabAsset: {fileID: 0}\n'
 player.write_text(p)
print(json.dumps({'prefab':str(PREFAB.relative_to(ROOT)),'guid':prefguid,'effect_root_ref':883400003,'animation_ref':883400004,'modified_player':str(player.relative_to(ROOT))},indent=2))
