// Staged outside Assets: import into Assets/Editor ONLY after confirming Edit mode.
// No scene switches, saves, Play-mode entry, network spawning, or live camera mutation.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HauntedFish.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Monologue.Dialogue;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HallwayStagingPreviewTask
{
    const string Project = "C:/Users/wkeif/Documents/GitHub/Haunted Fish Hotel";
    const string Evidence = "C:/Users/wkeif/Documents/Codex/2026-10-09/task-3/HallwayPreview";
    const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static bool Finished;
    static string LastStatus;
    static double NextCheck;
    static HallwayStagingPreviewTask()
    {
        if (Application.dataPath.Replace('\\', '/') == Project + "/Assets") EditorApplication.update += Check;
    }
    static void Status(string value)
    {
        if (LastStatus == value) return;
        Directory.CreateDirectory(Evidence);
        File.WriteAllText(Evidence + "/status.txt", value);
        LastStatus = value;
    }
    static void Check()
    {
        if (Finished || EditorApplication.timeSinceStartup < NextCheck) return;
        NextCheck = EditorApplication.timeSinceStartup + 1;
        if (!File.Exists(Evidence + "/request.txt")) return;
        string token = File.ReadAllText(Evidence + "/request.txt").Trim();
        if (token.Length == 0) return;
        if (File.Exists(Evidence + "/handled.txt") && File.ReadAllText(Evidence + "/handled.txt") == token)
        { Finished = true; EditorApplication.update -= Check; return; }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { Status("WAIT: editor import/compile is busy."); return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Status("BLOCKED: requires Edit mode; never stops gameplay."); return; }
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) { Status("BLOCKED: close prefab editing stage first; no stage switching is performed."); return; }
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) { Status("BLOCKED: loaded scene has unsaved changes; no scene writes."); return; }
        if (HotelPlayer.ActivePlayers.Count != 0 || HotelPlayer.LocalPlayer || GameSceneController.Current)
        { Status("BLOCKED: live/player scene state is present; requires empty runtime registries."); return; }
        if (typeof(GhostSelectionPresentation).GetMethod("AddHallwayFish", Instance) == null)
        { Status("WAIT: current hallway production script is not imported."); return; }
        Finished = true;
        EditorApplication.update -= Check;
        Run(token);
    }
    static FieldInfo Field(object value, string name) => value.GetType().GetField(name, Instance) ?? throw new Exception("Missing preview field " + name);
    static void Set(object value, string name, object data) => Field(value, name).SetValue(value, data);
    static object Get(object value, string name) => Field(value, name).GetValue(value);
    static void Call(object value, string name, params object[] args)
    {
        var method = value.GetType().GetMethod(name, Instance) ?? throw new Exception("Missing production method " + name);
        method.Invoke(value, args);
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static string SetupKey(SceneSetup[] values) => string.Join("|", values.Select(x => x.path + ":" + x.isLoaded + ":" + x.isActive));
    static GameObject Empty(string name)
    {
        var value = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
        value.SetActive(false);
        return value;
    }
    static IDictionary Visuals(GhostSelectionPresentation selection)
    {
        var owner = Get(selection, "_HallwayFish");
        return (IDictionary)owner.GetType().GetProperty("Visuals").GetValue(owner);
    }
    static RectTransform VisualRoot(object visual) => (RectTransform)visual.GetType().GetField("Root", Instance).GetValue(visual);
    static void ImmediateRelease<T>(T visual)
    {
        var root = VisualRoot(visual);
        if (!root) return;
        root.gameObject.SetActive(false);
        Object.DestroyImmediate(root.gameObject);
    }
    static void RetireEditorPreview(GhostSelectionPresentation selection, StoryUI attic)
    {
        // Runtime uses deferred Destroy. Edit-mode preview uses immediate native disposal,
        // while still exercising the actual owned registry's End and presentation Close path.
        // This is not a claim about deferred Play-mode GameObject destruction.
        var owner = Get(selection, "_HallwayFish");
        var release = owner.GetType().GetField("_Release", Instance);
        var itemType = release.FieldType.GetGenericArguments()[0];
        var method = typeof(HallwayStagingPreviewTask).GetMethod(nameof(ImmediateRelease), Static).MakeGenericMethod(itemType);
        release.SetValue(owner, Delegate.CreateDelegate(release.FieldType, method));
        var container = (RectTransform)Get(selection, "_HallwayFishRoot");
        Set(selection, "_HallwayFishRoot", null);
        Set(selection, "_Attic", null);
        Call(selection, "Close");
        Require(Visuals(selection).Count == 0, "Production registry retained copies after Close");
        if (container) Object.DestroyImmediate(container.gameObject);
        if (attic) Object.DestroyImmediate(attic.gameObject);
    }
    static void SetFixtureId(HotelPlayer player, uint id)
    {
        var identity = player.GetComponent<Mirage.NetworkIdentity>();
        // Initialize only a disabled fixture's identity cache; never invoke gameplay Awake.
        for (Type type = player.GetType(); type != null; type = type.BaseType)
            foreach (var field in type.GetFields(Instance | BindingFlags.DeclaredOnly))
                if (!field.IsInitOnly && field.FieldType == typeof(Mirage.NetworkIdentity)) field.SetValue(player, identity);
        var property = identity.GetType().GetProperty("NetId", Instance);
        Require(property != null && property.GetSetMethod(true) != null, "Fixture identity has no writable NetId");
        property.SetValue(identity, id);
        Require(player.NetId == id, "Disabled fixture identity did not resolve");
    }
    static bool Capture(Camera camera, string filename)
    {
        var render = new RenderTexture(1280, 720, 24);
        render.Create();
        var priorTarget = camera.targetTexture;
        var priorActive = RenderTexture.active;
        Texture2D texture = null;
        try
        {
            camera.targetTexture = render;
            var request = new RenderPipeline.StandardRequest { destination = render };
            if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
            else camera.Render();
            RenderTexture.active = render;
            texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Evidence + "/" + filename, texture.EncodeToPNG());
            var pixels = texture.GetPixels32();
            var first = pixels[0];
            int varied = pixels.Count(x => Math.Abs(x.r - first.r) + Math.Abs(x.g - first.g) + Math.Abs(x.b - first.b) > 12);
            return varied > 1024;
        }
        finally
        {
            camera.targetTexture = priorTarget;
            RenderTexture.active = priorActive;
            if (texture) Object.DestroyImmediate(texture);
            render.Release();
            Object.DestroyImmediate(render);
        }
    }
    static void Run(string token)
    {
        Directory.CreateDirectory(Evidence);
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var activeScene = SceneManager.GetActiveScene();
        var selectionBefore = Selection.objects;
        var randomBefore = UnityEngine.Random.state;
        var gameCurrent = typeof(GameSceneController).GetField("<Current>k__BackingField", Static);
        var actors = (IList<HotelPlayer>)typeof(HotelPlayer).GetField("_ActivePlayers", Static).GetValue(null);
        var playerStatics = typeof(HotelPlayer).GetFields(Static).Where(x => !x.IsInitOnly && !x.IsLiteral)
            .Select(x => new KeyValuePair<FieldInfo, object>(x, x.GetValue(null))).ToArray();
        PreviewRenderUtility preview = null;
        GhostSelectionPresentation selection = null;
        StoryUI attic = null;
        var notes = new List<string>();
        bool passed = false;
        try
        {
            Require(actors.Count == 0 && gameCurrent.GetValue(null) == null, "Clean runtime registry guard failed");
            preview = new PreviewRenderUtility();
            var scene = preview.camera.gameObject.scene;
            Require(EditorSceneManager.IsPreviewScene(scene), "Camera is not isolated in a preview scene");
            EditorSceneManager.SetTargetSceneForNewGameObjects(scene);
            var camera = preview.camera;
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 360;
            camera.nearClipPlane = .1f; camera.farClipPlane = 1000;
            camera.transform.position = new Vector3(0, 0, -500);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = HotelPalette.Night;
            camera.cullingMask = ~0;
            var context = Empty("Disabled preview Game context").AddComponent<GameSceneController>();
            gameCurrent.SetValue(null, context);
            selection = Empty("Disabled preview presentation controller").AddComponent<GhostSelectionPresentation>();
            var fixturesRoot = Empty("Disabled non-network preview fixtures");
            var playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/HotelNetworkPlayer.prefab");
            var atticAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/AtticRoundCutscene.prefab");
            Require(playerAsset && atticAsset, "Authored preview assets are not imported");
            var roster = new uint[] { 101, 102, 103 };
            var positions = new Dictionary<uint, Vector3>();
            for (int i = 0; i < roster.Length; i++)
            {
                var fixture = (GameObject)PrefabUtility.InstantiatePrefab(playerAsset, fixturesRoot.transform);
                fixture.SetActive(false);
                var player = fixture.GetComponent<HotelPlayer>();
                player.Networked = false;
                SetFixtureId(player, roster[i]);
                player.transform.position = context.SpawnPosition(i);
                positions.Add(roster[i], player.transform.position);
                actors.Add(player);
            }
            Require(actors.All(x => !x.Networked && !x.gameObject.activeInHierarchy), "Fixture activation/network guard failed");
            for (int round = 1; round <= 3; round++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(atticAsset, scene);
                attic = instance.GetComponent<StoryUI>();
                var canvas = instance.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                ((RectTransform)instance.transform).sizeDelta = new Vector2(1280, 720);
                Set(selection, "_Attic", attic);
                attic.Prepare();
                Call(selection, "AddHallwayFish", new object[] { roster });
                var visuals = Visuals(selection);
                Require(visuals.Count == 3, "Native production staging did not create exactly three distinct copies");
                Require(instance.GetComponentsInChildren<HotelPlayer>(true).Length == 0 && instance.GetComponentsInChildren<Mirage.NetworkIdentity>(true).Length == 0,
                    "Production visual staging duplicated player/network components");
                var floor = attic.Find("hallway_floor");
                float floorY = floor.TransformPoint(new Vector3(0, floor.rect.yMax, 0)).y;
                var initialFacing = new Dictionary<uint, float>();
                var turned = new HashSet<uint>();
                foreach (float time in new[] { 0f, .6f, 1.3f })
                {
                    Set(selection, "_HallwayFishStarted", Time.unscaledTime - time);
                    Call(selection, "AnimateHallwayFish");
                    float previousX = float.NegativeInfinity;
                    foreach (uint id in roster)
                    {
                        var root = VisualRoot(visuals[id]);
                        var feet = root.TransformPoint(Vector3.zero);
                        Require(Mathf.Abs(feet.y - floorY) < .02f, "Native fish feet are not on the authored beam");
                        Require(feet.x > previousX, "Native roster order does not match starting hallway positions");
                        previousX = feet.x;
                        if (time == 0) initialFacing.Add(id, root.localScale.x);
                        else if (Mathf.Sign(root.localScale.x) != Mathf.Sign(initialFacing[id])) turned.Add(id);
                        notes.Add("round=" + round + " t=" + time + " id=" + id + " feet=" + feet.ToString("F3") + " facing=" + Mathf.Sign(root.localScale.x));
                    }
                    Canvas.ForceUpdateCanvases();
                    Require(Capture(camera, "round-" + round + "-look-" + time.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + ".png"), "Native preview capture is blank");
                }
                Require(turned.Count == 3, "Not every native staged fish turned in the sampled hallway beat");
                foreach (var actor in actors) Require(actor.transform.position == positions[actor.NetId], "Fixture actor moved during visual-only staging");
                RetireEditorPreview(selection, attic); attic = null;
                Require(Visuals(selection).Count == 0, "Copies retained after native preview cleanup");
                Require(!Capture(camera, "round-" + round + "-cleared.png"), "Native preview still shows artwork after cleanup");
                notes.Add("round=" + round + " native registry/copy cleanup PASS (Edit-mode immediate disposal hook)");
            }
            passed = true;
        }
        catch (Exception error) { File.WriteAllText(Evidence + "/error.txt", error.ToString()); }
        finally
        {
            try { if (selection && Get(selection, "_HallwayFish") != null) RetireEditorPreview(selection, attic); }
            catch (Exception error) { File.WriteAllText(Evidence + "/cleanup-error.txt", error.ToString()); passed = false; }
            actors.Clear();
            foreach (var state in playerStatics) state.Key.SetValue(null, state.Value);
            gameCurrent.SetValue(null, null);
            UnityEngine.Random.state = randomBefore;
            EditorSceneManager.SetTargetSceneForNewGameObjects(activeScene);
            if (preview != null) preview.Cleanup();
            bool same = SetupKey(EditorSceneManager.GetSceneManagerSetup()) == SetupKey(setup) && SceneManager.GetActiveScene() == activeScene &&
                Selection.objects.SequenceEqual(selectionBefore) && HotelPlayer.ActivePlayers.Count == 0 && GameSceneController.Current == null;
            for (int i = 0; i < SceneManager.sceneCount; i++) same &= !SceneManager.GetSceneAt(i).isDirty;
            if (!same) { passed = false; File.WriteAllText(Evidence + "/state-error.txt", "Editor scene/selection/registry state did not match clean snapshot."); }
            File.WriteAllLines(Evidence + "/measurements.txt", notes);
            File.WriteAllText(Evidence + "/handled.txt", token);
            File.WriteAllText(Evidence + "/result.txt", passed ?
                "PASS native isolated Edit-mode prefab rendering and geometry/pose/three-repeat registry cleanup; three disabled representative fixtures at default Game spawn positions. Uses immediate Editor disposal hook, not Play-mode deferred Destroy. Scene switches/saves, networking, live roster gameplay, coroutine completion/disable delivery and actual deferred runtime destruction were NOT tested. Images still require visual inspection." :
                "FAILED native preview; inspect error/state/cleanup files. No visual pass is claimed.");
            Status(passed ? "Native preview completed; inspect images before accepting visual quality." : "Native preview failed; no visual pass claimed.");
        }
    }
}