#!/usr/bin/env python3
"""Source-contract checks, not Unity shader/native rendering evidence."""
from pathlib import Path
import hashlib, json, math, re
import yaml
ROOT=Path(__file__).resolve().parents[2]
GUID='c92cabf8c70e5495a0b4db09cb1d5f4e'
SPRITE='12598d6866ab5d72b14e30d5f55fcaa6'
MAT='dfed2841a24c53f898aa784753b43ec0'

def load(path):
    data={};kinds={}
    for m in re.finditer(r'^--- !u!(\d+) &(\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',path.read_text(),re.M|re.S):
        id=int(m[2]);assert id not in data, f'Duplicate fileID {id}'
        data[id]=next(iter(yaml.safe_load(m[3]).values()));kinds[id]=int(m[1])
    assert data,'No serialized objects'
    def visit(value):
        if isinstance(value,dict):
            if 'fileID' in value and value['fileID'] and 'guid' not in value:assert value['fileID'] in data,f'Dangling ref {path}:{value}'
            for v in value.values():visit(v)
        elif isinstance(value,list):
            for v in value:visit(v)
    visit(list(data.values()))
    return data,kinds

def main():
    manifest=json.loads((ROOT/'Tools/HallwayLighting/lighting-manifest.json').read_text());pf,kinds=load(ROOT/manifest['prefab'])
    lights=[v for k,v in pf.items() if kinds[k]==108]
    assert len(lights)==3
    prefab_text=(ROOT/manifest['prefab']).read_text()
    assert len(re.findall(r'^  m_Father: \{fileID: \d+\}$',prefab_text,re.M))==5,'Canonical inline hierarchy references required for aggregate graph tools'
    assert all(l['m_Enabled']==1 and l['m_Type']==2 and l['m_Lightmapping']==4 for l in lights)
    assert all(l['m_Shadows']['m_Type']==0 and l['m_DrawHalo']==0 and l['m_Cookie']=={'fileID':0} for l in lights)
    assert all(l['m_Range']==2.15 and l['m_Intensity']==.22 and l['m_UseColorTemperature']==0 for l in lights)
    assert len({tuple(pf[301+i*10]['m_LocalPosition'].values()) for i in range(3)})==3
    for i,source in enumerate(manifest['lights']):
        point=pf[301+i*10]['m_LocalPosition'];px,py=source['source_pixel_center']
        assert math.isclose(point['x'],(px-1173/2)/100*.162,abs_tol=1e-9)
        assert math.isclose(point['y'],(1714/2-py)/100*.162,abs_tol=1e-9)
        assert point['z']==-.22
    assert pf[202]['m_Sprite']['guid']==SPRITE
    assert pf[202]['m_Materials']==[{'fileID':2100000,'guid':MAT,'type':2}]
    art=ROOT/manifest['original_art'];assert hashlib.sha256(art.read_bytes()).hexdigest()==manifest['original_sha256']
    from PIL import Image
    im=Image.open(art);assert im.size==(1173,1714) and im.mode=='RGBA'
    assert im.getbbox()==(423,521,733,992),'Original unlit source alpha bounds altered'
    hallway,hk=load(ROOT/'Assets/Resources/GameHallway.prefab')
    fixtures=[n for n in hallway.values() if n.get('m_SourcePrefab',{}).get('guid')==GUID]
    assert len(fixtures)==20 and len(manifest['placements'])==20
    for inst,saved in zip(fixtures,manifest['placements']):
        props={p['propertyPath']:p['value'] for p in inst['m_Modification']['m_Modifications']}
        assert props['m_Name']==saved['name']
        for axis in 'xyz':
            assert math.isclose(float(props['m_LocalPosition.'+axis]),saved['position'][axis])
            assert math.isclose(float(props['m_LocalScale.'+axis]),saved['root_scale'][axis])
    assert all(n['m_Materials']==[{'fileID':2100000,'guid':MAT,'type':2}] for id,n in hallway.items() if hk[id]==212)
    # Exercise the repository's stricter canonical-YAML graph reader too.
    import sys
    sys.path.insert(0,str(ROOT/'Tools/ScenePresentationTests'))
    import verify_scene_assets as graph_checks
    for path in ['Assets/Resources/HallwaySconce.prefab','Assets/Resources/GameHallway.prefab','Assets/Art/Hallway/HallwaySpriteLit.mat']:
        source=(ROOT/path).read_text()
        assert not (graph_checks.reference_issues(source)|graph_checks.syntax_issues(source)),path
    graph=graph_checks.AssetGraph(ROOT/'Assets/Resources/GameHallway.prefab',{GUID:ROOT/manifest['prefab']})
    roots=[d for d in graph.docs.values() if d.kind==4 and d.stripped and GUID in d.body]
    assert len(roots)==20
    for doc,saved in zip(roots,manifest['placements']):
        position,rotation,scale=graph.node(doc.ident).pose()
        for i,axis in enumerate('xyz'):
            assert math.isclose(position[i],saved['position'][axis],abs_tol=1e-8)
            assert math.isclose(scale[i],saved['root_scale'][axis],abs_tol=1e-8)
    for path in ['Assets/Scenes/Game.unity','Assets/Resources/Tutorials/TutorialRecordingStage.prefab']:
        assert 'guid: 32ba6258b9755b3991bf35e425189dec' in (ROOT/path).read_text(),f'Missing shared hallway source {path}'
    attic=(ROOT/'Assets/Resources/AtticRoundCutscene.prefab').read_text();n=attic.count('guid: '+SPRITE)
    shader=(ROOT/manifest['shader']).read_text()+(ROOT/'Assets/Art/WorldLighting/ArtLighting.hlsl').read_text()+(ROOT/'Assets/Art/WorldLighting/ArtSpritePass.hlsl').read_text()
    for token in ['GetAdditionalLight(', 'LIGHT_LOOP_BEGIN(', '_CLUSTER_LIGHT_LOOP','inputData.normalizedScreenSpaceUV','source.shadowAttenuation','TransformObjectToWorldNormal','texel.rgb *= ArtIllumination']:
        assert token in shader,token
    assert 'm_RenderingMode: 2' in (ROOT/'Assets/Settings/PC_Renderer.asset').read_text(),'PC Forward+ required for panel light coverage'
    assert 'AddComponent' not in (ROOT/manifest['prefab']).read_text()
    report={'result':'PASS source checks only','fixtures':len(fixtures),'lights_per_fixture':3,'inherited_game_lights':60,'inherited_guide_lights':60,'overlay_original_art_references':n,'original_sha256':manifest['original_sha256'],'limits':['No native Unity import, shader compilation or runtime lighting has been verified.','Mobile Forward still caps per-object light selection at its existing setting.','Attic cutscene overlay art updates, but does not receive 3D point lights.']}
    (ROOT/'Tools/HallwayLighting/verification.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(report,indent=2))
if __name__=='__main__':main()
