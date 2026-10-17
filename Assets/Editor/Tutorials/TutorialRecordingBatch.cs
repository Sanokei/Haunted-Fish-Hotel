#if UNITY_EDITOR
using System;
using System.IO;
using IOPath = System.IO.Path;
using System.Linq;
using HauntedFish.Tutorials;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.Video;
using Object = UnityEngine.Object;

public static class TutorialRecordingBatch
{
    const string Key = "HauntedFish.AuthoredTutorialRecording";
    static string Evidence => IOPath.GetFullPath(IOPath.Combine(Application.dataPath, "../Recordings/TutorialEvidence"));
    [Serializable] sealed class SceneRecord { public string Path; public bool Loaded, Active; }
    [Serializable] sealed class Journal { public SceneRecord[] Scenes; public bool Background; }
    static RecorderController recorder;
    static RecorderControllerSettings controllerSettings;
    static MovieRecorderSettings movieSettings;
    static TutorialRecordingDirector director;
    static int role;
    static double deadline, playbackStarted;
    static bool captureStarted, playbackStartedRole, sought, wrapped, playbackError;
    static long beforeWrap;
    static string output, recordedInkHash;
    [Serializable] sealed class CheckGroups { public string[] Fish, Ghost; }
    [Serializable] sealed class CapturePlan { public string revision; public CheckGroups required_capture_checks; }
    [Serializable] sealed class CaptureRecord
    {
        public bool completed;
        public string native_scene, source, source_sha256, ink_sha256, observations, observations_sha256;
    }
    [Serializable] sealed class CaptureRoles { public CaptureRecord Fish, Ghost; }
    [Serializable] sealed class CaptureManifest { public int schema_version = 1; public string plan_revision; public CaptureRoles captures; }
    static CapturePlan Plan => JsonUtility.FromJson<CapturePlan>(File.ReadAllText(IOPath.Combine(Application.dataPath, "../Tools/TheatreVideos/recording-plan.json")));
    static readonly string[] protectedPaths = { "Assets/Scenes/Game.unity", "Assets/Scenes/Lobby.unity", "Assets/Scenes/BossFight.unity", "Assets/Resources/FishRoundHowTo.prefab", "Assets/Resources/GhostRoundHowTo.prefab", "Assets/Resources/MemberMenu.prefab", "Assets/Scenes/theatre.unity", "Assets/Resources/HotelNetworkPlayer.prefab", "Assets/Resources/GameHallway.prefab", "Assets/Resources/HallwayDoor.prefab", "Assets/Prefabs/Radio/LobbyRadioPlayback.prefab" };

