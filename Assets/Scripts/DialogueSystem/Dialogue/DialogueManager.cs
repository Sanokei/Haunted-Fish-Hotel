using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using Ink.Runtime;
using UnityEngine.InputSystem;
using HauntedFish.Multiplayer;
using Monologue.StoryInput;
using SimpleMan.CoroutineExtensions;

namespace Monologue.Dialogue
{
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance {get; private set;}
        public bool IsWaiting;
        bool remoteView, closingDialogue;
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
                // FIXME: Psuedo flag variable. Its actually worse, creating edge cases.
                return _DialoguePanel && _DialoguePanel.gameObject.activeSelf;
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
                Destroy(gameObject);
            if (m_GlobalsJSON) GlobalVars = new(m_GlobalsJSON);
            if (_DialoguePanel) ActiveDialoguePanel=false;
        }
        void OnEnable()
        {
            // FIXME: Passes up the Event, because I cannot invoke an event that isnt in the file. (in DialoguePrefab)
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
            IsSharedDialogue = false;
            Panel.OnChoiceSelectedEvent -= ChoiceSelected;
            
            StoryInputTextFieldManager.OnStoryInputStartEvent -= OnEnterInputMode;
            StoryInputTextFieldManager.OnStoryInputEndEvent -= OnExitInputMode;
            DontDestroyHelper.NotDestroyedHelperEvent -= DeactivatePanel;
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
        // FIXME: stupid flag variable
        bool _isAlreadyContinued;
        public void ContinueStory()
        {
            if (SharedDialogue.Instance && SharedDialogue.Instance.Route(1)) return;
            if (IsSharedDialogue && !SharedDialogue.Applying) return;
            OnDialogueTryingToContinueEvent?.Invoke();
            if (!ActiveDialoguePanel)
                return;
            
            if(CurrentStory.canContinue || _isAlreadyContinued)
            {
                //FIXME: this is awful.
                if(!_isAlreadyContinued)
                {
                    CurrentStory.Continue();
                    
                    _isAlreadyContinued = true;
                }
                if(_isAlreadyContinued && StoryInputTextFieldManager.Instance.ActiveInputPanel)
                {
                    return;   
                }
                _isAlreadyContinued = false;
                
                if(CurrentStory.currentText == "" && !CurrentStory.canContinue)
                    ExitDialogMode();
                
                OnDialogueContinuedEvent?.Invoke();

                // Strange bug with LINQ where it tries to send every Selected thing first before it tolists and sets.
                // tried it with a parentetical and it didnt work either. 
                var t = CurrentStory.currentChoices.Select(ctx => ctx.text).ToList();
                _DialoguePanel.DialogueOptions = t;
                if(t.Count > 0)
                    OnChoiceEvent?.Invoke(t);

                _DialoguePanel.DialogueText = CurrentStory.currentText;
                StoryFunctions.HandleTags(CurrentStory);
            }
            else
            {
                ExitDialogMode();
            }
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
            IsSharedDialogue = true;
            CurrentAsset = asset;
            remoteView = true;
            DialogueAnchor = data.anchor;
            WorldDialogueCanvas.Place(_DialoguePanel, DialogueAnchor);
            if (starting)
            {
                CurrentStory = new Story(asset.text);
                _DialoguePanel.EnterDialogueMode();
                OnDialogueStartEvent?.Invoke();
            }
            CurrentStory.state.LoadJson(data.storyState);
            foreach (var variable in CurrentStory.variablesState) if (GlobalVars.Globals.Contains(variable)) GlobalVars[variable]=CurrentStory.variablesState[variable];
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
            CurrentAsset = inkAsset;
            _isAlreadyContinued = false;
            remoteView = false;
            WorldDialogueCanvas.Place(_DialoguePanel, DialogueAnchor);
            OnDialogueStartEvent?.Invoke();

            CurrentStory = new Story(inkAsset.text);
            StoryFunctions.BindFunctions(CurrentStory);
            GlobalVars.StartListening(CurrentStory);
            _DialoguePanel.EnterDialogueMode();
            ActiveDialoguePanel = true;
            // Starts the story
            ContinueStory();
        }

        public void ExitDialogMode()
        {
            if (SharedDialogue.Instance && SharedDialogue.Instance.Route(3)) return;
            if (IsSharedDialogue && !SharedDialogue.Applying) return;
            if (CurrentStory == null) { IsSharedDialogue = false; return; }
            closingDialogue=true;
            try
            {
                if (StoryInputTextFieldManager.Instance && StoryInputTextFieldManager.Instance.ActiveInputPanel)
                    StoryInputTextFieldManager.Instance.ExitInputMode();
            }
            finally { closingDialogue=false; }
            IsSharedDialogue = false;
            if (!remoteView)
            {
                StoryFunctions.UnbindFunctions(CurrentStory);
                GlobalVars.StopListening(CurrentStory);
            }
            remoteView=false;
            _DialoguePanel.ExitDialogueMode();
            ActiveDialoguePanel = false;
            
            OnDialogueEndEvent?.Invoke();
        }

        public void ChoiceSelected(OptionPrefab option)
        {
            if (option) ChoiceSelected(option.index);
        }

        public void ChoiceSelected(int idx)
        {
            if (SharedDialogue.Instance && SharedDialogue.Instance.Route(2, idx)) return;
            if (IsSharedDialogue && !SharedDialogue.Applying) return;
            if (CurrentStory == null || idx < 0 || idx >= CurrentStory.currentChoices.Count) return;
            print("int" + idx);
            CurrentStory.ChooseChoiceIndex(idx);
            ContinueStory();
        }

    }
}
