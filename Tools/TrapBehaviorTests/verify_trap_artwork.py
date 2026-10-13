#!/usr/bin/env python3
"""Source/serialization/art geometry checks. Not a Unity import or rendering test.

Pillow is used only to READ source pixels. This guard never edits an image.
"""
from pathlib import Path
import hashlib
import json
import math
import re
import yaml
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/Art/Traps'

def read_prefab(name):
    text = (ROOT / f'Assets/Resources/Traps/{name}.prefab').read_text()
    ids = re.findall(r'^--- !u!\d+ &(\d+)$', text, re.M)
    assert len(ids) == len(set(ids)), (name, 'duplicate serialized IDs')
    result = {}
    for ident, body in re.findall(r'^--- !u!\d+ &(\d+)\n(.*?)(?=^--- !u!|\Z)', text, re.M | re.S):
        result[int(ident)] = next(iter(yaml.safe_load(body).values()))
    # Every local binding and reciprocal child/parent relation must resolve.
    for raw in re.findall(r'\{fileID: [^}]+\}', text):
        ref = yaml.safe_load(raw)
        if 'guid' not in ref and ref['fileID']:
            assert ref['fileID'] in result, (name, 'dangling reference', ref)
    for ident, obj in result.items():
        if 'm_Father' in obj:
            parent = obj['m_Father']['fileID']
            if parent:
                assert {'fileID': ident} in result[parent]['m_Children'], (name, 'missing parent child', ident)
        for child in obj.get('m_Children', []):
            assert result[child['fileID']]['m_Father']['fileID'] == ident, (name, 'wrong child parent', ident)
    return result, text

def sprite_meta(name):
    return yaml.safe_load((ART / (name + '.meta')).read_text())

manifest = json.loads((Path(__file__).parent / 'TrapArtworkSources.json').read_text())
for name, expected in manifest['files'].items():
    assert hashlib.sha256((ART/name).read_bytes()).hexdigest() == expected, (name, 'original bytes changed')
    meta = sprite_meta(name)['TextureImporter']
    assert meta['alphaIsTransparency'] == 1 and meta['mipmaps']['enableMipMap'] == 0
    assert all(p['textureCompression'] == 0 for p in meta['platformSettings'])
    assert Image.open(ART/name).size == (2048,2048)

cart, cart_text = read_prefab('ShoppingCartTrap')
angler, angler_text = read_prefab('AnglerFishTrap')
chandelier, chandelier_text = read_prefab('FallingChandelierTrap')
assert yaml.safe_load((ROOT/'Assets/Resources/Traps/ShoppingCartTrap.prefab.meta').read_text())['guid'] == '8af0ed28b9754f0db4a24380422d2ccc'
assert yaml.safe_load((ROOT/'Assets/Resources/Traps/FallingChandelierTrap.prefab.meta').read_text())['guid'] == '6eb0066e178747908afaee46ffb8e096'
for name, prefab in [('tablecart',cart),('angler',angler),('chandelier',chandelier)]:
    present = prefab[31000123]
    assert present['_DangerPattern']['guid'] == '605180dacd324b3893a1bde3fef7d1bf'
    assert present['_RangeMarker']['guid'] == 'ad74e6bf18f9482baf05852f6c78e8ab'
    assert present['_PatternRenderer']['fileID'] == 92000102
    assert len(present['_WorldImages']) == len(present['_WorldArt'])
    for image, renderer in zip(present['_WorldImages'],present['_WorldArt']):
        assert image['fileID'] in prefab and renderer['fileID'] in prefab
    assert prefab[31000999]['_Collider']['fileID'] == 31000094
    assert prefab[31000122]['m_Color'] == {'r':1,'g':1,'b':1,'a':1}

