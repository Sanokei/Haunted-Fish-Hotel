using UnityEngine;
using TMPro;
using Monologue.Dialogue;

namespace Monologue.StoryInput
{
    public class StoryInputTextFieldManager : MonoBehaviour
    {
        public static StoryInputTextFieldManager Instance {get; private set;}
        [SerializeField] TextFieldPanel _InputPanel; 
        public delegate void OnStoryInput();
        public static event OnStoryInput OnStoryInputStartEvent;
        public static event OnStoryInput OnStoryInputEndEvent;
        
        public bool ActiveInputPanel
        {
            get
            {
                return _InputPanel.gameObject.activeSelf;
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
            ChangeSceneOnLoadDontDestroy.Instance.NextScene();
            ActiveInputPanel = false;
        }
        void OnSubmit(string key, string value)
        {
            DialogueManager.Instance.GlobalVars[key] = value;
            ExitInputMode();
        }
        public void EnterInputMode(string questionText, string key, string profileImage = "", string placeholderText = "Enter text...")
        {
            OnStoryInputStartEvent?.Invoke();

            _InputPanel.QuestionText = questionText;
            _InputPanel.ProfileIncluded = profileImage != "";
            _InputPanel.ProfileImage.sprite = Resources.Load<Sprite>($"Characters/{profileImage}");
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