using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Monologue.Dialogue;
using System.Collections.Generic;

namespace HauntedFish.Multiplayer
{
    // Per-round presentation: no saved seen flag, skip action, or gameplay input.
    public sealed class GhostSelectionPresentation : MonoBehaviour
    {
        [SerializeField] GameObject _Root;
        [SerializeField] RectTransform _Wheel, _Pointer;
        [SerializeField] GhostSelectionWheel _Graphic;
        [SerializeField] Text _Title, _Caption, _PlayerLabel;
        [SerializeField] TextAsset _RoundInk;
        [SerializeField] StoryUI _AtticPrefab;
        StoryUI _Attic;
        Canvas _WheelCanvas, _AtticCanvas;
        void Awake() { if (_Root) _WheelCanvas = _Root.GetComponent<Canvas>(); }
        void LateUpdate()
        {
            bool watching = HotelPlayer.LocalPlayer && HotelPlayer.LocalPlayer.BossWatching;
            if (_WheelCanvas) _WheelCanvas.enabled = !watching;
            if (_Attic && (!_AtticCanvas || _AtticCanvas.gameObject != _Attic.gameObject)) _AtticCanvas = _Attic.GetComponent<Canvas>();
            if (_AtticCanvas) _AtticCanvas.enabled = !watching;
        }
        readonly System.Collections.Generic.List<Text> _Labels = new System.Collections.Generic.List<Text>();
        public void Play(Texture2D arrow, uint[] roster, uint ghost, uint local, Action finished)
        {
            Close();
            if (!_AtticPrefab) throw new InvalidOperationException("Assign the authored attic cutscene prefab.");
            var settings = _AtticPrefab.GetComponent<RoundIntroductionSettings>();
            if (!_RoundInk && (!settings || !settings.Sequence))
                throw new InvalidOperationException("Assign the compiled round Ink sequence on the cutscene prefab.");
            if (roster == null || roster.Length == 0 || Array.IndexOf(roster, ghost) < 0)
                throw new ArgumentException("The authoritative ghost must belong to the round roster.");
            StartCoroutine(Animate(arrow, roster, ghost, local, finished));
        }

