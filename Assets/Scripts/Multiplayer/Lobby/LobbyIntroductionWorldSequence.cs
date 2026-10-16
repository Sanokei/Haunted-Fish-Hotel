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
        // The final shot cranes up the lobby and tilts into the intro-only frame tunnel. All timing is authored.
        [SerializeField] GameObject _Tunnel;
        [SerializeField] Transform _TunnelCamera;
        [SerializeField, Range(5, 90)] float _TunnelFieldOfView = 24;
        [Tooltip("Share of the pass spent reaching the reception pose before the crane and tilt begin.")]
        [SerializeField, Range(.2f, .8f)] float _ReceptionShare = .5f;
        [Tooltip("Share of the post-reception pass spent reaching the tunnel pose; the rest keeps rising through the shaft.")]
        [SerializeField, Range(.1f, .9f)] float _TunnelTiltShare = .4f;
        [Tooltip("Metres the camera climbs after the tilt, accelerating into the dark beyond the frames.")]
        [SerializeField, Min(0)] float _TunnelRise = 52;
        [SerializeField] float _TunnelRollDegrees = 25;
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
        bool _Walking, _TunnelWasActive;
        float _CameraFieldOfView;

        public bool Supports(StorySequenceStep step) =>
            step.Kind == StorySequenceStepKind.LobbyLine ? _Caption :
            step.Kind == StorySequenceStepKind.LobbyShot && _Camera && _EntryCamera && _FishStart &&
            _AuthoredFish && _FishWalkEnd && _ReceptionCamera &&
            (step.Target == "entry" || step.Target == "walk_away" || step.Target == "reception" ||
             step.Target == "tunnel" && _Tunnel && _TunnelCamera);

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
            else if (step.Target == "tunnel")
            {
                // One unbroken pass: past the fish, through reception, then craning and tilting into the shaft.
                // A single spline through the poses keeps velocity continuous; it never stops until the cut.
                float total = Mathf.Max(.01f, step.Seconds);
                float reception = total * _ReceptionShare;
                float tilted = reception + (total - reception) * _TunnelTiltShare;
                var camera = _Camera.transform;
                var keys = new[]
                {
                    new CameraKey(0, camera.position, camera.rotation, _Camera.Lens.FieldOfView),
                    new CameraKey(reception, _ReceptionCamera.position, _ReceptionCamera.rotation, _Camera.Lens.FieldOfView),
                    new CameraKey(tilted, _TunnelCamera.position, _TunnelCamera.rotation, _TunnelFieldOfView),
                    new CameraKey(total, _TunnelCamera.position + Vector3.up * _TunnelRise,
                        _TunnelCamera.rotation * Quaternion.Euler(0, 0, _TunnelRollDegrees), _TunnelFieldOfView),
                };
                var path = new CameraPath(keys);
                for (float elapsed = 0; elapsed < total; elapsed += Time.unscaledDeltaTime)
                {
                    path.Apply(elapsed, camera, _Camera);
                    yield return null;
                }
                path.Apply(total, camera, _Camera);
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
                float deadline = Time.unscaledTime + 60;
                while (!HotelPlayer.LocalPlayer && Time.unscaledTime < deadline) yield return null;
                var player = HotelPlayer.LocalPlayer;
                var visual = player ? player.GetComponentInChildren<HotelFishSprite>(true) : null;
                if (!visual) throw new InvalidOperationException("Lobby intro requires a spawned local fish visual.");
                _Active = true;
                if (_Atmosphere) _Atmosphere.Begin();
                _CameraPosition = _Camera.transform.position;
                _CameraRotation = _Camera.transform.rotation;
                _CameraFieldOfView = _Camera.Lens.FieldOfView;
                BeginEndingVisibility();
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
        // The tunnel extends the real lobby upward and stays active for every world beat, so the crane
        // into it never pops anything into view mid-shot.
        void BeginEndingVisibility()
        {
            if (!_Tunnel) return;
            _TunnelWasActive = _Tunnel.activeSelf;
            _Tunnel.SetActive(true);
        }
        void EndEndingVisibility()
        {
            if (_Tunnel) _Tunnel.SetActive(_TunnelWasActive);
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

        readonly struct CameraKey
        {
            public readonly float Time, FieldOfView;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public CameraKey(float time, Vector3 position, Quaternion rotation, float fieldOfView)
            { Time = time; Position = position; Rotation = rotation; FieldOfView = fieldOfView; }
        }

        // Cubic Hermite through the keys with finite-difference tangents: position, FOV and angular speed
        // stay continuous at every key. It starts from rest and carries its speed out of the last key.
        sealed class CameraPath
        {
            readonly CameraKey[] _Keys;
            readonly Vector3[] _PositionTangent;
            readonly float[] _FovTangent, _AngularSpeed;
            public CameraPath(CameraKey[] keys)
            {
                _Keys = keys;
                int n = keys.Length;
                _PositionTangent = new Vector3[n]; _FovTangent = new float[n]; _AngularSpeed = new float[n];
                for (int i = 1; i < n; i++)
                {
                    int a = i - 1, b = i == n - 1 ? i : i + 1;
                    float dt = keys[b].Time - keys[a].Time;
                    _PositionTangent[i] = (keys[b].Position - keys[a].Position) / dt;
                    _FovTangent[i] = (keys[b].FieldOfView - keys[a].FieldOfView) / dt;
                    _AngularSpeed[i] = Quaternion.Angle(keys[a].Rotation, keys[b].Rotation) / dt;
                }
            }
            public void Apply(float time, Transform transform, CinemachineCamera camera)
            {
                int i = 0;
                while (i < _Keys.Length - 2 && time > _Keys[i + 1].Time) i++;
                var a = _Keys[i]; var b = _Keys[i + 1];
                float h = b.Time - a.Time;
                float t = Mathf.Clamp01((time - a.Time) / h);
                float t2 = t * t, t3 = t2 * t;
                float h00 = 2 * t3 - 3 * t2 + 1, h10 = t3 - 2 * t2 + t, h01 = -2 * t3 + 3 * t2, h11 = t3 - t2;
                var position = h00 * a.Position + h10 * h * _PositionTangent[i] + h01 * b.Position + h11 * h * _PositionTangent[i + 1];
                float fov = h00 * a.FieldOfView + h10 * h * _FovTangent[i] + h01 * b.FieldOfView + h11 * h * _FovTangent[i + 1];
                // Rotation progress is a Hermite scalar whose end slopes reproduce the neighbouring angular speeds.
                float angle = Quaternion.Angle(a.Rotation, b.Rotation);
                float progress = t;
                if (angle > .01f)
                    progress = h01 + h10 * (_AngularSpeed[i] * h / angle) + h11 * (_AngularSpeed[i + 1] * h / angle);
                transform.SetPositionAndRotation(position, Quaternion.SlerpUnclamped(a.Rotation, b.Rotation, progress));
                var lens = camera.Lens; lens.FieldOfView = fov; camera.Lens = lens;
            }
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
                var lens = _Camera.Lens; lens.FieldOfView = _CameraFieldOfView; _Camera.Lens = lens;
                EndEndingVisibility();
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

