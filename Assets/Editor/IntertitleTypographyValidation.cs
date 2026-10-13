#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Explicit, non-destructive native check of the committed original font assets.
/// Does not regenerate the font, modify scenes, create video or declare capture complete.
/// CPU authoring/tests do not substitute for running this in the real Unity Editor.
/// </summary>
public static class IntertitleTypographyValidation
{
    public const string SourcePath = "Assets/Art/Fonts/HauntedIntertitle-Regular.ttf";
    public const string FontPath = "Assets/Art/Fonts/HauntedIntertitle SDF.asset";
    const string AtlasPath = "Assets/Art/Fonts/HauntedIntertitle SDF Atlas.png";
    const string MaterialPath = "Assets/Art/Fonts/HauntedIntertitle SDF.mat";
    const string Sample = "Guide to all Fish\nGuide to the Ghost\nWowza! ABCDEFGHIJKLMNOPQRSTUVWXYZ\nabcdefghijklmnopqrstuvwxyz 0123456789\n‘’ “”—–… ←↑→↓↔ ©® °×÷";

    [MenuItem("Tools/Haunted Fish/Typography/Validate original intertitle TTF and TMP")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Validate original typography only in idle Edit mode.");
        var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Require(source && font && atlas && material, "The four original typography assets must import successfully first.");
        Require(font.atlasPopulationMode == AtlasPopulationMode.Static, "Expected authored static atlas.");
        Require(font.faceInfo.familyName == "Haunted Intertitle" && font.faceInfo.pointSize == 96,
            "Unexpected family or sampling size.");
        Require(font.atlasTextures.Length == 1 && font.atlasTextures[0] == atlas && font.material == material,
            "Broken TMP atlas or material reference.");
        Require(material.shader && material.shader.name == "TextMeshPro/Distance Field" && material.mainTexture == atlas,
            "Broken TMP shader or material texture binding.");
        Require(atlas.width == 2048 && atlas.height == 2048 && font.atlasPadding == 12 &&
            Mathf.Approximately(material.GetFloat("_GradientScale"), 13), "Wrong SDF dimensions or distance scale.");
        var serialized = new SerializedObject(font);
        Require(serialized.FindProperty("m_SourceFontFileGUID").stringValue == AssetDatabase.AssetPathToGUID(SourcePath),
            "TMP source link does not resolve to the original TTF.");
        var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        Require(!importer.sRGBTexture && !importer.mipmapEnabled && !importer.alphaIsTransparency &&
            importer.textureCompression == TextureImporterCompression.Uncompressed && importer.isReadable,
            "The SDF atlas must retain linear, readable, uncompressed alpha without mipmaps or dilation.");
        font.ReadFontAssetDefinition();
        Require(font.HasCharacters(Sample.Replace("\n", ""), out uint[] missing, false, false),
            "Missing original glyphs: " + string.Join(",", missing ?? Array.Empty<uint>()));
        Require(font.fallbackFontAssetTable == null || font.fallbackFontAssetTable.Count == 0,
            "Original intertitles must not silently use another font.");
        Require(FontEngine.InitializeFontEngine() == FontEngineError.Success, "Native FontEngine initialization failed.");
        Require(FontEngine.LoadFontFace(source, 96) == FontEngineError.Success, "Native FontEngine rejected the original TTF.");
        Require(FontEngine.GetFaceInfo().familyName == "Haunted Intertitle", "Native TTF family mismatch.");
        foreach (var character in font.characterTable)
            Require(FontEngine.TryGetGlyphIndex(character.unicode, out uint index) && index == character.glyphIndex,
                "TTF/TMP glyph index mismatch for U+" + character.unicode.ToString("X4"));
        var alpha = atlas.GetPixels32();
        foreach (var glyph in font.glyphTable)
        {
            var rect = glyph.glyphRect;
            if (rect.width == 0 || rect.height == 0) continue;
            Require(rect.x >= 13 && rect.y >= 13 && rect.x + rect.width + 13 < atlas.width &&
                rect.y + rect.height + 13 < atlas.height, "Glyph rectangle or SDF padding outside atlas.");
            bool hasInk = false;
            for (int y = rect.y; y < rect.y + rect.height && !hasInk; y++)
                for (int x = rect.x; x < rect.x + rect.width; x++)
                    if (alpha[y * atlas.width + x].a > 128) { hasInk = true; break; }
            Require(hasInk, "Empty native SDF glyph " + glyph.index);
        }
        // Exercise the actual TMP mesh pipeline. The temporary object is never
        // saved into a scene or prefab and is always destroyed, even on failure.
        var root = new GameObject("Temporary original intertitle TMP validation") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var text = root.AddComponent<TextMeshPro>();
            text.font = font;
            text.fontSharedMaterial = material;
            text.fontSize = 96;
            text.enableAutoSizing = false;
            text.richText = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(4000, 1400);
            text.text = Sample;
            text.ForceMeshUpdate(true, true);
            Require(text.textInfo.characterCount == Sample.Length, "TMP did not shape the complete validation sample.");
            Require(text.mesh.vertexCount > 0, "TMP generated no mesh.");
            foreach (var info in text.textInfo.characterInfo.Take(text.textInfo.characterCount))
                if (info.isVisible)
                    Require(info.fontAsset == font && info.materialReferenceIndex == 0,
                        "TMP silently substituted a font or material.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        Debug.Log("PASS: native Haunted Intertitle TTF load, static TMP references, glyph lookup, alpha data and mesh generation. " +
            "Visual card review, guide playback and real gameplay capture remain separate requirements.");
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
