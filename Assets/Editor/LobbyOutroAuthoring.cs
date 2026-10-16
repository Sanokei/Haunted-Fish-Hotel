using System;
using System.Linq;
using HauntedFish.Multiplayer;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Builds the lobby outro hierarchy in the open Lobby scene and previews each outro in Play Mode.
public static class LobbyOutroAuthoring
{
    const string InkFolder = "Assets/Dialogue/LobbyOutro/";
    const string FontPath = "Assets/Art/Fonts/HauntedIntertitle SDF.asset";
    const string GhostSpritePath = "Assets/Art/oct15art/INT-Attic-Boss.png";

    [MenuItem("Haunted Fish/Lobby Outro/Author In Open Lobby Scene")]
    public static void Author()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode before authoring the lobby outro.");
        var manager = UnityEngine.Object.FindAnyObjectByType<LobbyManager>(FindObjectsInactive.Include);
        var world = UnityEngine.Object.FindAnyObjectByType<LobbyIntroductionWorldSequence>(FindObjectsInactive.Include);
        if (!manager || !world) throw new InvalidOperationException("Open the Lobby scene first.");
        var worldData = new SerializedObject(world);
        var camera = worldData.FindProperty("_Camera").objectReferenceValue;
        var fish = worldData.FindProperty("_AuthoredFish").objectReferenceValue;
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var ghostSprite = AssetDatabase.LoadAllAssetsAtPath(GhostSpritePath).OfType<Sprite>().FirstOrDefault();
        if (!font || !ghostSprite) throw new InvalidOperationException("Missing outro font or ghost sprite.");

        var root = Node(manager.transform, "Lobby outro sequence");
        var outro = Get<LobbyOutroSequence>(root);
        var winner = new Vector3(0, .85f, 1);
        var look = winner + Vector3.up * .25f;
        var mark = Pose(root, "Outro winner mark", winner, Quaternion.identity);
        var crashStart = Pose(root, "Outro crash start", new Vector3(0, 1.55f, -4.8f), Quaternion.LookRotation(look - new Vector3(0, 1.55f, -4.8f)));
        var crashEndPosition = new Vector3(0, 1.3f, -1.7f);
        var crashEnd = Pose(root, "Outro crash end", crashEndPosition, Quaternion.LookRotation(look - crashEndPosition));
        var jibPosition = new Vector3(0, 7.6f, -7.4f);
        var jibEnd = Pose(root, "Outro jib end", jibPosition, Quaternion.LookRotation(look - jibPosition));

        var ghost = Node(root, "Outro ghost");
        var ghostRenderer = Get<SpriteRenderer>(ghost);
        ghostRenderer.sprite = ghostSprite;
        // About 1.3 m tall at the winner mark, whatever the source resolution.
        ghost.localScale = Vector3.one * (1.3f / ghostSprite.bounds.size.y);
        var fishBody = fish ? ((Component)fish).GetComponentInChildren<SpriteRenderer>(true) : null;
        if (fishBody) { ghostRenderer.sortingLayerID = fishBody.sortingLayerID; ghostRenderer.sortingOrder = fishBody.sortingOrder; ghostRenderer.sharedMaterial = fishBody.sharedMaterial; }
        ghost.gameObject.SetActive(false);

        var canvasTransform = UI(root, "Lobby outro overlay");
        var canvas = Get<Canvas>(canvasTransform);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        var scaler = Get<CanvasScaler>(canvasTransform);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        var group = Get<CanvasGroup>(canvasTransform);
        group.blocksRaycasts = false; group.interactable = false; group.alpha = 0;

        var fade = Stretch(UI(canvasTransform, "Fade"));
        var fadeImage = Get<Image>(fade);
        fadeImage.color = Color.clear; fadeImage.raycastTarget = false;

        var title = Text(Stretch(UI(canvasTransform, "Outro title")), font, 340, Color.white, FontStyles.Normal);
        title.rectTransform.offsetMin = new Vector2(0, 80);
        title.alignment = TextAlignmentOptions.Bottom;
        title.text = "";
        title.gameObject.SetActive(false);

