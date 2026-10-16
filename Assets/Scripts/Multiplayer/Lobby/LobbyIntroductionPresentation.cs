using System;
using System.Collections;
using System.Collections.Generic;
using Monologue.Dialogue;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    public sealed class LobbyIntroductionPresentation : MonoBehaviour
    {
        [SerializeField] StoryUI _UIPrefab;
        [SerializeField] StoryUI _AuthoredUI;
        [SerializeField] LightningEffectManager _Lightning;
        public bool IsReady { get; private set; }
        [SerializeField] LobbyIntroductionWorldSequence _World;
        StoryUI _UI;
        bool _WorldPlaying;
        public StoryUI UI => _UI;
        public event Action SkipRequested;
        static bool IsWorld(StorySequenceStep step) =>
            step.Kind == StorySequenceStepKind.LobbyShot || step.Kind == StorySequenceStepKind.LobbyLine;
        public void Prepare()
        {
            if (!_UI)
            {
                if (!_AuthoredUI) throw new InvalidOperationException("Assign the scene-authored introduction UI.");
                _UI = _AuthoredUI;
                // Skipping is hold-Space only (LobbyManager); the UI click skip is not forwarded.
            }
            if (!_WorldPlaying) _UI.Prepare();
            Canvas.ForceUpdateCanvases();
            IsReady = true;
        }
        public bool Supports(StorySequenceStep step) =>
            IsWorld(step) ? _World && _World.Supports(step) : _UIPrefab && _UIPrefab.Supports(step);
        public void ValidateSequence(IReadOnlyList<StorySequenceStep> steps)
        {
            if (!_UIPrefab) throw new InvalidOperationException("Assign the authored introduction UI prefab.");
            var supportsWorld = _World ? new Func<StorySequenceStep, bool>(_World.Supports) : null;
            var uiSteps = LobbyIntroductionSequenceValidation.UIOnly(steps, supportsWorld);
            foreach (var step in uiSteps) if (step.Kind == StorySequenceStepKind.CreateUI)
                throw new InvalidOperationException("Local intro UI must be authored before Play.");
            _UIPrefab.ValidateSequence(uiSteps);
        }
        public IEnumerator Play(StorySequenceStep step)
        {
            if (IsWorld(step))
            {
                if (!_WorldPlaying)
                {
                    Prepare();
                    yield return _World.Prepare();
                    _UI.PrepareWorld();
                    _WorldPlaying = true;
                }
                SetExteriorStorm(false);
                yield return _World.Play(step);
            }
            else
            {
                Prepare();
                if (_Lightning && step.Kind == StorySequenceStepKind.SetVisible && step.Target == "storm" && step.Visible)
                { SetExteriorStorm(true); _Lightning.StartAmbient(); }
                if (_Lightning && step.Kind == StorySequenceStepKind.SetVariant && step.Target == "cloud" && step.Variant == "cloud_lightning") _Lightning.Play();
                yield return _UI.Play(step);
            }
        }
        void SetExteriorStorm(bool exterior)
        {
            var audio = _Lightning ? _Lightning.GetComponentInChildren<LobbyStormAudio>(true) : null;
            if (audio) audio.SetExterior(exterior);
        }
        public void SetSkipProgress(float progress) { if (_UI) _UI.SetSkipProgress(progress); }
        public void Close()
        {
            IsReady = false;
            _WorldPlaying = false;
            if (_Lightning) _Lightning.StopAmbient();
            SetExteriorStorm(false);
            if (_World) _World.Close();
            if (!_UI) return;
            _UI.Close();
            // Scene-authored UI is retained for replay; Close hides/reset its content.
            _UI = null;
        }
        void OnDisable() => Close();
    }
}
