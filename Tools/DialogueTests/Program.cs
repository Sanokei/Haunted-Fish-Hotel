using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HauntedFish.Multiplayer;
using Ink.Runtime;
using Monologue;
using Monologue.Dialogue;
using Monologue.StoryInput;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

static class Program
{
    static int checks;
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
    static string Compile(string source)
    {
        var errors = new List<string>();
        var story = new Ink.Compiler(source, new Ink.Compiler.Options
        {
            errorHandler = (message, type) => { if (type == Ink.ErrorType.Error) errors.Add(message); }
        }).Compile();
        if (story == null || errors.Count > 0) throw new Exception(string.Join("\n", errors));
        return story.ToJson();
    }
    static Story Story(string source) => new(Compile(source));
    static TextAsset Asset(string source) => new(Compile(source));
    static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    static void Call(object target, string method) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    static OptionPrefab Choice(Panel panel, int index = 0) =>
        ((List<OptionPrefab>)typeof(Panel).GetField("_DialogueOptions",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel))[index];

    static Panel CreatePanel(out GroupPanelPrefab choices)
    {
        choices = new GroupPanelPrefab();
        Set(choices, "_VerticalGroup", new LayoutGroup());
        var panel = new Panel();
        Set(panel, "m_DialogueText", new TMPro.TMP_Text());
        Set(panel, "m_DialogueDisplayName", new TMPro.TMP_Text());
        Set(panel, "m_ProfilePicture", new Image());
        Set(panel, "_DialogueOptionPrefab", new OptionPrefab { OptionTextGO = new TMPro.TMP_Text() });
        Set(panel, "_DialogueChoicePanel", choices);
        return panel;
    }

    static void VariablesRespectScopeAndLifetime()
    {
        var globals = new Variables(Compile("VAR score = 7\n-> END"));
        var story = Story("VAR score = 1\nVAR localValue = 12\nHello.\n-> END");
        int notifications = 0;
        Variables.OnGlobalsChange handler = (key, value, previous) => notifications++;
        Variables.OnGlobalsChangeEvent += handler;
        try
        {
            globals.StartListening(story);
            globals.StartListening(story);
            Check((int)story.variablesState["score"] == 7, "Shared globals seed newly opened stories");
            Check((int)story.variablesState["localValue"] == 12, "Story-local defaults survive global synchronization");
            story.variablesState["localValue"] = 21;
            Check(!globals.Globals.Contains("localValue") && notifications == 0,
                "Story-local updates do not pollute persistent globals or their events");
            story.variablesState["score"] = 8;
            Check((int)globals["score"] == 8 && notifications == 1,
                "Repeated listening still propagates a shared-variable update once");
            globals.StopListening(story);
            globals.StopListening(story);
            story.variablesState["score"] = 9;
            Check((int)globals["score"] == 8 && notifications == 1,
                "A stopped story cannot mutate persistent state");
            globals.StartListening(story);
            Check((int)story.variablesState["score"] == 8 && (int)story.variablesState["localValue"] == 21,
                "Reopening synchronizes shared values while preserving local changes");
            using (var session = new DialogueStorySession(story, globals))
                story.variablesState["score"] = 10;
            story.variablesState["score"] = 11;
            Check((int)globals["score"] == 10, "Session disposal owns the observer lifetime");
        }
        finally
        {
            globals.StopListening(story);
            Variables.OnGlobalsChangeEvent -= handler;
        }
    }

    static void InputSuspendsWithoutReplayingEffects()
    {
        var story = Story("EXTERNAL Ask()\n~ Ask()\nAfter input.\nNext line.\n-> END");
        bool input = false;
        int effects = 0;
        story.BindExternalFunction("Ask", () => { effects++; input = true; });
        using var session = new DialogueStorySession(story, null);
        Check(session.Advance(() => input) == DialogueAdvanceResult.WaitingForInput, "Ink input suspends presentation");
        Check(session.Advance(() => input) == DialogueAdvanceResult.WaitingForInput && effects == 1,
            "Waiting does not execute the external input effect again");
        input = false;
        Check(session.Advance(() => input) == DialogueAdvanceResult.LineReady && story.currentText.Trim() == "After input.",
            "Submitting input presents the pending line without skipping it");
        Check(session.Advance(() => input) == DialogueAdvanceResult.LineReady && story.currentText.Trim() == "Next line." && effects == 1,
            "The next advance executes the next Ink line");
        Check(session.Advance(() => input) == DialogueAdvanceResult.Ended, "A completed story terminates cleanly");
        session.Dispose();
        Check(session.Advance(() => false) == DialogueAdvanceResult.Ended && !session.TryChoose(0),
            "A disposed session rejects progression and choice selection");

        using var empty = new DialogueStorySession(Story("-> END"), null);
        Check(empty.Advance(null) == DialogueAdvanceResult.Ended,
            "An Ink story with no dialogue produces no presentation line");
        var closingStory = Story("EXTERNAL Close()\n~ Close()\nUnreachable.\n-> END");
        using var closing = new DialogueStorySession(closingStory, null);
        closingStory.BindExternalFunction("Close", closing.Dispose);
        Check(closing.Advance(null) == DialogueAdvanceResult.Ended,
            "Closing a session from an external Ink function cannot present its stale result");
    }