assert cart[31000999]['_FamilyTag'] == 'cart'
assert cart[31000999]['_DisplayName'] == 'Tablecart'
assert cart[31000999]['_Icon']['fileID'] == 21300002
cart_sprites={s['internalID']:s for s in sprite_meta('TRAP-Hotel-cart.png')['TextureImporter']['spriteSheet']['sprites']}
assert cart_sprites[21300002]['rect']=={'serializedVersion':2,'x':201,'y':235,'width':1659,'height':1092}
assert cart[31000122]['m_Sprite']['fileID']==21300000
assert cart[31000999]['_Impact'] == 1
assert cart[31000999]['_StunSeconds'] == 1
assert cart[31000999]['_KnockbackDistance'] == 1.2
assert cart[31000999]['_KnockbackSeconds'] == .22
# Every nontransparent tablecart pixel is covered by the authored solid parts.
# Pixel centers are mapped through the exact crop envelope, without editing art.
im = Image.open(ART/'TRAP-Hotel-cart.png').convert('RGBA')
a = im.getchannel('A'); x0,y0,x1,y1 = a.getbbox()
body=cart[31000094]['m_Size']; ppu=(x1-x0)/body['x']
assert abs(body['y']-(y1-y0)/ppu)<1e-7
assert cart[31000999]['_ObjectSize'] == {'x':body['x'],'y':body['y']}
parts=[]
for ref in cart[31000999]['_SolidParts']:
    part=cart[ref['fileID']]
    assert part['m_Enabled'] == 1 and part['m_IsTrigger'] == 0
    size,center = part['m_Size'],part['m_Center']
    parts.append((center['x']-size['x']/2,center['y']-size['y']/2,center['x']+size['x']/2,center['y']+size['y']/2))
count=0
for y in range(y0,y1):
    for x in range(x0,x1):
        if not a.getpixel((x,y)): continue
        wx=(x+.5-(x0+x1)/2)/ppu; wy=((y0+y1)/2-y-.5)/ppu
        assert any(l-1e-7<=wx<=r+1e-7 and b-1e-7<=wy<=t+1e-7 for l,b,r,t in parts),(x,y,'uncovered tablecart pixel')
        count+=1

trap=angler[31000999]; present=angler[31000123]
assert (trap['_FamilyTag'],trap['_InputMode'],trap['_Behaviour'],trap['_Impact']) == ('angler',0,3,2)
assert trap['_PiercingSpeed']==14 and trap['_BodySpeedFactor']==.25 and trap['_TravelHalfWidth']==6
assert present['_PossessedLight']['fileID']==93000022 and angler[93000022]['m_Enabled']==0
assert present['_PossessedGlowReplacesBody']==1
assert present['_PossessedGlow']['fileID']==93000000 and angler[93000000]['m_IsActive']==0
assert angler[93000001]['m_Father']['fileID']==31000120
assert angler[93000003]['m_Sprite']['guid']==sprite_meta('TRAP-Angler-fish-glow.png')['guid']
assert {'fileID':93000003} in present['_WorldImages'] and {'fileID':93000012} in present['_WorldArt']
for ref in trap['_SolidParts']: assert angler[ref['fileID']]['m_Enabled']==1

for version in (1,2,3):
    name=f'TRAP-Chandelier-v{version}.png'
    meta=sprite_meta(name)['TextureImporter']; sprites={s['internalID']:s for s in meta['spriteSheet']['sprites']}
    assert meta['spriteMode']==2
    assert sprites[21300000]['rect']=={'serializedVersion':2,'x':0,'y':0,'width':2048,'height':2048}
    assert sprites[21300002]['rect']=={'serializedVersion':2,'x':0,'y':1788,'width':2048,'height':260}
    assert sprites[21300004]['rect']=={'serializedVersion':2,'x':0,'y':0,'width':2048,'height':1788}
    # There really is a hand-drawn black cord through the apparent empty gap.
    pixels=Image.open(ART/name).convert('RGBA')
    for y in (260,300,400,500,600):
        assert any(pixels.getpixel((x,y))[3]>128 for x in range(1015,1033)),(name,'missing cord',y)

