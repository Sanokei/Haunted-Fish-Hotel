using System;
using System.Collections;
using System.Collections.Generic;
using HauntedFish.Multiplayer;
using UnityEngine;

namespace Monologue.Dialogue
{
    // UI is authored in prefabs/scenes. Ink addresses these inspector bindings, never builds visuals.
    public sealed class StoryUI : MonoBehaviour
    {
        [Serializable] public sealed class Variant { public string Tag; public GameObject Target; public AudioClip Sound; }
        [Serializable] public sealed class Element
        {
            public string Tag;
            public RectTransform Target;
            public Variant[] Variants = Array.Empty<Variant>();
            public GameObject PulseLabel;
            public AudioClip PulseSound;
            public RectTransform ContactShadow;
            public Vector2 ShadowOffset;
        }
        [Serializable] public sealed class Pose
        {
            public RectTransform Target;
            public Vector2 Position, Size;
            public Vector3 Scale = Vector3.one;
            public float Slant = 120;
        }
        [Serializable] public sealed class Layout { public string Tag; public Pose[] Poses = Array.Empty<Pose>(); }
        [Serializable] public sealed class Template { public string Tag; public RectTransform Prefab; }
        [Serializable] public sealed class Visibility { public GameObject Target; public bool Visible; }
        [SerializeField] GameObject _Content;
        [SerializeField] Element[] _Elements = Array.Empty<Element>();
        [SerializeField] Layout[] _Layouts = Array.Empty<Layout>();
        [SerializeField] Template[] _Prefabs = Array.Empty<Template>();
        [SerializeField] GameObject _WhiteFrame;
        [SerializeField] Visibility[] _FlashVisibility = Array.Empty<Visibility>();
        [SerializeField] AudioSource _Audio;
        [SerializeField] IntroductionSkipCircle _SkipProgress;
        [Tooltip("Subscribe this UI to ordinary StoryFunctions events. Local sequences call Play directly.")]
        [SerializeField] bool _ListenToStoryEvents = true;
        readonly Dictionary<string, Element> _Objects = new Dictionary<string, Element>();
        readonly List<GameObject> _Spawned = new List<GameObject>();
        readonly Queue<StorySequenceStep> _Pending = new Queue<StorySequenceStep>();
        Coroutine _EventRoutine;