    static void ChoicesUseInkBranches()
    {
        var story = Story("Choose.\n* [Left]\n    Left branch.\n    -> END\n* [Right]\n    Right branch.\n    -> END");
        using var session = new DialogueStorySession(story, null);
        Check(session.Advance(null) == DialogueAdvanceResult.LineReady && story.currentChoices.Count == 2,
            "Dialogue exposes real Ink choices");
        Check(!session.TryChoose(-1) && !session.TryChoose(2) && story.currentChoices.Count == 2,
            "Invalid selections leave the current choices intact");
        Check(session.TryChoose(1) && session.Advance(null) == DialogueAdvanceResult.LineReady && story.currentText.Trim() == "Right branch.",
            "A valid choice executes the selected Ink branch");

        var choiceOnly = Story("-> options\n=== options ===\n* [Left]\n    Left branch.\n    -> END\n* [Right]\n    Right branch.\n    -> END");
        using var choices = new DialogueStorySession(choiceOnly, null);
        Check(choices.Advance(null) == DialogueAdvanceResult.LineReady && choiceOnly.currentText == "" &&
            !choiceOnly.canContinue && choiceOnly.currentChoices.Count == 2,
            "A choice-only knot presents its choices despite having no text or continuation");
        Check(choices.Advance(null) == DialogueAdvanceResult.WaitingForChoice && choiceOnly.currentChoices.Count == 2,
            "Advancing while a choice is pending neither executes Ink nor exits dialogue");
        Check(choices.TryChoose(1) && choices.Advance(null) == DialogueAdvanceResult.LineReady &&
            choiceOnly.currentText.Trim() == "Right branch.", "Choice-only knots still execute the selected branch");
    }

    static void PanelsOwnTheirChoices()
    {
        var first = CreatePanel(out var firstChoices);
        var second = CreatePanel(out _);
        first.DialogueOptions = new List<string> { "First" };
        second.DialogueOptions = new List<string> { "Second" };
        first.EnterDialogueMode(); first.EnterDialogueMode(); second.EnterDialogueMode();
        var optionsField = typeof(Panel).GetField("_DialogueOptions", BindingFlags.Instance | BindingFlags.NonPublic);
        var option = ((List<OptionPrefab>)optionsField.GetValue(first))[0];
        int selections = 0;
        int compatibilitySelections = 0;
        Panel.OnChoiceSelected handler = selected => selections++;
        Panel.OnChoiceSelected compatibilityHandler = selected => compatibilitySelections++;
        Panel.OnChoiceSelectedEvent += handler;
        OptionPrefab.OnChoiceSelectedEvent += compatibilityHandler;
        try
        {
            option.OnPointerClick(new PointerEventData { button = PointerEventData.InputButton.Left });
            Check(selections == 1 && compatibilitySelections == 1,
                "Repeated entry and a second panel cannot duplicate a choice or compatibility event");
            second.OnMakeChoice(option);
            Check(selections == 1, "Panels ignore choices owned by another panel");
            first.DialogueOptions = new List<string> { "Replacement" };
            Check(!option.gameObject.activeSelf, "Retired choices are deactivated before deferred destruction");
            Check(firstChoices.Count == 1 && first.DialogueOptions.SequenceEqual(new[] { "Replacement" }),
                "Choice ownership tracks current rows immediately during a rebuild");
            option.OnPointerClick(new PointerEventData { button = PointerEventData.InputButton.Left });
            Check(selections == 1, "Retired choices no longer notify their former panel");
            var replacement = ((List<OptionPrefab>)optionsField.GetValue(first))[0];
            replacement.OnPointerClick(new PointerEventData { button = PointerEventData.InputButton.Right });
            Check(selections == 1, "A non-left pointer button cannot select a choice");
            first.ExitDialogueMode(); first.ExitDialogueMode();
            replacement.OnPointerClick(new PointerEventData { button = PointerEventData.InputButton.Left });
            Check(selections == 1, "Closed panels cannot submit choices");
            first.EnterDialogueMode();
            replacement.OnPointerClick(new PointerEventData { button = PointerEventData.InputButton.Left });
            Check(selections == 2, "A reopened panel submits exactly one choice");
        }
        finally
        {
            Panel.OnChoiceSelectedEvent -= handler;
            OptionPrefab.OnChoiceSelectedEvent -= compatibilityHandler;
            Call(first, "OnDestroy"); Call(second, "OnDestroy");
        }
    }

