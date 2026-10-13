using System;
using System.Collections.Generic;

namespace HauntedFish.Multiplayer
{
    // Timing and gain policy; Unity owns clips, sources, light events and frame time.
    public sealed class LobbyStormAudioState
    {
        public readonly struct ThunderCue
        {
            public readonly double Due;
            public readonly int Clip;
            public readonly float Gain;
            public ThunderCue(double due, int clip, float gain) { Due = due; Clip = clip; Gain = gain; }
        }
        readonly List<ThunderCue> _Pending = new List<ThunderCue>();
        long _LastStrike = long.MinValue;
        bool _Dialogue;
        public bool Running { get; private set; }
        public int PendingCount => _Pending.Count;
        public float RainGain { get; private set; }
        public float ThunderGain { get; private set; }

        // Distance delay describes audible onset; compensate silence in the authored cue.
        public static double PlaybackDelay(double distanceDelay, double onsetSeconds, double pitch)
        {
            if (double.IsNaN(distanceDelay) || double.IsInfinity(distanceDelay) ||
                double.IsNaN(onsetSeconds) || double.IsInfinity(onsetSeconds) ||
                double.IsNaN(pitch) || double.IsInfinity(pitch))
                throw new ArgumentOutOfRangeException(nameof(onsetSeconds));
            double speed = Math.Max(.01, Math.Abs(pitch));
            return Math.Max(0, Math.Max(0, distanceDelay) - Math.Max(0, onsetSeconds) / speed);
        }

        public void Start() => Running = true;
        public void SetDialogue(bool active) => _Dialogue = active;
        public void Stop()
        {
            Running = false;
            _Pending.Clear();
            _LastStrike = long.MinValue;
            RainGain = ThunderGain = 0;
        }

        public void Strike(long id, double now, double delay, int clip, float gain)
        {
            if (!Running || id <= _LastStrike) return;
            if (double.IsNaN(now) || double.IsInfinity(now) || double.IsNaN(delay) ||
                double.IsInfinity(delay) || delay < 0 || clip < 0 ||
                float.IsNaN(gain) || float.IsInfinity(gain) || gain < 0)
                throw new ArgumentOutOfRangeException(nameof(delay));
            _LastStrike = id;
            var cue = new ThunderCue(now + delay, clip, gain);
            int index = _Pending.FindIndex(pending => pending.Due > cue.Due);
            if (index < 0) _Pending.Add(cue); else _Pending.Insert(index, cue);
        }

        // A free Unity voice reserves the next cue ahead of its DSP onset.
        public bool TryTakeNext(out ThunderCue cue)
        {
            if (!Running || _Pending.Count == 0) { cue = default; return false; }
            cue = _Pending[0]; _Pending.RemoveAt(0); return true;
        }

        public bool TryTakeDue(double now, out ThunderCue cue)
        {
            if (!Running || _Pending.Count == 0 || _Pending[0].Due > now)
            {
                cue = default;
                return false;
            }
            cue = _Pending[0];
            _Pending.RemoveAt(0);
            return true;
        }

        public void Advance(float delta, float rain, float thunder, float rainDuck, float thunderDuck, float fadeSeconds)
        {
            if (!Running) return;
            float blend = (float)(1 - Math.Exp(-Math.Max(0, delta) / Math.Max(.01f, fadeSeconds)));
            float rainTarget = Math.Max(0, rain) * (_Dialogue ? Clamp01(rainDuck) : 1);
            float thunderTarget = Math.Max(0, thunder) * (_Dialogue ? Clamp01(thunderDuck) : 1);
            RainGain += (rainTarget - RainGain) * blend;
            ThunderGain += (thunderTarget - ThunderGain) * blend;
        }
        static float Clamp01(float value) => Math.Max(0, Math.Min(1, value));
    }
}
