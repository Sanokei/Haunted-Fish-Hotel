#!/usr/bin/env python3
"""Author linked world sconces from the original artwork. No runtime construction.
Preserves existing placement transforms and original import GUID. Re-run idempotently.
"""
from pathlib import Path
import re, json, hashlib, copy
import yaml
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'Assets/Art/Hallway'
PREFAB_GUID='c92cabf8c70e5495a0b4db09cb1d5f4e'
SHADER_GUID='91fd72e13a7a5fc38cb2ad3d74a8290a'
MAT_GUID='dfed2841a24c53f898aa784753b43ec0'
SPRITE_GUID='12598d6866ab5d72b14e30d5f55fcaa6'
BASE_SCALE=.162
BULBS=[('Left',466,563),('Center',563,563),('Right',687,563)]

def read(path):
    text=path.read_text(); blocks=[]
    for m in re.finditer(r'^--- !u!(\d+) &(\d+)( stripped)?\n(.*?)(?=^--- !u!|\Z)',text,re.M|re.S):
        blocks.append([int(m[1]),int(m[2]),bool(m[3]),yaml.safe_load(m[4])])
    return blocks

class UnityDumper(yaml.SafeDumper):
    """Unity's canonical inline structs also support existing source graph tools."""
    pass

def unity_mapping(dumper, value):
    keys=set(value)
    inline=('fileID' in keys or (keys and keys <= set('xyzw')) or (keys and keys <= set('rgba')))
    return dumper.represent_mapping('tag:yaml.org,2002:map',value,flow_style=inline)
UnityDumper.add_representer(dict,unity_mapping)

def write(path,blocks):
    text='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
    for kind,id,stripped,obj in blocks:
        text+=f'--- !u!{kind} &{id}'+(' stripped' if stripped else '')+'\n'+yaml.dump(obj,Dumper=UnityDumper,sort_keys=False,width=160)
    text=re.sub(r"(?m)^(\s+value:) '([-+0-9.eE]+)'$",r"\1 \2",text)
    path.write_text(text)

def ref(id):return {'fileID':id}
def ext(id,guid,type=3):return {'fileID':id,'guid':guid,'type':type}
def vec(x,y,z):return {'x':x,'y':y,'z':z}
def header():return {'m_ObjectHideFlags':0,'m_CorrespondingSourceObject':ref(0),'m_PrefabInstance':ref(0),'m_PrefabAsset':ref(0)}
def go(id,name,components):
    return [1,id,False,{'GameObject':dict(header(),serializedVersion=6,m_Component=[{'component':ref(c)} for c in components],m_Layer=0,m_Name=name,m_TagString='Untagged',m_Icon=ref(0),m_NavMeshLayer=0,m_StaticEditorFlags=0,m_IsActive=1)}]
def trans(id,goid,parent,pos=(0,0,0),scale=(1,1,1),children=[]):
    return [4,id,False,{'Transform':dict(header(),m_GameObject=ref(goid),serializedVersion=2,m_LocalRotation=dict(vec(0,0,0),w=1),m_LocalPosition=vec(*pos),m_LocalScale=vec(*scale),m_ConstrainProportionsScale=0,m_Children=[ref(c) for c in children],m_Father=ref(parent),m_LocalEulerAnglesHint=vec(0,0,0))}]

