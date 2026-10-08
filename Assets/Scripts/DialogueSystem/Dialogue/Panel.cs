using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using TMPro;

namespace Monologue.Dialogue
{
    public class Panel : MonoBehaviour
    {
        [SerializeField] TMP_Text m_DialogueText;
        [SerializeField] TMP_Text m_DialogueDisplayName;
        
        [SerializeField] Image m_ProfilePicture;
        [SerializeField] OptionPrefab _DialogueOptionPrefab;
        [SerializeField] GroupPanelPrefab _DialogueChoicePanel;

        readonly List<OptionPrefab> _DialogueOptions = new();
        
        bool _isProfileIncluded = false;
        bool dialogueOpen;

        public delegate void OnChoiceSelected(OptionPrefab choiceIndex);
        public static event OnChoiceSelected OnChoiceSelectedEvent;
        
        void Start()
        {
            ProfileIncluded = false;
        }
        public void EnterDialogueMode()
        {
            dialogueOpen = true;
            ProfileIncluded = _isProfileIncluded;
        }

        public void ExitDialogueMode()
        {
            dialogueOpen = false;
            ProfileIncluded = false;
        }

        void OnDestroy()
        {
            foreach (var option in _DialogueOptions)
                if (option) option.ChoiceSelected -= OnMakeChoice;
        }

        public string this[int idx]
        {
            get
            {
                return _DialogueOptions[idx].OptionText;
            }
            set
            {
                _DialogueOptions[idx].OptionText = value;
            }
        }
        public string DialogueText
        {
            get
            {
                return m_DialogueText.text;
            }
            set
            {
                m_DialogueText.text = value;
            }
        }

        public string DialogueDisplayName
        {
            get
            {
                return m_DialogueDisplayName.text;
            }
            set
            {
                m_DialogueDisplayName.text = value;
            }
        }
        public bool ProfileIncluded
        {
            get
            {
                return _isProfileIncluded;
            }
            set
            {
                _isProfileIncluded = value;
                if (ProfileImage) ProfileImage.gameObject.SetActive(value);
            }
        }
        public Image ProfileImage
        {
            get
            {
                return m_ProfilePicture;
            }
            set
            {
                m_ProfilePicture = value;
            }
        }

        public List<string> DialogueOptions
        {
            get
            {
                var choices = new List<string>(_DialogueOptions.Count);
                foreach (var option in _DialogueOptions) choices.Add(option.OptionText);
                return choices;
            }
            set
            {
                _DialogueChoicePanel.gameObject.SetActive(false);
                ClearOptions();
                if(value.Count > 0)
                    _DialogueChoicePanel.gameObject.SetActive(true);

                int index = 0;
                foreach (string optionText in value)
                {
                    OptionPrefab option = _DialogueChoicePanel.Create(_DialogueOptionPrefab);
                    option.OptionText = optionText;
                    option.index = index;
                    option.ChoiceSelected += OnMakeChoice;

                    _DialogueOptions.Add(option);

                    index++;
                }
                Canvas.ForceUpdateCanvases();
                if (_DialogueChoicePanel.VerticalGroup.transform is RectTransform rect) LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }
        }


        public void ConfigureWorldLayout() { /* Layout is authored in serialized prefabs. */ }

        public void OnMakeChoice(OptionPrefab option)
        {
            if (!OwnsChoice(option)) return;
            OnChoiceSelectedEvent?.Invoke(option);
        }

        public bool OwnsChoice(OptionPrefab option)
        {
            return dialogueOpen && gameObject.activeInHierarchy && option &&
                option.gameObject.activeInHierarchy && _DialogueOptions.Contains(option);
        }

        void ClearOptions()
        {
            foreach (var option in _DialogueOptions)
            {
                if (!option) continue;
                option.ChoiceSelected -= OnMakeChoice;
                _DialogueChoicePanel.Children.Remove(option.gameObject);
                // Destroy is deferred until the end of the frame; stale choices must
                // stop receiving pointer events as soon as new choices are displayed.
                option.gameObject.SetActive(false);
                Destroy(option.gameObject);
            }
            _DialogueOptions.Clear();
        }
    }
}
