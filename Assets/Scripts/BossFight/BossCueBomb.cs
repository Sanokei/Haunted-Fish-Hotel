using System;
using System.Collections;
using UnityEngine;

namespace HauntedFish.BossFight
{
    // Authority-side hot potato. The cue ball is a lit bomb: it explodes wherever it happens to be when the fuse runs out,
    // so nobody wants to be the last to touch it. One coroutine waits for the deadline and is restarted whenever a paddle
    // strike or a dynamite crate moves that deadline. There is no per-frame code: collisions, the defense board and the
    // arena are the only inputs, and every consequence leaves through an event.
    public sealed class BossCueBomb : MonoBehaviour
    {
        [SerializeField] BossDefenseBoard _Defenses;
        [SerializeField] Rigidbody _FishPaddle, _GhostPaddle;
        [SerializeField, Min(1)] float _FuseSeconds = 9, _MinRemaining = 1.5f;
        [SerializeField, Min(0)] float _StrikePenalty = .6f;
        BossFuse _Fuse;
        // Created on first use: the arena can touch the bomb before this component's own Awake has run.
        BossFuse Fuse => _Fuse ?? (_Fuse = CreateFuse());
        Coroutine _Burn;

        // Heat 0..1 and the full fuse length; raised when lit and whenever a strike or crate changes the deadline.
        public event Action<float, float> Changed;
        public event Action<Vector3, BossSide> Detonated;
        public event Action Defused;
        public bool Armed => Fuse.Armed;
        public float Duration => _FuseSeconds;
        public float Heat => Fuse.Heat(Time.unscaledTime);
        public uint Revision { get; private set; }

        public bool ValidateAuthoredBindings(out string error)
        {
            error = null;
            if (!_Defenses || !_FishPaddle || !_GhostPaddle) { error = "Assign the defense board and both paddles to the cue bomb."; return false; }
            return true;
        }

        BossFuse CreateFuse()
        {
            if (!ValidateAuthoredBindings(out string error)) throw new InvalidOperationException(error);
            return new BossFuse(_FuseSeconds, _StrikePenalty, Mathf.Min(_MinRemaining, _FuseSeconds));
        }

        void Awake() => _ = Fuse;

        void OnEnable() => _Defenses.PieceStruck += OnPieceStruck;

        void OnDisable()
        {
            _Defenses.PieceStruck -= OnPieceStruck;
            Stop();
            Fuse.Disarm();
        }

        public void Arm()
        {
            Fuse.Arm(Time.unscaledTime);
            Revision++;
            Wait();
            Changed?.Invoke(0, _FuseSeconds);
        }

        public void Disarm()
        {
            bool wasArmed = Fuse.Armed;
            Stop();
            Fuse.Disarm();
            if (wasArmed) { Revision++; Defused?.Invoke(); }
        }

        void OnCollisionEnter(Collision collision)
        {
            var body = collision.rigidbody;
            BossSide side = body == _FishPaddle ? BossSide.Fish : body == _GhostPaddle ? BossSide.Ghost : BossSide.None;
            if (side == BossSide.None || !Fuse.Strike(Time.unscaledTime, side)) return;
            Rescheduled();
        }

        void OnPieceStruck(BossDefenseKind kind)
        {
            float seconds = BossDefenseCatalog.FuseShortening(kind);
            if (seconds > 0 && Fuse.Shorten(Time.unscaledTime, seconds)) Rescheduled();
        }

        void Rescheduled()
        {
            Revision++;
            Wait();
            Changed?.Invoke(Fuse.Heat(Time.unscaledTime), _FuseSeconds);
        }

        void Wait()
        {
            Stop();
            _Burn = StartCoroutine(Burn());
        }

        void Stop()
        {
            if (_Burn != null) StopCoroutine(_Burn);
            _Burn = null;
        }

        IEnumerator Burn()
        {
            yield return new WaitForSecondsRealtime(Fuse.Remaining(Time.unscaledTime));
            _Burn = null;
            if (!Fuse.Due(Time.unscaledTime)) { Wait(); yield break; } // the clock fell short of the deadline; wait out the rest
            BossSide striker = Fuse.LastStriker;
            Fuse.Disarm();
            Revision++;
            Detonated?.Invoke(transform.position, striker);
        }
    }
}
