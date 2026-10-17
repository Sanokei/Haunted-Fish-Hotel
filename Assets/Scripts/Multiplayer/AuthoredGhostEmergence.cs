using System;
using System.Collections;
using System.Collections.Generic;
using Monologue.Dialogue;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // One reusable, initially disabled effect is authored under each player.
    // It presents the death event only; it never changes the actor's role.
    public sealed class AuthoredGhostEmergence : MonoBehaviour
    {
        [SerializeField] StoryUI _UI;
        [SerializeField] TextAsset _Sequence;
        [SerializeField] RectTransform _Anchor;
        [SerializeField] Canvas _Canvas;
        [SerializeField, Min(.01f)] float _WorldWidth = 2.5f, _TimingScale = 1;
        Vector3 _DeathPosition;
        bool _Waiting;

        public void PlayAt(Vector3 position)
        {
            if (!_UI || !_Sequence || !_Anchor || !_Canvas) return;
            var steps = StoryFunctions.ReadAnimationSequence(_Sequence.text);
            // This effect may animate only existing authored elements/layouts.
            foreach (var step in steps)
                if (step.Kind == StorySequenceStepKind.CreateUI)
                    throw new InvalidOperationException("Death emergence cannot create UI objects.");
            _UI.ValidateSequence(steps);
            StopAllCoroutines();
            _UI.Close();
            _DeathPosition = position;
            gameObject.SetActive(true);
            _UI.Prepare();
            StartCoroutine(Animate(steps));
        }

        IEnumerator Animate(IReadOnlyList<StorySequenceStep> steps)
        {
            // Let the corpse land before the soul rises out of it.
            if (_Canvas) _Canvas.enabled = false;
            _Waiting = true;
            yield return new WaitForSeconds(HotelFishSprite.CorpseFallSeconds + .1f);
            _Waiting = false;
            PresentAnchor();
            foreach (var step in steps)
                yield return _UI.Play(new StorySequenceStep(step.Kind, step.Target, step.Variant,
                    step.Seconds * _TimingScale, step.X, step.Y, step.Count, step.Visible, step.Parent));
            Stop();
        }

        public void Stop()
        {
            StopAllCoroutines();
            _Waiting = false;
            if (_UI) _UI.Close();
            if (_Canvas) _Canvas.enabled = false;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        void LateUpdate() { if (!_Waiting) PresentAnchor(); }
        void PresentAnchor()
        {
            var camera = HotelViewCamera.Current;
            if (!_Canvas) return;
            _Canvas.enabled = camera && _Anchor;
            if (!_Canvas.enabled) return;
            _Canvas.worldCamera = camera;
            _Canvas.planeDistance = Mathf.Max(.5f, camera.nearClipPlane + .01f);
            // Keep the soul anchored to the replicated death location, even if
            // the retained actor/camera moves during the appear/rise/clear clip.
            var screen = camera.WorldToScreenPoint(_DeathPosition);
            if (screen.z <= camera.nearClipPlane || !RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)_Canvas.transform, screen, camera, out var anchor))
            { _Canvas.enabled = false; return; }
            _Anchor.position = anchor;
            float pixels = Mathf.Abs(camera.WorldToScreenPoint(_DeathPosition + Vector3.right * _WorldWidth).x - screen.x);
            _Anchor.localScale = Vector3.one * (pixels / (250 * Mathf.Max(.001f, _Canvas.scaleFactor)));
        }
        void OnDisable() { StopAllCoroutines(); if (_UI) _UI.Close(); if (_Canvas) _Canvas.enabled = false; }
    }
}
