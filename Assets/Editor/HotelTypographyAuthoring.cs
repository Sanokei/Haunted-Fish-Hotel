using System;
using System.Linq;
using HauntedFish.Tutorials;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Explicit editor authoring only. No scene writes or runtime UI construction.
public static class HotelTypographyAuthoring
{
    public const string HeadingPath = "Assets/Resources/HalloweenUI/Modak-Headings.asset";
    public const string HeadingGuid = "e08468cb765749c59b1362447214b0e5";
    public const string BodyPath = "Assets/Resources/HalloweenUI/VarelaRound-Body.asset";
    public const string BodyGuid = "98067b61ac5446dba12771373453ce8a";
    public static TMP_FontAsset BakeHeadingFont() => BakeFont(HeadingPath, HeadingGuid, "Assets/Art/Fonts/Modak-Regular.ttf", "Modak");
    public static TMP_FontAsset BakeBodyFont() => BakeFont(BodyPath, BodyGuid, "Assets/Plugins/CW/Shared/Common/Examples/Fonts/VarelaRound-Regular.ttf", "Varela Round");
    static TMP_FontAsset BakeFont(string path, string guid, string sourcePath, string family)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Typography authoring requires idle Edit mode.");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (!font || !source || AssetDatabase.AssetPathToGUID(path) != guid)
            throw new InvalidOperationException("Import the staged " + family + " asset and bundled source with exact metas first.");
        FontEngine.InitializeFontEngine();
        if (FontEngine.LoadFontFace(source, 48) != FontEngineError.Success)
            throw new InvalidOperationException("Native FontEngine could not load bundled " + family + ".");
        font.faceInfo = FontEngine.GetFaceInfo();
        font.ReadFontAssetDefinition();
        const string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,;:!?/'()-+%&";
        if (!font.HasCharacters(characters) && !font.TryAddCharacters(characters, out string missing))
            throw new InvalidOperationException("Missing " + family + " characters: " + missing);
        EditorUtility.SetDirty(font);
        if (font.material) EditorUtility.SetDirty(font.material);
        foreach (var atlas in font.atlasTextures) if (atlas) EditorUtility.SetDirty(atlas);
        AssetDatabase.SaveAssetIfDirty(font);
        return font;
    }
    [MenuItem("Tools/Haunted Hotel/Bake shared caption fonts only")]
    public static void BakeSharedFont() { BakeHeadingFont(); BakeBodyFont(); }
    [MenuItem("Tools/Haunted Hotel/Bake revised body font only")]
    public static void BakeBodyFontOnly() => BakeBodyFont();

    [MenuItem("Tools/Haunted Hotel/Bake shared typography and validate menu fit")]
    public static void Run()
    {
        BakeSharedFont();
        var body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyPath);
        var fish = Resources.Load<TutorialRoleContent>("Tutorials/FishTutorialContent");
        var ghost = Resources.Load<TutorialRoleContent>("Tutorials/GhostTutorialContent");
        if (!body || !fish || !ghost) throw new InvalidOperationException("Publish both recorder-owned role content assets before menu fit validation.");
        var root = PrefabUtility.LoadPrefabContents("Assets/Resources/MemberMenu.prefab");
        try
        {
            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            var heading = texts.Single(t => t.name == "Hotel heading");
            var title = texts.Single(t => t.name == "Tutorial title");
            var instructions = texts.Single(t => t.name == "Tutorial instructions");
            var status = texts.Single(t => t.name == "Tutorial playback status");
            if (instructions.font != body || instructions.fontSize < 24 || instructions.enableAutoSizing)
                throw new InvalidOperationException("Instructions must retain the shared Varela Round body font and fixed size.");
            Fit(heading, "Haunted Fish Hotel"); Fit(title, fish.Title); Fit(title, ghost.Title);
            Fit(status, "Video unavailable; use the instructions.");
            foreach (var text in texts.Where(t => t.name == "Master volume label" || t.name == "Music volume label" || t.name == "Sound effects volume label")) Fit(text, text.text);
            Debug.Log("PASS: native Modak/Varela Round glyph bake and authored heading/role/settings/status fit. Interactive 960x540 / 800x600 screenshots, video playback and scroll checks remain required.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        MemberMenuValidation.Run();
    }
    static void Fit(TMP_Text text, string content)
    {
        var rect = text.rectTransform;
        var width = rect.sizeDelta.x;
        var height = rect.sizeDelta.y;
        var size = text.GetPreferredValues(content, width, Mathf.Infinity);
        if (size.y > height + .5f)
            throw new InvalidOperationException(text.name + " does not fit its authored box: " + size + " available " + width + " x " + height);
    }
}
