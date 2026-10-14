using System;
using UnityEngine;

namespace HauntedFish.Presentation
{
    /// <summary>One scheduled owner for authored eyes; no runtime scene or material construction.</summary>
    [DisallowMultipleComponent]
    public sealed class WatchingFrameGallery : MonoBehaviour
    {
        [Serializable]
        public sealed class Eye
        {
            public Renderer Renderer;
            public Transform AimPoint;
            [Range(0, 1)] public float PresentationGain = 1;
            [NonSerialized] internal Transform Plane;
            [NonSerialized] internal Vector2 LastOffset;
            [NonSerialized] internal bool HasOffset;
            [NonSerialized] internal float Closure;
        }

        [Serializable]
        public sealed class StaticFrame
        {
            public Renderer Renderer;
            [Range(0, 1)] public float PresentationGain = 1;
        }

        [SerializeField] Transform _Viewer;
        [SerializeField] Eye[] _Eyes = Array.Empty<Eye>();
        [SerializeField] StaticFrame[] _StaticFrames = Array.Empty<StaticFrame>();
        [SerializeField, Range(5, 30)] float _SamplesPerSecond = 20;
        [SerializeField, Range(0, 1)] float _GazeGain = .35f;
        [SerializeField, Min(.01f)] float _MinimumDistance = 4;
        [SerializeField] Vector2 _TravelUV = new Vector2(.23f, .12f);
        [SerializeField, Min(0)] float _ViewerMovementThreshold = .004f;
        [Header("Hallway flashlight only: the actual local screen-beam owner supplies state")]
        [SerializeField] bool _ReactToFlashlight;
        [SerializeField] Camera _BeamCamera;
        [SerializeField] LayerMask _BeamOccluders = ~0;
        [SerializeField, Min(.01f)] float _CloseSeconds = .16f;
        [SerializeField, Min(.01f)] float _OpenSeconds = .24f;
        static readonly int OffsetId = Shader.PropertyToID("_PupilOffset");
        static readonly int GainId = Shader.PropertyToID("_PresentationGain");
        static readonly int ClosureId = Shader.PropertyToID("_Closure");
        MaterialPropertyBlock _Properties;
        Vector3 _LastViewerPosition;
        bool _HasViewerPosition, _BeamActive;
        Vector2 _BeamCenter;
        float _BeamRadius, _BeamSoftness, _BeamAspect, _LastSampleTime;

        public void BindViewer(Transform viewer)
        {
            _Viewer = viewer;
            _HasViewerPosition = false;
            if (isActiveAndEnabled) SampleGaze();
        }

        // Called by the scene-authored GameMouseSpotlight with the same state it rendered.
        // Other players, disabled lights and camera proximity cannot activate this response.
        public void SetFlashlightBeam(bool active, Vector2 center, float radius, float softness, float aspect)
        {
            _BeamActive = active;
            _BeamCenter = center;
            _BeamRadius = radius;
            _BeamSoftness = softness;
            _BeamAspect = aspect;
        }

        void OnEnable()
        {
            _Properties = _Properties ?? new MaterialPropertyBlock();
            _HasViewerPosition = false;
            _LastSampleTime = Time.unscaledTime;
            // Empty picture slots receive their authored depth shading once, never gaze or beam tests.
            foreach (var frame in _StaticFrames)
            {
                if (frame == null || !frame.Renderer) continue;
                frame.Renderer.GetPropertyBlock(_Properties);
                _Properties.SetFloat(GainId, Mathf.Clamp01(frame.PresentationGain));
                frame.Renderer.SetPropertyBlock(_Properties);
            }
            foreach (var eye in _Eyes)
            {
                if (eye == null) continue;
                eye.Plane = eye.AimPoint ? eye.AimPoint : eye.Renderer ? eye.Renderer.transform : null;
                eye.HasOffset = false;
                eye.Closure = 0;
            }
            SampleGaze();
            InvokeRepeating(nameof(SampleGaze), 1f / Mathf.Clamp(_SamplesPerSecond, 5, 30),
                1f / Mathf.Clamp(_SamplesPerSecond, 5, 30));
        }

