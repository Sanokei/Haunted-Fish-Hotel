using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Asset-only authoring. Never opens, modifies or saves a scene.
// LobbyAtmosphereAuthoringTask remains the scene integration owner.
public static class LobbyMoodyInteriorAuthoringTask
{
    public const string ProfilePath = "Assets/Prefabs/LobbyMood/LobbyMoodyInteriorProfile.asset";
    const string Evidence = "C:/Users/wkeif/Documents/Codex/2026-10-10/task-4/LobbyMoodEvidence";
    const string ProjectAssets = "C:/Users/wkeif/Documents/GitHub/Haunted Fish Hotel/Assets";
    static bool finished;


    static void Check()
    {
        if (finished || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode ||
            !File.Exists(Evidence + "/author.request")) return;
        finished = true;
        
        if (File.Exists(Evidence + "/native-profile-saved.txt") &&
            File.Exists(Evidence + "/preview-native.txt")) return;
        try { AuthorProfile(); }
        catch (Exception error)
        {
            Directory.CreateDirectory(Evidence);
            File.WriteAllText(Evidence + "/native-error.txt", error.ToString());
            Debug.LogException(error);
        }
    }

    [MenuItem("Tools/Haunted Fish Hotel/Lobby/Author isolated moody interior profile")]
    public static void AuthorProfile()
    {
        if (!LobbyStormRepairTask.RequireSafeEditMode()) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Profile authoring requires Edit Mode.");
        Directory.CreateDirectory(Evidence);
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (!profile)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/LobbyMood"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "LobbyMood");
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Lobby moody interior";
            AssetDatabase.CreateAsset(profile, ProfilePath);
            Configure(profile);
            foreach (var component in profile.components)
            {
                component.name = component.GetType().Name;
                component.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
                AssetDatabase.AddObjectToAsset(component, profile);
                EditorUtility.SetDirty(component);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
        }
        // Complete the import before querying canonical persistent component identities.
        AssetDatabase.ImportAsset(ProfilePath,
            ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        // A retry validates the same asset rather than adding duplicate sub-assets.
        profile = AssetDatabase.LoadAllAssetsAtPath(ProfilePath).OfType<VolumeProfile>().Single();
        File.WriteAllLines(Evidence + "/native-components.txt", profile.components.Select(component =>
        {
            if (!component) return "NULL";
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(component, out string componentGuid, out long componentId);
            return component.GetType().FullName + " active=" + component.active +
                " persistent=" + EditorUtility.IsPersistent(component) +
                " subAsset=" + AssetDatabase.IsSubAsset(component) +
                " path=" + AssetDatabase.GetAssetPath(component) + " guid=" + componentGuid + " fileID=" + componentId;
        }));
        Validate(profile);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(profile, out string guid, out long id);
        File.WriteAllLines(Evidence + "/native-profile-saved.txt", new[]
        {
            "PASS native AssetDatabase-authored VolumeProfile; 12 persistent component sub-assets.",
            "Profile=" + ProfilePath + " guid=" + guid + " fileID=" + id,
            "Scene files, runtime scripts, storm/audio sources, Skip and menus were not edited.",
            "Neutral tonemapping; exposure -0.35EV; contrast 6; saturation -10; temperature -7; tint 2.",
            "Lift +0.002 protects near-black detail; gamma +0.008; gain -0.015. Gentle cool/warm split.",
            "Bloom 0.10; threshold 1.25; scatter 0.45; clamp 3; vignette 0.13; grain 0.025.",
            "Depth of field, motion blur, chromatic aberration and lens distortion explicitly overridden off.",
            "Native reload/bindings validated. Live scene integration and rendered visual QA remain required."
        });
        File.WriteAllText(Evidence + "/profile-path.txt", ProfilePath);
        if (File.Exists(Evidence + "/native-error.txt")) File.Delete(Evidence + "/native-error.txt");
        Debug.Log("PASS: saved isolated Lobby moody interior profile and validated native sub-assets.");
        try { CapturePreview(profile); }
        catch (Exception error) { File.WriteAllText(Evidence + "/preview-error.txt", error.ToString()); }
    }

    static void Configure(VolumeProfile profile)
    {
        profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);
        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(-.35f);
        color.contrast.Override(6f);
        color.saturation.Override(-10f);
        color.hueShift.Override(0f);
        color.colorFilter.Override(new Color(.985f, .995f, 1f, 1f));
        var white = profile.Add<WhiteBalance>(true);
        white.temperature.Override(-7f);
        white.tint.Override(2f);
        var split = profile.Add<SplitToning>(true);
        split.shadows.Override(new Color(.475f, .500f, .515f, 1f));
        split.highlights.Override(new Color(.530f, .505f, .485f, 1f));
        split.balance.Override(-15f);
        var balls = profile.Add<LiftGammaGain>(true);
        balls.lift.Override(new Vector4(1f, 1f, 1f, .002f));
        balls.gamma.Override(new Vector4(1f, 1f, 1f, .008f));
        balls.gain.Override(new Vector4(1f, 1f, 1f, -.015f));
        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(.10f);
        bloom.threshold.Override(1.25f);
        bloom.scatter.Override(.45f);
        bloom.clamp.Override(3f);
        bloom.tint.Override(Color.white);
        bloom.highQualityFiltering.Override(true);
        bloom.dirtIntensity.Override(0f);
        bloom.dirtTexture.Override(null);
        var vignette = profile.Add<Vignette>(true);
        vignette.color.Override(Color.black);
        vignette.center.Override(new Vector2(.5f, .5f));
        vignette.intensity.Override(.13f);
        vignette.smoothness.Override(.65f);
        vignette.rounded.Override(true);
        var grain = profile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(.025f);
        grain.response.Override(.8f);
        // Active zero overrides also neutralize lower-priority/default effects.
        profile.Add<DepthOfField>(true).mode.Override(DepthOfFieldMode.Off);
        profile.Add<MotionBlur>(true).intensity.Override(0f);
        profile.Add<ChromaticAberration>(true).intensity.Override(0f);
        profile.Add<LensDistortion>(true).intensity.Override(0f);
    }

