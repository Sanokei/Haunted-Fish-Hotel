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
            headingFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/HauntedIntertitle-Regular.ttf");
            bodyFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/HauntedIntertitle-Regular.ttf");
            if (!headingFont || !bodyFont) throw new InvalidOperationException("Original Haunted Intertitle recording caption font missing.");
            var cart = Trap("ShoppingCartTrap", "TutorialCart");
            var chandelier = Trap("FallingChandelierTrap", "TutorialChandelier");
            var click = Trap("ClickTrap", "TutorialClickTrap");
            var angler = Trap("AnglerFishTrap", "TutorialAngler");
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
            var door = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/HallwayDoor.prefab"), hall.transform);
            door.transform.position = new Vector3(12, 2.055f, 1.9f);
            var worldObject = new GameObject("Production scoped trap inventory"); worldObject.transform.SetParent(root.transform);
            director.World = worldObject.AddComponent<GhostPlacementWorld>();
            director.Haunting = worldObject.AddComponent<GameHauntingController>();
            var fields = new SerializedObject(director.World);
            fields.FindProperty("_TrapPrefabs").arraySize = 4;
            fields.FindProperty("_TrapPrefabs").GetArrayElementAtIndex(0).objectReferenceValue = cart;
            fields.FindProperty("_TrapPrefabs").GetArrayElementAtIndex(1).objectReferenceValue = chandelier;
            fields.FindProperty("_TrapPrefabs").GetArrayElementAtIndex(2).objectReferenceValue = click;
            fields.FindProperty("_TrapPrefabs").GetArrayElementAtIndex(3).objectReferenceValue = angler;
            fields.FindProperty("_TrapContainer").objectReferenceValue = worldObject.transform;
            fields.FindProperty("_GraceZone").objectReferenceValue = safeCollider; fields.ApplyModifiedPropertiesWithoutUndo();
            director.CartDefinition = cart; director.ChandelierDefinition = chandelier; director.ClickDefinition = click; director.AnglerDefinition = angler;
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
            director.OtherFish = Actor(root.transform, false); director.OtherFish.name = "Authored second fish - production impact target";
            var fishPresentation = new SerializedObject(director.Fish.GetComponentInChildren<HotelFishSprite>(true));
            director.FishStunEffect = (GameObject)fishPresentation.FindProperty("_StunEffect").objectReferenceValue;
            director.FishStunAnimation = (Animation)fishPresentation.FindProperty("_StunAnimation").objectReferenceValue;
            if (!director.FishStunEffect || !director.FishStunAnimation || !director.Fish.DeathEmergence || !director.OtherFish.DeathEmergence)
                throw new InvalidOperationException("Production player prefab must bind authored piranha and death effects before recording.");
            var flight = new GameObject("Authored ghost flight"); flight.transform.SetParent(root.transform);
            director.GhostFlight = flight.transform; director.GhostArt = flight.AddComponent<SpriteRenderer>();
            director.GhostArt.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/BOSS-cursor.png");
            director.GhostArt.sortingOrder = 20; director.GhostArt.color = new Color(.9254902f, .99607843f, .7921569f, 1);
            director.GhostArt.transform.localScale = Vector3.one * .56f; // Exact controlled ghost canvas, tint and scale from Game.
            var carry = new GameObject("Authored held trap artwork"); carry.transform.SetParent(root.transform);
            director.CarriedPreview = carry.transform; director.CarryArt = carry.AddComponent<SpriteRenderer>();
            director.CarryArt.sprite = cart.Icon; director.CarryArt.color = new Color(1, .8f, .65f, .7f); director.CarryArt.sortingOrder = 8;
            Fit(director.CarryArt, cart.ObjectSize * .55f); carry.SetActive(false); // Opened package reads as a held item, not a placed trap.
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
                Shot(root.transform, "safe", -10, 1.5f, 3.2f), Shot(root.transform, "ceiling", 5, 1.5f, 3.5f),
                Shot(root.transform, "click", 10, 1.5f, 3.2f), Shot(root.transform, "door", 12, 1.5f, 3.2f),
                Shot(root.transform, "trap-wide", -1, 1.8f, 4.0f) };
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
            // Instructions live on the intertitle cards between scenes; nothing is drawn over gameplay.
            foreach (var hidden in new Component[] { shade, director.Heading, director.Caption, director.Progress }) hidden.gameObject.SetActive(false);
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
        var player = root.GetComponent<HotelPlayer>(); player.Networked = false; player.enabled = true;
        player.GhostId = ghost ? 0u : uint.MaxValue;
        foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            { if (renderer.name == "Fish Body") renderer.sortingOrder = 61; else if (renderer.name == "Fish Fin") renderer.sortingOrder = 60; } // In front of danger bands, as in Game.
        root.transform.position = new Vector3(-8, 1, 0);
        return player;
    }
    static GhostTrap Trap(string source, string name)
    {
        string target = Folder + "/" + name + ".prefab";
        string sourcePath = "Assets/Resources/Traps/" + source + ".prefab";
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath)) throw new InvalidOperationException("Missing production trap prefab: " + sourcePath);
        var root = PrefabUtility.LoadPrefabContents(sourcePath);
        try
        {
            var trap = root.GetComponent<GhostTrap>();
            // Film the production presentation intact: authored BOSS-cursor boundaries,
            // animated Fish-ver-ghost fill, possession light and separated trap artwork.
            // A single icon replacement loses the live danger band and moving-part bindings.
            var fields = new SerializedObject(trap);
            if (trap.PlacementMode == TrapPlacementMode.HallwayCeiling)
            {
                fields.FindProperty("_ArmedOffset").vector3Value = Vector3.up * (4.11f - trap.BodyHalfSize.y * 2);
                fields.FindProperty("_FallDistance").floatValue = 4.11f - trap.BodyHalfSize.y * 2;
                fields.ApplyModifiedPropertiesWithoutUndo();
            }
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
            "{input:Ghost/Move} Fly. {input:Ghost/Confirm}: open a nearby conveyor package, place in a clear space, possess your placed trap or unpossess. Dining cart: {input:Ghost/Move} moves it; hits knock back and briefly show the piranha stun effect without draining health. Angler: {input:Ghost/Move} drives through every fish, kills each crossed fish, and slows inside bodies; its light is on while possessed. Chandelier: aim ceiling placement with {input:Ghost/TrapPoint}; {input:Ghost/TrapActivate} drops it. Stationary click trap: {input:Ghost/TrapClick} on its body. {input:Ghost/Reset} resets when clear; {input:Ghost/Dispose} disposes held or possessed traps. Starting safe hallway forbids traps. Damaging traps drain health; dead fish spectate. Dining cart hits do not drain health. Choose Ready; after everyone is ready you have 10 seconds to set up." :
            "{input:Game/Move} Walk. {input:Game/Run} Run. {input:Game/Jump} Jump. {input:<Pointer>/position} Aim the spotlight. Hold run when jumping to clear the dining cart. Dining cart hits knock back and stun without draining health. Avoid the angler's lethal horizontal path and the swinging/falling chandelier; reach the stairwell and use {input:Game/Interact} to challenge the ghost. Damaging traps drain health; dead fish spectate. Dining cart hits do not drain health. Fish wins at 3 goals; ghost at 1. Others spectate. Choose Ready, wait for everyone, then the 10-second ghost setup before moving. Hallway return includes another ghost head start.";
        content.Instructions = ghost ? new[] {
            Instruction("Fly and collect", "{input:Ghost/Move} Fly. {input:Ghost/Confirm} opens a nearby package from the conveyor above the hallway."),
            Instruction("Place legally", "Carry to a clear floor position and {input:Ghost/Confirm} places. Keep trap footprints outside the safe starting hallway, fish and other traps. Chandelier placement aims at the ceiling with {input:Ghost/TrapPoint} within reach."),
            Instruction("Possess and use", "{input:Ghost/Confirm} near your placed trap possesses it. Dining cart: {input:Ghost/Move} moves; hits knock back and stun without killing. Chandelier: {input:Ghost/TrapActivate} drops. Stationary click trap: {input:Ghost/TrapClick} on its body."),
            Instruction("Angler", "{input:Ghost/Move} drives the angler horizontally through every fish. Each crossed fish dies; the angler slows while inside bodies. Its light is on only while possessed."),
            Instruction("Release, reset, dispose", "{input:Ghost/Confirm} unpossesses. {input:Ghost/Reset} resets when the reset position is clear. {input:Ghost/Dispose} discards a held trap or removes a possessed one."),
            Instruction("Health and spectating", "Damaging traps drain health. Dead fish spectate as their soul rises away. Dining cart hits only knock back and stun; the piranha effect clears on recovery."),
            Instruction("Ghost challenge", "{input:<Mouse>/leftButton} Hold and drag your striker across both axes of your own half to hit the cue ball. Drag sideline defenses onto your half; they bounce the ball and break after 1-3 hits. Ghost wins at 1 goal; fish at 3. Other participants spectate; hallway return includes a ghost head start."),
            Instruction("Round start", "Read the role page and deliberately choose Ready. Everyone must be ready before the 10-second ghost setup and fish release.")
        } : new[] {
            Instruction("Move and jump", "{input:Game/Move} Walk. {input:Game/Run} Run. {input:Game/Jump} Jump from the ground."),
            Instruction("See and avoid traps", "{input:<Pointer>/position} Aim the spotlight. Hold run when launching a jump to clear the dining cart. Watch for the angler's lethal horizontal rush and the swinging chandelier above."),
            Instruction("Health and spectating", "Damaging traps drain health. Dead fish spectate as their soul rises away. Dining cart hits only knock back and stun; the piranha effect clears on recovery."),
            Instruction("Stairwell challenge", "Reach the stairwell door and use {input:Game/Interact}. {input:<Mouse>/leftButton} Hold and drag your striker across both axes of your own half to hit the cue ball. Drag sideline defenses onto your half; they bounce the ball and break after 1-3 hits."),
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
