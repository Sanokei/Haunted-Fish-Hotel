using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using Ink.Runtime;
using Monologue.StoryInput;
using SimpleMan.CoroutineExtensions;

namespace Monologue.Dialogue
{
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance {get; private set;}
        public bool IsWaiting;
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
                return _DialoguePanel.gameObject.activeSelf;
            }
            set
            {
                _DialoguePanel.gameObject.SetActive(value);
            }
        }
        void Awake()
        {
            if (!Instance)
                Instance = this;
            else
                Destroy(gameObject);
            GlobalVars = new(m_GlobalsJSON);
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
            ChangeSceneOnLoadDontDestroy.Instance.NextScene();
            ActiveDialoguePanel = false;
        }
        void OnDisable()
        {
            Panel.OnChoiceSelectedEvent -= ChoiceSelected;
            
            StoryInputTextFieldManager.OnStoryInputStartEvent -= OnEnterInputMode;
            StoryInputTextFieldManager.OnStoryInputEndEvent -= OnExitInputMode;
            DontDestroyHelper.NotDestroyedHelperEvent -= DeactivatePanel;
        }
        void OnEnterInputMode()
        {
            ActiveDialoguePanel = false;
        }

        void OnExitInputMode()
        {
            ActiveDialoguePanel = true;
            ContinueStory();
        }
        void Update()
        {
            if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F) || (Input.GetMouseButtonDown(0) && CurrentStory?.currentChoices.Count == 0))
            && !IsWaiting)
                ContinueStory();
        }
        // FIXME: stupid flag variable
        bool _isAlreadyContinued;
        public void ContinueStory()
        {
            OnDialogueTryingToContinueEvent?.Invoke();
            if (!ActiveDialoguePanel)
                return;
            
            if(CurrentStory.canContinue)
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
                _DialoguePanel.DialogueOptions = new();
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
        public void EnterDialogMode(TextAsset inkAsset)
        {
            OnDialogueStartEvent?.Invoke();

            CurrentStory = new Story(inkAsset.text);
            StoryFunctions.BindFunctions(CurrentStory);
            GlobalVars.StartListening(CurrentStory);
            _DialoguePanel.EnterDialogueMode();
            ActiveDialoguePanel = true;
            // Starts the story
            ContinueStory();
        }

        void ExitDialogMode()
        {
            StoryFunctions.UnbindFunctions(CurrentStory);
            GlobalVars.StopListening(CurrentStory);
            _DialoguePanel.ExitDialogueMode();
            ActiveDialoguePanel = false;
            
            OnDialogueEndEvent?.Invoke();
        }

        public void ChoiceSelected(OptionPrefab option)
        {
            CurrentStory.ChooseChoiceIndex(option.index);
            ContinueStory();
        }

        public void ChoiceSelected(int idx)
        {
            print("int" + idx);
            CurrentStory.ChooseChoiceIndex(idx);
            ContinueStory();
        }

    }
}