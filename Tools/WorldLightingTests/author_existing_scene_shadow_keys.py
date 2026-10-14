#!/usr/bin/env python3
"""Author bounded shadow settings on existing Boss/Theatre lights; add no lights."""
from pathlib import Path
import copy,json,re,sys
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/WatchingFrameTests'))
import author_frame_galleries as a
from author_room_atmosphere import blocks
KEYS=[
 ('Assets/Scenes/BossFight.unity',672667459,981041,2,None),
 ('Assets/Scenes/theatre.unity',1297839878,981031,1,2),
]
def author():
 template=blocks((ROOT/'Assets/Scenes/Lobby.unity').read_text())[1998747292][2]['MonoBehaviour'];report=[]
 for asset,light_id,data_id,tier,shadow_type in KEYS:
  p=ROOT/asset;s=p.read_text();B=blocks(s);old=B[light_id][0];light=B[light_id][2]['Light'];go_id=light['m_GameObject']['fileID']
  if shadow_type is not None:
   new=re.sub(r'(  m_Shadows:\n    m_Type:) \d+',rf'\g<1> {shadow_type}',old)
   new=re.sub(r'(    m_Bias:) .*',r'\1 0.025',new);new=re.sub(r'(    m_NormalBias:) .*',r'\1 0.08',new);s=s.replace(old,new)
  B=blocks(s);go=B[go_id][0]
  if f'  - component: {{fileID: {data_id}}}' not in go:
   s=s.replace(go,go.replace('  m_Component:\n',f'  m_Component:\n  - component: {{fileID: {data_id}}}\n'))
  if data_id in B:
   assert B[data_id][1]==114 and B[data_id][2]['MonoBehaviour']['m_GameObject']['fileID']==go_id,'ID collision'
   s=s.replace(B[data_id][0],'')
  data=copy.deepcopy(template);data.update(m_GameObject=a.ref(go_id),m_UsePipelineSettings=0,m_AdditionalLightsShadowResolutionTier=tier)
  at=s.index('--- !u!1660057539');s=s[:at]+a.doc(114,data_id,'MonoBehaviour',data)+s[at:];p.write_text(s)
  updated=blocks(s)[light_id][2]['Light']
  report.append(dict(asset=asset,light_id=light_id,additional_data_id=data_id,light_count_unchanged=True,shadow_type=updated['m_Shadows']['m_Type'],bias=updated['m_Shadows']['m_Bias'],normal_bias=updated['m_Shadows']['m_NormalBias'],resolution_tier=tier,pc_resolution_pixels=1024 if tier==2 else 512,intensity=updated['m_Intensity'],range=updated['m_Range'],note='Existing pose, color, intensity, range and all gameplay/physics retained. Boss shadow type/bias retained; theatre enables its existing single spotlight shadow.'))
 out=ROOT/'Tools/WorldLightingTests/existing-key-shadows.json';out.write_text(json.dumps(dict(keys=report,native_gpu_validation=False),indent=2)+'\n');print('Authored shadow data on two existing keys; no new lights.')
if __name__=='__main__':author()
