using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HauntedFish.Multiplayer;
using Monologue.Dialogue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public static class ValidateIntroduction
{
    public static void Run()
    {
        Debug.Log("Starting Unity introduction validation.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Introduction validation").AddComponent<IntroductionValidationRunner>();
        EditorSceneManager.SaveScene(scene, "Assets/Validation.unity");
        EditorApplication.EnterPlaymode();
    }
}

public sealed class IntroductionValidationRunner : MonoBehaviour
{
    int _Checks;
    void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _Checks++;
        Debug.Log("INTRO_CHECK " + _Checks + ": " + message);
    }
    static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    static T Get<T>(object target, string field)
    {
        if (target is LobbyIntroductionPresentation presentation)
        {
            var ui = presentation.UI;
            object value = null;
            if (ui)
            {
                switch (field)
                {
                    case "_Stage": value = Get<GameObject>(ui, "_Content"); break;
                    case "_SkipCircle": value = Get<IntroductionSkipCircle>(ui, "_SkipProgress"); break;
                    case "_Panels": value = new[] { ui.Find("door_panel"), ui.Find("arrival_panel"), ui.Find("reading_panel") }; break;
                    case "_Reading": value = ui.Find("letter_read").gameObject; break;
                    case "_Lightning": value = Array.Find(Get<StoryUI.Element[]>(ui, "_Elements"), item => item.Tag == "cloud").Variants[1].Target; break;
                    case "_KnockingDoor": value = ui.Find("door"); break;
                    case "_SlidingLetter": value = ui.Find("letter"); break;
                    case "_Flash": value = Get<GameObject>(ui, "_WhiteFrame"); break;
                    case "_HotelLayer": value = ui.Find("hotel_layer"); break;
                    case "_FoliageLayer": value = ui.Find("foliage_layer"); break;
                    case "_GateLayer": value = ui.Find("gate_layer"); break;
                }
            }
            return (T)value;
        }
        return (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
    static TextAsset Ink(string source) => new TextAsset(new Ink.Compiler(source).Compile().ToJson());
    LobbyManager Create(TextAsset ink, HauntedHotelMultiplayer session, out GameObject root, out LobbyIntroductionPresentation presentation)
    {
        root = new GameObject("Lobby manager test");
        root.SetActive(false);
        var manager = root.AddComponent<LobbyManager>();
        presentation = root.AddComponent<LobbyIntroductionPresentation>();
        Set(presentation, "_UIPrefab", Resources.Load<StoryUI>("LobbyIntroductionUI"));
        Set(manager, "_Panel", root.AddComponent<HotelLobbyPanel>());
        Set(manager, "_Lobby", session);
        Set(manager, "_IntroductionInk", ink);
        Set(manager, "_IntroductionPresentation", presentation);
        root.SetActive(true);
        return manager;
    }
    IEnumerator Start()
    {
        // Flatten nested coroutines here so a presentation failure fails the process immediately.
        var stack = new Stack<IEnumerator>();
        stack.Push(Tests());
        while (stack.Count > 0)
        {
            object next;
            try
            {
                if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                next = stack.Peek().Current;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
                yield break;
            }
            if (next is IEnumerator nested) stack.Push(nested);
            else yield return next;
        }
        Debug.Log($"Passed {_Checks} Unity introduction checks.");
        EditorApplication.Exit(0);
    }
    IEnumerator Tests()
    {
        gameObject.AddComponent<AudioListener>();
        Application.runInBackground = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var session = new GameObject("Session double").AddComponent<HauntedHotelMultiplayer>();
        const string seenKey = "HauntedFish.IntroductionSeen.v1";
        int previousSeen = PlayerPrefs.GetInt(seenKey, -1);
        PlayerPrefs.SetInt(seenKey, 1);
        HotelPlayer.LocalPlayer = new GameObject("Player double").AddComponent<HotelPlayer>();
        var manager = Create(Resources.Load<TextAsset>("LobbyIntroduction"), session, out var root, out var presentation);
        yield return null;
        Check(manager.IntroductionPlaying && Get<GameObject>(presentation, "_Stage").activeSelf,
            "The opaque intro starts before player admission without a lobby frame");
        Check(manager.IntroductionPlaying, "Editor playback ignores a previously saved seen flag");
        PlayerPrefs.SetInt(seenKey, 42);
        session.ReadyToPlay = true;
        session.Publish();
        yield return null; yield return null;
        Check(manager.IntroductionPlaying && session.IntroductionPlaying, "Background admission leaves the client intro running");
        Check(session.Session.InputFocused, "Introduction blocks player movement");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
        yield return new WaitForSecondsRealtime(.15f);
        Check(manager.IntroductionPlaying, "A brief Space press cannot skip the introduction");
        var skipCircle = Get<IntroductionSkipCircle>(presentation, "_SkipCircle");
        Check(skipCircle.Progress > 0 && skipCircle.Progress < 1, "Holding Space gradually fills the progress circle");
        Check(skipCircle.GetComponent<CanvasRenderer>() != null, "The skip circle has a CanvasRenderer to draw its radial fill");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null; yield return null;
        Check(skipCircle.Progress == 0, "Releasing Space resets the skip circle");
        int finishes = 0;
        Get<UnityEvent>(manager, "_IntroductionFinished").AddListener(() => finishes++);
        manager.SkipIntroduction();
        yield return null;
        Check(PlayerPrefs.GetInt(seenKey) == 42, "Editor skipping does not write the saved seen flag");
        if (previousSeen < 0) PlayerPrefs.DeleteKey(seenKey); else PlayerPrefs.SetInt(seenKey, previousSeen);
        Check(!manager.IntroductionPlaying && !session.Session.InputFocused, "Skip releases introduction input focus");
        Check(!Get<GameObject>(presentation, "_Stage"), "Skip removes the storyboard stage");
        Check(finishes == 1, "Skip runs custom restoration once");
        session.Publish(); yield return null;
        Check(!manager.IntroductionPlaying, "Roster refresh does not replay a skipped introduction");
        Destroy(root); yield return null;

        var shortInk = Ink("EXTERNAL SetVisible(objectTag,visible)\nEXTERNAL MoveUI(objectTag,x,y,seconds)\nEXTERNAL PushTo(panelTag,seconds)\nEXTERNAL Wait(seconds)\n" +
            "~ SetVisible(\"storm\",false)\n~ SetVisible(\"interior\",true)\n~ PushTo(\"letter_under_door\",0.02)\n" +
            "~ MoveUI(\"letter\",0,50,0.02)\n~ SetVisible(\"letter_read\",true)\n~ PushTo(\"letter_read\",0.02)\n~ Wait(0.02)\n-> END");
        manager = Create(shortInk, session, out root, out presentation);
        Time.timeScale = 0;
        yield return new WaitForSecondsRealtime(.5f);
        Check(!manager.IntroductionPlaying && !session.Session.InputFocused, "Normal completion works with game time paused and releases input");
        Check(!Get<GameObject>(presentation, "_Stage"), "Normal completion cleans up visuals");
        Destroy(root); yield return null;
        var longPush = Ink("EXTERNAL SetVisible(objectTag,visible)\nEXTERNAL PushTo(panelTag,seconds)\n~ SetVisible(\"interior\",true)\n~ PushTo(\"letter_under_door\",10)\n-> END");
        manager = Create(longPush, session, out root, out presentation);
        yield return new WaitForSecondsRealtime(.1f);
        root.SetActive(false); yield return null;
        Check(!manager.IntroductionPlaying && !session.Session.InputFocused, "Disabling the lobby cancels a push and restores control");
        Check(!Get<GameObject>(presentation, "_Stage"), "Cancellation removes the stage and its audio source");
        Destroy(root); yield return null;
        manager = Create(longPush, session, out root, out presentation);
        Set(manager, "_SkipHoldSeconds", .15f);
        yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
        yield return new WaitForSecondsRealtime(.3f);
        Check(!manager.IntroductionPlaying && !session.Session.InputFocused, "A complete Space hold skips and restores player control");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        Destroy(root); yield return null;
        manager = Create(longPush, session, out root, out presentation);
        yield return new WaitForSecondsRealtime(.1f);
        session.ReadyToPlay = false; session.Publish(); yield return null;
        Check(manager.IntroductionPlaying && session.Session.InputFocused, "Connection state changes do not cancel the client-local intro");
        Destroy(root); yield return null;

        var stageRoot = new GameObject("Storyboard visual validation");
        presentation = stageRoot.AddComponent<LobbyIntroductionPresentation>();
        Set(presentation, "_UIPrefab", Resources.Load<StoryUI>("LobbyIntroductionUI"));
        yield return presentation.Play(new StorySequenceStep(StorySequenceStepKind.SetVisible, "background", visible: true));
        yield return presentation.Play(new StorySequenceStep(StorySequenceStepKind.SetVariant, "cloud", "cloud"));
        Check(!Get<GameObject>(presentation, "_Lightning").activeSelf, "Ordinary cloud precedes the lightning variant");
        yield return presentation.Play(new StorySequenceStep(StorySequenceStepKind.SetVariant, "cloud", "cloud_lightning"));
        Check(Get<GameObject>(presentation, "_Lightning").activeSelf, "Ink reveals cloud_lightning");
        Capture(presentation, "01-cloud-lightning");
        yield return presentation.Play(new StorySequenceStep(StorySequenceStepKind.SetVisible, "storm", visible: false));
        yield return presentation.Play(new StorySequenceStep(StorySequenceStepKind.SetVisible, "interior", visible: true));
        yield return presentation.Play(new StorySequenceStep(StorySequenceStepKind.Knock, "door", seconds: .02f, count: 1));
        Check(Get<RectTransform>(presentation, "_KnockingDoor").anchoredPosition == Vector2.zero, "Knock animation restores the door position");
        Capture(presentation, "02-door");
        yield return presentation.Play(new StorySequenceStep(StorySequenceStepKind.PushTo, "letter_under_door", seconds: .02f));
        yield return presentation.Play(new StorySequenceStep(StorySequenceStepKind.MoveUI, "letter", seconds: .02f, x: 0, y: 50));
        Check(Get<RectTransform[]>(presentation, "_Panels")[0].anchoredPosition.x == -320, "First push holds the door beside the envelope in two slanted sections");
        yield return new WaitForSecondsRealtime(.06f);
        Check(Get<RectTransform[]>(presentation, "_Panels")[0].anchoredPosition.x == -320, "First push stays at its destination during the hold");
        Check(Get<RectTransform>(presentation, "_SlidingLetter").anchoredPosition.y == 50, "Envelope slides completely onto the floor");
        Capture(presentation, "03-letter-under-door");
        yield return presentation.Play(new StorySequenceStep(StorySequenceStepKind.SetVisible, "letter_read", visible: true));
        var pushing = StartCoroutine(presentation.Play(new StorySequenceStep(StorySequenceStepKind.PushTo, "letter_read", seconds: .3f)));
        yield return new WaitForSecondsRealtime(.12f);
        float midPush = Get<RectTransform[]>(presentation, "_Panels")[0].anchoredPosition.x;
        Check(midPush < -320 && midPush > -427 && Get<GameObject>(presentation, "_Reading").activeSelf,
            "Second push moves existing sections left while the reading section enters");
        presentation.SetSkipProgress(.5f);
        Capture(presentation, "03b-push-in-progress");
        yield return pushing;
        yield return presentation.Play(new StorySequenceStep(StorySequenceStepKind.SetVisible, "letter_read", visible: true));
        Check(Mathf.Abs(Get<RectTransform[]>(presentation, "_Panels")[0].anchoredPosition.x + 1280f / 3) < .01f && Get<GameObject>(presentation, "_Reading").activeSelf,
            "Second push presents the letter being read");
        Capture(presentation, "04-letter-read");
        yield return new WaitForSecondsRealtime(.06f);
        Check(Mathf.Abs(Get<RectTransform[]>(presentation, "_Panels")[0].anchoredPosition.x + 1280f / 3) < .01f, "Second push stays at its destination during reading");
        yield return presentation.Play(new StorySequenceStep(StorySequenceStepKind.ExpandPanel, "letter_fullscreen", seconds: .05f));
        var readingPanel = Get<RectTransform[]>(presentation, "_Panels")[2];
        Check(readingPanel.sizeDelta == new Vector2(1280, 720) && readingPanel.anchoredPosition == Vector2.zero,
            "The reading rhombus expands over the earlier sections to fill the screen");
        Capture(presentation, "05-letter-expanded");
        var flash = presentation.UI.Play(new StorySequenceStep(StorySequenceStepKind.FlashFrames, count: 2));
        int whiteFrames = 0;
        while (flash.MoveNext())
        {
            Check(Get<GameObject>(presentation, "_Flash").activeSelf, "A white frame covers the scene change");
            whiteFrames++;
            yield return flash.Current;
        }
        Check(whiteFrames == 2 && !Get<GameObject>(presentation, "_Flash").activeSelf, "The white flash lasts exactly two frames");
        Capture(presentation, "06-gate-exterior");
        yield return presentation.Play(new StorySequenceStep(StorySequenceStepKind.DollyTo, "gate_close", x: 1, seconds: .05f));
        Check(Get<RectTransform>(presentation, "_GateLayer").localScale.x > Get<RectTransform>(presentation, "_FoliageLayer").localScale.x &&
            Get<RectTransform>(presentation, "_FoliageLayer").localScale.x > Get<RectTransform>(presentation, "_HotelLayer").localScale.x,
            "Gate, foliage and hotel move at different depths during the dolly");
        Capture(presentation, "07-gate-dolly");
        var authoredUI = Resources.Load<StoryUI>("LobbyIntroductionUI");
        Set(presentation.UI, "_Prefabs", new[] { new StoryUI.Template { Tag = "letter", Prefab = authoredUI.Find("letter") } });
        yield return presentation.Play(new StorySequenceStep(StorySequenceStepKind.CreateUI, "additional_letter", "letter", parent: "arrival_panel"));
        Check(presentation.UI.Find("additional_letter") && presentation.UI.Find("additional_letter").parent == presentation.UI.Find("arrival_panel"),
            "CreateUI instantiates an authored prefab under a tagged UI parent");
        yield return presentation.Play(new StorySequenceStep(StorySequenceStepKind.SetVisible, "additional_letter", visible: false));
        Check(!presentation.UI.Find("additional_letter").gameObject.activeSelf,
            "Created UI participates in the same typed story functions");
        presentation.Close(); yield return null;
        Check(!Get<GameObject>(presentation, "_Stage"), "Closing the storyboard releases its runtime objects");
        Time.timeScale = 1;
        InputSystem.RemoveDevice(keyboard);
    }
    static void Capture(LobbyIntroductionPresentation presentation, string name)
    {
        var canvas = Get<GameObject>(presentation, "_Stage").GetComponent<Canvas>();
        var camera = new GameObject("Capture camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        var target = new RenderTexture(1280, 720, 24);
        camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        texture.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "../" + name + ".png"), texture.EncodeToPNG());
        RenderTexture.active = previous;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        camera.targetTexture = null;
        target.Release(); Destroy(target); Destroy(texture); Destroy(camera.gameObject);
    }
}