body=chandelier[31000094]['m_Size']
assert abs(body['x']/body['y']-1826/1830)<1e-8 and body['y']==1.9
assert 4.11-body['y']>2.0 # standing fish clearance below the armed art
present=chandelier[31000123]
assert present['_SuspendedPivot']['fileID']==94000001
assert present['_FixedCeilingLayout']['fileID']==94000021
assert chandelier[94000001]['m_Pivot']=={'x':.5,'y':1788/2048}
assert chandelier[94000001]['m_Father']['fileID']==31000120
assert chandelier[94000011]['m_Father']['fileID']==94000001
assert chandelier[94000021]['m_Father']['fileID']==31000104
assert chandelier[94000031]['m_Father']['fileID']==94000021
assert chandelier[94000011]['m_AnchorMin']=={'x':0,'y':0}
assert chandelier[94000011]['m_AnchorMax']=={'x':1,'y':1788/2048}
assert chandelier[94000031]['m_AnchorMin']=={'x':0,'y':1788/2048}
assert chandelier[94000031]['m_AnchorMax']=={'x':1,'y':1}
assert chandelier[31000122]['m_Enabled']==0
assert {'fileID':31000122} not in present['_WorldImages'] # no combined image under split layers
assert chandelier[94000013]['m_Sprite']['fileID']==21300004
assert chandelier[94000033]['m_Sprite']['fileID']==21300002
# Rectangles partition original canvas exactly. Sway keeps the top of the cord
# fixed, while its bottom changes; reset is the authored identity pose.
join=(1024,260)
for degrees in (-3,0,3):
    angle=math.radians(degrees)
    def rotate(point):
        x,y=point[0]-join[0],point[1]-join[1]
        return (join[0]+math.cos(angle)*x-math.sin(angle)*y,join[1]+math.sin(angle)*x+math.cos(angle)*y)
    assert rotate(join)==join
    if degrees: assert abs(rotate((1024,1933))[0]-1024)>80
    else: assert rotate((1024,1933))==(1024,1933)

source=(ROOT/'Assets/Scripts/Multiplayer/GhostTrapAreaPresentation.cs').read_text()
assert '_FixedCeilingLayout.position = camera.WorldToScreenPoint(_Origin + _Trap.ArmedOffset +' in source
assert '_FixedCeilingLayout.pivot = _Cart.pivot;' in source and '_FixedCeilingLayout.sizeDelta = _Cart.sizeDelta;' in source
assert '_SuspendedRestRotation * Quaternion.Euler' in source and 'possessed && _Trap && _Trap.Phase == TrapPhase.Armed' in source
assert 'void OnDisable()\n        {\n            PresentPossession(false);' in source
assert source.index('PresentPossession(possessed);') < source.index('UpdateWorldArt(camera);')
assert 'image.rectTransform.GetWorldCorners(_Corners)' in source
assert '_CartImage.enabled = false' in source
assert '_PossessedGlowReplacesBody && IsPossessed && _PossessedGlow' in source
assert '_PossessedGlow.activeInHierarchy && image == _CartImage' in source
assert 'image != _AreaImage && !replacedBody &&' in source
for forbidden in ('new GameObject(', 'AddComponent<', 'Sprite.Create(', 'Texture2D('): assert forbidden not in source
assert '_Tiles' in source and '_SharedTime' in source
shader=(ROOT/'Assets/Resources/GhostTrapRange.shader').read_text()
assert '_SharedTime' in shader
scene=(ROOT/'Assets/Scenes/Game.unity').read_text()
rows=re.findall(r'  - Prefab: \{fileID: 31000999, guid: (\w+), type: 3\}\n    DisplayArt: \{fileID: (\d+), guid: (\w+), type: 3\}\n    Weight: (\d+)',scene)
assert len(rows)==4
angler_guid=yaml.safe_load((ROOT/'Assets/Resources/Traps/AnglerFishTrap.prefab.meta').read_text())['guid']
assert (angler_guid,'21300000',sprite_meta('TRAP-Angler-fish.png')['guid'],'2') in rows
print(f'PASS: 6 unchanged source PNG hashes; 3 valid prefab reference trees; {count} tablecart alpha pixels covered; aspect-correct cart; authored angler piercing/glow/light; split canopy/cord and fixed-anchor/reset source guards; original range art; 4-entry catalog. Not native Unity import/render/physics/network validation.')