        IEnumerator Animate(Texture2D arrow, uint[] roster, uint ghost, uint local, Action finished)
        {
            _Root.SetActive(true);
            _Graphic.Count = roster.Length;
            _Graphic.SetVerticesDirty();
            foreach (var label in _Labels) if (label) Destroy(label.gameObject);
            _Labels.Clear();
            _Title.text = "WHO WILL HAUNT THE HOTEL?";
            for (int i = 0; i < roster.Length; i++)
            {
                float angle = (i + .5f) * Mathf.PI * 2 / roster.Length;
                var label = Instantiate(_PlayerLabel, _Wheel, false);
                label.gameObject.SetActive(true);
                label.text = "Player " + roster[i] + (roster[i] == local ? " (you)" : "");
                label.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * 158;
                label.color = GhostSelectionWheel.SectorColor(i).grayscale > .48f ? HotelPalette.Night : HotelPalette.Light;
                _Labels.Add(label);
            }
            int selected = Array.IndexOf(roster, ghost);
            var settings = _AtticPrefab.GetComponent<RoundIntroductionSettings>();
            var sequence = _RoundInk ? _RoundInk : settings.Sequence;
            float timingScale = settings ? settings.TimingScale : 1;
            if (float.IsNaN(timingScale) || float.IsInfinity(timingScale) || timingScale <= 0)
                throw new InvalidOperationException("Round timing scale must be finite and positive.");
            var steps = new List<StorySequenceStep>();
            float totalSeconds = 0;
            foreach (var step in StoryFunctions.ReadAnimationSequence(sequence.text))
            {
                float seconds = step.Target == "wheel_spin" ? step.Seconds : step.Seconds * timingScale;
                steps.Add(new StorySequenceStep(step.Kind, step.Target, step.Variant, seconds,
                    step.X, step.Y, step.Count, step.Visible, step.Parent));
                totalSeconds += seconds * (step.Kind == StorySequenceStepKind.Knock ? step.Count : 1);
            }
            if (totalSeconds < GameRoundGate.MinimumDuration)
                throw new InvalidOperationException("Round sequence must meet the server's minimum presentation duration.");
            _AtticPrefab.ValidateSequence(new List<StorySequenceStep>(FilterAtticSteps(steps)));
            _Pointer.localRotation = Quaternion.identity;
            _Graphic.Selected = -1;
            _Graphic.SelectionPulse = 0;
            _Caption.text = "Choosing the ghost...";
            foreach (var step in steps)
            {
                if (step.Target == "wheel_spin" && step.Kind == StorySequenceStepKind.DollyTo)
                {
                    float destination = -(1800 + (selected + .5f) * 360 / roster.Length);
                    float start = Time.unscaledTime;
                    while (Time.unscaledTime - start < step.Seconds)
                    {
                        float t = Mathf.Clamp01((Time.unscaledTime - start) / step.Seconds);
                        _Pointer.localRotation = Quaternion.Euler(0, 0, destination * (1 - Mathf.Pow(1 - t, 4)));
                        yield return null;
                    }
                    _Pointer.localRotation = Quaternion.Euler(0, 0, destination);
                    _Graphic.Selected = selected;
                    _Graphic.SetVerticesDirty();
                    _Title.text = "THE GHOST HAS BEEN CHOSEN";
                    _Caption.text = ghost == local ? "You are the ghost" : "Player " + ghost + " is the ghost - You are a fish";
                }
                else if (step.Target == "selected_slice" && step.Kind == StorySequenceStepKind.Knock)
                {
                    for (int pulse = 0; pulse < step.Count; pulse++)
                    {
                        float start = Time.unscaledTime;
                        while (Time.unscaledTime - start < step.Seconds)
                        {
                            float t = Mathf.Clamp01((Time.unscaledTime - start) / step.Seconds);
                            float amount = Mathf.Sin(t * Mathf.PI);
                            _Graphic.SelectionPulse = amount;
                            _Graphic.SetVerticesDirty();
                            _Labels[selected].rectTransform.localScale = Vector3.one * (1 + amount * .15f);
                            yield return null;
                        }
                    }
                    _Graphic.SelectionPulse = 0;
                    _Graphic.SetVerticesDirty();
                    _Labels[selected].rectTransform.localScale = Vector3.one;
                }
                else if (step.Target == "attic_cutscene" && step.Kind == StorySequenceStepKind.SetVisible)
                {
                    _Attic = Instantiate(_AtticPrefab, transform, false);
                    _Attic.Prepare();
                    _Root.SetActive(false);
                }
                else if (_Attic) yield return _Attic.Play(step);
                else if (step.Kind == StorySequenceStepKind.Wait) yield return new WaitForSecondsRealtime(step.Seconds);
                else throw new InvalidOperationException("Unbound round operation: " + step.Target);
            }
            _Caption.text = "Waiting for everyone to finish...";
            finished();
        }

        static IEnumerable<StorySequenceStep> FilterAtticSteps(IReadOnlyList<StorySequenceStep> steps)
        {
            foreach (var step in steps)
                if (step.Target != "wheel_spin" && step.Target != "selected_slice" && step.Target != "attic_cutscene") yield return step;
        }

        public void Close()
        {
            StopAllCoroutines();
            if (_Attic) { _Attic.Close(); Destroy(_Attic.gameObject); _Attic = null; }
            if (_Pointer) _Pointer.localRotation = Quaternion.identity;
            if (_Graphic) { _Graphic.Selected = -1; _Graphic.SelectionPulse = 0; _Graphic.SetVerticesDirty(); }
            if (_Root) _Root.SetActive(false);
            foreach (var label in _Labels) if (label) Destroy(label.gameObject);
            _Labels.Clear();
        }
        void OnDisable() => Close();
    }
}