        public RectTransform Find(string tag) { Register(); return _Objects.TryGetValue(tag, out var item) ? item.Target : null; }
        void Register()
        {
            foreach (var element in _Elements)
                if (element.Target && !string.IsNullOrWhiteSpace(element.Tag)) _Objects[element.Tag] = element;
        }
        Layout FindLayout(string tag) => Array.Find(_Layouts, layout => layout.Tag == tag);
        public void ValidateSequence(IReadOnlyList<StorySequenceStep> steps)
        {
            Register();
            var created = new HashSet<string>();
            foreach (var step in steps)
            {
                bool supported = Supports(step);
                if (step.Kind == StorySequenceStepKind.CreateUI)
                {
                    supported = Array.Exists(_Prefabs, p => p.Tag == step.Variant && p.Prefab) &&
                        (_Objects.ContainsKey(step.Parent) || created.Contains(step.Parent)) &&
                        !_Objects.ContainsKey(step.Target) && created.Add(step.Target);
                }
                else if (created.Contains(step.Target) && (step.Kind == StorySequenceStepKind.SetVisible ||
                    step.Kind == StorySequenceStepKind.MoveUI || step.Kind == StorySequenceStepKind.Knock)) supported = true;
                if (!supported) throw new InvalidOperationException("Unbound story UI operation: " + step.Kind + "(" + step.Target + ")");
            }
        }
        public bool Supports(StorySequenceStep step)
        {
            Register();
            switch (step.Kind)
            {
                case StorySequenceStepKind.Wait: return true;
                case StorySequenceStepKind.FlashFrames: return _WhiteFrame;
                case StorySequenceStepKind.PushTo:
                case StorySequenceStepKind.ExpandPanel:
                case StorySequenceStepKind.DollyTo: return FindLayout(step.Target) != null;
                case StorySequenceStepKind.CreateUI:
                    return Array.Exists(_Prefabs, p => p.Tag == step.Variant && p.Prefab) && _Objects.ContainsKey(step.Parent);
                case StorySequenceStepKind.SetVariant:
                    return _Objects.TryGetValue(step.Target, out var item) && Array.Exists(item.Variants, v => v.Tag == step.Variant && v.Target);
                default: return _Objects.ContainsKey(step.Target);
            }
        }
        public void Prepare() { Register(); _Content.SetActive(true); }
        public void SetSkipProgress(float amount) { if (_SkipProgress) _SkipProgress.Progress = amount; }
        public void Close()
        {
            if (_EventRoutine != null) StopCoroutine(_EventRoutine);
            _EventRoutine = null;
            _Pending.Clear();
            if (_Audio) _Audio.Stop();
            foreach (var instance in _Spawned) if (instance) Destroy(instance);
            _Spawned.Clear();
            _Objects.Clear();
            if (_WhiteFrame) _WhiteFrame.SetActive(false);
            if (_Content) _Content.SetActive(false);
        }
        public IEnumerator Play(StorySequenceStep step)
        {
            Register();
            if (!Supports(step)) throw new InvalidOperationException("Unbound story UI operation: " + step.Kind + "(" + step.Target + ")");
            switch (step.Kind)
            {
                case StorySequenceStepKind.CreateUI:
                    var template = Array.Find(_Prefabs, p => p.Tag == step.Variant);
                    if (_Objects.ContainsKey(step.Target)) throw new InvalidOperationException("UI tag already exists: " + step.Target);
                    var instance = Instantiate(template.Prefab, _Objects[step.Parent].Target, false);
                    _Spawned.Add(instance.gameObject);
                    _Objects.Add(step.Target, new Element { Tag = step.Target, Target = instance });
                    break;
                case StorySequenceStepKind.SetVisible: _Objects[step.Target].Target.gameObject.SetActive(step.Visible); break;
                case StorySequenceStepKind.SetVariant:
                    foreach (var variant in _Objects[step.Target].Variants)
                    {
                        bool selected = variant.Tag == step.Variant;
                        variant.Target.SetActive(selected);
                        if (selected && variant.Sound && _Audio) _Audio.PlayOneShot(variant.Sound);
                    }
                    break;
                case StorySequenceStepKind.MoveUI:
                    var element = _Objects[step.Target];
                    var start = element.Target.anchoredPosition;
                    yield return Tween(step.Seconds, t =>
                    {
                        element.Target.anchoredPosition = Vector2.Lerp(start, new Vector2(step.X, step.Y), t);
                        if (element.ContactShadow) element.ContactShadow.anchoredPosition = element.Target.anchoredPosition + element.ShadowOffset;
                    });
                    break;
                case StorySequenceStepKind.PushTo:
                case StorySequenceStepKind.ExpandPanel:
                case StorySequenceStepKind.DollyTo:
                    var poses = FindLayout(step.Target).Poses;
                    var positions = new Vector2[poses.Length];
                    var sizes = new Vector2[poses.Length];
                    var scales = new Vector3[poses.Length];
                    var slants = new float[poses.Length];
                    for (int i = 0; i < poses.Length; i++)
                    {
                        positions[i] = poses[i].Target.anchoredPosition; sizes[i] = poses[i].Target.sizeDelta;
                        scales[i] = poses[i].Target.localScale;
                        var shape = poses[i].Target.GetComponent<IntroductionPanelShape>();
                        slants[i] = shape ? shape.Slant : 0;
                    }
                    yield return Tween(step.Seconds, t =>
                    {
                        if (step.Kind == StorySequenceStepKind.DollyTo) t *= step.X;
                        for (int i = 0; i < poses.Length; i++)
                        {
                            var pose = poses[i];
                            pose.Target.anchoredPosition = Vector2.Lerp(positions[i], pose.Position, t);
                            pose.Target.sizeDelta = Vector2.Lerp(sizes[i], pose.Size, t);
                            pose.Target.localScale = Vector3.Lerp(scales[i], pose.Scale, t);
                            var shape = pose.Target.GetComponent<IntroductionPanelShape>();
                            if (shape) shape.Slant = Mathf.Lerp(slants[i], pose.Slant, t);
                        }
                    });
                    break;
                case StorySequenceStepKind.Spin:
                    var spun = _Objects[step.Target].Target;
                    var rotation = spun.localRotation;
                    yield return Tween(step.Seconds, t => spun.localRotation = rotation * Quaternion.Euler(0, 0, step.X * 360 * t));
                    spun.localRotation = rotation;
                    break;
                case StorySequenceStepKind.Knock:
                    var knocked = _Objects[step.Target];
                    var rest = knocked.Target.anchoredPosition;
                    for (int i = 0; i < step.Count; i++)
                    {
                        if (knocked.PulseLabel) knocked.PulseLabel.SetActive(true);
                        if (knocked.PulseSound && _Audio) _Audio.PlayOneShot(knocked.PulseSound);
                        float pulse = Mathf.Min(.2f, step.Seconds);
                        yield return Tween(pulse, t => knocked.Target.anchoredPosition = rest + new Vector2(Mathf.Sin(t * Mathf.PI * 4) * (1 - t) * 9, 0));
                        knocked.Target.anchoredPosition = rest;
                        if (knocked.PulseLabel) knocked.PulseLabel.SetActive(false);
                        if (step.Seconds > pulse) yield return new WaitForSecondsRealtime(step.Seconds - pulse);
                    }
                    break;
                case StorySequenceStepKind.FlashFrames:
                    _WhiteFrame.SetActive(true);
                    foreach (var change in _FlashVisibility) if (change.Target) change.Target.SetActive(change.Visible);
                    for (int i = 0; i < step.Count; i++) yield return null;
                    _WhiteFrame.SetActive(false);
                    break;
                case StorySequenceStepKind.Wait: yield return new WaitForSecondsRealtime(step.Seconds); break;
            }
        }
        static IEnumerator Tween(float seconds, Action<float> apply)
        {
            if (seconds <= 0) { apply(1); yield break; }
            for (float elapsed = 0; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / seconds);
                apply(t * t * (3 - 2 * t));
                yield return null;
            }
            apply(1);
        }
        void Queue(StorySequenceStep step)
        {
            Prepare(); _Pending.Enqueue(step);
            if (_EventRoutine == null) _EventRoutine = StartCoroutine(Drain());
        }
        IEnumerator Drain()
        {
            yield return null;
            try { while (_Pending.Count > 0) yield return Play(_Pending.Dequeue()); }
            finally { _EventRoutine = null; }
        }
        void Visible(string tag, bool value) => Queue(new StorySequenceStep(StorySequenceStepKind.SetVisible, tag, visible: value));
        void VariantChanged(string tag, string variant) => Queue(new StorySequenceStep(StorySequenceStepKind.SetVariant, tag, variant));
        void Push(string tag, float seconds) => Queue(new StorySequenceStep(StorySequenceStepKind.PushTo, tag, seconds: seconds));
        void Move(string tag, float x, float y, float seconds) => Queue(new StorySequenceStep(StorySequenceStepKind.MoveUI, tag, seconds: seconds, x: x, y: y));
        void Knock(string tag, int count, float seconds) => Queue(new StorySequenceStep(StorySequenceStepKind.Knock, tag, seconds: seconds, count: count));
        void Spin(string tag, float turns, float seconds) => Queue(new StorySequenceStep(StorySequenceStepKind.Spin, tag, seconds: seconds, x: turns));
        void Wait(float seconds) => Queue(new StorySequenceStep(StorySequenceStepKind.Wait, seconds: seconds));
        void Expand(string tag, float seconds) => Queue(new StorySequenceStep(StorySequenceStepKind.ExpandPanel, tag, seconds: seconds));
        void Flash(int count) => Queue(new StorySequenceStep(StorySequenceStepKind.FlashFrames, count: count));
        void Dolly(string tag, float amount, float seconds) => Queue(new StorySequenceStep(StorySequenceStepKind.DollyTo, tag, seconds: seconds, x: amount));
        void Create(string prefab, string instance, string parent) => Queue(new StorySequenceStep(StorySequenceStepKind.CreateUI, instance, prefab, parent: parent));
        void OnEnable()
        {
            if (!_ListenToStoryEvents) return;
            StoryFunctions.OnSetVisibleEvent += Visible; StoryFunctions.OnSetVariantEvent += VariantChanged;
            StoryFunctions.OnPushToEvent += Push; StoryFunctions.OnMoveUIEvent += Move;
            StoryFunctions.OnSpinEvent += Spin;
            StoryFunctions.OnKnockEvent += Knock; StoryFunctions.OnWaitEvent += Wait;
            StoryFunctions.OnExpandPanelEvent += Expand; StoryFunctions.OnFlashFramesEvent += Flash;
            StoryFunctions.OnDollyToEvent += Dolly; StoryFunctions.OnCreateUIEvent += Create;
        }
        void OnDisable()
        {
            StoryFunctions.OnSetVisibleEvent -= Visible; StoryFunctions.OnSetVariantEvent -= VariantChanged;
            StoryFunctions.OnPushToEvent -= Push; StoryFunctions.OnMoveUIEvent -= Move;
            StoryFunctions.OnSpinEvent -= Spin;
            StoryFunctions.OnKnockEvent -= Knock; StoryFunctions.OnWaitEvent -= Wait;
            StoryFunctions.OnExpandPanelEvent -= Expand; StoryFunctions.OnFlashFramesEvent -= Flash;
            StoryFunctions.OnDollyToEvent -= Dolly; StoryFunctions.OnCreateUIEvent -= Create;
            Close();
        }
    }
}
