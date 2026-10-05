using System.Linq;
using UnityEngine;
using TMPro;
using Monologue.Dialogue;
using HauntedFish.Multiplayer;

namespace Monologue.StoryInput
{
    public class StoryInputTextFieldManager : MonoBehaviour
    {
        public static StoryInputTextFieldManager Instance {get; private set;}
        [SerializeField] TextFieldPanel _InputPanel;
        public void Configure(TextFieldPanel panel) { _InputPanel=panel; }
        public TextFieldPanel InputPanel => _InputPanel;
        public string CurrentKey => _InputPanel ? _InputPanel.Key : "";
        public string CurrentQuestion => _InputPanel ? _InputPanel.QuestionText : "";
        public delegate void OnStoryInput();
        public static event OnStoryInput OnStoryInputStartEvent;
        public static event OnStoryInput OnStoryInputEndEvent;
        
        public bool ActiveInputPanel
        {
            get
            {
                return _InputPanel && _InputPanel.gameObject.activeInHierarchy;
            }
            set
            {
                _InputPanel.gameObject.SetActive(value);
            }
        }
        void Awake()
        {
            if (!Instance)
                Instance = this;
            else
                Destroy(gameObject);
            if (_InputPanel) ActiveInputPanel=false;
        }
        void OnEnable()
        {
            DontDestroyHelper.NotDestroyedHelperEvent += DeactivatePanel;
        }
        void OnDisable()
        {
            DontDestroyHelper.NotDestroyedHelperEvent -= DeactivatePanel;
        }
        void DeactivatePanel()
        {
            if (ChangeSceneOnLoadDontDestroy.Instance) ChangeSceneOnLoadDontDestroy.Instance.NextScene();
            ActiveInputPanel = false;
        }
        void OnSubmit(string key, string value)
        {
            if (SharedDialogue.Instance && SharedDialogue.Instance.Route(4, -1, key, value)) return;
            var manager=DialogueManager.Instance;
            manager.GlobalVars[key] = value;
            // Keep subsequent Ink branches in step with the existing globals/format tags.
            if (manager.CurrentStory!=null && manager.CurrentStory.variablesState.Contains(key))
                manager.CurrentStory.variablesState[key]=value;
            ExitInputMode();
        }
        public void SubmitShared(string key, string value) { OnSubmit(key, value); }
        public void ApplySharedInput(bool active, string question, string key)
        {
            if (active && !ActiveInputPanel) EnterInputMode(question, key);
            else if (!active && ActiveInputPanel) ExitInputMode();
        }
        public void EnterInputMode(string questionText, string key, string profileImage = "", string placeholderText = "Enter text...")
        {
            OnStoryInputStartEvent?.Invoke();

            _InputPanel.QuestionText = questionText;
            _InputPanel.ProfileIncluded = profileImage != "";
            if (_InputPanel.ProfileImage) _InputPanel.ProfileImage.sprite = Resources.Load<Sprite>($"Characters/{profileImage}");
            _InputPanel.Key = key;

            TextFieldPanel.OnSubmitInputTextFieldEvent += OnSubmit;
            // _InputPanel.placeholder = placeholder;
            _InputPanel.EnterInputMode();
            ActiveInputPanel = true;
        }
        public void ExitInputMode()
        {

            TextFieldPanel.OnSubmitInputTextFieldEvent -= OnSubmit;
            _InputPanel.ExitInputMode();
            ActiveInputPanel = false;

            OnStoryInputEndEvent?.Invoke();
        }
    }
}