        void OnDisable()
        {
            CancelInvoke(nameof(SampleGaze));
            _HasViewerPosition = false;
            _BeamActive = false;
            foreach (var eye in _Eyes)
            {
                if (eye == null || !eye.Renderer || _Properties == null) continue;
                eye.Renderer.GetPropertyBlock(_Properties);
                _Properties.SetVector(OffsetId, Vector4.zero);
                _Properties.SetFloat(ClosureId, 0);
                eye.Renderer.SetPropertyBlock(_Properties);
                eye.HasOffset = false;
                eye.Closure = 0;
            }
        }

        bool Illuminated(Transform eye)
        {
            if (!_ReactToFlashlight || !_BeamActive || !_BeamCamera || !_BeamCamera.isActiveAndEnabled) return false;
            var screen = _BeamCamera.WorldToScreenPoint(eye.position);
            if (screen.z <= 0) return false;
            if (!WatchingFrameGaze.BeamContains(screen.x / Mathf.Max(1, Screen.width),
                screen.y / Mathf.Max(1, Screen.height), _BeamCenter.x, _BeamCenter.y,
                _BeamAspect, _BeamRadius, _BeamSoftness)) return false;
            // The existing flashlight is screen-space, so use its viewing ray, not a fictitious
            // avatar spot light. An opaque physics body before the eye blocks the response.
            var ray = _BeamCamera.ScreenPointToRay(screen);
            var distance = Vector3.Dot(eye.position - ray.origin, ray.direction);
            return distance > 0 && !Physics.Raycast(ray, Mathf.Max(0, distance - .03f),
                _BeamOccluders, QueryTriggerInteraction.Ignore);
        }

        void SampleGaze()
        {
            if (_Properties == null) return;
            float now = Time.unscaledTime;
            float elapsed = Mathf.Max(0, now - _LastSampleTime);
            _LastSampleTime = now;
            var viewer = _Viewer ? _Viewer.position : Vector3.zero;
            bool gazeMoved = !_HasViewerPosition || (_Viewer && (viewer - _LastViewerPosition).sqrMagnitude >=
                _ViewerMovementThreshold * _ViewerMovementThreshold);
            if (gazeMoved) _LastViewerPosition = viewer;
            _HasViewerPosition = true;
            foreach (var eye in _Eyes)
            {
                if (eye == null || !eye.Renderer || !eye.Plane) continue;
                var offset = eye.LastOffset;
                if (gazeMoved || !eye.HasOffset)
                {
                    var direction = _Viewer ? eye.Plane.InverseTransformDirection(viewer - eye.Plane.position) : Vector3.zero;
                    WatchingFrameGaze.Project(direction.x, direction.y, direction.z, _GazeGain,
                        _MinimumDistance, _TravelUV.x, _TravelUV.y, out var x, out var y);
                    offset = new Vector2(x, y);
                }
                float closure = WatchingFrameGaze.AdvanceClosure(eye.Closure,
                    Illuminated(eye.Plane), elapsed, _CloseSeconds, _OpenSeconds);
                if (eye.HasOffset && (offset - eye.LastOffset).sqrMagnitude < .00000001f &&
                    Mathf.Abs(closure - eye.Closure) < .00001f) continue;
                eye.Renderer.GetPropertyBlock(_Properties);
                _Properties.SetVector(OffsetId, new Vector4(offset.x, offset.y, 0, 0));
                _Properties.SetFloat(ClosureId, closure);
                _Properties.SetFloat(GainId, Mathf.Clamp01(eye.PresentationGain));
                eye.Renderer.SetPropertyBlock(_Properties);
                eye.LastOffset = offset;
                eye.Closure = closure;
                eye.HasOffset = true;
            }
        }
    }
}
