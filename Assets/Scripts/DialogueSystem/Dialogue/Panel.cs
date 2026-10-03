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
        bool worldLayout;

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
                    if (worldLayout) ConfigureWorldOption(option);
                    option.OptionText = optionText;
                    option.index = index;

                    _DialogueOptions.Add(option);

                    index++;
                }
                Canvas.ForceUpdateCanvases();
                if (_DialogueChoicePanel.VerticalGroup.transform is RectTransform rect) LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }
        }


        static void Fill(RectTransform rect, Vector2 min, Vector2 max, Vector2 inset)
        {
            rect.anchorMin=min; rect.anchorMax=max; rect.pivot=new Vector2(.5f,.5f);
            rect.offsetMin=inset; rect.offsetMax=-inset; rect.localScale=Vector3.one;
        }
        static Image Background(GameObject target, Color color)
        {
            var image=target.GetComponent<Image>();
            if (!image) image=target.AddComponent<Image>();
            image.color=color; image.raycastTarget=false; return image;
        }
        public void ConfigureWorldLayout()
        {
            worldLayout=true;
            var root=(RectTransform)transform;
            Background(gameObject,new Color(.035f,.045f,.065f,.96f));
            foreach (var name in new[]{"BackgroundImage","LowerHalfBg","Profile Panel"})
            { var legacy=transform.Find(name); if (legacy) legacy.gameObject.SetActive(false); }
            if (m_DialogueText)
            {
                var box=(RectTransform)m_DialogueText.transform.parent;
                box.SetParent(root,false); Fill(box,new Vector2(0,.55f),new Vector2(1,.9f),new Vector2(18,0));
                Background(box.gameObject,new Color(0,0,0,0));
                Fill(m_DialogueText.rectTransform,Vector2.zero,Vector2.one,new Vector2(18,10));
                m_DialogueText.fontSize=36; m_DialogueText.enableAutoSizing=false;
                m_DialogueText.color=Color.white; m_DialogueText.alignment=TextAlignmentOptions.TopLeft; m_DialogueText.raycastTarget=false;
            }
            if (m_DialogueDisplayName)
            {
                var box=(RectTransform)m_DialogueDisplayName.transform.parent;
                box.SetParent(root,false); Fill(box,new Vector2(0,.9f),Vector2.one,new Vector2(18,0));
                Background(box.gameObject,new Color(0,0,0,0));
                Fill(m_DialogueDisplayName.rectTransform,Vector2.zero,Vector2.one,new Vector2(18,4));
                m_DialogueDisplayName.fontSize=28; m_DialogueDisplayName.color=new Color(1,.83f,.5f);
                m_DialogueDisplayName.alignment=TextAlignmentOptions.MidlineLeft; m_DialogueDisplayName.raycastTarget=false;
            }
            if (_DialogueChoicePanel)
            {
                var box=(RectTransform)_DialogueChoicePanel.transform;
                box.SetParent(root,false); Fill(box,new Vector2(0,.03f),new Vector2(1,.53f),new Vector2(18,0));
                Background(box.gameObject,new Color(.05f,.065f,.09f,1));
                var scroll=box.GetComponentInChildren<ScrollRect>(true);
                var group=_DialogueChoicePanel.VerticalGroup;
                if (scroll)
                {
                    Fill((RectTransform)scroll.transform,Vector2.zero,Vector2.one,Vector2.zero);
                    if (scroll.viewport) Fill(scroll.viewport,Vector2.zero,Vector2.one,Vector2.zero);
                    scroll.horizontal=false; scroll.vertical=true; scroll.movementType=ScrollRect.MovementType.Clamped;
                    if (scroll.verticalScrollbar) scroll.verticalScrollbar.gameObject.SetActive(false);
                    if (scroll.horizontalScrollbar) scroll.horizontalScrollbar.gameObject.SetActive(false);
                    scroll.verticalScrollbar=null; scroll.horizontalScrollbar=null;
                }
                if (group)
                {
                    var content=(RectTransform)group.transform;
                    content.anchorMin=new Vector2(0,1); content.anchorMax=Vector2.one;
                    content.pivot=new Vector2(.5f,1); content.anchoredPosition=Vector2.zero; content.sizeDelta=Vector2.zero;
                    var vertical=group as VerticalLayoutGroup;
                    if (vertical) { vertical.padding=new RectOffset(12,12,12,12); vertical.spacing=12; vertical.childAlignment=TextAnchor.UpperLeft;
                        vertical.childControlWidth=true; vertical.childControlHeight=true; vertical.childForceExpandWidth=true; vertical.childForceExpandHeight=false; }
                    var fitter=content.GetComponent<ContentSizeFitter>();
                    if (!fitter) fitter=content.gameObject.AddComponent<ContentSizeFitter>();
                    fitter.horizontalFit=ContentSizeFitter.FitMode.Unconstrained; fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
                    if (scroll) scroll.content=content;
                }
            }
            foreach (var option in _DialogueOptions) if (option) ConfigureWorldOption(option);
        }
        static void ConfigureWorldOption(OptionPrefab option)
        {
            var rect=(RectTransform)option.transform; rect.localScale=Vector3.one;
            var layout=option.GetComponent<LayoutElement>(); if (!layout) layout=option.gameObject.AddComponent<LayoutElement>();
            layout.minHeight=56; layout.preferredHeight=56; layout.flexibleHeight=0;
            var image=Background(option.gameObject,new Color(.12f,.17f,.23f,1)); image.raycastTarget=true;
            if (option.OptionTextGO)
            {
                option.OptionTextGO.transform.SetParent(rect,false);
                Fill(option.OptionTextGO.rectTransform,Vector2.zero,Vector2.one,new Vector2(16,8));
                option.OptionTextGO.fontSize=32; option.OptionTextGO.enableAutoSizing=false; option.OptionTextGO.color=Color.white;
                option.OptionTextGO.alignment=TextAlignmentOptions.MidlineLeft; option.OptionTextGO.raycastTarget=false;
            }
        }

        public void OnMakeChoice(OptionPrefab option)
        {
            OnChoiceSelectedEvent?.Invoke(option);
        }
    }
}