#!/usr/bin/env python3
"""Serialized geometry/light adjustments only, independent of intro or gameplay code."""
import sys,re,json,math,copy
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];sys.path.insert(0,str(ROOT/'Tools/WatchingFrameTests'))
import author_frame_galleries as a
import yaml
D=ROOT/'Assets/Art/WorldLighting';P=ROOT/'Assets/Prefabs/LobbyAtmosphere/LobbyDepthDarkness.prefab'
def blocks(s):return {int(m[2]):(m[0],int(m[1]),yaml.safe_load(m[3])) for m in re.finditer(r'^--- !u!(\d+) &(-?\d+).*?\n(.*?)(?=^--- !u!|\Z)',s,re.M|re.S)}
def mat(name,shader,color=None):
 p=D/(name+'.mat');d=dict(serializedVersion=8,**a.common(),m_Name=name,m_Shader=a.ref(4800000,a.meta(D/(shader+'.shader'),'ShaderImporter')),m_ValidKeywords=[],m_InvalidKeywords=[],m_LightmapFlags=4,m_EnableInstancingVariants=0,m_DoubleSidedGI=1,m_CustomRenderQueue=-1,stringTagMap={},disabledShaderPasses=[],m_SavedProperties=dict(serializedVersion=3,m_TexEnvs=[],m_Ints=[],m_Floats=[{'_Ambient':.32},{'_LightResponse':.85},{'_LightLimit':1.1},{'_DarknessStart':4.25},{'_DarknessEnd':12},{'_DarknessFloor':.012}],m_Colors=[] if color is None else [{'_BaseColor':dict(zip('rgba',color))}]))
 p.write_text(a.HEADER+a.doc(21,2100000,'Material',d));return a.meta(p,'NativeFormatImporter')
def patch_fields(text,id,fields):
 B=blocks(text);block=B[id][0]
 for key,val in fields.items():
  block,n=re.subn(rf'(?m)^  {re.escape(key)}: .*$',f'  {key}: {val}',block);assert n==1,(id,key)
 return text.replace(B[id][0],block)
