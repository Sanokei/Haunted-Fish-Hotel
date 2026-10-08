#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using IOPath = System.IO.Path;
using System.Linq;
using System.Reflection;
using HauntedFish.Multiplayer;
using Ink.Runtime;
using Monologue.Dialogue;
using Monologue.StoryInput;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

// Copy into Assets only in a disposable cached project. No production scene is loaded.
public sealed class NativeDialogueValidation : MonoBehaviour
{
    int checks;
    string evidenceFolder;
    readonly List<string> errors = new();

    static void RequireDisposableProject()
    {
        string project = IOPath.GetFullPath(IOPath.Combine(Application.dataPath, ".."));
        string temp = IOPath.GetFullPath(IOPath.GetTempPath()).TrimEnd(IOPath.DirectorySeparatorChar,
            IOPath.AltDirectorySeparatorChar) + IOPath.DirectorySeparatorChar;
        if (!project.StartsWith(temp, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Native dialogue validation requires a disposable cached project under personal Temp.");
    }

    public static void Run()
    {
        RequireDisposableProject();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/DialogueValidation.unity");
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Monitor()
    {
        if (!Environment.GetCommandLineArgs().Contains("--hotel-dialogue-validation")) return;
        RequireDisposableProject();
        var root = new GameObject("Isolated native dialogue validation");
        DontDestroyOnLoad(root);
        root.AddComponent<NativeDialogueValidation>();
    }

    void Check(bool value, string description)
    {
        if (!value) throw new InvalidOperationException(description);
        checks++;
        Debug.Log("DIALOGUE_CHECK " + checks + ": " + description);
    }

    void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(message + "\n" + stack);
    }

    IEnumerator Start()
    {
        evidenceFolder = IOPath.GetFullPath(IOPath.Combine(Application.dataPath, "../DialogueValidationEvidence"));
        Directory.CreateDirectory(evidenceFolder);
        Application.logMessageReceived += Log;
        var tests = Tests();
        while (true)
        {
            object next;
            try
            {
                if (!tests.MoveNext()) break;
                next = tests.Current;
            }
            catch (Exception error)
            {
                Application.logMessageReceived -= Log;
                Debug.LogException(error);
                File.WriteAllText(IOPath.Combine(evidenceFolder, "result.txt"), "FAIL " + error);
                EditorApplication.Exit(1);
                yield break;
            }
            yield return next;
        }
        Application.logMessageReceived -= Log;
        if (errors.Count > 0)
        {
            File.WriteAllText(IOPath.Combine(evidenceFolder, "result.txt"), "FAIL native error log\n" + string.Join("\n", errors));
            EditorApplication.Exit(1);
            yield break;
        }
        File.WriteAllText(IOPath.Combine(evidenceFolder, "result.txt"),
            "PASS " + checks + " native dialogue checks: real prefab options, Ink choices and variables, input submission, disable/destroy lifecycle. Mirage transport and rendered layout were not exercised.");
        Debug.Log("NATIVE_DIALOGUE_PASS " + checks);
        EditorApplication.Exit(0);
    }

    static string Compile(string source)
    {
        var failures = new List<string>();
        var story = new Ink.Compiler(source, new Ink.Compiler.Options
        {
            errorHandler = (message, type) => { if (type == Ink.ErrorType.Error) failures.Add(message); }
        }).Compile();
        if (story == null || failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures));
        return story.ToJson();
    }

    static TextAsset Asset(string source) => new TextAsset(Compile(source));
    static List<OptionPrefab> Choices(Panel panel) => (List<OptionPrefab>)typeof(Panel)
        .GetField("_DialogueOptions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);

    static T Prefab<T>(string name) where T : Component
    {
        var prefab = Resources.Load<T>(name);
        if (!prefab) throw new InvalidOperationException("Missing authored resource: " + name);
        var instance = Instantiate(prefab);
        instance.gameObject.SetActive(false);
        var canvas = instance.GetComponentInParent<Canvas>(true);
        if (!canvas) throw new InvalidOperationException("The authored panel requires a Canvas: " + name);
        if (!canvas.GetComponent<WorldDialogueCanvas>()) canvas.gameObject.AddComponent<WorldDialogueCanvas>();
        return instance;
    }

    static DialogueManager Manager(TextAsset globals, Panel panel)
    {
        var root = new GameObject("Configured native dialogue manager");
        root.SetActive(false);
        var manager = root.AddComponent<DialogueManager>();
        manager.Configure(globals, panel);
        root.SetActive(true);
        return manager;
    }

    IEnumerator Tests()
    {
        Check(!DialogueManager.Instance && !StoryInputTextFieldManager.Instance,
            "An empty native scene owns isolated dialogue and input managers");
        var cameraRoot = new GameObject("Native dialogue test camera", typeof(Camera), typeof(HotelViewCamera));
        var eventRoot = new GameObject("Native dialogue test events", typeof(EventSystem), typeof(InputSystemUIInputModule));
        var globalsAsset = Asset("VAR score = 7\nVAR name = \"\"\n-> END");
        var globals = new Variables(globalsAsset);
        var scopedStory = new Story(Compile("VAR score = 0\nVAR localValue = 12\nHello.\n-> END"));
        globals.StartListening(scopedStory); globals.StartListening(scopedStory);
        Check((int)scopedStory.variablesState["score"] == 7 && (int)scopedStory.variablesState["localValue"] == 12,
            "Native Ink initializes shared globals while retaining story-local defaults");
        scopedStory.variablesState["localValue"] = 21;
        scopedStory.variablesState["score"] = 8;
        Check(!globals.Globals.Contains("localValue") && (int)globals["score"] == 8,
            "Native Ink changes persist shared values without polluting globals with story locals");
        globals.StopListening(scopedStory); globals.StopListening(scopedStory);
        scopedStory.variablesState["score"] = 9;
        Check((int)globals["score"] == 8, "Detached native Ink stories cannot mutate persistent values");

        var panel = Prefab<Panel>("MultiplayerDialoguePanel");
        var foreignPanel = Prefab<Panel>("MultiplayerDialoguePanel");
        var inputPanel = Prefab<TextFieldPanel>("MultiplayerInputPanel");
        var inputRoot = new GameObject("Configured native story input manager");
        inputRoot.SetActive(false);
        var input = inputRoot.AddComponent<StoryInputTextFieldManager>();
        input.Configure(inputPanel);
        inputRoot.SetActive(true);
        var manager = Manager(globalsAsset, panel);
        yield return null;
        Check(DialogueManager.Instance == manager && StoryInputTextFieldManager.Instance == input,
            "Actual Unity activation initializes managers after prefab configuration");

        var choiceOnly = Asset("-> options\n=== options ===\n* [Left]\n    Left branch.\n    -> END\n* [Right]\n    Right branch.\n    -> END");
        manager.EnterDialogMode(choiceOnly);
        Check(manager.ActiveDialoguePanel && manager.CurrentStory.currentText == "" &&
            !manager.CurrentStory.canContinue && panel.DialogueOptions.SequenceEqual(new[] { "Left", "Right" }),
            "The actual manager renders authored option prefabs for a choice-only Ink knot");
        manager.ContinueStory();
        Check(manager.ActiveDialoguePanel && manager.CurrentStory.currentChoices.Count == 2,
            "Continuing a choice-only native dialogue waits for selection");
        foreignPanel.gameObject.SetActive(true);
        foreignPanel.EnterDialogueMode();
        foreignPanel.DialogueOptions = new List<string> { "Foreign choice" };
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        Choices(foreignPanel)[0].OnPointerClick(pointer);
        Check(manager.CurrentStory.currentChoices.Count == 2 && manager.CurrentStory.currentText == "",
            "A real option on a foreign open panel cannot select the live manager's story");
        var stale = Choices(panel)[0];
        panel.DialogueOptions = panel.DialogueOptions;
        Check(!stale.gameObject.activeSelf && !panel.OwnsChoice(stale),
            "Native deferred destruction immediately retires the previous option row");
        manager.ChoiceSelected(stale);
        Check(manager.CurrentStory.currentChoices.Count == 2, "A retired native row cannot select a replacement choice");
        Choices(panel)[1].OnPointerClick(pointer);
        Check(manager.ActiveDialoguePanel && panel.DialogueText.Trim() == "Right branch.",
            "A current owned native row executes the selected choice-only branch");
        manager.ExitDialogMode();
        yield return null;
        Check(!stale, "Retired native option components are destroyed at the frame boundary");

        int ends = 0, inputStarts = 0;
        DialogueManager.OnDialogue ended = () =>
        {
            ends++;
            Check(!manager.IsSharedDialogue && !manager.ActiveDialoguePanel && !input.ActiveInputPanel,
                "Native end events observe closed dialogue, shared state, and owned input");
        };
        StoryInputTextFieldManager.OnStoryInput started = () => inputStarts++;
        DialogueManager.OnDialogueEndEvent += ended;
        StoryInputTextFieldManager.OnStoryInputStartEvent += started;
        try
        {
            var asking = Asset("VAR score = 0\nVAR name = \"\"\nEXTERNAL InputText(question,key,profile)\n~ InputText(\"Your name?\",\"name\",\"\")\nAfter input.\nNext line.\n-> END");
            manager.EnterDialogMode(asking);
            Check(input.ActiveInputPanel && !manager.ActiveDialoguePanel && inputStarts == 1,
                "Actual Ink InputText opens the authored native input panel once");
            var abandoned = manager.CurrentStory;
            manager.enabled = false;
            Check(!input.ActiveInputPanel && !manager.ActiveDialoguePanel && ends == 1,
                "Actual component disable closes owned native input and emits one end");
            abandoned.variablesState["score"] = 99;
            Check((int)manager.GlobalVars["score"] == 7,
                "An abandoned native input session no longer owns a globals observer");
            manager.enabled = true;
            manager.EnterDialogMode(asking);
            Check(input.ActiveInputPanel && inputStarts == 2, "A re-enabled native manager opens a fresh input session");
            input.SubmitShared("name", "Ada");
            Check(!input.ActiveInputPanel && manager.ActiveDialoguePanel && panel.DialogueText.Trim() == "After input." &&
                (string)manager.GlobalVars["name"] == "Ada" && inputStarts == 2,
                "Actual native submission updates globals and resumes the pending line without replaying InputText");
            manager.ExitDialogMode();
            manager.enabled = false;
            Destroy(manager.gameObject);
            yield return null;
            Check(ends == 2 && !DialogueManager.Instance,
                "Disable followed by native destruction emits no extra end and releases the singleton");

            manager = Manager(globalsAsset, panel);
            manager.EnterDialogMode(asking);
            Check(input.ActiveInputPanel && inputStarts == 3, "A replacement native manager can enter authored input");
            Destroy(manager.gameObject);
            yield return null;
            Check(!input.ActiveInputPanel && !panel.gameObject.activeInHierarchy && !DialogueManager.Instance && ends == 3,
                "Destroying an active native manager closes owned input and emits exactly one end");
        }
        finally
        {
            DialogueManager.OnDialogueEndEvent -= ended;
            StoryInputTextFieldManager.OnStoryInputStartEvent -= started;
        }
        Destroy(inputRoot); Destroy(panel.gameObject); Destroy(foreignPanel.gameObject);
        Destroy(inputPanel.gameObject); Destroy(cameraRoot); Destroy(eventRoot); Destroy(globalsAsset);
        yield return null;
    }
}
#endif
