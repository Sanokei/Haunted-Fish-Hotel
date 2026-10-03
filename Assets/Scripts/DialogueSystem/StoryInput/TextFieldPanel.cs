using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using TMPro;

namespace Monologue.StoryInput
{
    public class TextFieldPanel : MonoBehaviour
    {
        [SerializeField] TMP_Text _QuestionText;
        [SerializeField] TMP_InputField _InputTextField;
        [SerializeField] TMP_InputField _InputTextFieldWithProfile;
        [SerializeField] Image _ProfileImage;
        TMP_InputField m_CurrInputTextField;
        string m_Key;
        public delegate void OnSubmitInputTextField(string key, string value);
        public static event OnSubmitInputTextField OnSubmitInputTextFieldEvent;
        bool _isProfileIncluded = false;
        public bool ProfileIncluded
        {
            get
            {
                return _isProfileIncluded;
            }
            set
            {
                _isProfileIncluded = value;
                m_CurrInputTextField = _isProfileIncluded ? _InputTextFieldWithProfile : _InputTextField;
                ProfileImage.gameObject.SetActive(value);
            }
        }
        public Image ProfileImage
        {
            get
            {
                return _ProfileImage;
            }

            set
            {
                _ProfileImage = value;
            }
        }
        public string QuestionText
        {
            get
            {
                return _QuestionText.text;
            }
            set
            {
                _QuestionText.text = value;
            }
        }
        public string InputFieldText
        {
            get
            {
                return m_CurrInputTextField.text;
            }
            set
            {
                m_CurrInputTextField.text = value;
            }
        }

        public string Key
        {
            get
            {
                return m_Key;
            }
            set
            {
                m_Key = value;
            }
        }
        

        // FIX ME: THIS WILL CAUSE RACE CONDITION
        public void EnterInputMode()
        {
            _InputTextFieldWithProfile.gameObject.SetActive(false);
            _InputTextField.gameObject.SetActive(false);
            ProfileIncluded = _isProfileIncluded;

            m_CurrInputTextField.gameObject.SetActive(true);
            m_CurrInputTextField.onSubmit.AddListener(OnSubmit);
        }
        public void ExitInputMode()
        {
            _InputTextFieldWithProfile.text = "";
            _InputTextField.text = "";
            m_CurrInputTextField.gameObject.SetActive(false);
            m_CurrInputTextField.onSubmit.RemoveListener(OnSubmit);
        }

        void OnSubmit(string eventData)
        {
            if(m_CurrInputTextField.wasCanceled)
                return;
            OnSubmitInputTextFieldEvent?.Invoke(m_Key,eventData);
        }
    }
}