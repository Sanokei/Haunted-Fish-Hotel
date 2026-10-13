#!/usr/bin/env python3
"""Build the original Haunted Intertitle font's static TextMeshPro SDF assets.

CPU authoring only; this does not run or claim successful Unity import. Pillow's
FreeType rasterizer draws the TTF at 8x resolution. SciPy's exact Euclidean
transform produces a signed distance field, averaged down to the atlas grid.
The .5 contour is the outline, positive inside; one output pixel is
1 / (2 * (padding + 1)) alpha, matching the shipped TMP Distance Field shader.
Glyph rectangles exclude padding and use Unity's bottom-left texture origin.
No system font, fallback font or borrowed font outlines are used.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import uuid

from fontTools.ttLib import TTFont
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import distance_transform_edt

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Assets/Art/Fonts/HauntedIntertitle-Regular.ttf'
OUTPUT = ROOT / 'Assets/Art/Fonts'
BASENAME = 'HauntedIntertitle SDF'
POINT_SIZE = 96
PADDING = 12
SUPERSAMPLE = 8
ATLAS_SIZE = 2048
TMP_SCRIPT_GUID = '71c1514a6bd24e1e882cebbe1904ce04'
SHADER_PATH = 'Assets/TextMesh Pro/Shaders/TMP_SDF.shader'


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def guid(path):
    return re.search(r'^guid: (\w+)', Path(str(path) + '.meta').read_text(), re.M)[1]


def stable_guid(filename):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'haunted-fish-hotel/Assets/Art/Fonts/' + filename).hex


def number(value):
    return f'{value:.8g}'


def external(file_id, asset_guid, kind=2):
    return '{fileID: %d, guid: %s, type: %d}' % (file_id, asset_guid, kind)


def empty_ref():
    return '{fileID: 0}'


def write_meta(path, importer, body=''):
    identity = stable_guid(path.name)
    meta = Path(str(path) + '.meta')
    if meta.exists() and guid(path) != identity:
        raise ValueError('Refusing to replace an existing asset identity: ' + str(path))
    meta.write_text(f'fileFormatVersion: 2\nguid: {identity}\n{importer}:\n  externalObjects: {{}}\n' + body +
                    '  userData: CPU-authored static SDF; native import validation pending\n  assetBundleName:\n  assetBundleVariant:\n')
    return identity


def texture_meta(path):
    # Uncompressed RGBA, no gamma, no alpha dilation, no mipmaps. SDF is data.
    body = f'''  internalIDToNameTable: []
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 0
    linearTexture: 1
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
  isReadable: 1
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 1
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 4
  maxTextureSize: {ATLAS_SIZE}
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
  compressionQuality: 100
  spriteMode: 0
  spriteExtrude: 0
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spritePixelsToUnits: 100
  alphaUsage: 1
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 1
  applyGammaDecoding: 0
  swizzle: 50462976
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: {ATLAS_SIZE}
    resizeAlgorithm: 0
    textureFormat: 4
    textureCompression: 0
    compressionQuality: 100
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
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
'''
    return write_meta(path, 'TextureImporter', body)


def raster_glyph(font, raster_font, codepoint, glyph_name):
    glyph = font['glyf'][glyph_name]
    glyph.recalcBounds(font['glyf'])
    scale = POINT_SIZE / font['head'].unitsPerEm
    advance = font['hmtx'].metrics[glyph_name][0] * scale
    if not glyph.numberOfContours:
        return {'width': 0, 'height': 0, 'bearingX': 0, 'bearingY': 0,
                'advance': advance, 'bitmap': None}
    x0, y0 = math.floor(glyph.xMin * scale), math.floor(glyph.yMin * scale)
    x1, y1 = math.ceil(glyph.xMax * scale), math.ceil(glyph.yMax * scale)
    width, height = x1 - x0, y1 - y0
    spread = PADDING + 1
    # One guard texel separates adjacent padded SDFs even at bilinear edges.
    pad = spread
    mask = Image.new('L', ((width + pad * 2) * SUPERSAMPLE,
                           (height + pad * 2) * SUPERSAMPLE))
    ImageDraw.Draw(mask).text(((pad - x0) * SUPERSAMPLE, (pad + y1) * SUPERSAMPLE), chr(codepoint),
                             font=raster_font, fill=255, anchor='ls', stroke_width=0)
    ink = np.asarray(mask) >= 128
    signed = np.where(ink, distance_transform_edt(ink) - .5,
                      .5 - distance_transform_edt(~ink)) / SUPERSAMPLE
    # Averaging signed distance, rather than a low-resolution coverage bitmap,
    # preserves small curls and thin serifs. Alpha quantization happens last.
    averaged = signed.reshape(height + pad * 2, SUPERSAMPLE,
                              width + pad * 2, SUPERSAMPLE).mean(axis=(1, 3))
    sdf = np.rint(np.clip(.5 + averaged / (2 * spread), 0, 1) * 255).astype(np.uint8)
    return {'width': width, 'height': height, 'bearingX': x0, 'bearingY': y1,
            'advance': advance, 'bitmap': sdf}


def glyph_records(font, raster_font):
    cmap = font.getBestCmap()
    by_name = {}
    for codepoint, name in sorted(cmap.items()):
        if name not in by_name:
            by_name[name] = (codepoint, raster_glyph(font, raster_font, codepoint, name))
    records = []
    for name, (codepoint, record) in by_name.items():
        records.append(dict(record, index=font.getGlyphID(name), name=name, codepoint=codepoint))
    return sorted(records, key=lambda r: r['index']), cmap


def pack(records):
    atlas = np.zeros((ATLAS_SIZE, ATLAS_SIZE), dtype=np.uint8)
    used = []
    x, y, shelf = 1, 1, 0
    for record in sorted(records, key=lambda r: (-r['height'], -r['width'], r['index'])):
        bitmap = record['bitmap']
        if bitmap is None:
            record['rect'] = (0, 0, 0, 0)
            continue
        height, width = bitmap.shape
        if x + width + 1 > ATLAS_SIZE:
            x, y, shelf = 1, y + shelf + 1, 0
        if y + height + 1 > ATLAS_SIZE:
            raise ValueError('Atlas exhausted. Increase atlas size explicitly; do not omit glyphs.')
        atlas[y:y + height, x:x + width] = bitmap
        pad = PADDING + 1
        record['rect'] = (x + pad, ATLAS_SIZE - (y + pad + record['height']), record['width'], record['height'])
        used.append((x, ATLAS_SIZE - y - height, width, height))
        x += width + 1
        shelf = max(shelf, height)
    rgba = np.full((ATLAS_SIZE, ATLAS_SIZE, 4), 255, dtype=np.uint8)
    rgba[:, :, 3] = atlas
    return Image.fromarray(rgba), used


def rect_yaml(rect, prefix='    '):
    return ''.join(prefix + f'm_{key}: {value}\n' for key, value in zip(('X', 'Y', 'Width', 'Height'), rect))


def pair_records(font):
    # The original font exports its deliberate spacing pairs as legacy 'kern'.
    # Reject GPOS-only kerning instead of silently dropping authored spacing.
    pairs = {}
    if 'kern' in font:
        for subtable in font['kern'].kernTables:
            if subtable.version == 0 and subtable.coverage & 1:
                for pair, value in subtable.kernTable.items():
                    pairs[pair] = pairs.get(pair, 0) + value
    return [(font.getGlyphID(a), font.getGlyphID(b), value * POINT_SIZE / font['head'].unitsPerEm)
            for (a, b), value in sorted(pairs.items()) if value]


def write_font_asset(path, font, records, cmap, used, source_guid, atlas_guid, material_guid):
    scale = POINT_SIZE / font['head'].unitsPerEm
    names = font['name']
    family, style = names.getDebugName(1), names.getDebugName(2)
    hhea, os2, post = font['hhea'], font['OS/2'], font['post']
    fields = {
        'FaceIndex': 0, 'FamilyName': family, 'StyleName': style, 'PointSize': POINT_SIZE, 'Scale': 1,
        'UnitsPerEM': font['head'].unitsPerEm, 'LineHeight': (hhea.ascent - hhea.descent + hhea.lineGap) * scale,
        'AscentLine': hhea.ascent * scale, 'CapLine': os2.sCapHeight * scale, 'MeanLine': os2.sxHeight * scale,
        'Baseline': 0, 'DescentLine': hhea.descent * scale,
        'SuperscriptOffset': os2.ySuperscriptYOffset * scale,
        'SuperscriptSize': os2.ySuperscriptYSize / font['head'].unitsPerEm,
        'SubscriptOffset': -os2.ySubscriptYOffset * scale,
        'SubscriptSize': os2.ySubscriptYSize / font['head'].unitsPerEm,
        'UnderlineOffset': post.underlinePosition * scale,
        'UnderlineThickness': post.underlineThickness * scale,
        'StrikethroughOffset': os2.yStrikeoutPosition * scale,
        'StrikethroughThickness': os2.yStrikeoutSize * scale,
        'TabWidth': font['hmtx'].metrics[cmap[32]][0] * scale,
    }
    text = f'''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {external(11500000, TMP_SCRIPT_GUID, 3)}
  m_Name: {BASENAME}
  m_EditorClassIdentifier:
  hashCode: 0
  material: {external(2100000, material_guid)}
  materialHashCode: 0
  m_Version: 1.1.0
  m_SourceFontFileGUID: {source_guid}
  m_SourceFontFile_EditorRef: {external(12800000, source_guid, 3)}
  m_SourceFontFile: {{fileID: 0}}
  m_SourceFontFilePath:
  m_AtlasPopulationMode: 0
  InternalDynamicOS: 0
  m_FaceInfo:
'''
    text += ''.join(f'    m_{key}: {value if isinstance(value, str) else number(value)}\n' for key, value in fields.items())
    text += '  m_GlyphTable:\n'
    for r in records:
        text += f"  - m_Index: {r['index']}\n    m_Metrics:\n"
        for key, item in [('Width', 'width'), ('Height', 'height'), ('HorizontalBearingX', 'bearingX'),
                          ('HorizontalBearingY', 'bearingY'), ('HorizontalAdvance', 'advance')]:
            text += f'      m_{key}: {number(r[item])}\n'
        text += '    m_GlyphRect:\n' + rect_yaml(r['rect'], '      ') + '    m_Scale: 1\n    m_AtlasIndex: 0\n    m_ClassDefinitionType: 0\n'
    text += '  m_CharacterTable:\n'
    for unicode, name in sorted(cmap.items()):
        text += f'  - m_ElementType: 1\n    m_Unicode: {unicode}\n    m_GlyphIndex: {font.getGlyphID(name)}\n    m_Scale: 1\n'
    text += f'''  m_AtlasTextures:
  - {external(2800000, atlas_guid, 3)}
  m_AtlasTextureIndex: 0
  m_IsMultiAtlasTexturesEnabled: 0
  m_GetFontFeatures: 0
  m_ClearDynamicDataOnBuild: 0
  m_UsedGlyphRects:
'''
    for rect in used:
        text += '  - ' + rect_yaml(rect, '    ').lstrip()
    text += f'''  m_FreeGlyphRects: []
  m_AtlasWidth: {ATLAS_SIZE}
  m_AtlasHeight: {ATLAS_SIZE}
  m_AtlasPadding: {PADDING}
  m_AtlasRenderMode: 8230
  m_FontFeatureTable:
    m_MultipleSubstitutionRecords: []
    m_LigatureSubstitutionRecords: []
'''
    pairs = pair_records(font)
    text += '    m_GlyphPairAdjustmentRecords:' + ('\n' if pairs else ' []\n')
    for first, second, amount in pairs:
        for label, index, advance in [('First', first, amount), ('Second', second, 0)]:
            text += ('    - ' if label == 'First' else '      ') + f'm_{label}AdjustmentRecord:\n'
            text += f'        m_GlyphIndex: {index}\n        m_GlyphValueRecord:\n          m_XPlacement: 0\n          m_YPlacement: 0\n          m_XAdvance: {number(advance)}\n          m_YAdvance: 0\n'
        text += '      m_FeatureLookupFlags: 0\n'
    text += f'''    m_MarkToBaseAdjustmentRecords: []
    m_MarkToMarkAdjustmentRecords: []
  m_FallbackFontAssetTable: []
  m_CreationSettings:
    sourceFontFileName: {SOURCE.name}
    sourceFontFileGUID: {source_guid}
    pointSizeSamplingMode: 1
    pointSize: {POINT_SIZE}
    padding: {PADDING}
    packingMode: 0
    atlasWidth: {ATLAS_SIZE}
    atlasHeight: {ATLAS_SIZE}
    characterSetSelectionMode: 5
    characterSequence: {','.join(str(c) for c in sorted(cmap))}
    referencedFontAssetGUID:
    referencedTextAssetGUID:
    fontStyle: 0
    fontStyleModifier: 0
    renderMode: 8230
    includeFontFeatures: 1
  m_FontWeightTable:
'''
    text += '  - regularTypeface: {fileID: 0}\n    italicTypeface: {fileID: 0}\n' * 10
    text += '  normalStyle: 0\n  normalSpacingOffset: 0\n  boldStyle: 0.5\n  boldSpacing: 7\n  italicStyle: 35\n  tabSize: 4\n'
    path.write_text(text)
    write_meta(path, 'NativeFormatImporter', '  mainObjectFileID: 11400000\n')
    return len(pairs)


def write_material(path, atlas_guid):
    # Material schema taken from the project's shipped LiberationSans SDF asset.
    source = (ROOT / 'Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset').read_text()
    text = source.split('--- !u!114')[0].replace('&2180264', '&2100000')
    text = text.replace('LiberationSans SDF Material', BASENAME + ' Material')
    text = re.sub(r'm_Shader: .*', 'm_Shader: ' + external(4800000, guid(ROOT / SHADER_PATH), 3), text)
    text = re.sub(r'm_Texture: .*', 'm_Texture: ' + external(2800000, atlas_guid, 3), text)
    for key, value in {'GradientScale': PADDING + 1, 'TextureWidth': ATLAS_SIZE, 'TextureHeight': ATLAS_SIZE,
                       'WeightBold': .5, 'ScaleRatioA': 1, 'ScaleRatioB': 1, 'ScaleRatioC': 1}.items():
        text = re.sub(r'(_' + key + r':) [^\n]+', rf'\g<1> {value}', text)
    # The shipped template contains empty scalar fields with trailing spaces.
    # Normalize only line endings/whitespace, preserving all material values.
    path.write_text('\n'.join(line.rstrip() for line in text.splitlines()) + '\n')
    return write_meta(path, 'NativeFormatImporter', '  mainObjectFileID: 2100000\n')


def build(source=SOURCE, output=OUTPUT):
    output.mkdir(parents=True, exist_ok=True)
    font = TTFont(source)
    if 'glyf' not in font:
        raise ValueError('This reproducible tool expects original TrueType outlines.')
    raster_font = ImageFont.truetype(str(source), POINT_SIZE * SUPERSAMPLE)
    records, cmap = glyph_records(font, raster_font)
    atlas, used = pack(records)
    atlas_path, material_path, font_path = [output / (BASENAME + ext) for ext in (' Atlas.png', '.mat', '.asset')]
    atlas.save(atlas_path, optimize=False)
    atlas_guid = texture_meta(atlas_path)
    material_guid = write_material(material_path, atlas_guid)
    pair_count = write_font_asset(font_path, font, records, cmap, used, guid(source), atlas_guid, material_guid)
    report = {'schema_version': 1, 'status': 'cpu_authored_static_sdf_native_import_pending',
              'source_font': str(source.relative_to(ROOT)) if source.is_relative_to(ROOT) else str(source),
              'source_sha256': sha256(source), 'family': font['name'].getDebugName(1),
              'atlas_size': ATLAS_SIZE, 'point_size': POINT_SIZE, 'padding': PADDING,
              'supersample': SUPERSAMPLE, 'glyph_count': len(records), 'character_count': len(cmap),
              'kerning_pair_count': pair_count, 'codepoints': sorted(cmap),
              'files': {path.name: sha256(path) for path in (atlas_path, material_path, font_path)},
              'native_validation': 'NOT RUN by CPU authoring. Run IntertitleTypographyValidation.Run in Unity; no native rendering claim.'}
    report_path = output / 'HauntedIntertitle-SDF-build.json'
    report_path.write_text(json.dumps(report, indent=2) + '\n')
    write_meta(report_path, 'TextScriptImporter')
    print(json.dumps({key: value for key, value in report.items() if key not in ('codepoints', 'files')}, indent=2))
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--font', type=Path, default=SOURCE)
    parser.add_argument('--output-dir', type=Path, default=OUTPUT)
    args = parser.parse_args()
    build(args.font.resolve(), args.output_dir.resolve())
