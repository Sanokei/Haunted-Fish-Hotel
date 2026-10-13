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
    const string Evidence = "C:/Users/wkeif/Documents/Codex/2026-10-11/task-4/TutorialEvidence";
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
    static string output;
    static readonly string[] protectedPaths = { "Assets/Scenes/Game.unity", "Assets/Scenes/Lobby.unity", "Assets/Scenes/BossFight.unity", "Assets/Resources/FishRoundHowTo.prefab", "Assets/Resources/GhostRoundHowTo.prefab", "Assets/Resources/MemberMenu.prefab", "Assets/Scenes/theatre.unity", "Assets/Resources/HotelNetworkPlayer.prefab", "Assets/Resources/GameHallway.prefab", "Assets/Resources/HallwayDoor.prefab", "Assets/Prefabs/Radio/LobbyRadioPlayback.prefab" };

    [MenuItem("Tools/Haunted Fish/Authored tutorials/Record both role guides (owned scene)")]
    public static void ManualRecord()
    {
        if (!EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != TutorialRecordingAuthoring.ScenePath)
            throw new InvalidOperationException("Open the authored TutorialRecording scene and enter Play explicitly first.");
        if (SessionState.GetBool(Key, false)) throw new InvalidOperationException("A tutorial run is already armed.");
        Directory.CreateDirectory(Evidence);
        var journal = new Journal { Background = Application.runInBackground, Scenes = EditorSceneManager.GetSceneManagerSetup().Select(s => new SceneRecord { Path = s.path, Loaded = s.isLoaded, Active = s.isActive }).ToArray() };
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
            File.WriteAllText(Evidence + "/shared-font-bake.txt", "PASS HotelTypographyAuthoring.BakeSharedFont; " + font.faceInfo.familyName + "; actual native characters=" + font.characterTable.Count + "; atlas=" + atlas.width + "x" + atlas.height + "; preserved GUID=" + AssetDatabase.AssetPathToGUID("Assets/Resources/HalloweenUI/Modak-Headings.asset") + "; video captions use the same bundled Modak and Liberation source fonts.");
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
    static void Attach() { EditorApplication.update -= Tick; EditorApplication.update += Tick; deadline = EditorApplication.timeSinceStartup + 300; }
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
                Import();
                role = 0; SessionState.SetInt(Key + ".Role", 0); SessionState.SetInt(Key + ".Phase", 2);
                EditorSceneManager.OpenScene(TutorialRecordingAuthoring.ScenePath);
                EditorApplication.EnterPlaymode(); return;
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
            File.WriteAllLines(output + "-observations.txt", director.Observations);
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
    static void Import()
    {
        foreach (string name in new[] { "Fish", "Ghost" })
        {
            string source = File.ReadAllText(Evidence + "/" + name + "-latest.txt").Trim();
            string asset = "Assets/TutorialVideos/" + name + "TutorialProduction.mp4";
            string target = IOPath.Combine(Application.dataPath, "TutorialVideos", name + "TutorialProduction.mp4");
            string backup = Evidence + "/" + name + "TutorialProduction-before-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".mp4";
            File.Copy(target, backup, false);
            File.Copy(source, target, true);
            AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport);
            var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(asset);
            if (!clip || clip.width != 1280 || clip.height != 720 || clip.frameCount < 1000 || clip.length < 35)
                throw new InvalidOperationException("Recorded native " + name + " clip metadata invalid.");
            var content = AssetDatabase.LoadAssetAtPath<TutorialRoleContent>("Assets/Resources/Tutorials/" + name + "TutorialContent.asset");
            content.Recording = clip; content.RecordedWidth = (int)clip.width; content.RecordedHeight = (int)clip.height;
            content.RecordedFrames = clip.frameCount; content.RecordedSeconds = clip.length; content.RecordedFrameRate = clip.frameRate;
            content.CaptureProvenance = "Real Unity Recorder MainCamera capture, authored TutorialRecording.unity, Ink-directed production hallway gameplay APIs; boss segment pending revised drag/defense implementation; " + IOPath.GetFileName(source);
            EditorUtility.SetDirty(content);
            File.WriteAllText(Evidence + "/" + name + "-import.txt", "PASS native VideoClip " + clip.width + "x" + clip.height + " frames=" + clip.frameCount + " seconds=" + clip.length + " fps=" + clip.frameRate + " bytes=" + new FileInfo(target).Length + "\nSHA256=" + Hash(asset) + "\nBACKUP=" + backup);
        }
        AssetDatabase.SaveAssets();
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
            File.WriteAllText(Evidence + "/result.txt", "PASS real Unity Recorder capture for both authored role Ink sequences; production gameplay action observations; native 1280x720 clips imported preserving original GUIDs; authored native playback decoded nonblank frames and loop wrap; protected production scene/prefab hashes unchanged. Human UX and two-client network transport remain unverified.");
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
