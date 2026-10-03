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
                if (ProfileImage) ProfileImage.gameObject.SetActive(value);
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
        


        static void Fill(RectTransform rect, Vector2 min, Vector2 max, Vector2 inset)
        {
            rect.anchorMin=min; rect.anchorMax=max; rect.pivot=new Vector2(.5f,.5f);
            rect.offsetMin=inset; rect.offsetMax=-inset; rect.localScale=Vector3.one;
        }
        public void ConfigureWorldLayout()
        {
            var root=(RectTransform)transform;
            if (_QuestionText)
            {
                _QuestionText.transform.SetParent(root,false);
                Fill(_QuestionText.rectTransform,new Vector2(.08f,.7f),new Vector2(.92f,.95f),Vector2.zero);
                _QuestionText.fontSize=36; _QuestionText.enableAutoSizing=false;
                _QuestionText.color=Color.white; _QuestionText.alignment=TextAlignmentOptions.TopLeft; _QuestionText.raycastTarget=false;
            }
            foreach (var field in new[]{_InputTextField,_InputTextFieldWithProfile})
            {
                if (!field) continue;
                field.transform.SetParent(root,false);
                Fill((RectTransform)field.transform,new Vector2(.08f,.18f),new Vector2(.92f,.65f),Vector2.zero);
                var background=field.GetComponent<Image>(); if (!background) background=field.gameObject.AddComponent<Image>();
                background.color=new Color(.12f,.17f,.23f,1); background.raycastTarget=true;
                var colors=field.colors; colors.normalColor=Color.white; colors.highlightedColor=Color.white; colors.selectedColor=Color.white; field.colors=colors;
                if (field.textViewport) Fill(field.textViewport,Vector2.zero,Vector2.one,new Vector2(18,10));
                if (field.textComponent)
                {
                    Fill(field.textComponent.rectTransform,Vector2.zero,Vector2.one,Vector2.zero);
                    field.textComponent.fontSize=36; field.textComponent.color=Color.white; field.textComponent.raycastTarget=false;
                }
                if (field.placeholder is TMP_Text placeholder)
                {
                    Fill(placeholder.rectTransform,Vector2.zero,Vector2.one,Vector2.zero);
                    placeholder.fontSize=34; placeholder.color=new Color(.65f,.73f,.8f); placeholder.raycastTarget=false;
                }
                field.customCaretColor=true; field.caretColor=Color.white; field.selectionColor=new Color(.3f,.55f,.8f,.5f);
            }
            if (_ProfileImage)
            {
                _ProfileImage.transform.SetParent(root,false);
                Fill(_ProfileImage.rectTransform,new Vector2(.8f,.72f),new Vector2(.94f,.93f),Vector2.zero);
            }
            foreach (Transform child in root)
                if (child!=_QuestionText?.transform && child!=_InputTextField?.transform && child!=_InputTextFieldWithProfile?.transform && child!=_ProfileImage?.transform)
                    child.gameObject.SetActive(false);
            var image=GetComponent<Image>(); if (!image) image=gameObject.AddComponent<Image>();
            image.color=new Color(.035f,.045f,.065f,.96f); image.raycastTarget=false;
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