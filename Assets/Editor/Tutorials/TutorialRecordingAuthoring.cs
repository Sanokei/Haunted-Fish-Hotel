#if UNITY_EDITOR
using System;
using System.IO;
using IOPath = System.IO.Path;
using HauntedFish.Multiplayer;
using HauntedFish.BossFight;
using HauntedFish.Tutorials;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Object = UnityEngine.Object;

public static class TutorialRecordingAuthoring
{
    public const string ScenePath = "Assets/Scenes/TutorialRecording.unity";
    const string Folder = "Assets/Resources/Tutorials";
    static Font headingFont, bodyFont;
    [MenuItem("Tools/Haunted Fish/Authored tutorials/Build recording scene (Edit mode)")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Build only in idle Edit mode.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserve unsaved scenes before authoring.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Directory.CreateDirectory(IOPath.Combine(Application.dataPath, "Resources/Tutorials"));
            AssetDatabase.Refresh();
            headingFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/Modak-Regular.ttf");
            bodyFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
            if (!headingFont || !bodyFont) throw new InvalidOperationException("Shared licensed Modak/Liberation caption fonts missing.");
            var cart = Trap("ShoppingCartTrap", "TutorialCart");
            var chandelier = Trap("FallingChandelierTrap", "TutorialChandelier");
            var click = Trap("ClickTrap", "TutorialClickTrap");
            var fishContent = Content(false);
            var ghostContent = Content(true);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = Color.white;
            var root = new GameObject("Tutorial Recording Stage - authored");
            var director = root.AddComponent<TutorialRecordingDirector>();
            var hall = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/GameHallway.prefab"), root.transform);
            director.Hallway = hall;
            var floor = new GameObject("Tutorial floor - production world default floor");
            floor.transform.SetParent(hall.transform); floor.transform.position = new Vector3(0, -.5f, 0);
            floor.AddComponent<BoxCollider>().size = new Vector3(30, 1, 2);
            var ceiling = new GameObject("Tutorial hallway ceiling");
            ceiling.transform.SetParent(hall.transform); ceiling.transform.position = new Vector3(0, 4.36f, 0);
            ceiling.AddComponent<BoxCollider>().size = new Vector3(30, .5f, 2);
            var safe = new GameObject("Authored starting safe hallway");
            safe.transform.SetParent(root.transform); safe.transform.position = new Vector3(-12, 2, 0);
            var safeCollider = safe.AddComponent<BoxCollider>(); safeCollider.isTrigger = true; safeCollider.size = new Vector3(6, 4, 2);
            var safeSign = new GameObject("Safe hallway caption"); safeSign.transform.SetParent(safe.transform);
            safeSign.transform.localPosition = new Vector3(0, -1.6f, -.7f);
            var sign = safeSign.AddComponent<TextMesh>(); sign.text = "SAFE HALLWAY"; sign.fontSize = 32; sign.characterSize = .075f; sign.anchor = TextAnchor.MiddleCenter; sign.color = new Color(.95f, .63f, .48f);
            var door = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/HallwayDoor.prefab"), hall.transform);
            door.transform.position = new Vector3(12, 2.055f, 1.9f);
            var worldObject = new GameObject("Production scoped trap inventory"); worldObject.transform.SetParent(root.transform);
            director.World = worldObject.AddComponent<GhostPlacementWorld>();
            var fields = new SerializedObject(director.World);
            fields.FindProperty("_TrapPrefabs").arraySize = 3;
            fields.FindProperty("_TrapPrefabs").GetArrayElementAtIndex(0).objectReferenceValue = cart;
            fields.FindProperty("_TrapPrefabs").GetArrayElementAtIndex(1).objectReferenceValue = chandelier;
            fields.FindProperty("_TrapPrefabs").GetArrayElementAtIndex(2).objectReferenceValue = click;
            fields.FindProperty("_TrapContainer").objectReferenceValue = worldObject.transform;
            fields.FindProperty("_GraceZone").objectReferenceValue = safeCollider; fields.ApplyModifiedPropertiesWithoutUndo();
            director.CartDefinition = cart; director.ChandelierDefinition = chandelier; director.ClickDefinition = click;
            var supplies = new GameObject("Production conveyor"); supplies.transform.SetParent(root.transform);
            director.Supplies = supplies.AddComponent<TrapManager>();
            director.Supplies.Inject(director.World, null, AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Traps/ConveyorPackage.prefab").GetComponent<GhostTrapSupply>(),
                new[] { new TrapDefinition { Prefab = cart, Weight = 1 } });
            var supplyFields = new SerializedObject(director.Supplies);
            supplyFields.FindProperty("_RunPolicy").enumValueIndex = (int)ConveyorRunPolicy.Manual;
            supplyFields.FindProperty("_PathStart").vector3Value = new Vector3(-8, 6.5f, 0);
            supplyFields.FindProperty("_PathEnd").vector3Value = new Vector3(14, 6.5f, 0);
            supplyFields.FindProperty("_InitialPackages").intValue = 2;
            supplyFields.FindProperty("_SpawnInterval").floatValue = 12; supplyFields.ApplyModifiedPropertiesWithoutUndo();
            director.Fish = Actor(root.transform, false); director.Ghost = Actor(root.transform, true);
            var flight = new GameObject("Authored ghost flight"); flight.transform.SetParent(root.transform);
            director.GhostFlight = flight.transform; director.GhostArt = flight.AddComponent<SpriteRenderer>();
            director.GhostArt.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/BOSS-cursor.png");
            director.GhostArt.sortingOrder = 20; director.GhostArt.color = new Color(.9254902f, .99607843f, .7921569f, 1);
            director.GhostArt.transform.localScale = Vector3.one * .56f; // Exact controlled ghost canvas, tint and scale from Game.
            var carry = new GameObject("Authored held trap artwork"); carry.transform.SetParent(root.transform);
            director.CarriedPreview = carry.transform; director.CarryArt = carry.AddComponent<SpriteRenderer>();
            director.CarryArt.sprite = cart.Icon; director.CarryArt.color = new Color(1, .8f, .65f, .7f); director.CarryArt.sortingOrder = 8;
            Fit(director.CarryArt, cart.ObjectSize); carry.SetActive(false);
            var cameraRoot = new GameObject("Tutorial Output Camera");
            cameraRoot.transform.SetParent(root.transform); cameraRoot.tag = "MainCamera";
            director.Output = cameraRoot.AddComponent<Camera>(); director.Output.clearFlags = CameraClearFlags.SolidColor;
            director.Output.backgroundColor = new Color(.025f, .035f, .04f); director.Output.orthographic = true; director.Output.orthographicSize = 4.8f;
            director.Output.nearClipPlane = .05f; director.Output.farClipPlane = 2000; director.Output.aspect = 16f / 9f;
            cameraRoot.AddComponent<AudioListener>(); cameraRoot.AddComponent<HotelViewCamera>().BindOutput(director.Output);
            cameraRoot.transform.position = new Vector3(-3, 1.5f, -18);
            director.Shots = new[] {
                Shot(root.transform, "fish-wide", -3, 1.5f, 3.2f), Shot(root.transform, "fish-run", 5, 1.5f, 3.2f),
                Shot(root.transform, "conveyor", -3, 4.8f, 5.9f), Shot(root.transform, "floor", -1, 1.5f, 3.2f),
                Shot(root.transform, "safe", -10, 1.5f, 3.2f), Shot(root.transform, "ceiling", 5, 1.5f, 3.2f),
                Shot(root.transform, "click", 10, 1.5f, 3.2f), Shot(root.transform, "door", 12, 1.5f, 3.2f) };
            var canvasRoot = new GameObject("Authored tutorial captions", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasRoot.transform.SetParent(root.transform);
            var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = director.Output; canvas.planeDistance = .5f; canvas.sortingOrder = 100;
            var scaler = canvasRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720);
            var shade = Image(canvasRoot.transform, "Authored caption backing", new Color(.015f, .022f, .025f, .92f));
            Rect(shade.rectTransform, new Vector2(0, 0), new Vector2(1, .23f), Vector2.zero, Vector2.zero);
            director.Heading = Label(canvasRoot.transform, "Heading", 32, new Color(.97255f, .91765f, .80392f));
            Rect(director.Heading.rectTransform, new Vector2(.035f, .13f), new Vector2(.965f, .225f), Vector2.zero, Vector2.zero);
            director.Caption = Label(canvasRoot.transform, "Instruction", 26, new Color(.97255f, .91765f, .80392f));
            Rect(director.Caption.rectTransform, new Vector2(.035f, .018f), new Vector2(.965f, .132f), Vector2.zero, Vector2.zero);
            director.Progress = Label(canvasRoot.transform, "Role footer", 20, new Color(.97255f, .91765f, .80392f));
            Rect(director.Progress.rectTransform, new Vector2(.035f, .94f), new Vector2(.965f, .985f), Vector2.zero, Vector2.zero);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/TutorialSpotlight.mat");
            if (!material) { material = new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/GameMouseSpotlight.shader")); AssetDatabase.CreateAsset(material, Folder + "/TutorialSpotlight.mat"); }
            material.renderQueue = 3000; EditorUtility.SetDirty(material);
            director.Spotlight = Image(canvasRoot.transform, "Authored spotlight mask - production shader", Color.white); director.Spotlight.material = material;
            Rect(director.Spotlight.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            director.Spotlight.transform.SetAsFirstSibling(); director.Spotlight.gameObject.SetActive(false);
            var arenaRoot = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/BossFight/BossFightArena.prefab"), root.transform);
            director.ArenaRoot = arenaRoot; director.Arena = arenaRoot.GetComponentInChildren<BossArenaManager>(true);
            foreach (var camera in arenaRoot.GetComponentsInChildren<Camera>(true)) { camera.tag = "Untagged"; camera.enabled = false; }
            foreach (var listener in arenaRoot.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            foreach (var arenaCanvas in arenaRoot.GetComponentsInChildren<Canvas>(true)) if (arenaCanvas.renderMode == RenderMode.ScreenSpaceOverlay) { arenaCanvas.renderMode = RenderMode.ScreenSpaceCamera; arenaCanvas.worldCamera = director.Output; arenaCanvas.planeDistance = .6f; arenaCanvas.sortingOrder = 90; }
            arenaRoot.SetActive(false);
            var reviewTarget = AssetDatabase.LoadAssetAtPath<RenderTexture>(Folder + "/TutorialReview.renderTexture");
            if (!reviewTarget) { reviewTarget = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32); AssetDatabase.CreateAsset(reviewTarget, Folder + "/TutorialReview.renderTexture"); }
            var reviewRoot = new GameObject("Authored recording playback verification", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            reviewRoot.transform.SetParent(root.transform);
            var reviewCanvas = reviewRoot.GetComponent<Canvas>(); reviewCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var reviewImageObject = new GameObject("Authored decoded video image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            reviewImageObject.transform.SetParent(reviewRoot.transform, false);
            director.ReviewImage = reviewImageObject.GetComponent<RawImage>(); director.ReviewImage.texture = reviewTarget; director.ReviewImage.raycastTarget = false;
            Rect(director.ReviewImage.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            director.Review = reviewRoot.AddComponent<VideoPlayer>(); director.Review.playOnAwake = false; director.Review.isLooping = true;
            director.Review.renderMode = VideoRenderMode.RenderTexture; director.Review.targetTexture = reviewTarget;
            director.Review.audioOutputMode = VideoAudioOutputMode.None;
            director.ReviewTarget = reviewTarget; director.ReviewRoot = reviewRoot; reviewRoot.SetActive(false);
            director.FishContent = fishContent; director.GhostContent = ghostContent;
            PrefabUtility.SaveAsPrefabAsset(root, Folder + "/TutorialRecordingStage.prefab");
            Object.DestroyImmediate(root);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/TutorialRecordingStage.prefab"));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Authored TutorialRecording scene and reusable stage saved. No production scene/prefab modified.");
        }
        finally
        {
            if (setup.Length > 0 && !string.IsNullOrEmpty(setup[0].path)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
    static HotelPlayer Actor(Transform parent, bool ghost)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/HotelNetworkPlayer.prefab"), parent);
        root.name = ghost ? "Authored offline ghost command owner" : "Authored fish - production motor";
        var player = root.GetComponent<HotelPlayer>(); player.Networked = false; player.enabled = ghost;
        player.GhostId = ghost ? 0u : uint.MaxValue;
        foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true)) renderer.sortingOrder = renderer.name == "Fish Body" ? 6 : 5;
        root.transform.position = new Vector3(-8, 1, 0);
        return player;
    }
    static GhostTrap Trap(string source, string name)
    {
        string target = Folder + "/" + name + ".prefab";
        var root = PrefabUtility.LoadPrefabContents("Assets/Resources/Traps/" + source + ".prefab");
        try
        {
            var trap = root.GetComponent<GhostTrap>();
            var presentation = root.GetComponentInChildren<GhostTrapAreaPresentation>(true);
            var bounds = presentation ? presentation.OpaqueArtBounds : new Rect(0, 0, 1, 1);
            var fields = new SerializedObject(trap); fields.FindProperty("_Presentation").objectReferenceValue = null; fields.ApplyModifiedPropertiesWithoutUndo();
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true)) renderer.enabled = false;
            if (presentation) Object.DestroyImmediate(presentation);
            var art = new GameObject("Authored tutorial trap artwork"); art.transform.SetParent(root.transform, false);
            var sprite = art.AddComponent<SpriteRenderer>(); sprite.sprite = trap.Icon; sprite.sortingOrder = 3;
            float width = Mathf.Max(.01f, bounds.width), height = Mathf.Max(.01f, bounds.height);
            float aspect = trap.Icon.rect.width * width / (trap.Icon.rect.height * height);
            float artHeight = Mathf.Min(trap.BodyHalfSize.y * 2, Mathf.Min(trap.ObjectSize.y, trap.ObjectSize.x / aspect));
            var size = trap.Icon.bounds.size;
            float x = artHeight * aspect / (width * size.x), y = artHeight / (height * size.y);
            sprite.transform.localScale = new Vector3(x, y, 1);
            if (trap.PlacementMode == TrapPlacementMode.HallwayCeiling) { fields.FindProperty("_ArmedOffset").vector3Value = Vector3.up * (4.11f - trap.BodyHalfSize.y * 2); fields.FindProperty("_FallDistance").floatValue = 4.11f - trap.BodyHalfSize.y * 2; fields.ApplyModifiedPropertiesWithoutUndo(); }
            var animated = root.AddComponent<TutorialTrapArt>(); animated.Trap = trap; animated.Art = sprite;
            sprite.transform.localPosition = new Vector3(-(trap.Icon.bounds.min.x + size.x * bounds.center.x) * x, trap.BodyHalfSize.y - (trap.Icon.bounds.min.y + size.y * bounds.yMax) * y, -.05f);
            return PrefabUtility.SaveAsPrefabAsset(root, target).GetComponent<GhostTrap>();
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static TutorialRoleContent Content(bool ghost)
    {
        string role = ghost ? "Ghost" : "Fish", path = Folder + "/" + role + "TutorialContent.asset";
        var content = AssetDatabase.LoadAssetAtPath<TutorialRoleContent>(path);
        if (!content) { content = ScriptableObject.CreateInstance<TutorialRoleContent>(); AssetDatabase.CreateAsset(content, path); }
        content.Role = ghost ? TutorialRole.Ghost : TutorialRole.Fish; content.RoleLabel = role; content.Title = ghost ? "How to haunt the hotel" : "How to survive the hotel";
        content.Recording = AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/TutorialVideos/" + role + "TutorialProduction.mp4");
        content.RecordingInk = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Dialogue/TutorialRecording/" + role + "Recording.json");
        content.Loop = true; content.HasAudio = false;
        if (content.Recording) { content.RecordedWidth = (int)content.Recording.width; content.RecordedHeight = (int)content.Recording.height; content.RecordedSeconds = content.Recording.length; content.RecordedFrameRate = content.Recording.frameRate; content.RecordedFrames = content.Recording.frameCount; }
        content.Summary = ghost ?
            "WASD / arrows: fly. E / X: open a nearby conveyor package, place in a clear space, possess your placed trap or unpossess. Cart: A / D or stick moves it. Chandelier: aim ceiling placement with mouse; Space / A drops it. Stationary click trap: click its body. F / B resets when clear; Q disposes held or possessed traps. Starting safe hallway forbids traps. Contact from moving or active traps raises spook; at 100 the fish becomes ghost and you take its body. Choose Ready; after everyone is ready you have 10 seconds to set up." :
            "A / D or arrows: walk. Shift: run. Space: jump. Gamepad: left stick, stick click, A. Mouse aims the spotlight. Avoid carts and falling chandeliers; reach the stairwell and press E / X to challenge the ghost. Moving or active trap contact raises spook; at 100 you become ghost and the old ghost takes your body. Fish wins at 3 goals; ghost at 1. Others spectate. Choose Ready, wait for everyone, then the 10-second ghost setup before moving. Hallway return includes another ghost head start.";
        content.Instructions = ghost ? new[] {
            Instruction("Fly and collect", "WASD / arrows / left stick flies. E / gamepad X opens a nearby package from the conveyor above the hallway."),
            Instruction("Place legally", "Carry to a clear floor position and E / X places. Keep trap footprints outside the safe starting hallway, fish and other traps. Chandelier placement aims at the ceiling with the mouse within reach."),
            Instruction("Possess and use", "E / X near your placed trap possesses it. Cart: A / D / stick moves. Chandelier: Space / gamepad A drops. Stationary click trap: click its body."),
            Instruction("Release, reset, dispose", "E / X unpossesses. F / gamepad B resets when the reset position is clear. Q discards a held trap or removes a possessed one."),
            Instruction("Spook and role swap", "Contact from moving or active traps raises spook. At 100 the fish becomes the ghost and the old ghost takes its fish body."),
            Instruction("Ghost challenge", "Click and drag your striker across both axes of your own half to hit the cue ball. Drag sideline defenses onto your half; they bounce the ball and break after 1-3 hits. Ghost wins at 1 goal; fish at 3. Other participants spectate; hallway return includes a ghost head start."),
            Instruction("Round start", "Read the role page and deliberately choose Ready. Everyone must be ready before the 10-second ghost setup and fish release.")
        } : new[] {
            Instruction("Move and jump", "A / D or arrows walks; Shift runs; Space jumps from the ground. Gamepad: left stick, stick click to run, A to jump."),
            Instruction("See and avoid traps", "Mouse aims the spotlight. Watch the floor and ceiling, run and jump past moving carts and falling chandeliers."),
            Instruction("Spook and role swap", "Contact from moving or active traps raises spook. At 100 you become ghost and the old ghost takes your fish body."),
            Instruction("Stairwell challenge", "Reach the stairwell door and press E / gamepad X. Click and drag your striker across both axes of your own half to hit the cue ball. Drag sideline defenses onto your half; they bounce the ball and break after 1-3 hits."),
            Instruction("Win and return", "Fish wins at 3 goals; ghost wins at 1. Other participants spectate. The hallway return includes another ghost head start."),
            Instruction("Round start", "Read the role page and deliberately choose Ready. Everyone must be ready; wait through the 10-second ghost setup before moving.")
        };
        if (string.IsNullOrEmpty(content.CaptureProvenance)) content.CaptureProvenance = "Existing real production recording; authored replacement capture pending.";
        EditorUtility.SetDirty(content); return content;
    }
    static TutorialInstruction Instruction(string heading, string body) => new TutorialInstruction { Heading = heading, Body = body };
    static TutorialShot Shot(Transform root, string name, float x, float y, float size)
    {
        var marker = new GameObject("Camera shot - " + name); marker.transform.SetParent(root); marker.transform.position = new Vector3(x, y, -18);
        return new TutorialShot { Name = name, Pose = marker.transform, Size = size };
    }
    static void Fit(SpriteRenderer renderer, Vector2 target)
    {
        var size = renderer.sprite.bounds.size; float scale = Mathf.Min(target.x / size.x, target.y / size.y); renderer.transform.localScale = Vector3.one * scale;
    }
    static Image Image(Transform parent, string name, Color color)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); item.transform.SetParent(parent, false);
        var image = item.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
    }
    static Text Label(Transform parent, string name, int size, Color color)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)); item.transform.SetParent(parent, false);
        var label = item.GetComponent<Text>(); label.font = name == "Heading" ? headingFont : bodyFont; label.fontSize = size; label.color = color; label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate; label.alignment = TextAnchor.MiddleLeft; return label;
    }
    static void Rect(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax; }
}
#endif
