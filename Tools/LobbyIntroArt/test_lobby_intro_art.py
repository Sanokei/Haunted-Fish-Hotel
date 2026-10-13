"""Source contracts for supplied intro art. These do not stand in for Unity renders."""
from pathlib import Path
import hashlib
import re
import sys
import tempfile
import unittest

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools/ScenePresentationTests"))
from verify_scene_assets import records, value, local_ref, vector


class LobbyIntroArtTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source = (ROOT / "Assets/Resources/LobbyIntroductionUI.prefab").read_text()
        cls.docs = {d.ident:d for d in records(cls.source)}

    def test_original_png_bytes_and_import_settings(self):
        hashes = {
            "EXT-Gate": "7acaa1157bb8c01a4096ed7fcfb17abbc38522215b4ba77b1d1d0c0d9b2da75d",
            "EXT-Hotel-filled": "3f3b6c45278b11ddb1a8463d8ed53c7ae0b0dfcf19737f610c8cc82d56c2826f",
            "EXT-Hotel-outline": "37bd540652ecac3f5096653e09795df219b335cafdfaf11d38cc9bf5a6f6fbc0",
            "EXT-Tree-darker": "475c532a1147736fc3e678f9d9ee913aacca2a1b84d57b69e362c68b9bd96199",
            "EXT-Tree": "accc0fa1e89c345af28e53d557001daef5e9f0bafaeaae1b8d8de0ea8cb15d5d",
        }
        for name, expected in hashes.items():
            p=ROOT / "Assets/Art/LobbyIntro" / (name+".png")
            self.assertEqual(hashlib.sha256(p.read_bytes()).hexdigest(),expected,name)
            meta=p.with_name(p.name+".meta").read_text()
            for field in ["spriteMode: 1","alphaIsTransparency: 1","enableMipMap: 0","textureType: 8"]:
                self.assertIn(field,meta)

    def test_original_art_is_bound_to_existing_roles(self):
        bindings = {
            2601155217534826120:"b3ede863df795e6f83af75c35349e163",  # hotel
            2060449895393529573:"60eb0df2757853bca211af49d6fe4084",  # gate
            3125165777140928717:"7e72e811194b5fa0a4447b8a2c02c66e",  # left tree
            2168401042541769914:"0778e14b127357b78d296768223bbe5f",  # right tree
            1448289847570732678:"ee777e93e2335dd98078acf625184a71",  # knock door
            7101946521820841736:"ee777e93e2335dd98078acf625184a71",  # lower door
        }
        for ident,guid in bindings.items():
            self.assertIn(guid,value(self.docs[ident].body,"m_Sprite"))
            self.assertEqual(value(self.docs[ident].body,"m_PreserveAspect"),"1")
            self.assertEqual(value(self.docs[ident].body,"m_RaycastTarget"),"0")

    def test_supplied_letter_source_is_archived_unchanged(self):
        source=ROOT / "Assets/Art/LobbyIntro/Letter-source.png"
        self.assertEqual(hashlib.sha256(source.read_bytes()).hexdigest(),
                         "15fd9ec044df3b738f6e9ff4a0d6c8305cc0ef034a50cd7de3c43617ae45955c")
        with Image.open(source) as image:
            self.assertEqual(image.size,(2048,1271))

    def test_letter_pixels_and_handwriting_are_losslessly_preserved(self):
        source=Image.open(ROOT / "Assets/Art/LobbyIntro/Letter-source.png").convert("RGBA")
        for name,box in [("letter",(382,189,883,516)),("letter_read",(1213,146,1730,892))]:
            path=ROOT / "Assets/Art/LobbyIntro" / (name+".png")
            sprite=Image.open(path).convert("RGBA")
            original=source.crop(box)
            self.assertEqual(sprite.size,original.size)
            self.assertEqual(sprite.convert("RGB").tobytes(),original.convert("RGB").tobytes(),name)
            source_pixels=original.tobytes();sprite_pixels=sprite.tobytes()
            self.assertTrue(all(sprite_pixels[i+3]==255 for i in range(0,len(source_pixels),4)
                                if min(source_pixels[i:i+3])<255),name+" retains every drawn pixel")
            alpha=sprite.getchannel("A")
            self.assertEqual(alpha.getextrema(),(0,255))
            self.assertEqual(alpha.getpixel((sprite.width//2,sprite.height//2)),255)
            for xy in [(0,0),(sprite.width-1,0),(0,sprite.height-1),(sprite.width-1,sprite.height-1)]:
                self.assertEqual(alpha.getpixel(xy),0)
            metadata=path.with_name(path.name+".meta").read_text()
            for contract in ["spriteMode: 1","alphaIsTransparency: 1","enableMipMap: 0",
                             "textureType: 8","spriteGenerateFallbackPhysicsShape: 0","nPOTScale: 0"]:
                self.assertIn(contract,metadata)
            self.assertNotIn("textureCompression: 1",metadata)

    def test_different_source_cannot_overwrite_approved_archive(self):
        import segment_letter
        source=ROOT / "Assets/Art/LobbyIntro/Letter-source.png"
        before=source.read_bytes()
        with tempfile.TemporaryDirectory() as temp:
            other=Path(temp)/"different.png"
            other.write_bytes(b"not the approved source image")
            with self.assertRaisesRegex(ValueError,"Source differs"):
                segment_letter.segment(other)
        self.assertEqual(source.read_bytes(),before)

    def test_segmented_letter_sprites_replace_procedural_stationery(self):
        import author_lobby_intro
        for ident,name in [(4451160055209052605,"letter"),(2140734793831677871,"letter_read")]:
            body=self.docs[ident].body
            expected=author_lobby_intro.guid("Assets/Art/LobbyIntro/"+name+".png")
            self.assertIn(expected,value(body,"m_Sprite"))
            self.assertEqual(value(body,"m_Material"),"{fileID: 0}")
            self.assertEqual(value(body,"m_PreserveAspect"),"1")
            self.assertEqual(value(body,"m_Color"),"{r: 1, g: 1, b: 1, a: 1}")
            self.assertEqual(value(body,"m_RaycastTarget"),"0")
        # The original read/fullscreen targets and expansion survive; the page stays entirely visible.
        read=self.docs[4885660160476274963].body
        self.assertEqual(local_ref(read,"m_Father"),7322483714184217825)
        width,height=vector(value(read,"m_SizeDelta"))[:2]
        x,y=vector(value(read,"m_AnchoredPosition"))[:2]
        self.assertEqual((width,height),(330,470))
        self.assertLessEqual((abs(y)+height/2)*1.4,360)
        self.assertLess(width,538.6666-120)

    def test_blockout_labels_cannot_render(self):
        labels=[d for d in self.docs.values() if d.kind==1 and
                re.search(r"  m_Name: (Label|Background label)\n",d.body)]
        self.assertEqual(len(labels),14)
        self.assertTrue(all(value(d.body,"m_IsActive")=="0" for d in labels))

    def test_lower_door_does_not_occlude_arrived_envelope(self):
        door=self.docs[6628160514351308231].body
        door_y=vector(value(door,"m_AnchoredPosition"))[1]
        door_h=vector(value(door,"m_SizeDelta"))[1]
        bottom=door_y-door_h/2
        self.assertEqual(bottom,85)
        floor_y=vector(value(self.docs[5810841128683655720].body,"m_AnchoredPosition"))[1]
        letter_half=vector(value(self.docs[1298306737601677680].body,"m_SizeDelta"))[1]/2
        self.assertLess(floor_y+50+letter_half,bottom)

    def test_authored_rain_has_separate_depths_and_never_builds_objects(self):
        rain=[d for d in self.docs.values() if d.kind==1 and re.search(r"  m_Name: (Storm|Exterior) rain -",d.body)]
        self.assertEqual(len(rain),5)
        for layer in ["RainFar","RainMid","RainNear"]:
            mat=(ROOT/"Assets/Art/LobbyIntro/Materials"/(layer+".mat")).read_text()
            self.assertRegex(mat,r"_Mode: 1\n")
            self.assertRegex(mat,r"_Opacity: 0\.")
        shader=(ROOT/"Assets/Art/LobbyIntro/LobbyIntroWeather.shader").read_text()
        for contract in ["Blend SrcAlpha OneMinusSrcAlpha","fwidth(p.x)","float taper", "float breakup", "lampVisibility", "float ripple", "UNITY_UI_CLIP_RECT", "Stencil"]:
            self.assertIn(contract,shader)
        self.assertFalse(list((ROOT/"Assets/Art/LobbyIntro").rglob("*.cs")))

    def test_flash_skip_and_world_hide_contracts_are_retained(self):
        story=self.docs[7905806835003860482].body
        for field,ident in [("_Content",717384140753780425),("_WhiteFrame",1044326656756107573),("_SkipProgress",3092689425013262978)]:
            self.assertEqual(local_ref(story,field),ident)
        self.assertIn("- {fileID: 1235209454757460436}",story)  # viewport hides every weather layer for world shots
        flash=self.docs[9093068311631748104].body
        self.assertEqual(value(flash,"m_Color"),"{r: 1, g: 1, b: 1, a: 1}")
        self.assertEqual(value(flash,"m_Material"),"{fileID: 0}")
        self.assertEqual(value(flash,"m_Maskable"),"0")
        self.assertIn("  m_SortingOrder: 32767",self.source)
        self.assertIn("  m_MethodName: RequestSkip",self.source)

    def test_camera_rig_uses_authored_depths_and_simultaneous_crane_push(self):
        story=self.docs[7905806835003860482].body
        rig=story.split("  _ParallaxRigs:\n",1)[1].split("  _Prefabs:",1)[0]
        self.assertIn("- Tag: exterior",rig)
        self.assertIn("ProjectionOrigin: {x: 0, y: 0}",rig)
        self.assertIn("- Tag: wide\n      Camera: {x: 0, y: 0}\n      Push: 0",rig)
        self.assertIn("- Tag: gate_arrival\n      Camera: {x: 0, y: -140}\n      Push: 0.4",rig)
        layers={int(i):float(depth) for i,depth in re.findall(r"Target: \{fileID: (\d+)\}\n      Depth: ([0-9.]+)",rig)}
        self.assertEqual(len(layers),8)
        self.assertLess(layers[321912153842628392],layers[8785289786444420281])
        self.assertLess(layers[8785289786444420281],layers[7402074234106522035])
        for ident,depth in layers.items():
            body=self.docs[ident].body
            self.assertEqual(local_ref(body,"m_Father"),7828835273548323913)
            self.assertEqual(value(body,"m_AnchorMin"),"{x: 0.5, y: 0.5}")
            self.assertEqual(value(body,"m_AnchorMax"),"{x: 0.5, y: 0.5}")
            self.assertGreater(depth-.4,.05)
        # Evaluate serialized projection all along the move, not just its endpoints.
        for phase in [i/100 for i in range(101)]:
            t=phase**3*(phase*(phase*6-15)+10)
            for ident in [71778210276053668,70824330142336985,56783861116141782,11742778312899493]:
                body=self.docs[ident].body
                x,y=vector(value(body,"m_AnchoredPosition"))[:2]
                w,h=vector(value(body,"m_SizeDelta"))[:2]
                depth=layers[ident];scale=depth/(depth-.4*t)
                y=(y+140*t/depth)*scale
                self.assertLessEqual(x*scale-w*scale/2,-640)
                self.assertGreaterEqual(x*scale+w*scale/2,640)
                self.assertLessEqual(y-h*scale/2,-360)
                self.assertGreaterEqual(y+h*scale/2,360)
            ground=self.docs[3403337161785866876].body
            y=vector(value(ground,"m_AnchoredPosition"))[1]
            h=vector(value(ground,"m_SizeDelta"))[1]
            d=layers[3403337161785866876]
            self.assertLessEqual((y-h/2+140*t/d)*d/(d-.4*t),-360)
        ink=(ROOT/"Assets/Dialogue/LobbyIntroduction.ink").read_text()
        self.assertLess(ink.index('ParallaxTo("exterior", "wide"'),ink.index('~ FlashFrames(2)'))
        self.assertLess(ink.index('~ FlashFrames(2)'),ink.index('ParallaxTo("exterior", "gate_arrival"'))
        self.assertNotIn('~ DollyTo(',ink)

    def test_replay_resets_old_visibility_without_creating_ui(self):
        ink=(ROOT/"Assets/Dialogue/LobbyIntroduction.ink").read_text()
        calls=re.findall(r"^~ (.+)$",ink,re.M)
        self.assertEqual(calls[:2],["SetVisible(\"exterior\", false)","SetVisible(\"letter_read\", false)"])
        self.assertNotIn("CreateUI",ink)
        for operation in ["FlashFrames(2)","ParallaxTo(\"exterior\", \"gate_arrival\", 5.0, \"smoother\")","LobbyShot(\"entry\", 1.5)","LobbyShot(\"reception\", 4.0)"]:
            self.assertIn(operation,calls)


if __name__=="__main__":unittest.main()