// These doubles replace network admission and desk UI, not the sequence runner or animation renderer.
namespace HauntedFish.Multiplayer
{
    public sealed class HauntedHotelMultiplayer : MonoBehaviour
    {
        public bool ReadyToPlay, Transitioning, MemberMenuOpen, IntroductionPlaying;
        public string Code = "ABC123", Status = "Connected";
        public LobbyRoster Roster;
        public HotelSessionContext Session = new HotelSessionContext();
        bool _DeskFocused;
        public event Action StateChanged;
        public void Publish() => StateChanged?.Invoke();
        public void BindReadyZone(Func<HotelPlayer, bool> ready) { }
        public void SetInputFocused(bool focused) { _DeskFocused = focused; Session.SetInputFocused(focused || IntroductionPlaying || MemberMenuOpen); }
        public void SetIntroductionPlaying(bool playing) { IntroductionPlaying = playing; Session.SetInputFocused(playing || _DeskFocused || MemberMenuOpen); }
        public void JoinLobby(string code) { }
    }
    public sealed class HotelPlayer : MonoBehaviour
    {
        public static HotelPlayer LocalPlayer;
        public static event Action<HotelPlayer> LocalPlayerChanged;
        public bool IsRelevantPlayer = true;
    }
    public sealed class HotelLobbyPanel : MonoBehaviour
    {
        public bool InputFocused;
        public event Action CopyRequested;
        public event Action<string> JoinRequested;
        public event Action<bool> InputFocusChanged;
        public void ClearSelection() { }
        public void Render(bool connected, bool transitioning, bool menu, string code, LobbyRoster roster, string status, bool onStairs = false) { }
    }
    public sealed class LobbyTrigger : MonoBehaviour
    {
        public event Action<Collider, HotelPlayer, bool> ZonePresenceChanged;
        public void Replay(Action<Collider, HotelPlayer, bool> callback) { }
        public bool Contains(HotelPlayer player) => false;
        public bool ContainsPosition(HotelPlayer player) => false;
    }
}
