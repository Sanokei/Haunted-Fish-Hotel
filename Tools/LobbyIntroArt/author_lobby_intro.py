#!/usr/bin/env python3
"""Offline, identity-preserving authoring of the existing introduction prefab.

Imports the supplied PNGs without changing their bytes. No runtime construction.
This is not an Editor import or a native rendering test.
"""
from pathlib import Path
import argparse
import hashlib
import re
import shutil
import uuid

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "Assets/Art/LobbyIntro"
PREFAB = ROOT / "Assets/Resources/LobbyIntroductionUI.prefab"
NS = uuid.UUID("10f3b273-cebc-4390-9877-a27297dc344f")
DOC = re.compile(r"^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)", re.M | re.S)


def guid(path):
    return uuid.uuid5(NS, str(path)).hex


def meta(path, importer):
    path.with_name(path.name + ".meta").write_text(
        f"fileFormatVersion: 2\nguid: {guid(path.relative_to(ROOT))}\n{importer}:\n"
        "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def material(name, mode, **values):
    path = ART / "Materials" / (name + ".mat")
    floats = dict(_Mode=mode, _Density=38, _Speed=1.1, _Wind=0.14, _Seed=0,
                  _Opacity=0.3, _Length=0.32, _Width=0.035, _Aspect=1280/720,
                  _Light=1, _StencilComp=8, _Stencil=0, _StencilOp=0,
                  _StencilWriteMask=255, _StencilReadMask=255, _ColorMask=15,
                  _UseUIAlphaClip=0)
    floats.update(values)
    source = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 4800000, guid: {guid('Assets/Art/LobbyIntro/LobbyIntroWeather.shader')}, type: 3}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 0
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {{}}
  disabledShaderPasses: []
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _MainTex:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    m_Ints: []
    m_Floats:
