#!/usr/bin/env python3
"""Move the fogged block city from the gate rig into the opening (Storm) shot and give the opening photographic-style clouds.

Offline prefab authoring, like author_lobby_intro.py: edits Assets/Resources/LobbyIntroductionUI.prefab in place.
  * removes the three night-city slabs that were parented to Exterior parallax;
  * inserts FogSky + four front-on city layers at the back of the Storm group;
  * points both cloud variants at CloudBank.png (white, full frame) and hides the hotel/tree silhouettes in the opening.
Idempotent: refuses to run twice.
"""
import json, re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PREFAB = ROOT / 'Assets/Resources/LobbyIntroductionUI.prefab'
IDS = json.loads((ROOT / 'Tools/LobbyIntroArt/opening_city_guids.json').read_text())
STORM_RT, OLD_CITY = '6232796351706565258', ('5812000000000000201', '5812000000000000202', '5812000000000000203')
CLOUD_RTS = {'6996907180247280785': '{r: 0.80, g: 0.84, b: 0.90, a: 0.96}',      # overcast
             '8849920227451258767': '{r: 1, g: 1, b: 1, a: 1}'}                    # lightning flash
HIDE_GOS = ('Storm distant hotel silhouette', 'Storm left tree silhouette', 'Storm right tree silhouette')
LAYERS = [('Opening fog sky', 'FogSky'), ('Opening city - far', 'OpeningCityFar'), ('Opening city - mid', 'OpeningCityMid'),
          ('Opening city - back', 'OpeningCityBack'), ('Opening city - near', 'OpeningCityNear')]


def image_go(base, name, guid):
    go, rt, cr, im = (str(base + i) for i in range(4))
    return rt, f'''--- !u!1 &{go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {rt}}}
  - component: {{fileID: {cr}}}
  - component: {{fileID: {im}}}
  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{rt}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {STORM_RT}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0.5, y: 0.5}}
  m_AnchorMax: {{x: 0.5, y: 0.5}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 1600, y: 900}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{im}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Image
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 21300000, guid: {guid}, type: 3}}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
'''


def main():
    text = PREFAB.read_text()
    if 'Opening fog sky' in text:
        raise SystemExit('already authored')
    docs = re.split(r'(?m)^(?=--- !u!)', text)
    head, body = docs[0], docs[1:]
    key = lambda d: re.match(r'--- !u!\d+ &(\d+)', d).group(1)
    by = {key(d): d for d in body}
    # 1. drop the old night-city slabs (GameObject 101-103, RT 201-203, renderer 301-303, image 401-403)
    drop = {f'58120000000000{n}' for n in ('00101', '00102', '00103', '00201', '00202', '00203', '00301', '00302', '00303', '00401', '00402', '00403')}
    assert drop <= set(by), 'old city docs not found'
    body = [d for d in body if key(d) not in drop]
    text = head + ''.join(body)
    for rt in OLD_CITY:
        text = re.sub(rf'    - Target: \{{fileID: {rt}\}}\n      Depth: [0-9.]+\n', '', text)
        text = re.sub(rf'  - \{{fileID: {rt}\}}\n', '', text)
    # 2. new layers at the back of Storm
    new_docs, new_rts = [], []
    for i, (name, stem) in enumerate(LAYERS):
        rt, d = image_go(5812000000000002000 + i * 10, name, IDS[stem]); new_rts.append(rt); new_docs.append(d)
    storm_children = re.compile(r'(--- !u!224 &' + STORM_RT + r'\nRectTransform:.*?  m_Children:\n)', re.S)
    assert storm_children.search(text)
    text = storm_children.sub(lambda m: m.group(1) + ''.join(f'  - {{fileID: {r}}}\n' for r in new_rts), text, count=1)
    # 3. both cloud variants use the new bank, full frame, white
    for rt, tint in CLOUD_RTS.items():
        text = re.sub(rf'(--- !u!224 &{rt}\n.*?m_AnchoredPosition: )\{{[^}}]*\}}(\n  m_SizeDelta: )\{{[^}}]*\}}', r'\g<1>{x: 0, y: 0}\g<2>{x: 1600, y: 900}', text, count=1, flags=re.S)
        go = re.search(rf'--- !u!224 &{rt}\n.*?m_GameObject: \{{fileID: (\d+)\}}', text, re.S).group(1)
        text = re.sub(rf'(m_GameObject: \{{fileID: {go}\}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: \{{fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3\}}\n  m_Name: \n  m_EditorClassIdentifier: [^\n]*\n  m_Material: \{{fileID: 0\}}\n  m_Color: )\{{[^}}]*\}}', lambda m: m.group(1) + tint, text, count=1)
        text = re.sub(rf'(m_GameObject: \{{fileID: {go}\}}\n(?:(?!--- ).)*?m_Sprite: \{{fileID: 21300000, guid: )[0-9a-f]{{32}}', rf'\g<1>{IDS["CloudBank"]}', text, count=1, flags=re.S)
    # 4. the hotel and trees do not belong in the opening
    for name in HIDE_GOS:
        text = re.sub(rf'(  m_Name: {name}\n(?:  m_[^\n]*\n)*?  m_IsActive: )1', r'\g<1>0', text, count=1)
    PREFAB.write_text(text.rstrip('\n') + '\n' + ''.join(new_docs))
    print('authored', len(new_docs), 'layers')


if __name__ == '__main__':
    main()
