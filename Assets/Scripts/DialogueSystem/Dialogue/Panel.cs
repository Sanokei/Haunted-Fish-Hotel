using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.UI;

using Lean.Pool;
using TMPro;
using Ink.Runtime;
using System;

namespace Monologue.Dialogue
{
    public class Panel : MonoBehaviour
    {
        [SerializeField] TMP_Text m_DialogueText;
        [SerializeField] TMP_Text m_DialogueDisplayName;
        
        [SerializeField] Image m_ProfilePicture;
        [SerializeField] OptionPrefab _DialogueOptionPrefab;
        [SerializeField] GroupPanelPrefab _DialogueChoicePanel;

        List<OptionPrefab> _DialogueOptions = new();
        
        bool _isProfileIncluded = false;

        public delegate void OnChoiceSelected(OptionPrefab choiceIndex);
        public static event OnChoiceSelected OnChoiceSelectedEvent;
        
        void Start()
        {
            ProfileIncluded = false;
        }
        public void EnterDialogueMode()
        {
            
            ProfileIncluded = _isProfileIncluded;
            OptionPrefab.OnChoiceSelectedEvent += OnMakeChoice;
        }

        public void ExitDialogueMode()
        {
            ProfileIncluded = false;
            OptionPrefab.OnChoiceSelectedEvent -= OnMakeChoice;
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
                ProfileImage.gameObject.SetActive(value);
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
                var t = _DialogueOptions.Select(ctx => ctx.OptionText).ToList();
                return t;
            }
            set
            {
                _DialogueChoicePanel.gameObject.SetActive(false);
                if(_DialogueOptions.Count > 0)
                    foreach(var v in _DialogueOptions)
                    {
                        Destroy(v.gameObject);
                    }
                _DialogueOptions = new();
                if(value.Count > 0)
                    _DialogueChoicePanel.gameObject.SetActive(true);

                int index = 0;
                foreach (string optionText in value)
                {
                    OptionPrefab option = _DialogueChoicePanel.Create(_DialogueOptionPrefab);
                    option.OptionText = optionText;
                    option.index = index;

                    _DialogueOptions.Add(option);

                    index++;
                }
            }
        }

        public void OnMakeChoice(OptionPrefab option)
        {
            OnChoiceSelectedEvent?.Invoke(option);
        }
    }
}