#!/usr/bin/env python3
"""Import byte-identical Kenney Input Prompts and validate committed provenance.

Usage (from any working directory):
  python Tools/InputPromptTests/import_glyphs.py --archive /path/to/kenney_input-prompts_1.5.zip
  python Tools/InputPromptTests/import_glyphs.py --verify
  python Tools/InputPromptTests/import_glyphs.py --verify --contact-sheet /tmp/glyphs.png

Import requires Pillow to assemble the TMP atlas using exact pixel copies.
Verification uses the standard library for hashes, plus Pillow when available
to compare every atlas cell against its source. The optional contact sheet
also requires Pillow, and never changes imported asset pixels.
"""

import argparse
import hashlib
import json
from pathlib import Path
import string
import struct
import zipfile


ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / "Assets/Art/InputPrompts"
PAGE_URL = "https://kenney.nl/assets/input-prompts"
ARCHIVE_URL = "https://kenney.nl/media/pages/assets/input-prompts/8de120163f-1783763952/kenney_input-prompts_1.5.zip"
# Pin the exact archive, rather than silently importing a future pack revision.
ARCHIVE_SHA256 = "ac2fcf599080b0f3ba2d174c9474db6df1a0e96ff0662580e2da79a122ab78a1"
FAMILIES = {"KeyboardMouse": 0, "Xbox": 1, "PlayStation": 2, "GenericGamepad": 3}
ATLAS_PATH = "Assets/Art/InputPrompts/InputPromptAtlas.png"
TMP_PATH = "Assets/Art/InputPrompts/InputPromptSprites.asset"
TMP_NAME = "InputPromptSprites"
TMP_SCRIPT_GUID = "84a92b25f83d49b9bc132d206b370281"
TMP_SHADER_GUID = "cf81c85f95fe47e1a27f6ae460cf182c"
TMP_MATERIAL_FILE_ID = 2100000
ATLAS_SIZE = 2048
CELL_SIZE = 128


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def guid_for(relative_path):
    """Stable project-specific GUIDs; moving a source file changes its GUID."""
    return sha256(("haunted-fish-hotel:input-prompts:" + relative_path).encode())[:32]


