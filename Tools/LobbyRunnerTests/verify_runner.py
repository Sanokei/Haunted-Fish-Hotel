#!/usr/bin/env python3
"""Source-level validation of static runner mesh, scene instance, and preserved carpet."""
from pathlib import Path
import re,struct,yaml,json,hashlib,numpy as np
R=Path(__file__).resolve().parents[2];D=json.loads((R/'Tools/LobbyRunnerTests/runner-authoring.json').read_text());txt=(R/D['mesh']).read_text();doc=yaml.safe_load(re.sub(r'^%.*\n|^---.*\n','',txt,flags=re.M))['Mesh'];vd=doc['m_VertexData'];raw=bytes.fromhex(vd['_typelessdata']);idx=bytes.fromhex(doc['m_IndexBuffer']);assert len(raw)==vd['m_DataSize']==vd['m_VertexCount']*48;assert len(idx)==156
V=np.array([struct.unpack_from('<12f',raw,i*48) for i in range(vd['m_VertexCount'])]);I=struct.unpack('<78H',idx)
for k in range(0,len(I),3):
 a,b,c=V[list(I[k:k+3]),:3];assert np.cross(b-a,c-a)[1]>0
assert np.isclose(np.ptp(V[:,0]),D['width']);assert np.isclose(np.ptp(V[:,2]),D['length']);assert all(0<=j<len(V) for j in I)
for d in doc['m_SubMeshes']:assert d['firstByte']%2==0 and d['indexCount']%3==0 and d['firstByte']+d['indexCount']*2<=len(idx)
pref=(R/D['prefab']).read_text();assert 'MonoBehaviour' not in pref and 'Collider' not in pref;assert 'm_CastShadows: 0' in pref;assert len(re.findall(r'  - \{fileID: 2100000, guid:',pref))==3
assert hashlib.sha256((R/'Assets/Art/Materials/LobbyRevision/Lobby_Burgundy_Carpet.mat').read_bytes()).hexdigest()==D['preserved_carpet_sha256']
scene=(R/'Assets/Scenes/Lobby.unity').read_text();assert scene.count('&8141014201')==1 and scene.count('  - {fileID: 8141014201}')==1
report={'mesh_data_bytes':len(raw),'vertex_count':len(V),'triangle_count':len(I)//3,'all_face_normals_up':True,'valid_submesh_ranges':True,'width':D['width'],'length':D['length'],'prefab_static_no_scripts_or_colliders':True,'prior_damask_material_unchanged':True,'authored_scene_instance':8141014201,'native_unity_import_run':False}
(R/'Tools/LobbyRunnerTests/source-validation.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