    static void ManagerReleasesSessionsAndRendersRemoteState()
    {
        var panel = CreatePanel(out _);
        var manager = new DialogueManager();
        manager.Configure(Asset("VAR score = 7\n-> END"), panel);
        StoryInputTextFieldManager.Instance = new StoryInputTextFieldManager();
        Call(manager, "Awake"); Call(manager, "OnEnable");
        int ended = 0;
        DialogueManager.OnDialogue handler = () => ended++;
        DialogueManager.OnDialogueEndEvent += handler;
        try
        {
            var asset = Asset("VAR score = 0\nFirst.\nSecond.\n-> END");
            manager.EnterDialogMode(asset);
            var oldStory = manager.CurrentStory;
            var duplicate = new DialogueManager();
            duplicate.Configure(Asset("VAR score = 0\n-> END"), panel);
            Call(duplicate, "Awake"); Call(duplicate, "OnEnable");
            Call(duplicate, "OnDisable"); Call(duplicate, "OnDestroy");
            Check(DialogueManager.Instance == manager && manager.ActiveDialoguePanel && duplicate.GlobalVars == null,
                "A rejected duplicate manager cannot initialize or close the active owner's panel");
            manager.EnterDialogMode(asset);
            oldStory.variablesState["score"] = 90;
            Check((int)manager.GlobalVars["score"] == 7 && StoryFunctions.UnbindCount == 1,
                "Replacing a story releases its effects and persistent-variable observer");
            manager.ExitDialogMode(); manager.ExitDialogMode();
            Check(ended == 1 && StoryFunctions.BindCount == StoryFunctions.UnbindCount,
                "Repeated exit closes and releases a dialogue once");

            var inputAsset = Asset("EXTERNAL AskInput()\nEXTERNAL TestEffect()\n~ AskInput()\nAfter input.\n~ TestEffect()\nNext.\n-> END");
            manager.EnterDialogMode(inputAsset);
            Check(!manager.ActiveDialoguePanel && StoryInputTextFieldManager.Instance.ActiveInputPanel,
                "External input hides the actual manager's dialogue presentation");
            StoryInputTextFieldManager.Instance.ExitInputMode();
            Check(manager.ActiveDialoguePanel && panel.DialogueText.Trim() == "After input." && StoryFunctions.Effects == 0,
                "Input submission resumes the pending line without advancing effects");
            manager.ContinueStory();
            Check(panel.DialogueText.Trim() == "Next." && StoryFunctions.Effects == 1,
                "The manager advances effects only on the subsequent line");

            manager.EnterDialogMode(Asset("VAR score = 0\nEXTERNAL AskInput()\n~ AskInput()\nAfter input.\n-> END"));
            var beforeRemote = manager.CurrentStory;
            Check(StoryInputTextFieldManager.Instance.ActiveInputPanel && !manager.ActiveDialoguePanel,
                "Snapshot replacement also exercises a story paused with its panel hidden");
            var remoteAsset = Asset("VAR score = 15\nEXTERNAL TestEffect()\n~ TestEffect()\nRemote line.\n-> END");
            var remote = new Story(remoteAsset.text);
            remote.BindExternalFunction("TestEffect", () => { });
            remote.Continue();
            manager.ApplySharedSnapshot(new DialogueSnapshot
            {
                active = true, storyState = remote.state.ToJson(), text = "Remote line.", speaker = "Guest",
                choices = Array.Empty<string>(), inputQuestion = "", inputKey = ""
            }, remoteAsset);
            SharedDialogue.Applying = true;
            manager.ContinueStory();
            Check(panel.DialogueText == "Remote line." && StoryFunctions.Effects == 1,
                "Remote snapshots render state without executing Ink effects");
            beforeRemote.variablesState["score"] = 91;
            Check((int)manager.GlobalVars["score"] == 15,
                "Replacing an executable story with a snapshot releases its observer");
            manager.ExitDialogMode();
            SharedDialogue.Applying = false;
            int endedBeforeEmpty = ended;
            int tagsBeforeEmpty = StoryFunctions.Tags;
            manager.EnterDialogMode(Asset("-> END"));
            Check(!manager.ActiveDialoguePanel && ended == endedBeforeEmpty + 1 && StoryFunctions.Tags == tagsBeforeEmpty,
                "An empty story exits once without updating or tagging a closed panel");
            manager.EnterDialogMode(asset);
            var disabledStory = manager.CurrentStory;
            Call(manager, "OnDisable");
            disabledStory.variablesState["score"] = 92;
            Check(!manager.ActiveDialoguePanel && (int)manager.GlobalVars["score"] == 15 &&
                StoryFunctions.BindCount == StoryFunctions.UnbindCount,
                "Disabling the manager hides its panel and releases all executable bindings");
        }
        finally
        {
            DialogueManager.OnDialogueEndEvent -= handler;
            SharedDialogue.Applying = false;
            Call(manager, "OnDisable"); Call(manager, "OnDestroy");
            Call(panel, "OnDestroy");
            StoryInputTextFieldManager.Instance = null;
        }
        Check(!DialogueManager.Instance, "Destroying the manager clears the singleton owner");
    }

