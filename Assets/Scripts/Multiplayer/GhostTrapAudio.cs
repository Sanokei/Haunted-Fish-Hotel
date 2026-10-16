using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // Reads replicated trap state, so every peer hears the same trap.
    [RequireComponent(typeof(GhostTrap))]
    public sealed class GhostTrapAudio : MonoBehaviour
    {
        [Tooltip("Repeats while the trap travels (cart wheels).")]
        [SerializeField] AudioClip _Move;
        [SerializeField, Min(.05f)] float _MoveInterval = .45f;
        [Tooltip("Plays when the trap is activated (angler yawn, chandelier release).")]
        [SerializeField] AudioClip _Activate;
        [Tooltip("Plays when a falling trap lands, or a travelling trap stops dead.")]
        [SerializeField] AudioClip _Impact;
        [SerializeField, Range(0, 1)] float _Volume = .8f;
        GhostTrap _Trap;
        AudioSource _Source;
        int _Activations = -1, _Hits;
        TrapPhase _Phase;
        float _LastX, _Speed, _NextMove;

        void Awake()
        {
            _Trap = GetComponent<GhostTrap>();
            _Source = gameObject.AddComponent<AudioSource>();
            HotelAudioRouting.RouteEffects(_Source);
            _Source.playOnAwake = false; _Source.spatialBlend = 1; _Source.dopplerLevel = 0;
            _Source.minDistance = 2; _Source.maxDistance = 24; _Source.rolloffMode = AudioRolloffMode.Linear;
        }

        void OnEnable() { _Activations = -1; }

        void LateUpdate()
        {
            var state = _Trap.CaptureState();
            float x = transform.position.x, dt = Mathf.Max(Time.deltaTime, .0001f);
            if (_Activations < 0)
            {
                _Activations = state.Activations; _Hits = state.Hits; _Phase = _Trap.Phase; _LastX = x;
                return;
            }
            if (state.Activations > _Activations) Play(_Activate);
            _Activations = state.Activations;
            // Replicated hit count: every player hears a fish being struck.
            if (state.Hits > _Hits && !_Trap.Falls) Play(_Impact);
            _Hits = state.Hits;
            if (_Trap.Falls && _Phase == TrapPhase.Active && _Trap.Phase == TrapPhase.Latched) Play(_Impact);
            _Phase = _Trap.Phase;

            if (_Trap.Travels)
            {
                float speed = Mathf.Abs(x - _LastX) / dt;
                if (speed > .5f && Time.unscaledTime >= _NextMove)
                {
                    _NextMove = Time.unscaledTime + _MoveInterval * Random.Range(.85f, 1.15f);
                    Play(_Move, Random.Range(.92f, 1.08f), .6f);
                }
                // Crash into the end of the run.
                var limits = _Trap.Limits;
                bool atEnd = Mathf.Min(Mathf.Abs(x - limits.x), Mathf.Abs(x - limits.y)) < .05f;
                if (_Speed > 2 && speed < .2f && atEnd) Play(_Impact);
                _Speed = Mathf.Lerp(_Speed, speed, .5f);
            }
            _LastX = x;
        }

        void Play(AudioClip clip, float pitch = 1, float volume = 1)
        {
            if (!clip) return;
            _Source.pitch = pitch;
            _Source.PlayOneShot(clip, _Volume * volume);
        }
    }
}
