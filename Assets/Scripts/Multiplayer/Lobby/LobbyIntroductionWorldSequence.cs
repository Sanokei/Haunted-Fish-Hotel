using System;
using System.Collections;
using Monologue.Dialogue;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // A client-only cinematic uses the local fish's authored visual. It never teleports a network body.
    public sealed class LobbyIntroductionWorldSequence : MonoBehaviour
    {
        [SerializeField] CinemachineCamera _Camera;
        [SerializeField] Transform _EntryCamera, _FishStart, _FishWalkEnd, _ReceptionCamera;
        [SerializeField] TMP_Text _Caption;
        [SerializeField] HotelFishSprite _AuthoredFish;
        [SerializeField] AudioSource _CaptionVoice;
        [SerializeField] LobbyStormAudio _StormAudio;
        bool _Talking;
        [SerializeField] LobbyIntroAtmosphere _Atmosphere;
        CinemachineSplineDolly _Dolly;
        bool _DollyEnabled, _Active;
        Transform _Follow, _LookAt;
        Vector3 _CameraPosition;
        Quaternion _CameraRotation;
        HotelFishSprite _Fish;
        SpriteRenderer[] _OriginalRenderers;
        bool[] _OriginalHidden;
        bool _Walking;

        public bool Supports(StorySequenceStep step) =>
            step.Kind == StorySequenceStepKind.LobbyLine ? _Caption :
            step.Kind == StorySequenceStepKind.LobbyShot && _Camera && _EntryCamera && _FishStart &&
            _AuthoredFish && _FishWalkEnd && _ReceptionCamera &&
            (step.Target == "entry" || step.Target == "walk_away" || step.Target == "reception");

        public IEnumerator Play(StorySequenceStep step)
        {
            if (!Supports(step)) throw new InvalidOperationException("Unbound lobby world shot: " + step.Target);
            if (step.Kind == StorySequenceStepKind.LobbyLine)
            {
                _Caption.gameObject.SetActive(true);
                _Caption.text = step.Variant;
                _Talking = true;
                if (_StormAudio) _StormAudio.SetIntroductionTalking(true);
                if (_CaptionVoice && _CaptionVoice.clip) _CaptionVoice.Play();
                try { yield return new WaitForSecondsRealtime(step.Seconds); }
                finally { EndLine(); }
                yield break;
            }
            yield return Prepare();
            if (step.Target == "entry")
            {
                _Fish.transform.position = _FishStart.position;
                _Camera.transform.SetPositionAndRotation(_EntryCamera.position, _EntryCamera.rotation);
                yield return new WaitForSecondsRealtime(step.Seconds);
            }
            else if (step.Target == "walk_away")
            {
                _Walking = true;
                var start = _Fish.transform.position;
                yield return Tween(step.Seconds, t => _Fish.transform.position = Vector3.Lerp(start, _FishWalkEnd.position, t));
                _Walking = false;
            }
            else
            {
                var position = _Camera.transform.position;
                var rotation = _Camera.transform.rotation;
                yield return Tween(step.Seconds, t => _Camera.transform.SetPositionAndRotation(
                    Vector3.Lerp(position, _ReceptionCamera.position, t),
                    Quaternion.Slerp(rotation, _ReceptionCamera.rotation, t)));
            }
        }

        public IEnumerator Prepare()
        {
            if (!_Active)
            {
                // The opening UI starts before the network actor may exist. Bound the wait for a failed connection.
                float deadline = Time.unscaledTime + 15;
                while (!HotelPlayer.LocalPlayer && Time.unscaledTime < deadline) yield return null;
                var player = HotelPlayer.LocalPlayer;
                var visual = player ? player.GetComponentInChildren<HotelFishSprite>(true) : null;
                if (!visual) throw new InvalidOperationException("Lobby intro requires a spawned local fish visual.");
                _Active = true;
                if (_Atmosphere) _Atmosphere.Begin();
                _CameraPosition = _Camera.transform.position;
                _CameraRotation = _Camera.transform.rotation;
                _Follow = _Camera.Follow; _LookAt = _Camera.LookAt;
                _Dolly = _Camera.GetComponent<CinemachineSplineDolly>();
                if (_Dolly) { _DollyEnabled = _Dolly.enabled; _Dolly.enabled = false; }
                _Camera.Follow = null; _Camera.LookAt = null;
                if (!_AuthoredFish) throw new InvalidOperationException("Assign the scene-authored cinematic fish visual.");
                _Fish = _AuthoredFish;
                _Fish.transform.SetPositionAndRotation(_FishStart.position, visual.transform.rotation);
                _Fish.gameObject.SetActive(true);
                _Fish.CopyAppearanceFrom(visual);
                foreach (var collider in _Fish.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                _OriginalRenderers = visual.GetComponentsInChildren<SpriteRenderer>(true);
                _OriginalHidden = new bool[_OriginalRenderers.Length];
                for (int i = 0; i < _OriginalRenderers.Length; i++)
                {
                    _OriginalHidden[i] = _OriginalRenderers[i].forceRenderingOff;
                    _OriginalRenderers[i].forceRenderingOff = true;
                }
                _Camera.transform.SetPositionAndRotation(_EntryCamera.position, _EntryCamera.rotation);
                _Camera.PreviousStateIsValid = false;
            }
        }
        void EndLine()
        {
            _Talking = false;
            if (_CaptionVoice) _CaptionVoice.Stop();
            if (_StormAudio) _StormAudio.SetIntroductionTalking(false);
            if (_Caption) _Caption.gameObject.SetActive(false);
        }
        void LateUpdate()
        {
            if (_Fish) _Fish.Present(false, _Walking, _Talking);
        }

        static IEnumerator Tween(float seconds, Action<float> apply)
        {
            if (seconds <= 0) { apply(1); yield break; }
            for (float elapsed = 0; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                apply(Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }
            apply(1);
        }

        // Called through presentation Close for normal completion, held-space skip, disable, and error.
        public void Close()
        {
            EndLine();
            if (_Atmosphere) _Atmosphere.End();
            _Walking = false;
            if (_Caption) { _Caption.text = ""; _Caption.gameObject.SetActive(false); }
            if (_Fish) _Fish.gameObject.SetActive(false);
            _Fish = null;
            if (_OriginalRenderers != null)
                for (int i = 0; i < _OriginalRenderers.Length; i++)
                    if (_OriginalRenderers[i]) _OriginalRenderers[i].forceRenderingOff = _OriginalHidden[i];
            _OriginalRenderers = null; _OriginalHidden = null;
            if (_Active && _Camera)
            {
                _Camera.transform.SetPositionAndRotation(_CameraPosition, _CameraRotation);
                // Use the current owner after a disconnect/late replacement rather than a destroyed target.
                var player = HotelPlayer.LocalPlayer;
                _Camera.Follow = player ? player.transform : _Follow;
                _Camera.LookAt = _LookAt;
                if (_Dolly) _Dolly.enabled = _DollyEnabled;
                _Camera.PreviousStateIsValid = false;
            }
            _Active = false;
        }
        void OnDisable() => Close();
    }
}

