using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HauntedFish.Multiplayer;
using HauntedFish.Tutorials;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.Video;

// Explicit isolated native validation. Fixture objects are authored in Edit mode;
// Play mode exercises production components without constructing UI or networking.
[InitializeOnLoad]
public static class MemberMenuNativeCapture
{
    const string Active = "HF.MenuCapture.Active", Phase = "HF.MenuCapture.Phase";
    const string Evidence = "C:/Users/wkeif/Documents/Codex/2026-10-11/task-3/NativeMenuEvidence";
    const string Fixture = "Assets/Editor/MemberMenuNativeFixture.unity";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static MemberMenu owner; static MemberMenuPresentation presentation; static MemberMenuTutorial tutorial;
    static HauntedHotelMultiplayer session; static VideoPlayer video; static AudioMixer mixer;
    static Keyboard keyboard; static int hierarchy, assertions; static double due, videoDeadline;
    static string capture; static int next; static bool success;
    [Serializable] sealed class Setup { public SceneSetup[] Scenes; }
    [Serializable] sealed class Prefs { public bool[] Had; public float[] Values; public bool HadDisplay; public int Display; }
    static readonly string[] Keys = { "HauntedHotel.Settings.MasterVolume", "HauntedHotel.Settings.MusicVolume", "HauntedHotel.Settings.SoundEffectsVolume" };
    static MemberMenuNativeCapture() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || AssetDatabase.LoadAssetAtPath<SceneAsset>(Fixture)) throw new InvalidOperationException("Needs idle Edit mode and an absent owned validation fixture.");
        HotelTypographyAuthoring.Run();
        Directory.CreateDirectory(Evidence);
        File.WriteAllText(Evidence + "/scene-setup.json", JsonUtility.ToJson(new Setup { Scenes = EditorSceneManager.GetSceneManagerSetup() }));
        File.WriteAllText(Evidence + "/prefs-before.json", JsonUtility.ToJson(new Prefs { Had = Keys.Select(PlayerPrefs.HasKey).ToArray(), Values = Keys.Select(k => PlayerPrefs.GetFloat(k, 1)).ToArray(), HadDisplay = PlayerPrefs.HasKey("HauntedHotel.Settings.DisplayMode"), Display = PlayerPrefs.GetInt("HauntedHotel.Settings.DisplayMode") }));
        File.WriteAllText(Evidence + "/native-ui-checks.txt", "Native isolated production-component validation. Synthetic keyboard/session readiness; no network peer or physical OS input.\n");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var menu = (GameObject)PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>("MemberMenu"), scene);
        var fixture = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/MemberMenuFocusFixture.prefab"), scene);
        fixture.SetActive(false);
        PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>("MemberMenuEventSystem"), scene);
        var cameraObject = new GameObject("Authored validation camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera"; cameraObject.transform.position = new Vector3(0, 0, -10);
        var camera = cameraObject.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f, .025f, .055f); camera.orthographic = true;
        EditorSceneManager.SaveScene(scene, Fixture);
        SetGameViewSize(1280, 720);
        SessionState.SetBool(Active, true); SessionState.SetInt(Phase, 0); SessionState.SetFloat("HF.MenuCapture.Deadline", (float)(EditorApplication.timeSinceStartup + 240));
        EditorApplication.EnterPlaymode();
    }
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static Button Button(string name) => owner.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
    static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        assertions++; File.AppendAllText(Evidence + "/native-ui-checks.txt", "PASS " + message + "\n");
    }
    static void Escape()
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); InputSystem.Update();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
    }
    static void Capture(string file, int phase)
    {
        Canvas.ForceUpdateCanvases(); ScreenCapture.CaptureScreenshot(Evidence + "/" + file);
        capture = file; next = phase; due = EditorApplication.timeSinceStartup + 1; SessionState.SetInt(Phase, 90);
    }
    static void Gain(float music, float effects)
    {
        Check(mixer.GetFloat(HotelAudioSettings.MusicParameter, out var m) && Math.Abs(m - MemberMenuSettingsDraft.ToDecibels(music)) < .1f, "native Music mixer gain " + music);
        Check(mixer.GetFloat(HotelAudioSettings.EffectsParameter, out var e) && Math.Abs(e - MemberMenuSettingsDraft.ToDecibels(effects)) < .1f, "native SFX mixer gain " + effects);
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Active, false)) return;
        try
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat("HF.MenuCapture.Deadline", float.MaxValue)) throw new TimeoutException("Native UI validation exceeded its deadline.");
            int phase = SessionState.GetInt(Phase, 0);
            if (phase >= 100) { if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode) Finish(); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
            if (phase == 90)
            {
                if (EditorApplication.timeSinceStartup < due) return;
                Check(File.Exists(Evidence + "/" + capture) && new FileInfo(Evidence + "/" + capture).Length > 4096, "actual Unity ScreenCapture " + capture);
                SessionState.SetInt(Phase, next); return;
            }
            if (phase == 0)
            {
                owner = UnityEngine.Object.FindObjectsByType<MemberMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
                presentation = owner.GetComponent<MemberMenuPresentation>(); tutorial = owner.GetComponentInChildren<MemberMenuTutorial>(true); video = Field<VideoPlayer>(tutorial, "_Video");
                session = UnityEngine.Object.FindObjectsByType<HauntedHotelMultiplayer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
                var type = typeof(HauntedHotelMultiplayer).Assembly.GetType("HauntedFish.Multiplayer.HotelSessionLifecycle");
                var lifecycle = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
                type.GetField("_Scene", Private).SetValue(lifecycle, session); type.GetField("<State>k__BackingField", Private).SetValue(lifecycle, HotelSessionState.Connected);
                typeof(HauntedHotelMultiplayer).GetField("_Lifecycle", Private).SetValue(session, lifecycle); Call(session, "PublishState"); owner.Bind(session);
                keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>(); InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                mixer = Resources.Load<AudioMixer>("HotelAudio"); hierarchy = owner.GetComponentsInChildren<Transform>(true).Length;
                Check(owner.enabled && presentation.HasAuthoredBindings && tutorial.HasAuthoredBindings, "native authored menu/control references");
                Check(mixer && mixer.FindMatchingGroups("Music").Any(g => g.name == "Music") && mixer.FindMatchingGroups("Sound Effects").Any(g => g.name == "Sound Effects"), "native imported mixer groups");
                Check(!owner.IsOpen && session.ReadyToPlay, "starts closed with isolated readiness");
                Escape(); Check(owner.IsOpen && Field<GameObject>(owner, "_Menu").activeInHierarchy, "ESC binding opens visible modal via native InputSystem state");
                Check(owner.GetComponent<Canvas>().sortingOrder == 32767 && session.MemberMenuOpen && session.Session.InputFocused, "top overlay and modal input focus");
                Capture("room-1280x720.png", 1);
            }
            else if (phase == 1)
            {
                Button("Settings").onClick.Invoke(); Check(presentation.SettingsOpen, "authored Settings button");
                Field<Slider>(presentation, "_Volume").value = .7f; Field<Slider>(presentation, "_MusicVolume").value = 0; Field<Slider>(presentation, "_EffectsVolume").value = 1;
                Button("Apply").onClick.Invoke(); due = EditorApplication.timeSinceStartup + .3; SessionState.SetInt(Phase, 2);
            }
            else if (phase == 2 && EditorApplication.timeSinceStartup >= due)
            {
                Gain(0, 1); Check(Math.Abs(AudioListener.volume - .7f) < .001f, "master applied once through AudioListener");
                Check(PlayerPrefs.GetFloat(Keys[1]) == 0 && PlayerPrefs.GetFloat(Keys[2]) == 1, "independent category preferences persisted natively");
                Capture("settings-1280x720.png", 3);
            }
            else if (phase == 3)
            {
                Field<Slider>(presentation, "_MusicVolume").value = .4f; Field<Slider>(presentation, "_EffectsVolume").value = .2f; Button("Cancel").onClick.Invoke();
                Gain(0, 1); Check(HotelSettings.MusicVolume == 0 && HotelSettings.SoundEffectsVolume == 1, "Cancel discards category draft");
                typeof(HotelSettings).GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                Check(HotelSettings.MusicVolume == 0 && HotelSettings.SoundEffectsVolume == 1, "saved categories reload through production preference load");
                Button("Settings").onClick.Invoke(); Field<Slider>(presentation, "_MusicVolume").value = 1; Field<Slider>(presentation, "_EffectsVolume").value = 0; Button("Apply").onClick.Invoke();
                due = EditorApplication.timeSinceStartup + .3; SessionState.SetInt(Phase, 4);
            }
            else if (phase == 4 && EditorApplication.timeSinceStartup >= due)
            {
                Gain(1, 0); Escape(); Check(owner.IsOpen && !presentation.SettingsOpen, "ESC backs Settings to Room");
                Button("How To").onClick.Invoke(); Check(presentation.HowToOpen, "authored How To opens"); videoDeadline = EditorApplication.timeSinceStartup + 45; SessionState.SetInt(Phase, 5);
            }
            else if (phase == 5)
            {
                if (EditorApplication.timeSinceStartup > videoDeadline) throw new TimeoutException("Fish recording prepare/frame decode failed.");
                if (!video.isPrepared || video.frame < 1 || !video.isPlaying) return;
                var fish = Resources.Load<TutorialRoleContent>("Tutorials/FishTutorialContent"); var text = Field<TMP_Text>(tutorial, "_Instructions").text;
                Check(video.clip == fish.Recording && fish.Instructions.All(s => text.Contains(s.Heading) && text.Contains(s.Body)), "Fish video and all role instructions resolve");
                Check(!fish.HasAudio && video.audioOutputMode == VideoAudioOutputMode.None && Field<AudioSource>(tutorial, "_Audio").mute, "silent Fish recording stays muted");
                Button("Tutorial play pause").onClick.Invoke(); Check(!video.isPlaying, "video Pause control"); Button("Tutorial replay").onClick.Invoke(); Check(video.isPlaying, "video Replay control");
                Capture("fish-howto-1280x720.png", 6);
            }
            else if (phase == 6)
            {
                Button("Ghost tutorial").onClick.Invoke(); videoDeadline = EditorApplication.timeSinceStartup + 45; SessionState.SetInt(Phase, 7);
            }
            else if (phase == 7)
            {
                if (EditorApplication.timeSinceStartup > videoDeadline) throw new TimeoutException("Ghost recording prepare/frame decode failed.");
                if (!video.isPrepared || video.frame < 1 || !video.isPlaying) return;
                var ghost = Resources.Load<TutorialRoleContent>("Tutorials/GhostTutorialContent"); var text = Field<TMP_Text>(tutorial, "_Instructions").text;
                Check(video.clip == ghost.Recording && ghost.Instructions.All(s => text.Contains(s.Heading) && text.Contains(s.Body)), "Ghost switching binds actual clip and all sections");
                Check(video.isLooping == ghost.Loop && video.audioOutputMode == VideoAudioOutputMode.None, "Ghost loop/silent content contract");
                Check(!Field<Image>(tutorial, "_GhostPreview").enabled, "BOSS-cursor loading preview hides after native decode");
                Capture("ghost-howto-1280x720.png", 8);
            }
            else if (phase == 8)
            {
                Field<ScrollRect>(tutorial, "_Scroll").verticalNormalizedPosition = 0; Capture("ghost-howto-bottom-1280x720.png", 9);
            }
            else if (phase == 9)
            {
                Check(owner.GetComponentsInChildren<Transform>(true).Length == hierarchy, "Open/settings/roles create no runtime UI hierarchy");
                SetGameViewSize(960, 540); due = EditorApplication.timeSinceStartup + 1; SessionState.SetInt(Phase, 10);
            }
            else if (phase == 10 && EditorApplication.timeSinceStartup >= due) { Capture("ghost-howto-960x540.png", 11); }
            else if (phase == 11) { SetGameViewSize(800, 600); due = EditorApplication.timeSinceStartup + 1; SessionState.SetInt(Phase, 12); }
            else if (phase == 12 && EditorApplication.timeSinceStartup >= due) { Capture("ghost-howto-800x600.png", 13); }
            else if (phase == 13)
            {
                Escape(); Check(owner.IsOpen && !presentation.HowToOpen && !video.isPlaying, "ESC backs How To and stops tutorial playback"); Escape();
                Check(!owner.IsOpen && !Field<GameObject>(owner, "_Menu").activeInHierarchy, "second ESC closes visible menu");
                Call(owner, "ReleaseInputCapture", Time.frameCount + 2, (bool?)false); Check(!session.MemberMenuOpen && !session.Session.InputFocused, "closing input capture releases without stuck focus");
                Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(owner.gameObject) == 0, "no missing imported menu scripts");
                success = true; SessionState.SetBool("HF.MenuCapture.Success", true); File.AppendAllText(Evidence + "/native-ui-checks.txt", "TOTAL " + assertions + " assertions. Protected scenes/media checked by host after exit.\n");
                StopPlay();
            }
        }
        catch (Exception e) { File.WriteAllText(Evidence + "/native-ui-error.txt", e.ToString()); SessionState.SetBool("HF.MenuCapture.Success", false); StopPlay(); }
    }
    static void StopPlay() { SessionState.SetInt(Phase, 100); EditorApplication.ExitPlaymode(); }
    static void Finish()
    {
        SessionState.SetBool(Active, false); var prefs = JsonUtility.FromJson<Prefs>(File.ReadAllText(Evidence + "/prefs-before.json"));
        for (int i = 0; i < Keys.Length; i++) { if (prefs.Had[i]) PlayerPrefs.SetFloat(Keys[i], prefs.Values[i]); else PlayerPrefs.DeleteKey(Keys[i]); }
        if (prefs.HadDisplay) PlayerPrefs.SetInt("HauntedHotel.Settings.DisplayMode", prefs.Display); else PlayerPrefs.DeleteKey("HauntedHotel.Settings.DisplayMode"); PlayerPrefs.Save();
        typeof(HotelSettings).GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        var setup = JsonUtility.FromJson<Setup>(File.ReadAllText(Evidence + "/scene-setup.json")); EditorSceneManager.RestoreSceneManagerSetup(setup.Scenes);
        AssetDatabase.DeleteAsset(Fixture); success = SessionState.GetBool("HF.MenuCapture.Success", false);
        File.WriteAllText(Evidence + "/native-ui-result.txt", success ? "PASS; prefs restored; owned temporary fixture removed; Unity released on exit." : "FAIL; prefs restored; owned temporary fixture removed; see native-ui-error.txt.");
        EditorApplication.Exit(success ? 0 : 1);
    }
    static void SetGameViewSize(int width, int height)
    {
        var asm = typeof(Editor).Assembly; var sizesType = asm.GetType("UnityEditor.GameViewSizes");
        var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);
        var groupType = sizesType.GetProperty("currentGroupType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(singleton);
        var group = sizesType.GetMethod("GetGroup").Invoke(singleton, new[] { groupType });
        var kind = asm.GetType("UnityEditor.GameViewSizeType"); var sizeType = asm.GetType("UnityEditor.GameViewSize");
        var size = Activator.CreateInstance(sizeType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { Enum.ToObject(kind, 1), width, height, "Menu validation " + width + "x" + height }, null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        var count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
        var viewType = asm.GetType("UnityEditor.GameView"); var view = EditorWindow.GetWindow(viewType);
        viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(view, count - 1); view.Show(); view.Repaint();
    }
}