    static void Main()
    {
        VariablesRespectScopeAndLifetime();
        InputSuspendsWithoutReplayingEffects();
        ChoicesUseInkBranches();
        PanelsOwnTheirChoices();
        ManagerReleasesSessionsAndRendersRemoteState();
        ManagerRejectsForeignAndStaleOptions();
        ManagerClosesOwnedInputOnDisable();
        Console.WriteLine($"PASS: {checks} dialogue checks using the production sources and real Ink runtime.");
    }

    static void ManagerRejectsForeignAndStaleOptions()
    {
        var panel = CreatePanel(out _);
        var foreignPanel = CreatePanel(out _);
        foreignPanel.DialogueOptions = new List<string> { "Foreign choice" };
        foreignPanel.EnterDialogueMode();
        var foreign = Choice(foreignPanel);
        var manager = new DialogueManager();
        manager.Configure(Asset("VAR score = 7\n-> END"), panel);
        Call(manager, "Awake"); Call(manager, "OnEnable");
        var asset = Asset("Choose.\n* [Left]\n    Left branch.\n    -> END\n* [Right]\n    Right branch.\n    -> END");
        var pointer = new PointerEventData { button = PointerEventData.InputButton.Left };
        var router = new SharedDialogue();
        try
        {
            manager.EnterDialogMode(asset);
            foreign.OnPointerClick(pointer);
            Check(manager.CurrentStory.currentChoices.Count == 2 && panel.DialogueText.Trim() == "Choose.",
                "A live manager ignores a static selection emitted by a foreign panel");
            var stale = Choice(panel);
            panel.DialogueOptions = panel.DialogueOptions;
            manager.ChoiceSelected(stale);
            Check(manager.CurrentStory.currentChoices.Count == 2,
                "A retired row cannot select the current local story");
            manager.ChoiceSelected(1);
            Check(panel.DialogueText.Trim() == "Right branch.",
                "The trusted integer overload still selects an authoritative Ink branch");

            manager.EnterDialogMode(Asset("-> options\n=== options ===\n* [Left]\n    Left branch.\n    -> END\n* [Right]\n    Right branch.\n    -> END"));
            Check(manager.ActiveDialoguePanel && panel.DialogueOptions.SequenceEqual(new[] { "Left", "Right" }),
                "The live manager keeps a choice-only story visible");
            manager.ContinueStory();
            Check(manager.ActiveDialoguePanel && manager.CurrentStory.currentChoices.Count == 2,
                "A continue request cannot close a live manager waiting on choices");
            manager.ChoiceSelected(1);
            Check(panel.DialogueText.Trim() == "Right branch.", "The manager selects branches in a choice-only story");

            SharedDialogue.Instance = router;
            SharedDialogue.Applying = true;
            manager.BeginSharedDialogue(asset, Vector3.zero);
            SharedDialogue.Applying = false;
            router.RouteEnabled = true;
            var owned = Choice(panel);
            foreign.OnPointerClick(pointer);
            manager.ChoiceSelected(stale);
            Check(router.Routed.Count == 0 && manager.CurrentStory.currentChoices.Count == 2,
                "Foreign and retired rows are rejected before sending any shared-dialogue RPC");
            owned.OnPointerClick(pointer);
            Check(router.Routed.Count == 1 && router.Routed[0] == (2, 0),
                "A current owned row sends exactly one shared choice request");

            panel.ExitDialogueMode();
            manager.ChoiceSelected(owned);
            Check(router.Routed.Count == 1, "Rows on a closed panel cannot send shared requests");
            panel.EnterDialogueMode();
            panel.gameObject.SetActive(false);
            manager.ChoiceSelected(owned);
            Check(router.Routed.Count == 1, "Rows on a hidden panel cannot send shared requests");
            panel.gameObject.SetActive(true);
            owned.gameObject.SetActive(false);
            manager.ChoiceSelected(owned);
            Check(router.Routed.Count == 1, "An inactive row cannot send shared requests");
            owned.gameObject.SetActive(true);
            manager.ChoiceSelected(1);
            Check(router.Routed.Count == 2 && router.Routed[1] == (2, 1),
                "The integer overload remains available for trusted shared-dialogue routing");
        }
        finally
        {
            router.RouteEnabled = false;
            SharedDialogue.Applying = true;
            manager.ExitDialogMode();
            SharedDialogue.Instance = null;
            SharedDialogue.Applying = false;
            Call(manager, "OnDisable"); Call(manager, "OnDestroy");
            Call(panel, "OnDestroy"); Call(foreignPanel, "OnDestroy");
        }
    }