def selection():
    """Yield family, Unity control path and the untouched archive member."""
    def keyboard(control, stem):
        return ("KeyboardMouse", "<Keyboard>/" + control,
                "Keyboard & Mouse/Double/keyboard_" + stem + ".png")

    for key in string.ascii_lowercase + string.digits:
        yield keyboard(key, key)
    for number in range(1, 13):
        yield keyboard("f" + str(number), "f" + str(number))
    for control, stem in {
        "upArrow": "arrow_up", "downArrow": "arrow_down",
        "leftArrow": "arrow_left", "rightArrow": "arrow_right",
        "leftShift": "shift", "rightShift": "shift", "shift": "shift",
        "leftCtrl": "ctrl", "rightCtrl": "ctrl", "ctrl": "ctrl",
        "leftAlt": "alt", "rightAlt": "alt", "alt": "alt",
        "space": "space", "enter": "enter", "escape": "escape", "tab": "tab",
        "backspace": "backspace", "delete": "delete", "insert": "insert",
        "home": "home", "end": "end", "pageUp": "page_up", "pageDown": "page_down",
        "capsLock": "capslock", "numLock": "numlock", "scrollLock": "scroll_lock",
        "pause": "pause", "printScreen": "printscreen", "backquote": "tilde",
        "quote": "apostrophe", "semicolon": "semicolon", "comma": "comma",
        "period": "period", "slash": "slash_forward", "backslash": "slash_back",
        "leftBracket": "bracket_open", "rightBracket": "bracket_close",
        "minus": "minus", "equals": "equals", "anyKey": "any",
        "numpadEnter": "numpad_enter", "numpadPlus": "numpad_plus",
        "numpadMinus": "minus", "numpadMultiply": "asterisk",
        "numpadDivide": "slash_forward", "numpadPeriod": "period",
    }.items():
        yield keyboard(control, stem)
    for number in range(10):
        yield keyboard("numpad" + str(number), str(number))
    # No WASD or AD graphic exists in the upstream pack. Compose actual bound
    # individual key sprites in the presentation layer instead of inventing art.
    yield keyboard("arrows", "arrows_all")
    for control, stem in {
        "<Mouse>/leftButton": "mouse_left", "<Mouse>/rightButton": "mouse_right",
        "<Mouse>/middleButton": "mouse_scroll", "<Mouse>/scroll": "mouse_scroll_vertical",
        "<Mouse>/scroll/y": "mouse_scroll_vertical", "<Mouse>/scroll/up": "mouse_scroll_up",
        "<Mouse>/scroll/down": "mouse_scroll_down", "<Mouse>/position": "mouse_move",
        "<Mouse>/delta": "mouse_move", "<Pointer>/position": "mouse_move",
        "<Pointer>/delta": "mouse_move", "<Mouse>/forwardButton": "mouse_side_forward",
        "<Mouse>/backButton": "mouse_side_back",
    }.items():
        yield "KeyboardMouse", control, "Keyboard & Mouse/Double/" + stem + ".png"

    gamepads = [
        ("Xbox", "Xbox Series", "xbox", {
            "buttonSouth": "xbox_button_a", "buttonEast": "xbox_button_b",
            "buttonWest": "xbox_button_x", "buttonNorth": "xbox_button_y",
            "leftShoulder": "xbox_lb", "rightShoulder": "xbox_rb",
            "leftTrigger": "xbox_lt", "rightTrigger": "xbox_rt",
            "start": "xbox_button_menu", "select": "xbox_button_view",
        }),
        ("PlayStation", "PlayStation Series", "playstation", {
            "buttonSouth": "playstation_button_cross", "buttonEast": "playstation_button_circle",
            "buttonWest": "playstation_button_square", "buttonNorth": "playstation_button_triangle",
            "leftShoulder": "playstation_trigger_l1", "rightShoulder": "playstation_trigger_r1",
            "leftTrigger": "playstation_trigger_l2", "rightTrigger": "playstation_trigger_r2",
            "start": "playstation4_button_options", "select": "playstation4_button_share",
            "touchpadButton": "playstation4_touchpad_press",
        }),
    ]
    for family, folder, prefix, controls in gamepads:
        for control, stem in controls.items():
            yield family, control, folder + "/Double/" + stem + ".png"
        for side, suffix in [("left", "l"), ("right", "r")]:
            for control_suffix, source_suffix in [
                ("", ""), ("Press", "_press"), ("/x", "_horizontal"), ("/y", "_vertical"),
                ("/up", "_up"), ("/down", "_down"), ("/left", "_left"), ("/right", "_right"),
            ]:
                yield family, side + "Stick" + control_suffix, (
                    folder + "/Double/" + prefix + "_stick_" + suffix + source_suffix + ".png")
        for control_suffix, source_suffix in [
            ("", "_all"), ("/x", "_horizontal"), ("/y", "_vertical"),
            ("/up", "_up"), ("/down", "_down"), ("/left", "_left"), ("/right", "_right"),
        ]:
            yield family, "dpad" + control_suffix, folder + "/Double/" + prefix + "_dpad" + source_suffix + ".png"

    # The Switch folder contains unlabeled positional face-button diagrams.
    # These indicate physical positions without claiming Xbox or PS labels.
    for control, direction in {
        "buttonSouth": "down", "buttonEast": "right", "buttonWest": "left", "buttonNorth": "up",
    }.items():
        yield "GenericGamepad", control, "Nintendo Switch/Double/switch_buttons_" + direction + ".png"
    for side in ["left", "right"]:
        for control_suffix, source_suffix in [
            ("", ""), ("Press", "_press"), ("/x", "_horizontal"), ("/y", "_vertical"),
            ("/up", "_up"), ("/down", "_down"), ("/left", "_left"), ("/right", "_right"),
        ]:
            yield "GenericGamepad", side + "Stick" + control_suffix, (
                "Generic/Double/generic_stick" + source_suffix + ".png")
    for control_suffix, source_suffix in [
        ("", "_all"), ("/x", "_horizontal"), ("/y", "_vertical"),
        ("/up", "_up"), ("/down", "_down"), ("/left", "_left"), ("/right", "_right"),
    ]:
        yield "GenericGamepad", "dpad" + control_suffix, "Nintendo Switch/Double/switch_dpad" + source_suffix + ".png"


