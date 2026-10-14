#!/usr/bin/env python3
"""Offline authoring of a dormant, inward-facing frame tunnel used only by the intro ending."""
import json,math,random,sys,re,yaml
from pathlib import Path
import numpy as np
from scipy.spatial.transform import Rotation
import author_frame_galleries as a
ROOT=a.ROOT
OUT=ROOT/'Assets/Prefabs/WatchingFrames/IntroTunnel'
POSITION=[0,0,0]
ROTATION=Rotation.identity()
ROOT_Q=[0,0,0,1]
TOP=80.0
FRAME_BOTTOM=22.08
FRAME_TOP=61.08
FRAME_SCALE=1.5
FADE_START=25.0
FADE_RATE=.10
WALL_MATERIAL='Assets/Art/WorldLighting/LobbyUpperRoomAtmosphere.mat'

def lobby_wall_planes():
    scene=(ROOT/'Assets/Scenes/Lobby.unity').read_text()
    docs={int(m[2]):(int(m[1]),next(iter(yaml.safe_load(m[3]).values()))) for m in re.finditer(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',scene,re.M|re.S)}
    walls={}
    for name,tf_id,renderer,sign in [('Left',1929967303,1929967305,1),('Right',1490410121,1490410123,-1),('Back',1020738172,1020738174,-1)]:
        t=docs[tf_id][1];parent=docs[t['m_Father']['fileID']][1]
        center=np.array([t['m_LocalPosition'][k]+parent['m_LocalPosition'][k] for k in 'xyz'])
        q=Rotation.from_quat([t['m_LocalRotation'][k] for k in 'xyzw'])
        normal=q.apply([sign,0,0]);normal[np.abs(normal)<1e-12]=0
        point=center+normal*t['m_LocalScale']['x']/2
        upper=center[1]+t['m_LocalScale']['y']/2
        walls[name]=dict(name=name,plane_point=point.tolist(),inward_normal=normal.tolist(),bottom=upper,source_transform=tf_id,source_renderer=renderer,source_material=WALL_MATERIAL,source_inner_plane_point=point.tolist(),source_upper_y=upper)
    walls['Front']=dict(name='Front',plane_point=[0,0,-2.95],inward_normal=[0,0,1],bottom=max(w['bottom'] for w in walls.values()),source_transform=None,source_renderer=None,source_material=WALL_MATERIAL,source_inner_plane_point=None,source_upper_y=None)
    def cross(a,b):
        a,b=walls[a],walls[b];na=np.array(a['inward_normal'])[[0,2]];nb=np.array(b['inward_normal'])[[0,2]]
        x,z=np.linalg.solve(np.stack([na,nb]),[np.dot(na,np.array(a['plane_point'])[[0,2]]),np.dot(nb,np.array(b['plane_point'])[[0,2]])])
        return np.array([x,0,z])
    fl,fr,bl,br=cross('Left','Front'),cross('Right','Front'),cross('Left','Back'),cross('Right','Back')
    for name,ends in [('Left',[fl,bl]),('Right',[br,fr]),('Back',[bl,br]),('Front',[fr,fl])]:walls[name]['endpoints']=[x.tolist() for x in ends]
    return list(walls.values()),[fl.tolist(),fr.tolist(),br.tolist(),bl.tolist()]

def folder(path):
    path.mkdir(parents=True,exist_ok=True)
    a.meta(path,'DefaultImporter')
    mp=Path(str(path)+'.meta');s=mp.read_text()
    if 'folderAsset: yes' not in s:s=s.replace('DefaultImporter:', 'folderAsset: yes\nDefaultImporter:');mp.write_text(s)

def world(p):return (ROTATION.apply(p)+np.array(POSITION)).tolist()
def world_q(q):return (ROTATION*Rotation.from_quat(q)).as_quat().tolist()

def main():
    folder(OUT);folder(OUT/'Materials')
    # Opaque inward-only geometry conceals the normal Lobby behind the cinematic shaft.
    shader=OUT/'Materials/IntroTunnelBlack.shader'
    shader.write_text('''Shader "HauntedFish/IntroTunnelBlack" {
 SubShader { Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
 Pass { Tags { "LightMode"="SRPDefaultUnlit" } Cull Back ZWrite On ZTest LEqual
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A { float3 vertex : POSITION; }; struct V { float4 vertex : SV_POSITION; };
 V vert(A i) { V o; o.vertex=TransformObjectToHClip(i.vertex); return o; }
 half4 frag(V i) : SV_Target { return half4(0,0,0,1); }
 ENDHLSL
 } } FallBack Off
}
''')
    a.meta(shader,'ShaderImporter')
    mat=OUT/'Materials/IntroTunnelBlack.mat'
    mat.write_text(a.HEADER+a.doc(21,2100000,'Material',dict(serializedVersion=8,**a.common(),m_Name='Intro-only inward black tunnel',m_Shader=a.ref(4800000,a.guid(shader.relative_to(ROOT))),m_ValidKeywords=[],m_InvalidKeywords=[],m_LightmapFlags=4,m_EnableInstancingVariants=0,m_DoubleSidedGI=0,m_CustomRenderQueue=-1,stringTagMap={},disabledShaderPasses=[],m_SavedProperties=dict(serializedVersion=3,m_TexEnvs=[],m_Ints=[],m_Floats=[],m_Colors=[]))))
    black=a.meta(mat,'NativeFormatImporter')
    fade_shader=OUT/'Materials/IntroTunnelHeightFade.shader'
    fade_shader.write_text('''Shader "HauntedFish/IntroTunnelHeightFade" {
 Properties { _FadeStart ("World height where darkness begins", Float)=25 _FadeRate ("Height extinction", Float)=0.1 }
 SubShader { Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
 Pass { Tags { "LightMode"="SRPDefaultUnlit" } Cull Back ZWrite Off ZTest LEqual Blend One OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial) float _FadeStart, _FadeRate; CBUFFER_END
 struct A { float3 vertex : POSITION; }; struct V { float4 vertex : SV_POSITION; float height:TEXCOORD0; };
 V vert(A i) { V o; float3 p=TransformObjectToWorld(i.vertex); o.vertex=TransformWorldToHClip(p); o.height=p.y; return o; }
 half4 frag(V i) : SV_Target { return half4(0,0,0,1-exp(-max(0,i.height-_FadeStart)*_FadeRate)); }
 ENDHLSL
 } } FallBack Off
}
''')
    a.meta(fade_shader,'ShaderImporter')
    fade_mat=OUT/'Materials/IntroTunnelHeightFade.mat'
    fade_mat.write_text(a.HEADER+a.doc(21,2100000,'Material',dict(serializedVersion=8,**a.common(),m_Name='Continuous height darkness over matching Lobby wall',m_Shader=a.ref(4800000,a.guid(fade_shader.relative_to(ROOT))),m_ValidKeywords=[],m_InvalidKeywords=[],m_LightmapFlags=4,m_EnableInstancingVariants=0,m_DoubleSidedGI=0,m_CustomRenderQueue=-1,stringTagMap={},disabledShaderPasses=[],m_SavedProperties=dict(serializedVersion=3,m_TexEnvs=[],m_Ints=[],m_Floats=[{'_FadeStart':FADE_START},{'_FadeRate':FADE_RATE}],m_Colors=[]))))
    fade_guid=a.meta(fade_mat,'NativeFormatImporter')
    rng=random.Random(202610143106);usage={f['id']:0 for f in a.ART['frames']};placements=[];surfaces=[]
    walls,footprint=lobby_wall_planes()
    for wall in walls:
        name=wall['name'];p0,p1=map(np.array,wall['endpoints']);u=(p1-p0)/np.linalg.norm(p1-p0);width=np.linalg.norm(p1-p0);n=np.array(wall['inward_normal'])
        base=Rotation.from_matrix(np.column_stack([u,[0,1,0],-n]));height=TOP-wall['bottom'];wallcenter=(p0+p1)/2+[0,(TOP+wall['bottom'])/2,0]
        common=dict(name=name,local_position=wallcenter.tolist(),local_rotation_quaternion=base.as_quat().tolist(),scale=[width,height,1],position=wallcenter.tolist(),rotation_quaternion=base.as_quat().tolist(),inward_normal=n.tolist(),plane_point=wall['plane_point'],vertical_range=[wall['bottom'],TOP],endpoints=wall['endpoints'],source_renderer=wall['source_renderer'],source_transform=wall['source_transform'],source_inner_plane_point=wall['source_inner_plane_point'],source_upper_y=wall['source_upper_y'])
        surfaces.append(dict(common,kind='matched_wall',material=WALL_MATERIAL,material_guid=a.guid(WALL_MATERIAL),sorting_order=0))
        overlaycenter=wallcenter+n*.003
        surfaces.append(dict(common,name=name+' smooth darkness',kind='height_darkness',local_position=overlaycenter.tolist(),position=overlaycenter.tolist(),material=str(fade_mat.relative_to(ROOT)),material_guid=fade_guid,sorting_order=-10,alpha='1-exp(-max(0,worldY-25)*0.10)'))
        packed=a.cluster_layout((width-.20)/FRAME_SCALE,(FRAME_TOP-FRAME_BOTTOM)/FRAME_SCALE,rng,usage)
        for j,p in enumerate(packed):
            y=FRAME_BOTTOM+p['y']*FRAME_SCALE
            center=p0+u*(.10+p['x']*FRAME_SCALE)+[0,y,0]+n*.027
            q=(base*Rotation.from_euler('z',p['angle'],degrees=True)).as_quat().tolist()
            placement=a.make_placement(f'Intro upper extension {name} {j+1:03d}','IntroTunnel',name,center.tolist(),0,p['w']*FRAME_SCALE,p['h']*FRAME_SCALE,rng,usage,p['frame'],p['angle'],p['cluster'])
            placement['local_position']=center.tolist();placement['local_rotation_quaternion']=q;placement['position']=center.tolist();placement['rotation_quaternion']=q;placement['eye_rotation_quaternion']=q
            aim=placement['eye_local_position'];offset=[aim[0]*placement['width'],aim[1]*placement['height'],0]
            placement['eye_world_position']=(center+Rotation.from_quat(q).apply(offset)).tolist()
            placement['wall_plane_position']=[p['x']*FRAME_SCALE,p['y']*FRAME_SCALE];placement['wall_plane_bounds']=[width-.20,FRAME_TOP-FRAME_BOTTOM]
            placement['presentation_gain']=math.exp(-max(0,y-FADE_START)*FADE_RATE)
            placement['world_height']=y;placement['depth']=y-wall['bottom']
            placements.append(placement)
    minx=min(p[0] for p in footprint);maxx=max(p[0] for p in footprint);minz=min(p[2] for p in footprint);maxz=max(p[2] for p in footprint)
    capcenter=[(minx+maxx)/2,TOP,(minz+maxz)/2];capq=Rotation.from_euler('x',-90,degrees=True).as_quat().tolist()
    surfaces.append(dict(name='Deep black upper cap',kind='black_cap',local_position=capcenter,local_rotation_quaternion=capq,scale=[maxx-minx,maxz-minz,1],position=capcenter,rotation_quaternion=capq,material=str(mat.relative_to(ROOT)),material_guid=black,sorting_order=0,inward_normal=[0,-1,0],plane_point=capcenter,vertical_range=[TOP,TOP]))
    selection=a.apply_eye_selection(placements)
    ids=[100100+i*10 for i in range(len(placements))];wall_ids=[900000+i*10 for i in range(len(surfaces))]
    data=a.HEADER
    root=a.go('INTRO ONLY - continuous vertical upper gallery',100000,[400000,1140000]);root['m_IsActive']=0
    data+=a.doc(1,100000,'GameObject',root)
    data+=a.doc(4,400000,'Transform',a.tf(100000,pos=POSITION,q=ROOT_Q,children=[i+1 for i in ids+wall_ids]))
    eyes=[dict(Renderer=a.ref(i+2),AimPoint=a.ref(i+5),PresentationGain=p['presentation_gain']) for i,p in zip(ids,placements) if p['has_eye']]
    static=[dict(Renderer=a.ref(i+2),PresentationGain=p['presentation_gain']) for i,p in zip(ids,placements) if not p['has_eye']]
    data+=a.doc(114,1140000,'MonoBehaviour',dict(**a.common(100000),m_Enabled=1,m_EditorHideFlags=0,m_Script=a.ref(1150000,a.guid('Assets/Scripts/Presentation/WatchingFrames/WatchingFrameGallery.cs')),m_Name='',m_EditorClassIdentifier='',_Viewer=a.ref(0),_BeamCamera=a.ref(0),_ReactToFlashlight=0,_BeamOccluders=dict(serializedVersion=2,m_Bits=4294967295),_CloseSeconds=.16,_OpenSeconds=.24,_Eyes=eyes,_StaticFrames=static,_SamplesPerSecond=20,_GazeGain=.35,_MinimumDistance=4,_TravelUV=dict(x=.23,y=.12),_ViewerMovementThreshold=.004))
    oldout=a.OUT;a.OUT=OUT;materials={}
    for i,p in zip(ids,placements):
        key=(p['frame_id'],p['variant'],p['has_eye'])
        if key not in materials:materials[key]=a.material(a.ART['frames'][p['frame_id']-1],p['variant'],'IntroTunnel',p['has_eye'])
        p['material']=str(a.material_path(a.ART['frames'][p['frame_id']-1],p['variant'],'IntroTunnel',p['has_eye']).relative_to(ROOT))
        data+=a.doc(1,i,'GameObject',a.go(f'{p["name"]} | {p["frame_name"]} | {p["variant"]}',i,[i+1,i+2,i+3]))
        data+=a.doc(4,i+1,'Transform',a.tf(i,400000,p['local_position'],p['local_rotation_quaternion'],[p['width'],p['height'],1],[i+5] if p['has_eye'] else []))
        data+=a.doc(33,i+3,'MeshFilter',dict(**a.common(i),m_Mesh=a.ref(10210,'0000000000000000e000000000000000',0)))
        data+=a.doc(23,i+2,'MeshRenderer',dict(**a.renderer(i,materials[key],0),m_AdditionalVertexStreams=a.ref(0)))
        if p['has_eye']:
            data+=a.doc(1,i+4,'GameObject',a.go('Authored original-eye gaze center',i+4,[i+5]))
            data+=a.doc(4,i+5,'Transform',a.tf(i+4,i+1,p['eye_local_position']))
    a.OUT=oldout
    for i,p in zip(wall_ids,surfaces):
        data+=a.doc(1,i,'GameObject',a.go(p['name']+' - inward continuation',i,[i+1,i+2,i+3]))
        data+=a.doc(4,i+1,'Transform',a.tf(i,400000,p['local_position'],p['local_rotation_quaternion'],p['scale']))
        data+=a.doc(33,i+3,'MeshFilter',dict(**a.common(i),m_Mesh=a.ref(10210,'0000000000000000e000000000000000',0)))
        data+=a.doc(23,i+2,'MeshRenderer',dict(a.renderer(i,p['material_guid'],p['sorting_order']),m_CastShadows=2 if p['kind']=='matched_wall' else 0,m_ReceiveShadows=1 if p['kind']=='matched_wall' else 0,m_AdditionalVertexStreams=a.ref(0)))
    prefab=OUT/'IntroWatchingFrameTunnel.prefab';prefab.write_text(data);g=a.meta(prefab)
    source=json.loads((ROOT/'Tools/WatchingFrameTests/gallery-placement.json').read_text())
    report=dict(schema=3,eye_selection=selection,prefab=str(prefab.relative_to(ROOT)),prefab_guid=g,root_gameobject=100000,root_transform=400000,gallery_component=1140000,root_position=POSITION,root_rotation_quaternion=ROOT_Q,axis='World +Y; exact continuation of original tapered-in-plan Lobby footprint',footprint_xz=footprint,wall_planes=walls,vertical_range=[min(w['bottom'] for w in walls),TOP],frame_height_interval=[FRAME_BOTTOM,FRAME_TOP],black_throat=[FRAME_TOP,TOP],activation='Prefab root authored inactive; WorldSequence.Prepare enables before first world view and restores prior state on Close. Existing walls and normal gallery remain visible throughout; no timed threshold or occluder swaps.',lights=[],interior_color=source['interior_color'],eye=source['eye'],counts=dict(frames=len(placements),eyes=sum(p['has_eye'] for p in placements),empty_slots=sum(not p['has_eye'] for p in placements),surfaces=len(surfaces)),presentation_gain='Serialized Eye.PresentationGain = exp(-max(0,worldY-25)*0.10). RGB-only extinction, alpha and local masks unchanged. Normal galleries gain1. Matched native wall material is unchanged through Y25; a separately sorted black overlay uses complementary continuous height extinction above25.',endpoint=dict(position=[0,8.5,3.2],pitch=-89,fov=24),surfaces=surfaces,placements=placements)
    (ROOT/'Tools/WatchingFrameTests/intro-tunnel-placement.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(dict(prefab=g,counts=report['counts'])))
if __name__=='__main__':
    if '--geometry-only' in sys.argv:
        _write_text=Path.write_text
        def geometry_write(path,text,*args,**kwargs):
            if path.suffix in ('.mat','.shader') or path.name.endswith(('.mat.meta','.shader.meta')):
                if not path.exists():raise RuntimeError('Geometry-only authoring requires existing material asset: '+str(path))
                return len(text)
            return _write_text(path,text,*args,**kwargs)
        Path.write_text=geometry_write
    main()
