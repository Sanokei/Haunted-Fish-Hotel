using System;
using System.Collections;
using System.Collections.Generic;
using Monologue.Dialogue;
using UnityEngine;

namespace HauntedFish.BossFight
{
    // Authored visual proxies only: the real actors retain network identity and paddle ownership.
    public sealed class BossCharacterPresentation : MonoBehaviour
    {
        [SerializeField] StoryUI _FishUI, _GhostUI;
        [SerializeField] RectTransform _FishActor, _GhostActor;
        [SerializeField] TextAsset _Happy, _Frustrated, _Win, _Lose;
        [SerializeField, Min(.1f)] float _TimingScale = 1;
        readonly Dictionary<TextAsset, IReadOnlyList<StorySequenceStep>> _Sequences = new Dictionary<TextAsset, IReadOnlyList<StorySequenceStep>>();
        Vector2 _FishPosition, _GhostPosition;
        Quaternion _FishRotation, _GhostRotation;
        Vector3 _FishScale, _GhostScale;
        uint _Epoch, _Event;
        bool _Initialized;
        public uint FishId { get; private set; }
        public uint GhostId { get; private set; }
        public int EventsPlayed { get; private set; }
        public bool Playing { get; private set; }
        public RectTransform FishActor => _FishActor;
        public RectTransform GhostActor => _GhostActor;
        void Awake() => Initialize();
        void Initialize()
        {
            if (_Initialized) return; _Initialized = true;
            _FishPosition = _FishActor.anchoredPosition; _GhostPosition = _GhostActor.anchoredPosition;
            _FishRotation = _FishActor.localRotation; _GhostRotation = _GhostActor.localRotation;
            _FishScale = _FishActor.localScale; _GhostScale = _GhostActor.localScale;
            foreach (var asset in new[] { _Happy, _Frustrated, _Win, _Lose })
            {
                if (!asset) throw new InvalidOperationException("Boss reaction Ink asset is not assigned.");
                var steps = StoryFunctions.ReadAnimationSequence(asset.text);
                _FishUI.ValidateSequence(steps); _GhostUI.ValidateSequence(steps);
                _Sequences[asset] = steps;
            }
        }
        float Scale => float.IsFinite(_TimingScale) ? Mathf.Clamp(_TimingScale, .1f, 3) : 1;
        float Duration(TextAsset asset)
        {
            if (!_Sequences.TryGetValue(asset, out var steps)) steps = StoryFunctions.ReadAnimationSequence(asset.text);
            float total = 0; foreach (var step in steps) total += step.Seconds * Scale; return total;
        }
        public float ReactionDuration(bool final) { Initialize(); return Mathf.Max(Duration(final ? _Win : _Happy), Duration(final ? _Lose : _Frustrated)); }
        public void Present(BossArenaSnapshot state)
        {
            Initialize();
            FishId = state.FishId; GhostId = state.GhostId;
            if (_Epoch != state.Epoch) { ResetPose(); _Epoch = state.Epoch; _Event = 0; }
            if (state.Cancelled || state.ReactionRemaining <= 0) { if (Playing) ResetPose(); return; }
            if (state.ReactionEvent == 0 || state.ReactionEvent <= _Event) return;
            ResetPose(); _Event = state.ReactionEvent; EventsPlayed++; Playing = true;
            var scorer = state.ReactionFinal ? _Win : _Happy;
            var loser = state.ReactionFinal ? _Lose : _Frustrated;
            _FishUI.Prepare(); _GhostUI.Prepare();
            StartCoroutine(Play(_FishUI, state.ReactionFishScored ? scorer : loser, state.ReactionRemaining));
            StartCoroutine(Play(_GhostUI, state.ReactionFishScored ? loser : scorer, state.ReactionRemaining));
            StartCoroutine(Finish(state.ReactionRemaining));
        }
        IEnumerator Play(StoryUI ui, TextAsset asset, float remaining)
        {
            float elapsed = Mathf.Max(0, ReactionDuration(asset == _Win || asset == _Lose) - remaining);
            foreach (var step in _Sequences[asset])
            {
                float seconds = step.Seconds * Scale;
                if (elapsed >= seconds) { elapsed -= seconds; continue; }
                seconds -= elapsed; elapsed = 0;
                yield return ui.Play(new StorySequenceStep(step.Kind, step.Target, step.Variant, seconds, step.X, step.Y, step.Count, step.Visible, step.Parent));
            }
        }
        IEnumerator Finish(float seconds) { yield return new WaitForSecondsRealtime(seconds); ResetPose(); }
        public void ResetPose()
        {
            Initialize();
            StopAllCoroutines(); Playing = false;
            if (_FishActor) { _FishActor.anchoredPosition = _FishPosition; _FishActor.localRotation = _FishRotation; _FishActor.localScale = _FishScale; }
            if (_GhostActor) { _GhostActor.anchoredPosition = _GhostPosition; _GhostActor.localRotation = _GhostRotation; _GhostActor.localScale = _GhostScale; }
        }
        void OnDisable() => ResetPose();
    }
}
