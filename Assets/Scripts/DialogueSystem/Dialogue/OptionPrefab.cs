using System.Collections;
using System.Collections.Generic;
using TMPro;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Monologue.Dialogue
{
    public class OptionPrefab : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] Image _ForegroundImage;
        UnityEngine.Color m_MouseOverColor = new Color32(255,255,255,100);
        UnityEngine.Color m_OriginalColor = new Color32(0,0,0,0);
        [HideInInspector] public int index;
        public TMP_Text OptionTextGO;
        public static event Panel.OnChoiceSelected OnChoiceSelectedEvent;
        public string OptionText
        {
            get
            {
                return OptionTextGO.text;
            }
            set
            {
                OptionTextGO.text = value;
            }
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            OnChoiceSelectedEvent?.Invoke(this);
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            _ForegroundImage.color = m_MouseOverColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _ForegroundImage.color = m_OriginalColor;
        }
    }
}