def main():
 # Preserve original upper plaster hue, now under authored world-height darkness.
 original=(ROOT/'Assets/Art/Materials/HotelLobby/HotelLobbyColor6.mat').read_text()
 c=re.search(r'- _BaseColor: \{r: ([\d.eE+-]+), g: ([\d.eE+-]+), b: ([\d.eE+-]+), a: ([\d.eE+-]+)\}',original)
 upper=mat('LobbyUpperRoomAtmosphere','LobbyHeightAtmosphere',[float(v) for v in c.groups()]);black=mat('LobbyStairwellDepthBlack','LobbyDepthBlack')
 panels=[('Rear staircase darkness',(-6.85,5.1,13.04),(6.4,10.4,1),(0,0,0,1)),('Outer staircase darkness',(-9.87,5.1,9.35),(8.1,10.4,1),a.quat(90)),('Staircase overhead darkness',(-6.85,10.3,9.3),(6.4,8.1,1),[math.sqrt(.5),0,0,math.sqrt(.5)])]
 s=a.HEADER+a.doc(1,100,'GameObject',a.go('Authored staircase depth darkness',100,[101]))+a.doc(4,101,'Transform',a.tf(100,children=[201,211,221]))
 for index,(name,pos,scale,q) in enumerate(panels):
  i=200+index*10
  s+=a.doc(1,i,'GameObject',a.go(name,i,[i+1,i+2,i+3]))+a.doc(4,i+1,'Transform',a.tf(i,101,pos,q,scale))
  d=a.renderer(i,black);d.update(m_CastShadows=0,m_ReceiveShadows=0)
  s+=a.doc(23,i+2,'MeshRenderer',d)+a.doc(33,i+3,'MeshFilter',dict(**a.common(i),m_Mesh=a.ref(10210,'0000000000000000e000000000000000',0)))
 P.write_text(s);pg=a.meta(P)
 scene=ROOT/'Assets/Scenes/Lobby.unity';s=scene.read_text();B=blocks(s)
 for id in [1020738174,1490410123,1929967305]:
  old=B[id][0];s=s.replace(old,re.sub(r'(m_Materials:\n\s+- \{fileID: 2100000, guid: )\w+',r'\g<1>'+upper,old))
 s=patch_fields(s,2,{'m_FogColor':'{r: 0.018, g: 0.021, b: 0.03, a: 1}','m_LinearFogStart':'12','m_LinearFogEnd':'38'})
 s=patch_fields(s,330585545,{'m_ClearFlags':'2','m_BackGroundColor':'{r: 0, g: 0, b: 0, a: 1}'})
 # Only two punctual shadow cubemaps, plus the existing single main directional source.
 light_changes=[]
 for id in [1555505994,1060684691,1998747293]:
  B=blocks(s);old=B[id][0];new=re.sub(r'(  m_Shadows:\n    m_Type:) \d+',r'\1 2',old)
  new=re.sub(r'(    m_Bias:) .*',r'\1 0.025',new);new=re.sub(r'(    m_NormalBias:) .*',r'\1 0.08',new)
  if id==1998747293:new=re.sub(r'(  m_Intensity:) .*',r'\1 0.35',new)
  s=s.replace(old,new);light_changes.append({'scene':'Lobby','file_id':id,'shadow_type':'Soft','bias':.025,'normal_bias':.08,'intensity':.35 if id==1998747293 else None})
 # URP uses its asset bias unless explicitly disabled. Author the override components,
 # rather than relying on runtime GetUniversalAdditionalLightData to construct them.
 template=copy.deepcopy(blocks(s)[1998747292][2]['MonoBehaviour'])
 s=patch_fields(s,1998747292,{'m_UsePipelineSettings':'0'})
 for light_id,component_id in [(1555505994,981011),(1060684691,981021)]:
  B=blocks(s);goid=B[light_id][2]['Light']['m_GameObject']['fileID'];go_block=B[goid][0]
  if f'  - component: {{fileID: {component_id}}}' not in go_block:
   s=s.replace(go_block,go_block.replace('  m_Component:\n',f'  m_Component:\n  - component: {{fileID: {component_id}}}\n'))
  if component_id in blocks(s):s=s.replace(blocks(s)[component_id][0],'')
  data=copy.deepcopy(template);data.update(m_GameObject=a.ref(goid),m_UsePipelineSettings=0,m_AdditionalLightsShadowResolutionTier=1)
  at=s.index('--- !u!1660057539');s=s[:at]+a.doc(114,component_id,'MonoBehaviour',data)+s[at:]
 # Reduce fill spilling into the stair portal, preserve warm playable-room lights.
 s=patch_fields(s,184464686,{'m_Intensity':'0.8','m_Range':'3.4'})
 # Identity main light faced straight forward previously; angle its thin-card shadows onto floors/walls.
 B=blocks(s);light_go=B[1998747293][2]['Light']['m_GameObject']['fileID']
 tid=next(id for id,(_,k,b) in B.items() if k==4 and b['Transform'].get('m_GameObject',{}).get('fileID')==light_go)
 # Unity Euler X50,Y-30,Z0.
 x,y=math.radians(50)/2,math.radians(-30)/2;q=[math.sin(x)*math.cos(y),math.cos(x)*math.sin(y),-math.sin(x)*math.sin(y),math.cos(x)*math.cos(y)]
 s=patch_fields(s,tid,{'m_LocalRotation':'{'+', '.join(f'{k}: {v:.9f}' for k,v in zip('xyzw',q))+'}','m_LocalEulerAnglesHint':'{x: 50, y: -30, z: 0}'})
 # Own one stable scene instance only; preserve every unrelated block/scene root.
 iid,tid=8141014401,8141014402
 for id in [iid,tid]:
  if id in blocks(s):s=s.replace(blocks(s)[id][0],'')
 instance=dict(m_ObjectHideFlags=0,serializedVersion=2,m_Modification=dict(serializedVersion=3,m_TransformParent=a.ref(0),m_Modifications=[],m_RemovedComponents=[],m_RemovedGameObjects=[],m_AddedGameObjects=[],m_AddedComponents=[]),m_SourcePrefab=a.ref(100100000,pg))
 ss=a.doc(1001,iid,'PrefabInstance',instance)+a.doc(4,tid,'Transform',dict(m_CorrespondingSourceObject=a.ref(101,pg),m_PrefabInstance=a.ref(iid),m_PrefabAsset=a.ref(0))).replace(f'--- !u!4 &{tid}\n',f'--- !u!4 &{tid} stripped\n')
 roots=s.index('--- !u!1660057539')
 s=s[:roots]+ss+s[roots:]
 if f'  - {{fileID: {tid}}}' not in s:s=s.rstrip()+f'\n  - {{fileID: {tid}}}\n'
 scene.write_text(s)
 # The intro-only extension's wall continuation uses the same upper-wall finish.
 p=ROOT/'Assets/Prefabs/WatchingFrames/IntroTunnel/IntroWatchingFrameTunnel.prefab'
 if not p.exists():
  matches=list((ROOT/'Assets/Prefabs/WatchingFrames').rglob('*Tunnel.prefab'));p=matches[0]
 t=p.read_text();old=a.guid('Assets/Art/Materials/HotelLobby/HotelLobbyColor6.mat');t=t.replace(old,upper);p.write_text(t)
 # Hallway owns ONE directional key; inherited by Game and recording stage. Sconces stay shadowless.
 p=ROOT/'Assets/Resources/GameHallway.prefab';t=p.read_text();B=blocks(t)
 for id in [981001,981002,981003,981004]:
  if id in B:t=t.replace(B[id][0],'')
 B=blocks(t);root=B[101][0]
 if '  - {fileID: 981002}' not in root:t=t.replace(root,root.replace('  m_Children:\n','  m_Children:\n  - {fileID: 981002}\n'))
 source=copy.deepcopy(blocks(s)[1998747293][2]['Light']);source.update(m_GameObject=a.ref(981001),m_Color=dict(r=.65,g=.74,b=1,a=1),m_Intensity=.75,m_Lightmapping=4)
 t+=a.doc(1,981001,'GameObject',a.go('Authored hallway directional shadow key',981001,[981002,981003,981004]))+a.doc(4,981002,'Transform',a.tf(981001,101,q=q,pos=(0,4,-2)))+a.doc(108,981003,'Light',source)
 keydata=copy.deepcopy(template);keydata.update(m_GameObject=a.ref(981001),m_UsePipelineSettings=0,m_AdditionalLightsShadowResolutionTier=1)
 t+=a.doc(114,981004,'MonoBehaviour',keydata)
 p.write_text(t)
 (ROOT/'Tools/WorldLightingTests/room-atmosphere.json').write_text(json.dumps({'upper_material':str((D/'LobbyUpperRoomAtmosphere.mat').relative_to(ROOT)),'height_fade':{'start':4.25,'end':12,'minimum':.012},'stair_backdrop':panels,'lobby_lights_changed':light_changes,'stair_threshold':{'light_id':184464686,'intensity':.8,'range':3.4},'hallway_key':{'light_id':981003,'intensity':.75,'color':[.65,.74,1],'rotation_euler':[50,-30,0],'shadows':'Soft'},'sconce_point_shadows':'Unchanged: none, all 60 bulbs','limitations':'Source authored only; native shader import and visual/performance checks remain required.'},indent=2)+'\n')
 # Explicit proof recipe for the shared frame-atmosphere shader and original Lobby cards.
 manifest=ROOT/'Tools/WorldLightingTests/room-atmosphere.json';record=json.loads(manifest.read_text())
 record['frame_near_visibility']={'maximum_multiplier':.1,'fade_start_distance':2.7,'fade_end_distance':9,'formula':'max(height_visibility, .1 * (1 - smoothstep(2.7,9,distance(camera,frame))))','scope':'Whole frame composite including pupils, after lighting, before existing PresentationGain. Pupils obey atmosphere but remain independent of light/shadow response.'}
 record['lobby_physical_sprite_renderers']=[{'id':i,'source':v} for i,v in [(108660056,'FrontDesk-bell.png'),(593733501,'FrontDesk-lamp.png'),(734497626,'Windows/Window.png'),(783037327,'Radio-body.png'),(1313543254,'Fish-Plaque.png'),(1616107843,'Windows/Window.png'),(2016943117,'Radio-dial.png'),(15202743,'HotelFish/FISH-right.png'),(2070578375,'HotelFish/FISH-tail.png')]]
 record['nonphysical_lobby_sprite_renderers']=[597769595,974023391,1381542350,1982349989]
 record['urp_shadow_bias_policy']={'use_pipeline_settings':False,'point_resolution_tier':1,'point_resolution_pixels_pc':512,'lobby_data_components':[1998747292,981011,981021],'hallway_data_component':981004}
 record['intro_frame_near_visibility']={'ambient':.8,'maximum_multiplier':.8,'pupil_intensity':.125,'fade_start_distance':9.6,'fade_end_distance':32,'material_override':'_NearArtDistance32 on IntroTunnel materials only; normal Lobby/Hallway default9.','formula':'max(height_visibility,.8*(1-smoothstep(9.6,32,camera_distance)))'}
 manifest.write_text(json.dumps(record,indent=2)+'\n')
 print('Authored upper-room darkness, three stair backdrops, bounded room shadows, one inherited hallway key.')
if __name__=='__main__':main()
