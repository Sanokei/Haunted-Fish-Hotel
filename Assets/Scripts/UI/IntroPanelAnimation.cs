using System;
using System.Collections.Generic;
using Lean.Gui;
using Lean.Transition;
using Lean.Transition.Method;
using UnityEngine;
using UnityEngine.Events;
namespace HauntedFish.UI
{
    // Cloud visual inspection informs the bindings; targets/timing remain authored in Inspector.
    public sealed class IntroPanelAnimation : MonoBehaviour
    {
        [Serializable] public sealed class Plate
        {
            public RectTransform Target;
            public Vector2 DollyPosition;
            public Vector3 DollyScale=Vector3.one;
        }
        [Serializable] public sealed class PanelPush
        {
            public RectTransform Target;
            [Tooltip("Author a leftward destination to match the storyboard push arrow.")]
            public Vector2 Destination;
            [Min(0)] public float Duration;
        }
        public CanvasGroup Lightning;
        public PanelPush KnockPanel=new PanelPush();
        public PanelPush DeliveredItemPanel=new PanelPush();
        public PanelPush LetterPanel=new PanelPush();
        public RectTransform SlidingItem;
        [Tooltip("Author a downward destination for the item delivered at the door.")]
        public Vector2 ItemSlidePosition;
        [Min(0)] public float ItemSlideDuration;
        // Optional generic push binding retained for existing Inspector hookups.
        public RectTransform PushPanel;
        public Vector2 PushPosition;
        public Plate ForegroundGate=new Plate();
        public Plate HotelBackground=new Plate();
        public GameObject ArrivalRoot;
        public LeanWindow ArrivalWindow;
        [Min(0)] public float LightningDuration,PushDuration,DollyDuration;
        public LeanEase Ease=LeanEase.Smooth;
        public UnityEvent OnLightning=new UnityEvent(),OnKnock=new UnityEvent(),OnDeliveredItem=new UnityEvent(),
            OnLetter=new UnityEvent(),OnSlide=new UnityEvent(),OnPush=new UnityEvent(),OnDolly=new UnityEvent(),OnArrival=new UnityEvent();
        readonly List<LeanState> running=new List<LeanState>();
        readonly Dictionary<RectTransform,(Vector2 position,Vector3 scale)> originals=
            new Dictionary<RectTransform,(Vector2,Vector3)>();
        float initialAlpha;
        bool captured,arrivalInitiallyActive,windowInitiallyOn;
        void OnEnable() { LeanTransition.OnFinished+=Finished; }
        void Finished(LeanState state) { running.Remove(state); }
        void Capture()
        {
            if (!captured)
            {
                captured=true;
                initialAlpha=Lightning ? Lightning.alpha : 0;
                arrivalInitiallyActive=ArrivalRoot && ArrivalRoot.activeSelf;
                windowInitiallyOn=ArrivalWindow && ArrivalWindow.On;
            }
            Capture(PushPanel); Capture(SlidingItem);
            Capture(KnockPanel.Target); Capture(DeliveredItemPanel.Target); Capture(LetterPanel.Target);
            Capture(ForegroundGate.Target); Capture(HotelBackground.Target);
        }
        void Capture(RectTransform target)
        { if (target && !originals.ContainsKey(target)) originals[target]=(target.anchoredPosition,target.localScale); }
        public void RevealLightning()
        {
            Capture();
            if (Lightning) running.Add(LeanCanvasGroupAlpha.Register(Lightning,1,LightningDuration,Ease));
            OnLightning?.Invoke();
        }
        public void HideLightning()
        {
            Capture();
            if (Lightning) running.Add(LeanCanvasGroupAlpha.Register(Lightning,0,LightningDuration,Ease));
        }
        public void PushKnock() { Push(KnockPanel); OnKnock?.Invoke(); }
        public void PushDeliveredItem() { Push(DeliveredItemPanel); OnDeliveredItem?.Invoke(); }
        public void PushLetter() { Push(LetterPanel); OnLetter?.Invoke(); }
        void Push(PanelPush panel)
        {
            Capture();
            if (panel.Target) running.Add(LeanRectTransformAnchoredPosition.Register(panel.Target,panel.Destination,panel.Duration,Ease));
            OnPush?.Invoke();
        }
        public void SlideItem()
        {
            Capture();
            if (SlidingItem) running.Add(LeanRectTransformAnchoredPosition.Register(SlidingItem,ItemSlidePosition,ItemSlideDuration,Ease));
            OnSlide?.Invoke();
        }
        public void Push()
        {
            Capture();
            if (PushPanel) running.Add(LeanRectTransformAnchoredPosition.Register(PushPanel,PushPosition,PushDuration,Ease));
            OnPush?.Invoke();
        }
        public void Dolly()
        {
            Capture();
            DollyPlate(ForegroundGate); DollyPlate(HotelBackground);
            OnDolly?.Invoke();
        }
        void DollyPlate(Plate plate)
        {
            if (!plate.Target) return;
            running.Add(LeanRectTransformAnchoredPosition.Register(plate.Target,plate.DollyPosition,DollyDuration,Ease));
            running.Add(LeanTransformLocalScale.Register(plate.Target,plate.DollyScale,DollyDuration,Ease));
        }
        public void Arrive()
        {
            Capture();
            if (ArrivalRoot) ArrivalRoot.SetActive(true);
            if (ArrivalWindow) ArrivalWindow.On=true;
            OnArrival?.Invoke();
        }
        public void ResetPanels()
        {
            if (!captured) return;
            foreach (var state in running) state.Stop();
            running.Clear();
            foreach (var item in originals) if (item.Key)
            { item.Key.anchoredPosition=item.Value.position; item.Key.localScale=item.Value.scale; }
            if (Lightning) Lightning.alpha=initialAlpha;
            if (ArrivalRoot) ArrivalRoot.SetActive(arrivalInitiallyActive);
            if (ArrivalWindow) ArrivalWindow.On=windowInitiallyOn;
        }
        void OnDisable() { ResetPanels(); LeanTransition.OnFinished-=Finished; }
    }
}

