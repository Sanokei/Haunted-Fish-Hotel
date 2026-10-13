#if UNITY_EDITOR
using System;
using System.IO;
using IOPath = System.IO.Path;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;
using HauntedFish.Multiplayer;
using Object = UnityEngine.Object;

// Explicit owner menu commands only. Never enters Play, switches scenes or synthesizes input.
[InitializeOnLoad]
public static class GameTutorialCaptureTask
{
    const string Evidence = "C:/Users/wkeif/Documents/Codex/2026-10-09/task-3/TutorialCapture";
    static RecorderController _Recorder;
    static RecorderControllerSettings _ControllerSettings;
    static MovieRecorderSettings _Movie;
    static HotelPlayer _Actor;
    static bool _Ghost, _Walk, _Jump, _Possess, _Use;
    static Vector3 _Start;
    static double _Deadline;
    static string _Output;
    static GameTutorialCaptureTask()
    {
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingPlayMode) Stop(); };
    }
    [MenuItem("Tools/Haunted Fish/Record tutorial/Fish walk and jump (8 seconds)")]
    static void Fish() => Start(false);
    [MenuItem("Tools/Haunted Fish/Record tutorial/Ghost possess and activate (8 seconds)")]
    static void Ghost() => Start(true);
    static void Start(bool ghost)
    {
        if (_Recorder != null) throw new InvalidOperationException("One tutorial capture is already active.");
        var player = HotelPlayer.LocalPlayer; var camera = HotelViewCamera.Current;
        if (!EditorApplication.isPlaying || !GameSceneController.Current || !player || !player.RoundReleased ||
            player.InputBlocked || player.InBossFight || !player.Movement.GameActive ||
            player.ControlMode != (ghost ? HotelControlMode.Ghost : HotelControlMode.Fish))
            throw new InvalidOperationException("Start only in the released live Game round as the requested role; complete explicit ready-up and countdown first.");
        if (!camera || Camera.main != camera) throw new InvalidOperationException("The authored Game output must already be MainCamera; this command will not retag or replace cameras.");
        if (new DriveInfo(IOPath.GetPathRoot(Application.dataPath)).AvailableFreeSpace < 512L * 1024 * 1024)
            throw new IOException("Keep at least 512 MiB free before the bounded capture.");
        Directory.CreateDirectory(Evidence);
        _Ghost = ghost; _Actor = player; _Start = player.transform.position;
        _Walk = _Jump = _Possess = _Use = false;
        _Output = IOPath.Combine(Evidence, (ghost ? "Ghost" : "Fish") + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        _ControllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        _Movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        _Movie.name = "Genuine role tutorial gameplay"; _Movie.Enabled = true;
        _Movie.EncoderSettings = new CoreEncoderSettings { Codec = CoreEncoderSettings.OutputCodec.MP4,
            EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.Low };
        _Movie.CaptureAudio = false; _Movie.CaptureAlpha = false;
        _Movie.ImageInputSettings = new CameraInputSettings { Source = ImageSource.MainCamera,
            OutputWidth = 640, OutputHeight = 360, CaptureUI = false };
        _Movie.OutputFile = _Output;
        _ControllerSettings.AddRecorderSettings(_Movie);
        _ControllerSettings.SetRecordModeToManual();
        _ControllerSettings.FrameRate = 30; _ControllerSettings.FrameRatePlayback = FrameRatePlayback.Variable;
        _ControllerSettings.CapFrameRate = false; _ControllerSettings.ExitPlayMode = false;
        _Recorder = new RecorderController(_ControllerSettings);
        try
        {
            _Recorder.PrepareRecording();
            if (!_Recorder.StartRecording()) throw new InvalidOperationException("Recorder did not start.");
            _Deadline = EditorApplication.timeSinceStartup + 8;
            EditorApplication.update += Observe;
            Debug.Log(ghost ? "Recording real gameplay: possess a trap with E, then use its Space/click action. Capture stops in 8 seconds." :
                "Recording real gameplay: walk with A/D, then jump with Space. Capture stops in 8 seconds.");
        }
        catch { Stop(); throw; }
    }
    static void Observe()
    {
        if (!_Actor || !_Actor.RoundReleased || _Actor.InputBlocked || _Actor.InBossFight || !EditorApplication.isPlaying)
        { Stop(); return; }
        var position = _Actor.transform.position;
        _Walk |= Mathf.Abs(position.x - _Start.x) > .15f;
        _Jump |= position.y - _Start.y > .35f;
        bool possessed = _Actor.ControlledCube >= 0;
        _Possess |= possessed;
        _Use |= possessed && ((Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame));
        if (EditorApplication.timeSinceStartup >= _Deadline) Stop();
    }
    [MenuItem("Tools/Haunted Fish/Record tutorial/Stop capture")]
    static void Stop()
    {
        EditorApplication.update -= Observe;
        if (_Recorder == null) return;
        bool validAction = _Ghost ? _Possess && _Use : _Walk && _Jump;
        try
        {
            _Recorder.StopRecording();
            string file = _Output + ".mp4";
            File.WriteAllText(_Output + ".txt", "SOURCE=actual Unity Recorder MainCamera gameplay\nROLE=" + (_Ghost ? "Ghost" : "Fish") +
                "\nOBSERVED_REQUIRED_ACTION=" + validAction + "\nNOTE=Ghost observation confirms possession and action input, not server trap success; inspect video before accepting.\nFILE=" + file);
            if (validAction && File.Exists(file) && new FileInfo(file).Length > 1024)
                File.WriteAllText(IOPath.Combine(Evidence, (_Ghost ? "Ghost" : "Fish") + "-latest.txt"), file);
            Debug.Log("Tutorial capture stopped. " + (validAction ? "Action markers observed; visually inspect the MP4 before binding." : "Required action markers missing; capture was not marked bindable."));
        }
        finally
        {
            _Recorder = null; _Actor = null;
            if (_Movie) Object.DestroyImmediate(_Movie);
            if (_ControllerSettings) Object.DestroyImmediate(_ControllerSettings);
            _Movie = null; _ControllerSettings = null;
        }
    }
    [MenuItem("Tools/Haunted Fish/Record tutorial/Bind reviewed completed recordings (Edit mode)")]
    static void Bind()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("Bind recordings only in idle Edit mode outside prefab editing.");
        for (int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserve unsaved scene work before binding.");
        foreach (string role in new[] { "Fish", "Ghost" })
        {
            string receipt = IOPath.Combine(Evidence, role + "-latest.txt");
            if (!File.Exists(receipt)) throw new FileNotFoundException("No action-observed genuine recording for " + role);
            string source = File.ReadAllText(receipt).Trim();
            if (!File.Exists(source)) throw new FileNotFoundException(source);
            string folder = "Assets/TutorialVideos";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "TutorialVideos");
            string target = folder + "/" + IOPath.GetFileName(source);
            string absolute = IOPath.Combine(Application.dataPath, "TutorialVideos", IOPath.GetFileName(source));
            if (!File.Exists(absolute)) File.Copy(source, absolute, false);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(target);
            if (!clip) throw new InvalidOperationException("Unity could not import recorded " + role + " VideoClip.");
            string prefab = "Assets/Resources/" + role + "RoundHowTo.prefab";
            var root = PrefabUtility.LoadPrefabContents(prefab);
            try { root.GetComponent<VideoPlayer>().clip = clip; PrefabUtility.SaveAsPrefabAsset(root, prefab); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Debug.Log("Reviewed genuine recordings bound to the two authored role-page prefabs. No user scene was saved or switched.");
    }
}
#endif
