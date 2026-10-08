using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using Ink.Runtime;
using UnityEngine.InputSystem;
using HauntedFish.Multiplayer;
using Monologue.StoryInput;

namespace Monologue.Dialogue
{
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance {get; private set;}
        public bool IsWaiting;
        bool remoteView, closingDialogue;
        bool dialogueOpen;
        DialogueStorySession storySession;
        Story boundFunctionsStory;
        public void Configure(TextAsset globals, Panel panel) { m_GlobalsJSON=globals; _DialoguePanel=panel; }
        public void ConfigureMissing(TextAsset globals, Panel panel)
        {
            if (!m_GlobalsJSON) m_GlobalsJSON=globals;
            if (!_DialoguePanel) _DialoguePanel=panel;
            if (GlobalVars==null && m_GlobalsJSON) GlobalVars=new(m_GlobalsJSON);
        }
        public bool IsSharedDialogue { get; private set; }
        public TextAsset CurrentAsset { get; private set; }
        public Vector3 DialogueAnchor { get; private set; }
        public delegate void OnDialogue();
        public static event OnDialogue OnDialogueStartEvent;
        public static event OnDialogue OnDialogueEndEvent;
        public static event OnDialogue OnDialogueContinuedEvent;
        public static event OnDialogue OnDialogueTryingToContinueEvent;
        public delegate void OnChoice(List<string> choices);
        public static event OnChoice OnChoiceEvent;
        
        [Header("Globals Ink")]
        [SerializeField] TextAsset m_GlobalsJSON;
        public Variables GlobalVars;
        [Header("Dialogue UI")]
        public Story CurrentStory;
        
        // Prefabs
        public Panel _DialoguePanel;