"""
    source += "".join(f"    - {k}: {v}\n" for k,v in floats.items())
    source += "    m_Colors:\n    - _Color: {r: 1, g: 1, b: 1, a: 1}\n  m_BuildTextureStacks: []\n  m_AllowLocking: 1\n"
    path.write_text("\n".join(line.rstrip() for line in source.splitlines())+"\n")
    meta(path, "NativeFormatImporter")
    path.with_name(path.name + ".meta").write_text(path.with_name(path.name + ".meta").read_text().replace("  externalObjects: {}", "  externalObjects: {}\n  mainObjectFileID: 2100000"))
    return f"{{fileID: 2100000, guid: {guid(path.relative_to(ROOT))}, type: 2}}"


class Prefab:
    def __init__(self):
        self.source = PREFAB.read_text()
        self.docs = {int(m[2]): [int(m[1]),m[3]] for m in DOC.finditer(self.source)}
        self.authored_ids = set()

    def field(self, ident, key, value):
        kind, body = self.docs[ident]
        body, count = re.subn(r"^  " + re.escape(key) + r": [^\n]*", f"  {key}: {value}", body, count=1, flags=re.M)
        assert count, (ident,key)
        self.docs[ident] = [kind,body]

    def children(self, ident, ids):
        kind, body = self.docs[ident]
        raw = "\n" + "".join(f"  - {{fileID: {i}}}\n" for i in ids) if ids else " []\n"
        self.docs[ident] = [kind,re.sub(r"  m_Children:.*?(?=  m_Father:)", "  m_Children:"+raw, body, flags=re.S)]

    def color(self, ident, rgba):
        self.field(ident, "m_Color", "{"+", ".join(f"{k}: {v}" for k,v in zip("rgba",rgba))+"}")

    def rect(self, ident, xy, size):
        self.field(ident,"m_AnchoredPosition",f"{{x: {xy[0]}, y: {xy[1]}}}")
        self.field(ident,"m_SizeDelta",f"{{x: {size[0]}, y: {size[1]}}}")

    def image(self, name, parent, xy=(0,0), size=(1280,720), sprite=None, mat=None, color=(1,1,1,1)):
        ids = [int(hashlib.sha256((name+":"+str(n)).encode()).hexdigest()[:14],16) for n in range(4)]
        original = [143456720451019260,6996907180247280785,3593022274128384702,1028541734010488956]
        for old,new in zip(original,ids):
            kind, body = self.docs[old]
            for a,b in zip(original,ids): body=body.replace(f"fileID: {a}",f"fileID: {b}")
            self.docs[new] = [kind,body]
            self.authored_ids.add(new)
        go,rect,renderer,img = ids
        self.field(go,"m_Name",name)
        self.field(go,"m_IsActive",1)
        self.children(rect,[])
        self.field(rect,"m_Father",f"{{fileID: {parent}}}")
        self.field(rect,"m_LocalScale","{x: 1, y: 1, z: 1}")
        self.rect(rect,xy,size)
        self.field(img,"m_Sprite",sprite or "{fileID: 0}")
        self.field(img,"m_Material",mat or "{fileID: 0}")
        self.field(img,"m_PreserveAspect",0)
        self.color(img,color)
        return rect

    def save(self):
        for ident in self.authored_ids:
            self.docs[ident][1] = "\n".join(line.rstrip() for line in self.docs[ident][1].splitlines())+"\n"
        PREFAB.write_text("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"+"".join(f"--- !u!{kind} &{ident}\n{body}" for ident,(kind,body) in self.docs.items()))


def main():
    ap=argparse.ArgumentParser();ap.add_argument("--source-dir",type=Path);args=ap.parse_args()
    if args.source_dir:
        template=(ROOT/"Assets/Art/Hallway/HALLWAY-Door-full.png.meta").read_text()
        for name in ["EXT-Tree-darker.png","EXT-Hotel-filled.png","EXT-Gate.png","EXT-Tree.png","EXT-Hotel-outline.png"]:
            dest=ART/name;shutil.copyfile(args.source_dir/name,dest)
            s=re.sub(r"guid: \w+",f"guid: {guid(dest.relative_to(ROOT))}",template,count=1)
            s=re.sub(r"spriteID: \w+",f"spriteID: {guid(str(dest.relative_to(ROOT))+':sprite')}",s)
            s=s.replace("spriteGenerateFallbackPhysicsShape: 1","spriteGenerateFallbackPhysicsShape: 0")
            dest.with_name(dest.name+".meta").write_text("\n".join(line.rstrip() for line in s.splitlines())+"\n")
    meta(ART/"LobbyIntroWeather.shader","ShaderImporter")
    sky=material("NightClouds",0)
    ground=material("WetDriveway",2,_Opacity=0.42,_Aspect=1280/350)
    far=material("RainFar",1,_Density=67,_Speed=0.38,_Wind=0.13,_Length=0.21,_Width=0.018,_Opacity=0.35,_Seed=2.3,_Light=0.4)
    mid=material("RainMid",1,_Density=39,_Speed=0.66,_Wind=0.16,_Length=0.32,_Width=0.026,_Opacity=0.44,_Seed=17.1)
    near=material("RainNear",1,_Density=23,_Speed=0.98,_Wind=0.20,_Length=0.46,_Width=0.031,_Opacity=0.30,_Seed=39.7,_Light=0.65)
    glow=material("GateLampGlow",3,_Opacity=0.13)
    paper=material("BlankLetterPaper",4)
    envelope=material("EnvelopePaper",5)
    def sprite(name):return f"{{fileID: 21300000, guid: {guid('Assets/Art/LobbyIntro/'+name+'.png')}, type: 3}}"
    p=Prefab()
    # Keep all original file IDs, binding tags, layouts, Ink timing, audio, skip, and independent flash canvas.
    # Authoring labels stay serialized but are invisible; no invented narrative replaces the blank letter.
    for ident,(kind,body) in list(p.docs.items()):
        if kind==1 and re.search(r"  m_Name: (Label|Background label)\n",body):p.field(ident,"m_IsActive",0)
    for image_id,name,tint in [
        (2601155217534826120,"EXT-Hotel-filled",(.73,.80,.91,1)),
        (2060449895393529573,"EXT-Gate",(.71,.73,.76,1)),
        (3125165777140928717,"EXT-Tree",(.40,.44,.54,1)),
        (2168401042541769914,"EXT-Tree-darker",(.31,.36,.47,1))]:
        p.field(image_id,"m_Sprite",sprite(name));p.color(image_id,tint);p.field(image_id,"m_PreserveAspect",1)
    door="{fileID: 21300000, guid: ee777e93e2335dd98078acf625184a71, type: 3}"
    for image_id in [1448289847570732678,7101946521820841736]:
        p.field(image_id,"m_Sprite",door);p.color(image_id,(.86,.89,.95,1));p.field(image_id,"m_PreserveAspect",1)
    # Door source includes transparent margin above its crest. Scale/offset from the actual alpha bounds.
    p.rect(3342913104293369868,(0,72),(440,644))
    # Keep the visible bottom at y=85: the envelope starts behind it, then slides below it.
    p.rect(6628160514351308231,(0,699),(840,1228))
    p.color(3720855840725677550,(.035,.045,.065,1))
    p.color(6349326198350239655,(.040,.050,.061,1))
    p.color(6225460243091785818,(.057,.052,.050,1))
    p.color(3408271380882960119,(.064,.060,.056,1))
    p.color(7760204798687381609,(.01,.014,.021,.55))
    p.field(4451160055209052605,"m_Material",envelope);p.color(4451160055209052605,(1,1,1,1))
    p.field(2140734793831677871,"m_Material",paper);p.color(2140734793831677871,(1,1,1,1))
    # Sky and wet ground span the whole authored viewport, with no bright horizon seam.
    p.field(4224427339354618196,"m_Material",sky);p.color(4224427339354618196,(1,1,1,1))
    p.field(6619972303101062993,"m_Material",sky);p.color(6619972303101062993,(1,1,1,1));p.rect(71778210276053668,(0,0),(1280,720))
    p.field(6408888311380578958,"m_Material",ground);p.color(6408888311380578958,(1,1,1,1));p.rect(3403337161785866876,(0,-190),(1280,340))
    p.rect(7402074234106522035,(0,35),(0,0));p.rect(4149885311197301764,(0,15),(580,580))
    p.rect(1137275075083366971,(-475,30),(770,770));p.rect(1572945775632438615,(500,10),(810,810))
    p.rect(321912153842628392,(0,-90),(0,0));p.rect(3192077876362570172,(0,0),(1420,594.21875))
    # Existing cloud variants retain the exact Ink tags. Artwork is dark/scattered, not a colored rectangle.
    for image_id,rect_id,tint in [(1028541734010488956,6996907180247280785,(.055,.076,.11,.14)),(4118464044493237038,8849920227451258767,(.25,.32,.40,.30))]:
        p.field(image_id,"m_Sprite","{fileID: 21300000, guid: 56ff9d14119656d42987ea9100a68ff0, type: 3}")
        p.color(image_id,tint);p.rect(rect_id,(80,255),(1250,380))
    exterior=7828835273548323913
    far_rect=p.image("Exterior rain - distant soft droplets",exterior,mat=far)
    mid_rect=p.image("Exterior rain - lamplit middle depth",exterior,mat=mid)
    near_rect=p.image("Exterior rain - sparse near droplets",exterior,mat=near)
    gate=321912153842628392
    left_glow=p.image("Gate lamp - left warm scatter",gate,(-456,145),(235,255),mat=glow)
    right_glow=p.image("Gate lamp - right warm scatter",gate,(456,145),(235,255),mat=glow)
    p.children(gate,[left_glow,right_glow,3192077876362570172])
    p.children(exterior,[71778210276053668,3403337161785866876,far_rect,7402074234106522035,mid_rect,8785289786444420281,321912153842628392,near_rect])
    # Opening storm is its own authored silhouette composition so flash/arrival never shares active state.
    storm=6232796351706565258
    silhouette=p.image("Storm distant hotel silhouette",storm,(95,-90),(720,720),sprite=sprite("EXT-Hotel-outline"),color=(.3,.4,.55,.86))
    tree_l=p.image("Storm left tree silhouette",storm,(-530,-35),(870,870),sprite=sprite("EXT-Tree-darker"),color=(.10,.16,.25,.85))
    tree_r=p.image("Storm right tree silhouette",storm,(555,-20),(820,820),sprite=sprite("EXT-Tree-darker"),color=(.08,.13,.23,.8))
    storm_far=p.image("Storm rain - distant soft droplets",storm,mat=far)
    storm_near=p.image("Storm rain - sparse near droplets",storm,mat=near)
    p.children(storm,[5937862503364382944,silhouette,storm_far,tree_l,tree_r,storm_near])
    # Reusable camera rig: all targets are authored siblings in the exterior's canvas space.
    # Overscan moving full-screen sky/rain layers so the crane never reveals their bounds.
    for layer in [71778210276053668, far_rect, mid_rect, near_rect]: p.rect(layer,(0,0),(1600,900))
    story_id=7905806835003860482
    kind,body=p.docs[story_id]
    body=re.sub(r"  _ParallaxRigs:.*?(?=  _Prefabs:)","",body,flags=re.S)
    binding="  _ParallaxRigs:\n  - Tag: exterior\n    ProjectionOrigin: {x: 0, y: 0}\n    Layers:\n"
    for ident,depth in [(71778210276053668,12),(3403337161785866876,2.2),(far_rect,7),
                        (7402074234106522035,3.5),(mid_rect,2.7),(8785289786444420281,1.9),
                        (321912153842628392,1.5),(near_rect,1.3)]:
        binding+=f"    - Target: {{fileID: {ident}}}\n      Depth: {depth}\n"
    binding+="    Poses:\n    - Tag: wide\n      Camera: {x: 0, y: 0}\n      Push: 0\n"
    binding+="    - Tag: gate_arrival\n      Camera: {x: 0, y: -140}\n      Push: 0.4\n"
    p.docs[story_id]=[kind,body.replace("  _Prefabs:",binding+"  _Prefabs:")]
    p.save()
    print("Authored supplied exterior art, hallway doors, stationery materials and five rain layers. Existing bindings preserved.")


if __name__=="__main__":main()