    [MenuItem("Tools/Haunted Fish/Authored tutorials/Record both role guides (owned scene)")]
    public static void ManualRecord()
    {
        if (!EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != TutorialRecordingAuthoring.ScenePath)
            throw new InvalidOperationException("Open the authored TutorialRecording scene and enter Play explicitly first.");
        if (SessionState.GetBool(Key, false)) throw new InvalidOperationException("A tutorial run is already armed.");
        Directory.CreateDirectory(Evidence);
        // GetSceneManagerSetup throws in Play mode; the runtime scene list carries the same paths.
        var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var scenes = Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Select(UnityEngine.SceneManagement.SceneManager.GetSceneAt);
        var journal = new Journal { Background = Application.runInBackground, Scenes = scenes.Select(s => new SceneRecord { Path = s.path, Loaded = s.isLoaded, Active = s == active }).ToArray() };
        SessionState.SetString(Key + ".Journal", JsonUtility.ToJson(journal));
        File.WriteAllLines(Evidence + "/protected-before.txt", protectedPaths.Select(p => p + " " + Hash(p)));
        SessionState.SetBool(Key + ".Batch", false);
        SessionState.SetInt(Key + ".Phase", 0); SessionState.SetInt(Key + ".Role", 0);
        SessionState.SetString(Key + ".Error", ""); SessionState.SetBool(Key, true);
        Attach();
    }
    // Launch only after the editor coordinator grants this task the exclusive editor window.
    public static void Run()
    {
        if (!Application.isBatchMode || !Environment.GetCommandLineArgs().Contains("--authored-tutorial-record"))
            throw new InvalidOperationException("Requires explicit owned --authored-tutorial-record batch flag.");
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Requires an idle editor.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Refusing unsaved scene changes.");
        Directory.CreateDirectory(Evidence);
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var journal = new Journal { Background = Application.runInBackground, Scenes = setup.Select(s => new SceneRecord { Path = s.path, Loaded = s.isLoaded, Active = s.isActive }).ToArray() };
        SessionState.SetString(Key + ".Journal", JsonUtility.ToJson(journal));
        File.WriteAllLines(Evidence + "/protected-before.txt", protectedPaths.Select(p => p + " " + Hash(p)));
        BakeSharedFontEvidence();
        TutorialRecordingAuthoring.Build();
        SessionState.SetBool(Key + ".Batch", true);
        SessionState.SetInt(Key + ".Phase", 0); SessionState.SetInt(Key + ".Role", 0);
        SessionState.SetString(Key + ".Error", ""); SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene(TutorialRecordingAuthoring.ScenePath);
        Attach(); EditorApplication.EnterPlaymode();
    }
    static void BakeSharedFontEvidence()
    {
        var helper = typeof(TutorialRecordingBatch).Assembly.GetType("HotelTypographyAuthoring");
        if (helper == null) throw new InvalidOperationException("Agreed shared font authoring dependency missing.");
        helper.GetMethod("BakeSharedFont").Invoke(null, null);
        var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Resources/HalloweenUI/Modak-Headings.asset");
        if (!font || font.characterTable.Count < 70 || font.atlasTextures.Length == 0)
            throw new InvalidOperationException("Native shared Modak glyph bake incomplete.");
        var atlas = font.atlasTextures[0];
        var target = RenderTexture.GetTemporary(atlas.width, atlas.height, 0, RenderTextureFormat.ARGB32);
        var prior = RenderTexture.active; var pixels = new Texture2D(atlas.width, atlas.height, TextureFormat.RGBA32, false);
        try
        {
            Graphics.Blit(atlas, target); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, atlas.width, atlas.height), 0, 0); pixels.Apply();
            File.WriteAllBytes(Evidence + "/Modak-native-atlas.png", pixels.EncodeToPNG());
            File.WriteAllText(Evidence + "/shared-font-bake.txt", "PASS HotelTypographyAuthoring.BakeSharedFont; " + font.faceInfo.familyName + "; actual native characters=" + font.characterTable.Count + "; atlas=" + atlas.width + "x" + atlas.height + "; preserved GUID=" + AssetDatabase.AssetPathToGUID("Assets/Resources/HalloweenUI/Modak-Headings.asset") + "; this is game UI typography evidence only. Silent guide cards use original Haunted Intertitle via the offline authoring renderer; validate its separate TMP asset with IntertitleTypographyValidation.Run.");
        }
        finally { RenderTexture.active = prior; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(pixels); }
    }
    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (SessionState.GetBool(Key + ".Batch", false) && (!Application.isBatchMode || !Environment.GetCommandLineArgs().Contains("--authored-tutorial-record")))
        { SessionState.SetBool(Key, false); return; }
        Attach(); // Resumes explicit armed command; never starts a new Play session on load.
    }
    static void Attach() { EditorApplication.update -= Tick; EditorApplication.update += Tick; deadline = EditorApplication.timeSinceStartup + 900; } // Both roles share one budget; Ghost alone runs ~2 min of source.
    static string Name => role == 0 ? "Fish" : "Ghost";
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        try
        {
            int phase = SessionState.GetInt(Key + ".Phase", 0);
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Authored tutorial capture/playback timeout, phase=" + phase);
            if (phase == 1)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                ExportCaptureManifest();
                SessionState.SetInt(Key + ".Phase", 3);
                Finish(); return;
            }
            if (phase == 3)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Finish(); return;
            }
            if (!EditorApplication.isPlaying) return;
            Application.runInBackground = true; EditorApplication.QueuePlayerLoopUpdate();
            role = SessionState.GetInt(Key + ".Role", 0);
            if (!director) director = Object.FindFirstObjectByType<TutorialRecordingDirector>();
            if (!director) throw new InvalidOperationException("Authored recording director missing.");
            if (phase == 2) { VerifyPlayback(); return; }
            if (!captureStarted) StartRecording();
            if (!string.IsNullOrEmpty(director.Failure)) throw new InvalidOperationException(director.Failure);
            if (!director.Completed) return;
            StopRecording();
            if (role == 0)
            {
                SessionState.SetInt(Key + ".Role", 1); captureStarted = false; return;
            }
            SessionState.SetInt(Key + ".Phase", 1); EditorApplication.ExitPlaymode();
        }
        catch (Exception error)
        {
            StopRecording(false);
            SessionState.SetString(Key + ".Error", error.ToString());
            SessionState.SetInt(Key + ".Phase", 3);
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode(); else Finish();
        }
    }
    static void StartRecording()
    {
        if (Camera.main != director.Output || !director.Output.isActiveAndEnabled)
            throw new InvalidOperationException("Recording must use the authored MainCamera output.");
        if (new DriveInfo(IOPath.GetPathRoot(Application.dataPath)).AvailableFreeSpace < 512L * 1024 * 1024)
            throw new IOException("Keep at least 512 MiB free for this bounded capture.");
        string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        output = Evidence + "/" + Name + "Tutorial-" + stamp;
        controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movieSettings.name = "Haunted Fish authored " + Name + " tutorial";
        movieSettings.Enabled = true;
        movieSettings.EncoderSettings = new CoreEncoderSettings { Codec = CoreEncoderSettings.OutputCodec.MP4, EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.Medium };
        movieSettings.CaptureAudio = false; movieSettings.CaptureAlpha = false;
        movieSettings.ImageInputSettings = new CameraInputSettings { Source = ImageSource.MainCamera, OutputWidth = 1280, OutputHeight = 720, CaptureUI = false };
        movieSettings.OutputFile = output;
        controllerSettings.AddRecorderSettings(movieSettings);
        controllerSettings.SetRecordModeToManual();
        controllerSettings.FrameRate = 30; controllerSettings.FrameRatePlayback = FrameRatePlayback.Constant;
        controllerSettings.CapFrameRate = true; controllerSettings.ExitPlayMode = false;
        recorder = new RecorderController(controllerSettings);
        recordedInkHash = Hash("Assets/Dialogue/TutorialRecording/" + Name + "Recording.ink").ToLowerInvariant();
        director.Begin(role == 0 ? TutorialRole.Fish : TutorialRole.Ghost);
        recorder.PrepareRecording();
        if (!recorder.StartRecording()) throw new InvalidOperationException("Unity Recorder did not start.");
        director.SetCapturing(true); captureStarted = true;
        File.WriteAllText(Evidence + "/progress.txt", "RECORDING " + Name + " real Unity Recorder frames. Output=" + output + ".mp4");
        Debug.Log("AUTHORED_TUTORIAL_RECORD_START " + Name);
    }
    static void StopRecording(bool success = true)
    {
        if (recorder == null) return;
        try
        {
            recorder.StopRecording();
            if (director) director.SetCapturing(false);
            if (!success) return;
            if (!director || !director.Completed || director.RenderedFrames < 300 || !File.Exists(output + ".mp4") || new FileInfo(output + ".mp4").Length < 1024)
                throw new InvalidOperationException("No completed real role capture.");
            if (role == 0 && !director.Observations.Contains("OBSERVED actual grounded jump")) throw new InvalidOperationException("Recorded fish sequence never performed a real grounded jump.");
            var checks = role == 0 ? Plan.required_capture_checks.Fish : Plan.required_capture_checks.Ghost;
            foreach (var check in checks)
                if (!director.Observations.Contains("PASS " + check)) throw new InvalidOperationException("Missing actual native observation: " + check);
            if (recordedInkHash != Hash("Assets/Dialogue/TutorialRecording/" + Name + "Recording.ink").ToLowerInvariant())
                throw new InvalidOperationException("Recording Ink changed during the capture.");
            File.WriteAllLines(output + "-observations.txt", director.Observations);
            var capture = new CaptureRecord { completed = true, native_scene = TutorialRecordingAuthoring.ScenePath,
                source = output + ".mp4", source_sha256 = Hash(output + ".mp4").ToLowerInvariant(), ink_sha256 = recordedInkHash,
                observations = output + "-observations.txt", observations_sha256 = Hash(output + "-observations.txt").ToLowerInvariant() };
            File.WriteAllText(Evidence + "/" + Name + "-capture.json", JsonUtility.ToJson(capture, true));
            File.WriteAllText(Evidence + "/" + Name + "-latest.txt", output + ".mp4");
            File.WriteAllText(Evidence + "/" + Name + "-capture.txt", "SOURCE=actual Unity Recorder MainCamera output from authored TutorialRecording scene\nROLE=" + Name +
                "\nUNITY_RENDERED_FRAMES=" + director.RenderedFrames + "\nSIMULATION_SECONDS=" + director.Elapsed.ToString("F3") +
                "\nMETHOD=production public command and motor APIs, isolated deterministic filming adapter; no network or readiness override in production scenes\nFILE=" + output + ".mp4");
        }
        finally
        {
            recorder = null; captureStarted = false;
            if (movieSettings) Object.DestroyImmediate(movieSettings);
            if (controllerSettings) Object.DestroyImmediate(controllerSettings);
            movieSettings = null; controllerSettings = null;
        }
    }
    static void ExportCaptureManifest()
    {
        var manifest = new CaptureManifest { plan_revision = Plan.revision, captures = new CaptureRoles {
            Fish = JsonUtility.FromJson<CaptureRecord>(File.ReadAllText(Evidence + "/Fish-capture.json")),
            Ghost = JsonUtility.FromJson<CaptureRecord>(File.ReadAllText(Evidence + "/Ghost-capture.json")) } };
        File.WriteAllText(Evidence + "/native-captures.json", JsonUtility.ToJson(manifest, true));
    }

    static void VerifyPlayback()
    {
        var video = director.Review;
        if (!playbackStartedRole)
        {
            director.ReviewRoot.SetActive(true);
            video.clip = role == 0 ? director.FishContent.Recording : director.GhostContent.Recording;
            video.errorReceived += OnVideoError; video.loopPointReached += OnVideoLoop;
            video.isLooping = true; video.Play();
            playbackStarted = EditorApplication.timeSinceStartup;
            playbackStartedRole = true; sought = wrapped = playbackError = false; beforeWrap = -1;
        }
        if (playbackError) throw new InvalidOperationException("Native tutorial playback error.");
        if (EditorApplication.timeSinceStartup - playbackStarted > 25) throw new TimeoutException("Native role decode timed out.");
        if (!video.isPrepared || !video.isPlaying || video.frame < 3) return;
        if (director.ReviewImage.texture != video.targetTexture || !video.targetTexture || !video.targetTexture.IsCreated())
            throw new InvalidOperationException("Authored VideoPlayer/RawImage target mismatch.");
        if (!sought)
        {
            Snapshot(Name + "-decoded-start.png");
            video.time = video.clip.length - 1.5; sought = true; beforeWrap = (long)video.clip.frameCount - 20; return;
        }
        if (!wrapped) return;
        if (video.time > 1.8 || video.frame < 3) return;
        Snapshot(Name + "-decoded-loop.png");
        File.WriteAllText(Evidence + "/" + Name + "-playback.txt", "PASS actual authored VideoPlayer prepared and decoded advancing nonblank frames into its authored RawImage target; native end seek then loop wrap observed; frame=" + video.frame + " time=" + video.time + " loop=" + video.isLooping + " clip=" + video.clip.name);
        video.errorReceived -= OnVideoError; video.loopPointReached -= OnVideoLoop; video.Stop(); director.ReviewRoot.SetActive(false);
        playbackStartedRole = false;
        if (role == 0) { SessionState.SetInt(Key + ".Role", 1); return; }
        SessionState.SetInt(Key + ".Phase", 3); EditorApplication.ExitPlaymode();
    }
    static void OnVideoLoop(VideoPlayer player) { wrapped = true; }
    static void OnVideoError(VideoPlayer player, string message) { playbackError = true; Debug.LogError(message); }
    static void Snapshot(string name)
    {
        var prior = RenderTexture.active; var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            RenderTexture.active = director.Review.targetTexture; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            var pixels = image.GetPixels32(); var first = pixels[0]; bool varied = false;
            for (int i = 0; i < pixels.Length; i += 251)
                if (Math.Abs(pixels[i].r - first.r) + Math.Abs(pixels[i].g - first.g) + Math.Abs(pixels[i].b - first.b) > 40) { varied = true; break; }
            if (!varied) throw new InvalidOperationException("Decoded tutorial frame appears blank.");
            File.WriteAllBytes(Evidence + "/" + name, image.EncodeToPNG());
        }
        finally { RenderTexture.active = prior; Object.DestroyImmediate(image); }
    }
    static string Hash(string path)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(IOPath.Combine(Application.dataPath, "..", path)))).Replace("-", "");
    }
    static void Finish()
    {
        EditorApplication.update -= Tick;
        string error = SessionState.GetString(Key + ".Error", "");
        try
        {
            string[] after = protectedPaths.Select(p => p + " " + Hash(p)).ToArray();
            File.WriteAllLines(Evidence + "/protected-after.txt", after);
            if (!File.ReadAllLines(Evidence + "/protected-before.txt").SequenceEqual(after))
                throw new InvalidOperationException("Another owner's protected scene/prefab changed during owned capture.");
            var journal = JsonUtility.FromJson<Journal>(SessionState.GetString(Key + ".Journal", ""));
            if (journal != null)
            {
                Application.runInBackground = journal.Background;
                if (journal.Scenes.Length > 0 && !string.IsNullOrEmpty(journal.Scenes[0].Path))
                    EditorSceneManager.RestoreSceneManagerSetup(journal.Scenes.Select(s => new SceneSetup { path = s.Path, isLoaded = s.Loaded, isActive = s.Active }).ToArray());
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            File.WriteAllText(Evidence + "/result.txt", "PASS real Unity Recorder capture and required production gameplay observations for both authored role Ink sequences. Hashed native-captures.json exported. Shipped guide MP4s and provenance were not changed. Silent-film assembly, native final playback, human visual review and two-client transport remain unverified.");
            Debug.Log("AUTHORED_TUTORIAL_RECORDING PASS");
        }
        catch (Exception failed) { error = failed.ToString(); File.WriteAllText(Evidence + "/result.txt", "FAIL " + error); Debug.LogException(failed); }
        finally
        {
            SessionState.SetBool(Key, false);
            if (SessionState.GetBool(Key + ".Batch", false)) EditorApplication.Exit(string.IsNullOrEmpty(error) ? 0 : 1);
        }
    }
}
#endif