def main():
    hallway=ROOT/'Assets/Resources/GameHallway.prefab'; blocks=read(hallway); data={b[1]:next(iter(b[3].values())) for b in blocks}
    # Preserve one saved renderer layout; on repeated runs use the authored prefab source.
    original=next((n for n in data.values() if n.get('m_Sprite',{}).get('guid')==SPRITE_GUID),None)
    if original is None:original=next(next(iter(b[3].values())) for b in read(ROOT/'Assets/Resources/HallwaySconce.prefab') if b[0]==212)
    render=copy.deepcopy(original);render['m_GameObject']=ref(200);render['m_Materials']=[ext(2100000,MAT_GUID,2)];render['m_Size']={'x':11.73,'y':17.14};render['m_ReceiveShadows']=1;render['m_CastShadows']=2
    prefab=[go(100,'Hallway sconce - original art - three real bulb lights',[101]),trans(101,100,0,children=[201,301,311,321]),go(200,'Original unlit sconce artwork',[201,202]),trans(201,200,101,scale=(BASE_SCALE,)*3),[212,202,False,{'SpriteRenderer':render}]]
    lights=[]
    for i,(name,px,py) in enumerate(BULBS):
        id=300+i*10;pos=((px-1173/2)/100*BASE_SCALE,(1714/2-py)/100*BASE_SCALE,-.22)
        l=dict(header(),m_GameObject=ref(id),m_Enabled=1,serializedVersion=11,m_Type=2,m_Shape=0,m_Color={'r':.38,'g':.68,'b':1,'a':1},m_Intensity=.22,m_Range=2.15,m_SpotAngle=30,m_InnerSpotAngle=21.80208,m_CookieSize=10,m_Shadows={'m_Type':0,'m_Resolution':-1,'m_CustomResolution':-1,'m_Strength':1,'m_Bias':.05,'m_NormalBias':.4,'m_NearPlane':.2},m_Cookie=ref(0),m_DrawHalo=0,m_Flare=ref(0),m_RenderMode=0,m_CullingMask={'serializedVersion':2,'m_Bits':4294967295},m_RenderingLayerMask=1,m_Lightmapping=4,m_LightShadowCasterMode=0,m_AreaSize={'x':1,'y':1},m_BounceIntensity=0,m_ColorTemperature=6500,m_UseColorTemperature=0,m_BoundingSphereOverride={'x':0,'y':0,'z':0,'w':0},m_UseBoundingSphereOverride=0,m_UseViewFrustumForShadowCasterCull=1,m_ShadowRadius=0,m_ShadowAngle=0)
        prefab += [go(id,name+' bulb - realtime point light',[id+1,id+2]),trans(id+1,id,101,pos=pos),[108,id+2,False,{'Light':l}]]
        lights.append({'name':name,'source_pixel_center':[px,py],'local_position':list(pos),'color':[.38,.68,1],'intensity':.22,'range':2.15,'shadows':'None','type':'Point'})
    write(ROOT/'Assets/Resources/HallwaySconce.prefab',prefab)
    (ROOT/'Assets/Resources/HallwaySconce.prefab.meta').write_text(f'fileFormatVersion: 2\nguid: {PREFAB_GUID}\nPrefabImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    (ART/'HallwaySpriteLit.shader.meta').write_text(f'fileFormatVersion: 2\nguid: {SHADER_GUID}\nShaderImporter:\n  externalObjects: {{}}\n  defaultTextures: []\n  nonModifiableTextures: []\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    material={'Material':dict(serializedVersion=8,m_ObjectHideFlags=0,m_CorrespondingSourceObject=ref(0),m_PrefabInstance=ref(0),m_PrefabAsset=ref(0),m_Name='Hallway original art - real point light response',m_Shader=ext(4800000,SHADER_GUID),m_Parent=ref(0),m_ModifiedSerializedProperties=0,m_ValidKeywords=[],m_InvalidKeywords=[],m_LightmapFlags=4,m_EnableInstancingVariants=0,m_DoubleSidedGI=0,m_CustomRenderQueue=-1,stringTagMap={},disabledShaderPasses=[],m_LockedProperties='',m_SavedProperties={'serializedVersion':3,'m_TexEnvs':[{'_MainTex':{'m_Texture':ref(0),'m_Scale':{'x':1,'y':1},'m_Offset':{'x':0,'y':0}}}],'m_Ints':[],'m_Floats':[{'_Ambient':.52},{'_LightResponse':.72},{'_LightLimit':1.1},{'_ShadowCutoff':.2},{'_DarknessStart':10000},{'_DarknessEnd':10001},{'_DarknessFloor':1}],'m_Colors':[{'_Color':{'r':1,'g':1,'b':1,'a':1}}]},m_BuildTextureStacks=[])}
    write(ART/'HallwaySpriteLit.mat',[[21,2100000,False,material]])
    (ART/'HallwaySpriteLit.mat.meta').write_text(f'fileFormatVersion: 2\nguid: {MAT_GUID}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    placements=[];remove=set();inserts={}
    for id,n in data.items():
        if not(n.get('m_Name','').startswith('Panel ') and 'Sconce' in n['m_Name']):continue
        tids=[c['component']['fileID'] for c in n['m_Component']];tid=tids[0];t=data[tid];remove.update([id]+tids)
        iid=900000+id;mods=[{'target':ext(100,PREFAB_GUID),'propertyPath':'m_Name','value':n['m_Name'],'objectReference':ref(0)}]
        for prop in ['m_LocalPosition','m_LocalScale','m_LocalRotation']:
            vals=t[prop].copy()
            if prop=='m_LocalScale':vals={k:v/BASE_SCALE for k,v in vals.items()}
            for k,v in vals.items():mods.append({'target':ext(101,PREFAB_GUID),'propertyPath':prop+'.'+k,'value':str(v),'objectReference':ref(0)})
        instance={'PrefabInstance':{'m_ObjectHideFlags':0,'serializedVersion':2,'m_Modification':{'serializedVersion':3,'m_TransformParent':t['m_Father'],'m_Modifications':mods,'m_RemovedComponents':[],'m_RemovedGameObjects':[],'m_AddedGameObjects':[],'m_AddedComponents':[]},'m_SourcePrefab':ext(100100000,PREFAB_GUID)}}
        inserts[id]=[[1001,iid,False,instance],[4,tid,True,{'Transform':{'m_CorrespondingSourceObject':ext(101,PREFAB_GUID),'m_PrefabInstance':ref(iid),'m_PrefabAsset':ref(0)}}]]
        placements.append({'name':n['m_Name'],'position':t['m_LocalPosition'],'root_scale':{k:v/BASE_SCALE for k,v in t['m_LocalScale'].items()},'art_scale':t['m_LocalScale']})
    out=[]
    for b in blocks:
        if b[1] in inserts:out+=inserts[b[1]]
        if b[1] in remove:continue
        if b[0]==212:
            node=next(iter(b[3].values()));node['m_Materials']=[ext(2100000,MAT_GUID,2)];node['m_ReceiveShadows']=1;node['m_CastShadows']=2
        out.append(b)
    if placements:write(hallway,out)
    # Original cutscene Image references automatically resolve the replaced original,
    # no fake point components are added to a screen-space-overlay canvas.
    manifest=ROOT/'Tools/HallwayLighting/lighting-manifest.json'
    if not placements and manifest.exists():placements=json.loads(manifest.read_text())['placements']
    record={'schema':1,'source_status':'Authored source, requires native Unity shader import/render verification','original_art':'Assets/Art/Hallway/HALLWAY-sconce.png','original_sha256':hashlib.sha256((ART/'HALLWAY-sconce.png').read_bytes()).hexdigest(),'size':[1173,1714],'sprite_pivot':[.5,.5],'pixels_per_unit':100,'prefab':'Assets/Resources/HallwaySconce.prefab','prefab_guid':PREFAB_GUID,'sprite_local_scale':[BASE_SCALE]*3,'lights':lights,'material':'Assets/Art/Hallway/HallwaySpriteLit.mat','shader':'Assets/Art/Hallway/HallwaySpriteLit.shader','shader_response':{'ambient':.52,'light_response':.72,'additional_limit':1.1,'formula':'albedo * (ambient + min(sum(main and punctual light color * Unity distance attenuation * shadow attenuation * two-sided Lambert) * response, limit))'},'placements':placements,'inherited_consumers':['Assets/Scenes/Game.unity','Assets/Resources/Tutorials/TutorialRecordingStage.prefab','Assets/Scenes/TutorialRecording.unity'],'overlay_cutscene':'Original image shared; screen-space-overlay UI is not a 3D light receiver.'}
    manifest.write_text(json.dumps(record,indent=2)+'\n')
    print(f'Authored {len(placements)} linked sconce fixtures, {len(lights)} point lights each; source-only verification required.')
if __name__=='__main__':main()
