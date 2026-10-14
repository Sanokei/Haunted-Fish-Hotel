#!/usr/bin/env python3
"""Author a static capsule-inspired runner. Does not modify the Lobby scene or existing carpet."""
from pathlib import Path
import argparse,struct,uuid,re,json,hashlib
ROOT=Path(__file__).resolve().parents[2];DIR=ROOT/'Assets/Art/LobbyRunner';PREFAB=ROOT/'Assets/Prefabs/LobbyAtmosphere/LobbyCapsuleCarpetRunner.prefab'
def guid(rel):return uuid.uuid5(uuid.NAMESPACE_URL,'haunted-fish-hotel/'+str(rel)).hex

def meta(path,kind='NativeFormatImporter',fileid=None):
 rel=path.relative_to(ROOT);g=guid(rel);extra=f'  mainObjectFileID: {fileid}\n' if fileid else ''
 path.with_suffix(path.suffix+'.meta').write_text(f'fileFormatVersion: 2\nguid: {g}\n{kind}:\n  externalObjects: {{}}\n'+extra+'  userData:\n  assetBundleName:\n  assetBundleVariant:\n');return g

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--width',type=float,required=True);ap.add_argument('--start',type=float,required=True);ap.add_argument('--end',type=float,required=True);ap.add_argument('--x',type=float,default=0);ap.add_argument('--y',type=float,required=True);a=ap.parse_args();w=a.width;length=a.end-a.start;assert w>1 and length>2;center=(a.start+a.end)/2
 DIR.mkdir(exist_ok=True);(DIR.parent/(DIR.name+'.meta')).write_text(f'fileFormatVersion: 2\nguid: {guid(DIR.relative_to(ROOT))}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
 mats=[];template=(ROOT/'Assets/Art/Materials/LobbyRevision/Lobby_Carpet_Antique_Binding.mat').read_text()
 def srgb(v):return 12.92*v if v<=.0031308 else 1.055*v**(1/2.4)-.055
 colors={'Capsule_Runner_Burgundy':(.11,.017,.026),'Capsule_Runner_Binding':(.025,.007,.011),'Capsule_Runner_Antique_Gold':(.19,.117,.05)}
 for name,linear in colors.items():
  rgb=[srgb(v) for v in linear];color='{r: '+str(rgb[0])+', g: '+str(rgb[1])+', b: '+str(rgb[2])+', a: 1}'
  txt=template.replace('m_Name: Lobby_Carpet_Antique_Binding','m_Name: '+name)
  for field in ['_BaseColor','_Color']:txt=re.sub(rf'    - {field}: [^\n]+',f'    - {field}: {color}',txt)
  txt=re.sub(r'    - _Smoothness: [^\n]+','    - _Smoothness: 0.035',txt);txt=re.sub(r'    - _Metallic: [^\n]+','    - _Metallic: 0',txt);txt=re.sub(r'    - _BumpScale: [^\n]+','    - _BumpScale: 0.12',txt)
  path=DIR/(name+'.mat');path.write_text(txt);mats.append(meta(path,fileid=2100000))
 verts=[];groups=[[],[],[]]
 def quad(x0,x1,z0,z1,y,group):
  start=len(verts)
  for x,z in [(x0,z0),(x1,z0),(x1,z1),(x0,z1)]:verts.append((x,y,z,0,1,0,1,0,0,-1,(x+w/2)/w,(z+length/2)/length))
  groups[group].extend([start,start+2,start+1,start,start+3,start+2])
 def ring(inset,thick,y,group):
  x0=-w/2+inset;x1=w/2-inset;z0=-length/2+inset;z1=length/2-inset
  quad(x0,x0+thick,z0,z1,y,group);quad(x1-thick,x1,z0,z1,y,group);quad(x0+thick,x1-thick,z0,z0+thick,y,group);quad(x0+thick,x1-thick,z1-thick,z1,y,group)
 quad(-w/2,w/2,-length/2,length/2,0,0);ring(0,.038,.0004,1)
 ring(.08,.025,.0009,2);ring(.22,.018,.0009,2)
 raw=b''.join(struct.pack('<12f',*v) for v in verts);inds=[i for g in groups for i in g];ib=struct.pack('<'+'H'*len(inds),*inds).hex();mesh=DIR/'LobbyCapsuleRunnerMesh.asset';prior=(ROOT/'Assets/Art/Materials/LobbyRevision/LobbyCarpetSurface.asset').read_text()
 header=prior[:prior.index('  m_SubMeshes:')].replace('m_Name: LobbyCarpetSurface','m_Name: LobbyCapsuleRunnerMesh')
 header=re.sub(r'  m_Name: [^\n]+','  m_Name: LobbyCapsuleRunnerMesh',header)
 subs='  m_SubMeshes:\n';offset=0
 for g in groups:
  first=min(g);count=max(g)-first+1
  subs+=f'''  - serializedVersion: 2
    firstByte: {offset*2}
    indexCount: {len(g)}
    topology: 0
    baseVertex: 0
    firstVertex: {first}
    vertexCount: {count}
    localAABB:
      m_Center: {{x: 0, y: 0.00045, z: 0}}
      m_Extent: {{x: {w/2}, y: 0.00045, z: {length/2}}}
''';offset+=len(g)
 mid=prior[prior.index('  m_Shapes:'):prior.index('  m_VertexData:')];mid=re.sub(r'  m_IndexBuffer: [^\n]*','  m_IndexBuffer: '+ib,mid);mid=mid.replace('m_IsReadable: 0','m_IsReadable: 1')
 channels='  m_VertexData:\n    serializedVersion: 3\n    m_VertexCount: '+str(len(verts))+'\n    m_Channels:\n'
 for i in range(14):
  off,dim={0:(0,3),1:(12,3),2:(24,4),4:(40,2)}.get(i,(0,0));channels+=f'    - stream: 0\n      offset: {off}\n      format: 0\n      dimension: {dim}\n'
 channels+=f'    m_DataSize: {len(raw)}\n    _typelessdata: {raw.hex()}\n'
 end=prior[prior.index('  m_CompressedMesh:'):];end=re.sub(r'  m_LocalAABB:\n    m_Center: [^\n]+\n    m_Extent: [^\n]+',f'  m_LocalAABB:\n    m_Center: {{x: 0, y: 0.00045, z: 0}}\n    m_Extent: {{x: {w/2}, y: 0.00045, z: {length/2}}}',end)
 end=end[:end.index('  m_MeshLodInfo:')]+'''  m_MeshLodInfo:
    serializedVersion: 2
    m_LodSelectionCurve:
      serializedVersion: 1
      m_LodSlope: 0
      m_LodBias: 0
    m_NumLevels: 1
    m_SubMeshes:
'''
 offset=0
 for g in groups:end+=f'    - serializedVersion: 2\n      m_Levels:\n      - serializedVersion: 1\n        m_IndexStart: {offset}\n        m_IndexCount: {len(g)}\n';offset+=len(g)
 mesh.write_text(header+subs+mid+channels+end);meshguid=meta(mesh,fileid=4300000)
 # Reuse the established static MeshRenderer layout, with no runtime scripts or colliders.
 prefab=(ROOT/'Assets/Art/BlenderDepthImports/LobbyRadioWoodDepth.prefab').read_text()
 ids={'3234945796684309974':'100000','2785219942595218905':'400000','8783938921518774103':'3300000','8634972262274324643':'2300000'}
 for before,after in ids.items():prefab=prefab.replace(before,after)
 prefab=prefab.replace('m_Name: LobbyRadioWoodDepth','m_Name: Lobby capsule burgundy runner with double gold trim')
 prefab=prefab.replace('m_LocalPosition: {x: 0, y: 0, z: 0}',f'm_LocalPosition: {{x: {a.x}, y: {a.y}, z: {center}}}')
 prefab=re.sub(r'm_Mesh: .*',f'm_Mesh: {{fileID: 4300000, guid: {meshguid}, type: 2}}',prefab)
 prefab=re.sub(r'  m_Materials:\n(?:  - .*\n)+','  m_Materials:\n'+''.join('  - {fileID: 2100000, guid: '+g+', type: 2}\n' for g in mats),prefab)
 prefab=prefab.replace('m_CastShadows: 2','m_CastShadows: 0');PREFAB.write_text(prefab);pg=meta(PREFAB,'PrefabImporter')
 data={'schema':1,'width':w,'length':length,'start_z':a.start,'end_z':a.end,'center':[a.x,a.y,center],'gold_borders':[{'inset':.08,'width':.025},{'inset':.22,'width':.018}],'base_linear_rgb':colors['Capsule_Runner_Burgundy'],'gold_linear_rgb':colors['Capsule_Runner_Antique_Gold'],'mesh':str(mesh.relative_to(ROOT)),'mesh_guid':meshguid,'prefab':str(PREFAB.relative_to(ROOT)),'prefab_guid':pg,'root_gameobject_id':100000,'root_transform_id':400000,'materials':list(colors),'vertices':len(verts),'triangles':len(inds)//3,'source_capsule':'store_capsules_20261014/source/build_capsules.py','preserved_carpet_sha256':hashlib.sha256((ROOT/'Assets/Art/Materials/LobbyRevision/Lobby_Burgundy_Carpet.mat').read_bytes()).hexdigest()}
 (Path(__file__).parent/'runner-authoring.json').write_text(json.dumps(data,indent=2)+'\n');print(json.dumps(data,indent=2))
if __name__=='__main__':main()