        // Public
        public bool ActiveDialoguePanel
        {
            get
            {
                return _DialoguePanel && _DialoguePanel.gameObject.activeInHierarchy;
            }
            set
            {
                if (_DialoguePanel) _DialoguePanel.gameObject.SetActive(value);
            }
        }
        void Awake()
        {
            if (!Instance)
                Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }
            if (m_GlobalsJSON) GlobalVars = new(m_GlobalsJSON);
            if (_DialoguePanel) ActiveDialoguePanel=false;
        }
        void OnEnable()
        {
            if (Instance != this) return;
            Panel.OnChoiceSelectedEvent += ChoiceSelected;

            StoryInputTextFieldManager.OnStoryInputStartEvent += OnEnterInputMode;
            StoryInputTextFieldManager.OnStoryInputEndEvent += OnExitInputMode;
            DontDestroyHelper.NotDestroyedHelperEvent += DeactivatePanel;
        }
        void DeactivatePanel()
        {
            if (ChangeSceneOnLoadDontDestroy.Instance) ChangeSceneOnLoadDontDestroy.Instance.NextScene();
            ActiveDialoguePanel = false;
        }
        void OnDisable()
        {
            Panel.OnChoiceSelectedEvent -= ChoiceSelected;
            
            StoryInputTextFieldManager.OnStoryInputStartEvent -= OnEnterInputMode;
            StoryInputTextFieldManager.OnStoryInputEndEvent -= OnExitInputMode;
            DontDestroyHelper.NotDestroyedHelperEvent -= DeactivatePanel;
            if (Instance == this) CloseDialogueLocally();
            else ReleaseStorySession();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                CloseDialogueLocally();
                Instance = null;
            }
            else ReleaseStorySession();
        }
        void OnEnterInputMode()
        {
            if (StoryInputTextFieldManager.Instance) WorldDialogueCanvas.Place(StoryInputTextFieldManager.Instance.InputPanel, DialogueAnchor);
            ActiveDialoguePanel = false;
        }

        void OnExitInputMode()
        {
            // Remote snapshots control presentation only; never continue client Ink.
            if (remoteView || closingDialogue) return;
            if (IsSharedDialogue && !SharedDialogue.Applying) return;
            ActiveDialoguePanel = true;
            ContinueStory();
        }
        void Update()
        {
            if (!ActiveDialoguePanel || IsWaiting || CurrentStory == null) return;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var advance = (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame ||
                keyboard.eKey.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame)) ||
                (mouse != null && mouse.leftButton.wasPressedThisFrame && CurrentStory.currentChoices.Count == 0);
            if (advance) ContinueStory();
        }
        public void ContinueStory()
        {
            if (SharedDialogue.Instance && SharedDialogue.Instance.Route(1)) return;
            if (IsSharedDialogue && !SharedDialogue.Applying) return;
            OnDialogueTryingToContinueEvent?.Invoke();
            if (!ActiveDialoguePanel || storySession == null) return;

            var session = storySession;
            var result = session.Advance(() => StoryInputTextFieldManager.Instance &&
                StoryInputTextFieldManager.Instance.ActiveInputPanel);
            if (session != storySession || result == DialogueAdvanceResult.WaitingForInput ||
                result == DialogueAdvanceResult.WaitingForChoice) return;
            if (result == DialogueAdvanceResult.Ended)
            {
                ExitDialogMode();
                return;
            }

            OnDialogueContinuedEvent?.Invoke();
            if (session != storySession) return;
            var choices = session.Story.currentChoices.Select(choice => choice.text).ToList();
            _DialoguePanel.DialogueOptions = choices;
            if (choices.Count > 0) OnChoiceEvent?.Invoke(choices);
            if (session != storySession) return;

            _DialoguePanel.DialogueText = session.Story.currentText;
            StoryFunctions.HandleTags(session.Story);
        }
        public void EnterDialogMode(TextAsset inkAsset, DialogueAudience audience, Vector3 anchor)
        {
            if (IsSharedDialogue) return;
            if (audience == DialogueAudience.Everyone && SharedDialogue.Instance && SharedDialogue.Instance.Open(inkAsset, anchor)) return;
            DialogueAnchor = anchor;
            EnterDialogMode(inkAsset);
        }
        public void BeginSharedDialogue(TextAsset inkAsset, Vector3 anchor)
        {
            IsSharedDialogue = true;
            DialogueAnchor = anchor;
            EnterDialogMode(inkAsset);
        }
        public void ApplySharedSnapshot(DialogueSnapshot data, TextAsset asset)
        {
            if (!data.active)
            {
                if (IsSharedDialogue) ExitDialogMode();
                return;
            }
            bool starting = !IsSharedDialogue || CurrentAsset != asset;
            if (starting && ActiveDialoguePanel) ExitDialogMode();
            if (starting) ReleaseStorySession();
            IsSharedDialogue = true;
            CurrentAsset = asset;
            remoteView = true;
            DialogueAnchor = data.anchor;
            WorldDialogueCanvas.Place(_DialoguePanel, DialogueAnchor);
            if (starting)
            {
                dialogueOpen = true;
                CurrentStory = new Story(asset.text);
                _DialoguePanel.EnterDialogueMode();
                OnDialogueStartEvent?.Invoke();
            }
            CurrentStory.state.LoadJson(data.storyState);
            if (GlobalVars != null)
                foreach (var variable in CurrentStory.variablesState)
                    if (GlobalVars.Globals.Contains(variable)) GlobalVars[variable]=CurrentStory.variablesState[variable];
            _DialoguePanel.DialogueText = data.text;
            _DialoguePanel.DialogueDisplayName = data.speaker;
            StoryFunctions.ApplyNetworkCue(new SharedWorldCue { Type=0, First=data.speaker });
            _DialoguePanel.DialogueOptions = data.choices.ToList();
            if (StoryInputTextFieldManager.Instance)
                StoryInputTextFieldManager.Instance.ApplySharedInput(data.inputActive, data.inputQuestion, data.inputKey);
            ActiveDialoguePanel = !data.inputActive;
            OnDialogueContinuedEvent?.Invoke();
        }
        public void EnterDialogMode(TextAsset inkAsset)
        {
            if (!inkAsset || (IsSharedDialogue && !SharedDialogue.Applying)) return;
            ReleaseStorySession();
            CurrentAsset = inkAsset;
            remoteView = false;
            dialogueOpen = true;
            WorldDialogueCanvas.Place(_DialoguePanel, DialogueAnchor);
            OnDialogueStartEvent?.Invoke();

            CurrentStory = new Story(inkAsset.text);
            StoryFunctions.BindFunctions(CurrentStory);
            boundFunctionsStory = CurrentStory;
            storySession = new DialogueStorySession(CurrentStory, GlobalVars);
            _DialoguePanel.EnterDialogueMode();
            ActiveDialoguePanel = true;
            // Starts the story
            ContinueStory();
        }

        public void ExitDialogMode()
        {
            if (SharedDialogue.Instance && SharedDialogue.Instance.Route(3)) return;
            if (IsSharedDialogue && !SharedDialogue.Applying) return;
            CloseDialogueLocally();
        }

        void CloseDialogueLocally()
        {
            bool wasOpen = dialogueOpen;
            dialogueOpen = false;
            try
            {
                if (wasOpen) CloseInputPanel();
            }
            finally
            {
                IsSharedDialogue = false;
                try { ReleaseStorySession(); }
                finally
                {
                    remoteView = false;
                    if (_DialoguePanel) _DialoguePanel.ExitDialogueMode();
                    ActiveDialoguePanel = false;
                    if (wasOpen) OnDialogueEndEvent?.Invoke();
                }
            }
        }

        void CloseInputPanel()
        {
            closingDialogue = true;
            try
            {
                if (StoryInputTextFieldManager.Instance && StoryInputTextFieldManager.Instance.ActiveInputPanel)
                    StoryInputTextFieldManager.Instance.ExitInputMode();
            }
            finally { closingDialogue = false; }
        }

        public void ChoiceSelected(OptionPrefab option)
        {
            if (_DialoguePanel && _DialoguePanel.OwnsChoice(option)) ChoiceSelected(option.index);
        }

        public void ChoiceSelected(int idx)
        {
            if (SharedDialogue.Instance && SharedDialogue.Instance.Route(2, idx)) return;
            if (IsSharedDialogue && !SharedDialogue.Applying) return;
            if (IsWaiting || storySession == null || !storySession.TryChoose(idx)) return;
            ContinueStory();
        }

        void ReleaseStorySession()
        {
            var session = storySession;
            var functionsStory = boundFunctionsStory;
            storySession = null;
            boundFunctionsStory = null;
            try
            {
                if (functionsStory != null) StoryFunctions.UnbindFunctions(functionsStory);
            }
            finally
            {
                session?.Dispose();
            }
        }

    }
}