    static void ManagerClosesOwnedInputOnDisable()
    {
        var panel = CreatePanel(out _);
        var manager = new DialogueManager();
        manager.Configure(Asset("VAR score = 7\n-> END"), panel);
        var input = new StoryInputTextFieldManager();
        StoryInputTextFieldManager.Instance = input;
        var router = new SharedDialogue();
        SharedDialogue.Instance = router;
        Call(manager, "Awake"); Call(manager, "OnEnable");
        int ended = 0;
        DialogueManager.OnDialogue handler = () =>
        {
            ended++;
            Check(!manager.IsSharedDialogue && !manager.ActiveDialoguePanel && !input.ActiveInputPanel,
                "End notification observes inactive shared dialogue, presentation, and owned input");
        };
        DialogueManager.OnDialogueEndEvent += handler;
        try
        {
            var asset = Asset("VAR score = 0\nEXTERNAL AskInput()\n~ AskInput()\nAfter input.\nNext.\n-> END");
            int inputsBefore = StoryFunctions.Inputs;
            SharedDialogue.Applying = true;
            manager.BeginSharedDialogue(asset, Vector3.zero);
            SharedDialogue.Applying = false;
            Check(manager.IsSharedDialogue && input.ActiveInputPanel && !manager.ActiveDialoguePanel,
                "An authoritative shared story owns its active input panel");
            var abandoned = manager.CurrentStory;
            router.RouteEnabled = true;
            Call(manager, "OnDisable");
            Check(!input.ActiveInputPanel && !manager.ActiveDialoguePanel && ended == 1 && router.Routed.Count == 0,
                "Disable closes owned input locally and publishes one end without routing an RPC");
            abandoned.variablesState["score"] = 99;
            Check((int)manager.GlobalVars["score"] == 7 && StoryFunctions.BindCount == StoryFunctions.UnbindCount,
                "An abandoned input story releases effects and persistent-variable observers");
            Call(manager, "OnDisable");
            Check(ended == 1, "Repeated disable cannot publish a second end for an interrupted story");

            router.RouteEnabled = false;
            Call(manager, "OnEnable");
            manager.EnterDialogMode(asset);
            Check(input.ActiveInputPanel && StoryFunctions.Inputs == inputsBefore + 2,
                "Reopening after disable creates a fresh input session once");
            input.ExitInputMode();
            Check(manager.ActiveDialoguePanel && panel.DialogueText.Trim() == "After input." &&
                StoryFunctions.Inputs == inputsBefore + 2,
                "Submitting reopened input resumes its own pending line without replaying the abandoned session");
            manager.ExitDialogMode();
            Call(manager, "OnDisable"); Call(manager, "OnDestroy");
            Check(ended == 2 && StoryFunctions.BindCount == StoryFunctions.UnbindCount,
                "Destroy after closing or disabling emits no extra end and leaves no bindings");
        }
        finally
        {
            DialogueManager.OnDialogueEndEvent -= handler;
            router.RouteEnabled = false;
            SharedDialogue.Instance = null;
            SharedDialogue.Applying = false;
            Call(manager, "OnDisable"); Call(manager, "OnDestroy");
            Call(panel, "OnDestroy");
            StoryInputTextFieldManager.Instance = null;
        }
    }
}