        var viewport = Stretch(UI(canvasTransform, "Credits viewport"));
        if (!viewport.GetComponent<RectMask2D>()) viewport.gameObject.AddComponent<RectMask2D>();
        var content = UI(viewport, "Credits content");
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, 1);
        content.sizeDelta = new Vector2(1500, 0);
        content.anchoredPosition = new Vector2(0, -1080);
        var layout = Get<VerticalLayoutGroup>(content);
        layout.childAlignment = TextAnchor.UpperCenter; layout.spacing = 6;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        var fitter = Get<ContentSizeFitter>(content);
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var templates = UI(viewport, "Credits row templates");
        var heading = Text(UI(templates, "Heading"), font, 36, new Color(.86f, .74f, .5f), FontStyles.UpperCase);
        heading.characterSpacing = 12;
        var line = Text(UI(templates, "Line"), font, 60, Color.white, FontStyles.Normal);
        var small = Text(UI(templates, "Small print"), font, 28, new Color(.78f, .78f, .78f), FontStyles.Normal);
        foreach (var template in new[] { heading, line, small }) template.gameObject.SetActive(false);
        viewport.gameObject.SetActive(false);

        var skip = UI(canvasTransform, "Hold space to skip");
        skip.anchorMin = skip.anchorMax = skip.pivot = new Vector2(1, 0);
        skip.anchoredPosition = new Vector2(-56, 56);
        skip.sizeDelta = new Vector2(56, 56);
        var circle = Get<IntroductionSkipCircle>(skip);
        circle.color = Color.white; circle.raycastTarget = false;
        skip.gameObject.SetActive(false);

        var data = new SerializedObject(outro);
        Set(data, "_GhostWinInk", Ink("GhostWin"));
        Set(data, "_GhostDefeatInk", Ink("GhostDefeat"));
        Set(data, "_FishWinInk", Ink("FishWin"));
        Set(data, "_FishDefeatInk", Ink("FishDefeat"));
        Set(data, "_CreditsInk", Ink("LobbyCredits"));
        Set(data, "_CreditsText", AssetDatabase.LoadAssetAtPath<TextAsset>(InkFolder + "LobbyCredits.txt"));
        Set(data, "_Camera", camera);
        Set(data, "_Fish", fish);
        Set(data, "_Ghost", ghostRenderer);
        Set(data, "_WinnerMark", mark);
        Set(data, "_CrashStart", crashStart);
        Set(data, "_CrashEnd", crashEnd);
        Set(data, "_JibEnd", jibEnd);
        Set(data, "_Overlay", group);
        Set(data, "_Fade", fadeImage);
        Set(data, "_Title", title);
        Set(data, "_CreditsViewport", viewport);
        Set(data, "_CreditsContent", content);
        Set(data, "_HeadingTemplate", heading);
        Set(data, "_LineTemplate", line);
        Set(data, "_SmallTemplate", small);
        Set(data, "_SkipCircle", circle);
        data.ApplyModifiedProperties();

        var leave = UnityEngine.Object.FindObjectsByType<LobbyTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(x => x.name == "Leave");
        if (!leave) throw new InvalidOperationException("The Lobby scene has no Leave trigger.");
        var managerData = new SerializedObject(manager);
        Set(managerData, "_Outro", outro);
        Set(managerData, "_LeaveZone", leave);
        managerData.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Debug.Log("Lobby outro authored. Save the Lobby scene to keep it.");
    }

    [MenuItem("Haunted Fish/Lobby Outro/Preview Ghost Win")] static void PreviewGhostWin() => Preview(LobbyOutroKind.GhostWin);
    [MenuItem("Haunted Fish/Lobby Outro/Preview Ghost Defeat")] static void PreviewGhostDefeat() => Preview(LobbyOutroKind.GhostDefeat);
    [MenuItem("Haunted Fish/Lobby Outro/Preview Fish Win")] static void PreviewFishWin() => Preview(LobbyOutroKind.FishWin);
    [MenuItem("Haunted Fish/Lobby Outro/Preview Fish Defeat")] static void PreviewFishDefeat() => Preview(LobbyOutroKind.FishDefeat);
    [MenuItem("Haunted Fish/Lobby Outro/Preview Credits")] static void PreviewCredits()
    {
        var outro = UnityEngine.Object.FindAnyObjectByType<LobbyOutroSequence>();
        if (!outro || !outro.PlayCredits()) Debug.LogWarning("Enter Play Mode in the Lobby to preview the credits.");
    }
    [MenuItem("Haunted Fish/Lobby Outro/Preview Ghost Win", true)]
    [MenuItem("Haunted Fish/Lobby Outro/Preview Ghost Defeat", true)]
    [MenuItem("Haunted Fish/Lobby Outro/Preview Fish Win", true)]
    [MenuItem("Haunted Fish/Lobby Outro/Preview Fish Defeat", true)]
    [MenuItem("Haunted Fish/Lobby Outro/Preview Credits", true)]
    static bool CanPreview() => EditorApplication.isPlaying;
    static void Preview(LobbyOutroKind kind)
    {
        var outro = UnityEngine.Object.FindAnyObjectByType<LobbyOutroSequence>();
        if (!outro || !outro.PlayOutcome(kind)) Debug.LogWarning("Enter Play Mode in the Lobby, after the introduction, to preview the outro.");
    }

    static TextAsset Ink(string name)
    {
        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(InkFolder + name + ".json");
        if (!asset) throw new InvalidOperationException("Compile " + InkFolder + name + ".ink first.");
        return asset;
    }
    static void Set(SerializedObject data, string name, UnityEngine.Object value)
    {
        var property = data.FindProperty(name) ?? throw new InvalidOperationException("Missing field " + name);
        property.objectReferenceValue = value;
    }
    static T Get<T>(Component owner) where T : Component =>
        owner.TryGetComponent(out T component) ? component : Undo.AddComponent<T>(owner.gameObject);
    static Transform Node(Transform parent, string name)
    {
        var found = parent.Find(name);
        if (found) return found;
        var child = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(child, "Author lobby outro");
        child.transform.SetParent(parent, false);
        return child.transform;
    }
    static RectTransform UI(Transform parent, string name)
    {
        var found = parent.Find(name);
        if (found is RectTransform rect) return rect;
        if (found) throw new InvalidOperationException(name + " exists but is not a UI object.");
        var child = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(child, "Author lobby outro");
        child.transform.SetParent(parent, false);
        return (RectTransform)child.transform;
    }
    static Transform Pose(Transform parent, string name, Vector3 position, Quaternion rotation)
    {
        var pose = Node(parent, name);
        pose.SetPositionAndRotation(position, rotation);
        return pose;
    }
    static RectTransform Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    static TextMeshProUGUI Text(RectTransform rect, TMP_FontAsset font, float size, Color color, FontStyles style)
    {
        var text = Get<TextMeshProUGUI>(rect);
        text.font = font; text.fontSize = size; text.color = color; text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }
}