def simple_meta(relative_path, folder=False):
    importer = "DefaultImporter" if folder else "TextScriptImporter"
    return ("fileFormatVersion: 2\nguid: " + guid_for(relative_path) + "\n"
            + ("folderAsset: yes\n" if folder else "")
            + importer + ":\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def sprite_meta(relative_path):
    return f"""fileFormatVersion: 2
guid: {guid_for(relative_path)}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 128
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 128
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: {guid_for(relative_path + ':sprite')}
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def png_dimensions(data):
    if data[:8] != b"\x89PNG\r\n\x1a\n" or data[12:16] != b"IHDR":
        raise ValueError("Expected a PNG with an IHDR chunk")
    return struct.unpack(">II", data[16:24])


def write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    if isinstance(data, str):
        data = data.encode("utf-8")
    path.write_bytes(data)


def tmp_hash(value):
    # TMP_TextUtilities.GetSimpleHashCode, matching the project's EmojiOne asset.
    result = 0
    for char in value:
        result = ((result * 33) ^ ord(char)) & 0xffffffff
    return result if result < 0x80000000 else result - 0x100000000


def atlas_meta():
    # A texture, not a Unity sprite sheet: TMP owns the glyph rects.
    return sprite_meta(ATLAS_PATH).replace("maxTextureSize: 128", "maxTextureSize: 2048").replace(
        "  spriteMode: 1", "  spriteMode: 0").replace("  textureType: 8", "  textureType: 0")


def tmp_meta():
    return (f"fileFormatVersion: 2\nguid: {guid_for(TMP_PATH)}\n"
            "NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def tmp_asset(entries):
    # Retain the installed project's TMP sprite serialization and built-in
    # shader/material properties. No dependency on a Unity Editor invocation.
    template = (ROOT / "Assets/TextMesh Pro/Resources/Sprite Assets/EmojiOne.asset").read_text()
    header = template.split("  m_SpriteCharacterTable:\n", 1)[0]
    header = header.replace("dffef66376be4fa480fb02b19edbe903", guid_for(ATLAS_PATH))
    header = header.replace("2103686", str(TMP_MATERIAL_FILE_ID))
    header = header.replace("m_Name: EmojiOne", "m_Name: " + TMP_NAME)
    header = header.replace("hashCode: -1836805472", "hashCode: " + str(tmp_hash(TMP_NAME)))
    header = header.replace("m_Name: TextMeshPro/Sprite", "m_Name: InputPromptSprites Material")
    # White originals should retain their unmodified colors, including position
    # highlights. Runtime inline tags should use tint=0 when parent text is tinted.
    chars, glyphs = [], []
    for entry in entries:
        index, name = entry["spriteIndex"], entry["spriteName"]
        rect = entry["atlasRect"]
        chars.append(f"""  - m_ElementType: 2
    m_Unicode: 65534
    m_GlyphIndex: {index}
    m_Scale: 1
    m_Name: {name}
    m_HashCode: {tmp_hash(name)}
""")
        glyphs.append(f"""  - m_Index: {index}
    m_Metrics:
      m_Width: 128
      m_Height: 128
      m_HorizontalBearingX: 0
      m_HorizontalBearingY: 115.6
      m_HorizontalAdvance: 128
    m_GlyphRect:
      m_X: {rect['x']}
      m_Y: {rect['y']}
      m_Width: 128
      m_Height: 128
    m_Scale: 1
    m_AtlasIndex: 0
    sprite: {{fileID: 21300000, guid: {entry['spriteGuid']}, type: 3}}
""")
    return (header + "  m_SpriteCharacterTable:\n" + "".join(chars)
            + "  m_SpriteGlyphTable:\n" + "".join(glyphs)
            + "  spriteInfoList: []\n  fallbackSpriteAssets: []\n")


def build_atlas(entries):
    from PIL import Image

    unique = list({e["relativePath"]: e for e in entries}.values())
    assert len(unique) <= (ATLAS_SIZE // CELL_SIZE) ** 2, "Atlas is full"
    atlas = Image.new("RGBA", (ATLAS_SIZE, ATLAS_SIZE), (0, 0, 0, 0))
    by_path = {}
    for index, entry in enumerate(unique):
        x, top = (index % 16) * CELL_SIZE, (index // 16) * CELL_SIZE
        sprite = Image.open(ROOT / entry["relativePath"]).convert("RGBA")
        # Deliberately no alpha mask: copy all RGBA channels, including alpha,
        # byte-for-byte. Using a mask here would multiply alpha a second time.
        atlas.paste(sprite, (x, top))
        by_path[entry["relativePath"]] = {
            "spriteIndex": index, "spriteName": f"g{index:03d}",
            "atlasRect": {"x": x, "y": ATLAS_SIZE - top - CELL_SIZE, "width": CELL_SIZE, "height": CELL_SIZE},
        }
    for entry in entries:
        entry.update(by_path[entry["relativePath"]])
    unique = list({e["relativePath"]: e for e in entries}.values())
    atlas.save(ROOT / ATLAS_PATH, compress_level=9)
    write(ROOT / (ATLAS_PATH + ".meta"), atlas_meta())
    write(ROOT / TMP_PATH, tmp_asset(unique))
    write(ROOT / (TMP_PATH + ".meta"), tmp_meta())
    return {
        "name": TMP_NAME, "relativePath": TMP_PATH, "guid": guid_for(TMP_PATH), "fileId": 11400000,
        "materialFileId": TMP_MATERIAL_FILE_ID, "scriptGuid": TMP_SCRIPT_GUID, "shaderGuid": TMP_SHADER_GUID,
        "sha256": sha256((ROOT / TMP_PATH).read_bytes()),
        "atlasRelativePath": ATLAS_PATH, "atlasGuid": guid_for(ATLAS_PATH),
        "atlasSha256": sha256((ROOT / ATLAS_PATH).read_bytes()),
        "atlasWidth": ATLAS_SIZE, "atlasHeight": ATLAS_SIZE,
        "spriteCount": len(unique),
        "assembly": "Exact RGBA pixel copies into 128px cells; no resize, crop, recolor or alpha multiplication.",
    }


def import_archive(archive_path):
    archive_bytes = archive_path.read_bytes()
    if sha256(archive_bytes) != ARCHIVE_SHA256:
        raise ValueError("Archive hash differs from the pinned official Kenney Input Prompts 1.5A archive")
    entries = []
    with zipfile.ZipFile(archive_path) as archive:
        for family, control_path, source in selection():
            image_bytes = archive.read(source)
            width, height = png_dimensions(image_bytes)
            if (width, height) != (128, 128):
                raise ValueError("Expected a 128 x 128 Double asset: " + source)
            relative_path = "Assets/Art/InputPrompts/" + source.replace("/Double/", "/")
            write(ROOT / relative_path, image_bytes)
            write(ROOT / (relative_path + ".meta"), sprite_meta(relative_path))
            entries.append({
                "family": family, "familyId": FAMILIES[family], "controlPath": control_path,
                "spriteGuid": guid_for(relative_path), "spriteFileId": 21300000,
                "relativePath": relative_path, "source": source, "sha256": sha256(image_bytes),
                "width": width, "height": height,
            })
        license_bytes = archive.read("License.txt")
        write(DEST / "License.txt", license_bytes)
    tmp_info = build_atlas(entries)
    manifest = {
        "schemaVersion": 1, "assetPack": "Kenney Input Prompts", "version": "1.5A",
        "license": "CC0-1.0", "licenseUrl": "https://creativecommons.org/publicdomain/zero/1.0/",
        "sourcePage": PAGE_URL, "archiveUrl": ARCHIVE_URL,
        "archiveSha256": ARCHIVE_SHA256, "licenseSha256": sha256(license_bytes),
        "transformation": "None. PNG files and License.txt are copied byte-for-byte from the archive.",
        "familyIds": FAMILIES,
        "tmpSpriteAsset": tmp_info,
        "notes": [
            "Original Double PNGs are 128 x 128 pixels. Source directory and image stems are preserved.",
            "No source WASD or AD artwork exists; compose the individual actual-bound-key sprites at runtime.",
            "Generic face glyphs are the unlabeled positional diagrams in the Nintendo Switch source folder.",
            "Generic sticks are unlettered; accompany them with the action or control caption when left/right matters.",
            "Generic shoulders, triggers and menu controls deliberately use the presenter's text fallback.",
            "PlayStation start/select use PS4 Options/Share artwork because one family covers PS4 and PS5.",
        ],
        "glyphs": entries,
    }
    write(DEST / "glyph-manifest.json", json.dumps(manifest, indent=2) + "\n")
    write(DEST / "SOURCE.txt", (
        "Kenney Input Prompts 1.5A\n\n"
        "Creator: Kenney (https://kenney.nl)\n"
        f"Official source page: {PAGE_URL}\nOfficial archive: {ARCHIVE_URL}\n"
        f"Archive SHA-256: {ARCHIVE_SHA256}\n"
        "License: Creative Commons Zero (CC0 1.0 Universal)\n"
        "License URL: https://creativecommons.org/publicdomain/zero/1.0/\n\n"
        "The bundled License.txt is copied byte-for-byte from the official archive.\n"
        "Every included PNG is an unmodified 128 x 128 Double image from that archive.\n"
        "No generative artwork, paint-over, recoloring, cropping or pixel resampling was used.\n"
        "glyph-manifest.json records each archive member, SHA-256, control path and Sprite GUID.\n"
        "Folder names and image stems preserve the source names; the Double path component is omitted.\n\n"
        "The positional generic face buttons come from Nintendo Switch/Double/switch_buttons_*.png.\n"
        "They contain positions, not Nintendo letter assignments. Generic sticks are unlettered.\n"
        "There are no WASD/AD composite images in this source; individual bound key sprites are composed in UI.\n\n"
        "Reproduce with Tools/InputPromptTests/import_glyphs.py --archive <downloaded archive>.\n"
        "Validate committed content with Tools/InputPromptTests/import_glyphs.py --verify.\n"
        "Unity import: Sprite (Single), full rect, alpha transparency, no mipmaps or compression,\n"
        "bilinear filtering, clamp wrap, 100 pixels per unit. Metadata GUIDs are deterministic.\n"
        "InputPromptAtlas.png copies each original's exact RGBA pixels into a 2048 x 2048 atlas.\n"
        "InputPromptSprites.asset is a TMP_SpriteAsset with an embedded TMP Sprite material.\n"
        "The atlas uses no resize, crop, recolor or alpha multiplication; each cell is 128 x 128.\n"
        "Manifest spriteName/spriteIndex/atlasRect identify each inline glyph. Use tint=0 to keep source colors.\n"
    ))
    for path in sorted(DEST.rglob("*")) + [DEST]:
        if path.is_dir():
            write(Path(str(path) + ".meta"), simple_meta(path.relative_to(ROOT).as_posix(), True))
        elif path.suffix in (".txt", ".json"):
            write(Path(str(path) + ".meta"), simple_meta(path.relative_to(ROOT).as_posix()))
    return manifest


def verify():
    manifest = json.loads((DEST / "glyph-manifest.json").read_text())
    assert manifest["archiveSha256"] == ARCHIVE_SHA256, "Wrong pinned archive"
    assert manifest["familyIds"] == FAMILIES, "Family enumeration drifted"
    assert sha256((DEST / "License.txt").read_bytes()) == manifest["licenseSha256"], "License changed"
    expected_selection = set(selection())
    actual_selection = set()
    keys = set()
    guid_paths = {}
    image_paths = set()
    for entry in manifest["glyphs"]:
        key = entry["family"], entry["controlPath"]
        assert key not in keys, "Duplicate family/control: " + str(key)
        keys.add(key)
        actual_selection.add((entry["family"], entry["controlPath"], entry["source"]))
        assert entry["familyId"] == FAMILIES[entry["family"]], "Wrong family ID"
        relative_path = entry["relativePath"]
        assert relative_path == "Assets/Art/InputPrompts/" + entry["source"].replace("/Double/", "/"), "Source path changed"
        data = (ROOT / relative_path).read_bytes()
        assert sha256(data) == entry["sha256"], "PNG bytes changed: " + relative_path
        assert png_dimensions(data) == (entry["width"], entry["height"]) == (128, 128), "PNG dimensions changed"
        assert entry["spriteGuid"] == guid_for(relative_path), "Sprite GUID changed"
        assert entry["spriteFileId"] == 21300000, "Wrong Sprite file ID"
        assert guid_paths.setdefault(entry["spriteGuid"], relative_path) == relative_path, "Sprite GUID collision"
        assert (ROOT / (relative_path + ".meta")).read_text() == sprite_meta(relative_path), "Sprite settings changed: " + relative_path
        image_paths.add(relative_path)
    assert expected_selection == actual_selection, "Selection differs from import definition"
    assert image_paths | {ATLAS_PATH} == {p.relative_to(ROOT).as_posix() for p in DEST.rglob("*.png")}, "Unexpected or missing PNG"
    info = manifest["tmpSpriteAsset"]
    unique = list({e["relativePath"]: e for e in manifest["glyphs"]}.values())
    assert info["spriteCount"] == len(unique), "Incorrect TMP glyph count"
    assert info["guid"] == guid_for(TMP_PATH) and info["atlasGuid"] == guid_for(ATLAS_PATH), "TMP GUID changed"
    assert sha256((ROOT / TMP_PATH).read_bytes()) == info["sha256"], "TMP asset changed"
    assert sha256((ROOT / ATLAS_PATH).read_bytes()) == info["atlasSha256"], "Atlas changed"
    assert (ROOT / TMP_PATH).read_text() == tmp_asset(unique), "TMP glyph mappings changed"
    assert (ROOT / (ATLAS_PATH + ".meta")).read_text() == atlas_meta(), "Atlas settings changed"
    assert (ROOT / (TMP_PATH + ".meta")).read_text() == tmp_meta(), "TMP metadata changed"
    assert png_dimensions((ROOT / ATLAS_PATH).read_bytes()) == (ATLAS_SIZE, ATLAS_SIZE), "Atlas dimensions changed"
    shader_meta = ROOT / "Assets/TextMesh Pro/Shaders/TMP_Sprite.shader.meta"
    assert "guid: " + TMP_SHADER_GUID in shader_meta.read_text(), "TMP Sprite shader reference is missing"
    try:
        from PIL import Image
    except ImportError:
        print("Pillow unavailable: atlas PNG hash verified, per-cell decoded pixel comparison skipped.")
    else:
        atlas = Image.open(ROOT / ATLAS_PATH).convert("RGBA")
        for index, entry in enumerate(unique):
            assert entry["spriteIndex"] == index and entry["spriteName"] == f"g{index:03d}", "Inline sprite IDs changed"
            rect = entry["atlasRect"]
            x, top = rect["x"], ATLAS_SIZE - rect["y"] - CELL_SIZE
            assert x == index % 16 * CELL_SIZE and top == index // 16 * CELL_SIZE, "Incorrect atlas rect"
            source = Image.open(ROOT / entry["relativePath"]).convert("RGBA")
            assert source.tobytes() == atlas.crop((x, top, x + CELL_SIZE, top + CELL_SIZE)).tobytes(), "Atlas pixels changed: " + entry["relativePath"]
    for path in sorted(DEST.rglob("*")) + [DEST]:
        if path.is_dir() or path.suffix in (".txt", ".json"):
            assert Path(str(path) + ".meta").read_text() == simple_meta(path.relative_to(ROOT).as_posix(), path.is_dir()), "Metadata changed: " + str(path)
    print(f"Verified {len(image_paths)} byte-pinned PNGs, {len(keys)} control mappings across {len(FAMILIES)} families, and {len(unique)} TMP atlas glyphs.")
    return manifest


def contact_sheet(manifest, output):
    from PIL import Image, ImageDraw

    output = output.resolve()
    if output == ROOT / "Assets" or ROOT / "Assets" in output.parents:
        raise ValueError("Contact sheets are review artifacts and must be saved outside Assets")
    entries = {e["relativePath"]: e for e in manifest["glyphs"]}
    columns, cell_w, cell_h = 8, 188, 172
    sheet = Image.new("RGB", (columns * cell_w, ((len(entries) + columns - 1) // columns) * cell_h + 54), (20, 22, 28))
    draw = ImageDraw.Draw(sheet)
    draw.text((16, 12), "Kenney Input Prompts 1.5A | exact 128px source sprites | CC0", fill="white")
    draw.text((16, 30), "No sprite pixels modified. Source filename labels appear below each image.", fill=(180, 185, 198))
    for index, entry in enumerate(entries.values()):
        x, y = index % columns * cell_w, index // columns * cell_h + 54
        sprite = Image.open(ROOT / entry["relativePath"]).convert("RGBA")
        sheet.paste(sprite, (x + (cell_w - 128) // 2, y), sprite)
        stem = Path(entry["source"]).stem
        if len(stem) > 29:
            split = stem.rfind("_", 0, 29)
            stem = stem[:split] + "\n" + stem[split + 1:]
        draw.text((x + 6, y + 132), stem, fill=(225, 228, 237))
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output)
    print("Contact sheet: " + str(output))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", type=Path, help="Import the pinned official downloaded zip")
    parser.add_argument("--verify", action="store_true", help="Verify existing assets without importing")
    parser.add_argument("--contact-sheet", type=Path, help="Save optional Pillow review sheet outside Assets")
    args = parser.parse_args()
    if not args.archive and not args.verify:
        parser.error("Provide --archive or --verify")
    if args.archive:
        import_archive(args.archive)
    manifest = verify()
    if args.contact_sheet:
        contact_sheet(manifest, args.contact_sheet)


if __name__ == "__main__":
    main()
