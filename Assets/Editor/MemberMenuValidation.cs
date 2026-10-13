using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HauntedFish.Multiplayer;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public static class MemberMenuValidation
{
    static void RequireIdleEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Requires idle Edit mode outside prefab editing.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save or discard scene changes before explicitly running this tool.");
    }

    const string Evidence = "C:/Users/wkeif/Documents/Codex/2026-10-09/task-6/MenuValidationEvidence";
    static string _Status;
    static void Status(string value)
    {
        if (_Status == value) return;
        _Status = value; Directory.CreateDirectory(Evidence); File.WriteAllText(Evidence + "/status.txt", value);
    }
    [MenuItem("Tools/Haunted Hotel/Author menu font and validate prefab")]
    public static void AuthorAndValidate()
    {
        RequireIdleEditMode();
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Status("Waiting for Edit mode; prefab repair prepared, no scene or Play mode changes."); return; }
        if (!Resources.Load<Sprite>("HalloweenUI/Button") || !Resources.Load<Sprite>("HalloweenUI/Skull") || !Resources.Load<Font>("HalloweenUI/Creepster-Regular"))
        { Status("Waiting for authored menu/font asset import/refresh."); return; }

        try { BakeHeadingFont(); Run(); RunFocusAndLayerChecks(); Status("PASS: authored prefab Awake/settings and isolated native Escape callback/Resume focus, intro priority, held click, transition closure, and loaded scene/prefab overlay priorities. Readiness/transition were synthetic; physical keyboard input, peer gameplay clicks and screenshots still require Play mode."); File.WriteAllText(Evidence + "/authored-prefab-passed.txt", _Status); }
        catch (Exception error) { Status("FAILED authored-prefab validation; see authored-prefab-error.txt."); File.WriteAllText(Evidence + "/authored-prefab-error.txt", error.ToString()); Debug.LogException(error); }
    }
    // Editor authoring only: create and persist a TMP font asset, never runtime font/UI construction.
    static void BakeHeadingFont()
    {
        var font = HotelTypographyAuthoring.BakeHeadingFont();
        var prefab = PrefabUtility.LoadPrefabContents("Assets/Resources/MemberMenu.prefab");
        try
        {
            var heading = prefab.GetComponent<MemberMenuPresentation>().Heading;
            if (heading.font != font) { heading.font = font; PrefabUtility.SaveAsPrefabAsset(prefab, "Assets/Resources/MemberMenu.prefab"); }
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
    }
    [MenuItem("Tools/Haunted Hotel/Validate authored pause menu and settings")]
    public static void Run()
    {
        RequireIdleEditMode();
        const string volumeKey = "HauntedHotel.Settings.MasterVolume", displayKey = "HauntedHotel.Settings.DisplayMode";
        const string musicKey = "HauntedHotel.Settings.MusicVolume", effectsKey = "HauntedHotel.Settings.SoundEffectsVolume";
        bool hadMusic = PlayerPrefs.HasKey(musicKey), hadEffects = PlayerPrefs.HasKey(effectsKey);
        float savedMusic = PlayerPrefs.GetFloat(musicKey, 1), savedEffects = PlayerPrefs.GetFloat(effectsKey, 1);
        float previousMusic = HotelSettings.MusicVolume, previousEffects = HotelSettings.SoundEffectsVolume;
        bool hadVolume = PlayerPrefs.HasKey(volumeKey), hadDisplay = PlayerPrefs.HasKey(displayKey);
        float savedVolume = PlayerPrefs.GetFloat(volumeKey, 1), listenerVolume = AudioListener.volume;
        int savedDisplay = PlayerPrefs.GetInt(displayKey, (int)Screen.fullScreenMode);
        var previousDisplay = HotelSettings.DisplayMode; var previousVolume = HotelSettings.MasterVolume;
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); bool dirty = activeScene.isDirty;
        GameObject prefab = null;
        try
        {
            prefab = PrefabUtility.LoadPrefabContents("Assets/Resources/MemberMenu.prefab");
            var owner = prefab.GetComponent<MemberMenu>(); var presentation = prefab.GetComponent<MemberMenuPresentation>();
            Check(owner && presentation && presentation.HasAuthoredBindings, "serialized owner/presentation bindings");
            int hierarchyBefore = prefab.GetComponentsInChildren<Transform>(true).Length;
            // Invoke the exact production Awake path that previously threw MissingComponentException.
            typeof(MemberMenu).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
            Check(owner.enabled, "production Awake completes with existing CanvasGroup");
            var action = (InputAction)typeof(MemberMenu).GetField("_ToggleMenu", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
            Check(action != null && action.bindings.Any(b => b.path == "<Keyboard>/escape"), "Escape input action registered");
            var authored = new SerializedObject(owner); var menu = (GameObject)authored.FindProperty("_Menu").objectReferenceValue;
            Check(!menu.activeSelf, "starts closed without discarding authored hierarchy");
            var fallback = (GameObject)authored.FindProperty("_EventSystemPrefab").objectReferenceValue;
            Check(fallback && fallback.GetComponent<UnityEngine.EventSystems.EventSystem>() && fallback.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(), "authored EventSystem fallback");
            Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(fallback) == 0, "authored fallback has no missing scene-only components");
            menu.SetActive(true); presentation.Open();
            var canvas = prefab.GetComponent<Canvas>();
            Check(canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.overrideSorting && canvas.sortingOrder == 32767, "modal overlay sorting");
            var rect = (RectTransform)menu.transform;
            Check(rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one && menu.GetComponent<Image>().raycastTarget && menu.GetComponent<CanvasGroup>().blocksRaycasts && menu.GetComponent<CanvasGroup>().alpha == 1, "full-screen visible click blocker");
            Button Find(string name) => menu.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
            var slider = menu.GetComponentInChildren<Slider>(true);
            Find("Settings").onClick.Invoke(); Check(presentation.SettingsOpen && Mathf.Approximately(slider.value, previousVolume), "settings tab opens from authored button");
            slider.value = .42f; Find("Cancel").onClick.Invoke();
            Check(!presentation.SettingsOpen && Mathf.Approximately(HotelSettings.MasterVolume, previousVolume) && Mathf.Approximately(AudioListener.volume, listenerVolume), "Cancel leaves current settings/audio unchanged");
            Find("Settings").onClick.Invoke(); Check(Mathf.Approximately(slider.value, previousVolume), "Cancel discards draft on reopen");
            slider.value = .42f; Find("Apply").onClick.Invoke();
            Check(Mathf.Approximately(AudioListener.volume, .42f) && Mathf.Approximately(PlayerPrefs.GetFloat(volumeKey), .42f), "Apply persists actual audio preference");
            Check(presentation.Back() && !presentation.SettingsOpen && !presentation.Back(), "Escape Back settings then room");
            Find("Settings").onClick.Invoke(); Check(Mathf.Approximately(slider.value, .42f), "reopen reflects Apply");
            Check(prefab.GetComponentsInChildren<Transform>(true).Length == hierarchyBefore, "Awake/Open/settings never create UI objects");
            Debug.Log("PASS: 14 actual authored-prefab/Awake/presentation assertions. Connected gameplay input and screenshots remain separate.");
        }
        finally
        {
            if (prefab)
            {
                var owner = prefab.GetComponent<MemberMenu>();
                typeof(MemberMenu).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
                PrefabUtility.UnloadPrefabContents(prefab);
            }
            HotelSettings.Save(previousVolume, previousMusic, previousEffects, previousDisplay);
            if (hadMusic) PlayerPrefs.SetFloat(musicKey, savedMusic); else PlayerPrefs.DeleteKey(musicKey);
            if (hadEffects) PlayerPrefs.SetFloat(effectsKey, savedEffects); else PlayerPrefs.DeleteKey(effectsKey);
            if (hadVolume) PlayerPrefs.SetFloat(volumeKey, savedVolume); else PlayerPrefs.DeleteKey(volumeKey);
            if (hadDisplay) PlayerPrefs.SetInt(displayKey, savedDisplay); else PlayerPrefs.DeleteKey(displayKey);
            PlayerPrefs.Save(); AudioListener.volume = listenerVolume;
            Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene() == activeScene && activeScene.isDirty == dirty, "authored scene/dirty state preserved");
        }
    }
    [MenuItem("Tools/Haunted Hotel/Validate isolated menu focus and overlay priority")]
    static void RunFocusAndLayerChecks()
    {
        RequireIdleEditMode();
        if (LobbyCursorManager.Instance && LobbyCursorManager.Instance.InspectingRadio) throw new InvalidOperationException("Isolated focus check deferred: live radio inspection would be touched.");
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); bool dirty = activeScene.isDirty;
        bool cursorVisible = Cursor.visible; var cursorLock = Cursor.lockState;
        GameObject menuPrefab = null, fixture = null;
        var details = new System.Collections.Generic.List<string>(); int count = 0;
        void Assert(bool ok, string text) { Check(ok, text); count++; details.Add("PASS " + text); }
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            menuPrefab = PrefabUtility.LoadPrefabContents("Assets/Resources/MemberMenu.prefab");
            fixture = PrefabUtility.LoadPrefabContents("Assets/Editor/MemberMenuFocusFixture.prefab");
            var owner = menuPrefab.GetComponent<MemberMenu>(); var session = fixture.GetComponent<HauntedHotelMultiplayer>();
            typeof(MemberMenu).GetMethod("Awake", privateInstance).Invoke(owner, null);
            // A network operation is deliberately not run. Only lifecycle readiness is fabricated;
            // callbacks and session focus use the actual production Unity components below.
            var lifecycleType = typeof(HauntedHotelMultiplayer).Assembly.GetType("HauntedFish.Multiplayer.HotelSessionLifecycle");
            var lifecycle = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(lifecycleType);
            lifecycleType.GetField("_Scene", privateInstance).SetValue(lifecycle, session);
            lifecycleType.GetField("<State>k__BackingField", privateInstance).SetValue(lifecycle, HotelSessionState.Connected);
            typeof(HauntedHotelMultiplayer).GetField("_Lifecycle", privateInstance).SetValue(session, lifecycle);
            typeof(HauntedHotelMultiplayer).GetMethod("PublishState", privateInstance).Invoke(session, null);
            owner.Bind(session);
            typeof(MemberMenu).GetMethod("OnEnable", privateInstance).Invoke(owner, null);
            var authored = new SerializedObject(owner); var menu = (GameObject)authored.FindProperty("_Menu").objectReferenceValue;
            var presentation = menuPrefab.GetComponent<MemberMenuPresentation>();
            var toggle = typeof(MemberMenu).GetMethod("ToggleMenu", privateInstance);
            var release = typeof(MemberMenu).GetMethod("ReleaseInputCapture", privateInstance);
            void Escape() => toggle.Invoke(owner, new object[] { default(InputAction.CallbackContext) });
            void Release(int frame, bool? down) => release.Invoke(owner, new object[] { frame, down });
            Button Find(string name) => menu.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
            var nativeAction = (InputAction)typeof(MemberMenu).GetField("_ToggleMenu", privateInstance).GetValue(owner);
            Assert(nativeAction.enabled, "production OnEnable enables authored Escape action");
            Assert(session.ReadyToPlay && session.Session.Connected && !session.MemberMenuOpen && !session.Session.InputFocused, "fixture readiness without networking; initial focus clear");
            Escape();
            Assert(owner.IsOpen && menu.activeInHierarchy && session.MemberMenuOpen && session.Session.InputFocused && menu.GetComponent<CanvasGroup>().alpha == 1, "production Escape callback opens visible authored modal and captures focus");
            Assert(typeof(MemberMenu).GetField("_EventSystem", privateInstance).GetValue(owner) is GameObject || UnityEngine.EventSystems.EventSystem.current, "native authored UI event-system availability");
            Find("Settings").onClick.Invoke(); Escape();
            Assert(owner.IsOpen && !presentation.SettingsOpen && session.Session.InputFocused, "Escape backs out of Settings while retaining modal focus");
            int frame = Time.frameCount; Find("Resume").onClick.Invoke();
            Assert(!owner.IsOpen && !menu.activeSelf && session.MemberMenuOpen && session.Session.InputFocused, "Resume hides modal while keeping closing click captured");
            Release(frame, false); Assert(session.MemberMenuOpen, "same-frame Resume click cannot reach gameplay");
            Release(frame + 1, true); Assert(session.MemberMenuOpen, "held closing click remains captured");
            Release(frame + 2, null); Assert(!session.MemberMenuOpen && !session.Session.InputFocused, "later release or missing pointer restores gameplay input");
            session.SetInputFocused(true); Escape(); Find("Resume").onClick.Invoke(); Release(Time.frameCount + 1, false);
            Assert(!session.MemberMenuOpen && session.Session.InputFocused, "Resume preserves a pre-existing desk focus owner");
            session.SetInputFocused(false); session.SetIntroductionPlaying(true); Escape();
            Assert(owner.IsOpen && session.Session.InputFocused, "Escape opens above connected introduction");
            Find("Resume").onClick.Invoke(); Release(Time.frameCount + 1, false);
            Assert(!session.MemberMenuOpen && session.IntroductionPlaying && session.Session.InputFocused, "Resume returns focus to existing introduction owner");
            session.SetIntroductionPlaying(false); Assert(!session.Session.InputFocused, "introduction owner can clear its own focus afterward");
            Escape(); typeof(HauntedHotelMessageTravel).GetField("<Loading>k__BackingField", privateInstance).SetValue(session.SceneTravel, true);
            typeof(MemberMenu).GetMethod("LateUpdate", privateInstance).Invoke(owner, null);
            Assert(!owner.IsOpen && !menu.activeSelf, "production transition check closes modal");
            Release(Time.frameCount + 1, false); Assert(!session.MemberMenuOpen, "transition closure does not leave menu focus stuck");
            typeof(HauntedHotelMessageTravel).GetField("<Loading>k__BackingField", privateInstance).SetValue(session.SceneTravel, false);
            Escape(); Find("Resume").onClick.Invoke(); Release(Time.frameCount + 1, false);
            Assert(!session.Session.InputFocused, "menu can reopen and release input after transition");
            typeof(MemberMenu).GetMethod("OnDisable", privateInstance).Invoke(owner, null);
            Assert(!nativeAction.enabled && !session.MemberMenuOpen, "production disable releases action and menu ownership");
            var canvas = menuPrefab.GetComponent<Canvas>();
            Assert(canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.sortingOrder == 32767, "menu uses native overlay rendering at32767");
            foreach (var path in new[] { "Assets/Scenes/Game.unity", "Assets/Scenes/BossFight.unity" })
            {
                var preview = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    var canvases = preview.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).ToArray();
                    Assert(canvases.All(c => c.renderMode != RenderMode.ScreenSpaceOverlay ||
                        SortingLayer.GetLayerValueFromID(c.sortingLayerID) < SortingLayer.GetLayerValueFromID(canvas.sortingLayerID) ||
                        (c.sortingLayerID == canvas.sortingLayerID && c.sortingOrder < canvas.sortingOrder)), "menu sorts above native loaded overlay canvases in " + path);
                    details.Add("READ ONLY " + path + " canvases=" + canvases.Length + "; world/camera visuals render below screen overlay");
                }
                finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview); }
            }
            foreach (var resource in new[] { "LobbyIntroductionUI", "AtticRoundCutscene", "FishRoundHowTo", "GhostRoundHowTo" })
            {
                var asset = Resources.Load<GameObject>(resource);
                if (!asset) { details.Add("ABSENT optional resource " + resource); continue; }
                Assert(asset.GetComponentsInChildren<Canvas>(true).All(c => c.renderMode != RenderMode.ScreenSpaceOverlay || c.sortingOrder < canvas.sortingOrder), "menu sorts above imported intro/how-to prefab " + resource);
            }
            details.Add("No Lobby scene opened, modified or saved. No physical keyboard/mouse events or network operations generated. No pixels captured.");
            Directory.CreateDirectory(Evidence); File.WriteAllLines(Evidence + "/native-focus-layers-passed.txt", new[] { "PASS " + count + " native component/focus/layer assertions; synthetic lifecycle readiness." }.Concat(details));
        }
        finally
        {
            if (menuPrefab)
            {
                typeof(MemberMenu).GetMethod("OnDisable", privateInstance).Invoke(menuPrefab.GetComponent<MemberMenu>(), null);
                typeof(MemberMenu).GetMethod("OnDestroy", privateInstance).Invoke(menuPrefab.GetComponent<MemberMenu>(), null);
                PrefabUtility.UnloadPrefabContents(menuPrefab);
            }
            if (fixture)
            {
                typeof(HauntedHotelMultiplayer).GetField("_Lifecycle", privateInstance).SetValue(fixture.GetComponent<HauntedHotelMultiplayer>(), null);
                PrefabUtility.UnloadPrefabContents(fixture);
            }
            Cursor.visible = cursorVisible; Cursor.lockState = cursorLock;
            Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene() == activeScene && activeScene.isDirty == dirty, "focus/layer test preserves active authored scene");
        }
    }
    static void Check(bool success, string message) { if (!success) throw new InvalidOperationException("Authored pause menu validation failed: " + message); }
}