    static T Require<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet<T>(out var component) || !component)
            throw new InvalidOperationException("Missing linked " + typeof(T).Name);
        if (!component.active)
            throw new InvalidOperationException("Inactive authored " + typeof(T).Name);
        bool persistent = EditorUtility.IsPersistent(component);
        string path = AssetDatabase.GetAssetPath(component);
        bool identified = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(component, out string guid, out long id);
        string expectedGuid = AssetDatabase.AssetPathToGUID(ProfilePath);
        bool canonical = AssetDatabase.LoadAllAssetsAtPath(ProfilePath).Any(asset => asset == component);
        // Native database identity plus canonical reload establishes persistence;
        // IsSubAsset is retained in diagnostics rather than used as the sole oracle
        // immediately after adding/importing components.
        if (!persistent || path != ProfilePath || !identified || guid != expectedGuid ||
            id == 0 || id == 11400000 || !canonical)
            throw new InvalidOperationException(typeof(T).Name + " persistence failed: persistent=" +
                persistent + " path=" + path + " identified=" + identified +
                " guid=" + guid + " fileID=" + id + " canonical=" + canonical);
        return component;
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Validate(VolumeProfile profile)
    {
        Assert(profile && AssetDatabase.Contains(profile), "Profile must be a saved native asset.");
        Assert(profile.components.Count == 12 && profile.components.All(x => x) &&
            profile.components.Select(x => x.GetType()).Distinct().Count() == 12,
            "Profile has missing or duplicate components.");
        var tone = Require<Tonemapping>(profile);
        var color = Require<ColorAdjustments>(profile);
        var white = Require<WhiteBalance>(profile);
        var split = Require<SplitToning>(profile);
        var balls = Require<LiftGammaGain>(profile);
        var bloom = Require<Bloom>(profile);
        var vignette = Require<Vignette>(profile);
        var grain = Require<FilmGrain>(profile);
        Assert(tone.mode.overrideState && tone.mode.value == TonemappingMode.Neutral, "Neutral tone binding");
        Assert(color.postExposure.overrideState && Mathf.Approximately(color.postExposure.value, -.35f) &&
            color.contrast.value == 6f && color.saturation.value == -10f, "Color grade binding");
        Assert(white.temperature.overrideState && white.temperature.value == -7f && white.tint.value == 2f, "White balance binding");
        Assert(split.shadows.overrideState && split.highlights.overrideState && split.balance.value == -15f, "Split tone binding");
        Assert(balls.lift.overrideState && balls.lift.value.w > 0f && balls.gamma.value.w > 0f, "Shadow/midtone protection");
        Assert(bloom.intensity.overrideState && bloom.intensity.value <= .12f && bloom.threshold.value >= 1.2f &&
            bloom.dirtIntensity.value == 0f, "Restrained bloom binding");
        Assert(vignette.intensity.overrideState && vignette.intensity.value <= .15f &&
            grain.intensity.overrideState && grain.intensity.value <= .03f, "Vignette/grain binding");
        var dof = Require<DepthOfField>(profile);
        var blur = Require<MotionBlur>(profile);
        var ca = Require<ChromaticAberration>(profile);
        var lens = Require<LensDistortion>(profile);
        Assert(dof.mode.overrideState && dof.mode.value == DepthOfFieldMode.Off &&
            blur.intensity.overrideState && blur.intensity.value == 0f &&
            ca.intensity.overrideState && ca.intensity.value == 0f &&
            lens.intensity.overrideState && lens.intensity.value == 0f, "Sharp presentation overrides");
    }

    // Opens a native preview copy of the saved scene. No production scene objects
    // are changed, no scene is saved, and no second Unity editor is launched.
    static void CapturePreview(VolumeProfile profile)
    {
        if (!LobbyStormRepairTask.RequireSafeEditMode()) return;
        var preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Lobby.unity");
        try
        {
            var roots = preview.GetRootGameObjects();
            var camera = roots.SelectMany(x => x.GetComponentsInChildren<Camera>(true))
                .First(x => x.name == "Main Camera");
            camera.enabled = false;
            camera.scene = preview;
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (!data) throw new InvalidOperationException("Saved Lobby camera has no URP data.");
            data.renderPostProcessing = true;
            data.volumeLayerMask = 1 << 31;
            data.volumeTrigger = camera.transform;
            var volumes = roots.SelectMany(x => x.GetComponentsInChildren<Volume>(true)).ToArray();
            foreach (var volume in volumes) volume.gameObject.layer = 31;
            var global = volumes.Single(x => x.isGlobal && x.name == "Global Volume");
            var originalProfile = global.sharedProfile;
            var originalWeight = global.weight;
            var originalPriority = global.priority;
            var world = roots.SelectMany(x => x.GetComponentsInChildren<MonoBehaviour>(true))
                .Single(x => x && x.GetType().Name == "LobbyIntroductionWorldSequence");
            var serialized = new SerializedObject(world);
            var entry = (Transform)serialized.FindProperty("_EntryCamera").objectReferenceValue;
            var reception = (Transform)serialized.FindProperty("_ReceptionCamera").objectReferenceValue;
            if (!entry || !reception) throw new InvalidOperationException("Saved dolly poses are unbound.");
            var poseNames = new[] { "01-entry", "02-dolly", "03-reception", "04-grandma", "05-fireplace" };
            for (int view = 0; view < poseNames.Length; view++)
            {
                camera.nearClipPlane = .05f;
                camera.farClipPlane = 100f;
                if (view < 3)
                {
                    float t = view * .5f;
                    camera.transform.position = Vector3.Lerp(entry.position, reception.position, t);
                    camera.transform.rotation = Quaternion.Slerp(entry.rotation, reception.rotation, t);
                }
                else if (view == 3)
                {
                    var plaque = roots.Single(x => x.name == "Great Grandfish").transform.Find("img");
                    if (!plaque) throw new InvalidOperationException("Grandma plaque missing from saved scene.");
                    camera.transform.position = plaque.position - plaque.forward * 4.8f;
                    camera.transform.LookAt(plaque.position);
                    camera.fieldOfView = 38f;
                }
                else
                {
                    camera.transform.position = new Vector3(3.9f, 3.3f, -4.4f);
                    camera.transform.LookAt(new Vector3(6.9f, 1.25f, -.4f));
                    camera.fieldOfView = 38f;
                }
                global.sharedProfile = originalProfile;
                global.weight = originalWeight;
                global.priority = originalPriority;
                Capture(camera, Evidence + "/" + poseNames[view] + "-before.png");
                global.sharedProfile = profile;
                global.weight = 1f;
                global.priority = 1000f;
                Capture(camera, Evidence + "/" + poseNames[view] + "-candidate.png");
            }
            data.renderPostProcessing = false;
            camera.transform.position = entry.position;
            camera.transform.LookAt(new Vector3(-6.9f, 2.2f, 11.4f));
            camera.fieldOfView = 52f;
            Capture(camera, Evidence + "/06-stairs-no-postprocessing.png");
            var enclosure = roots.SelectMany(x => x.GetComponentsInChildren<Transform>(true))
                .Where(x => x.name.IndexOf("stairwell", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            x.name.IndexOf("closure", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(x => x.name).ToArray();
            File.WriteAllLines(Evidence + "/preview-native.txt", new[]
            {
                "PASS saved Lobby snapshot rendered through native URP camera in an isolated preview scene.",
                "Five matching baseline/candidate poses plus unprocessed stair sightline. Scene was never saved.",
                "Candidate swaps only the existing Global Volume in the preview copy.",
                "Geometry names containing stairwell/closure: " + string.Join(" | ", enclosure),
                "This verifies authored static scene sprites. Spawned gameplay fish, lightning, live Skip/menu interaction remain separate checks.",
                "Production Lobby, storm bindings, UI assets and native loaded scene state were not changed."
            });
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    static void Capture(Camera camera, string path)
    {
        var target = new RenderTexture(1600, 1000, 24);
        target.Create();
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        Texture2D pixels = null;
        try
        {
            camera.targetTexture = target;
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (!RenderPipeline.SupportsRenderRequest(camera, request))
                throw new InvalidOperationException("Active pipeline cannot submit a native camera render request.");
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = target;
            pixels = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous;
            RenderTexture.active = active;
            if (pixels) UnityEngine.Object.DestroyImmediate(pixels);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
