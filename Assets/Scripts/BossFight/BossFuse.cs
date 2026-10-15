using System;

namespace HauntedFish.BossFight
{
    public enum BossSide { None = 0, Fish = 1, Ghost = 2 }

    // The cue ball is a lit bomb. Pure timing rules; callers supply the clock, so nothing here polls.
    // Every paddle strike burns the fuse down a little (the potato gets hotter) but never below MinRemaining,
    // so a volley can speed the blast up without detonating it on the striker's own paddle.
    public sealed class BossFuse
    {
        public float Duration { get; }
        public float StrikePenalty { get; }
        public float MinRemaining { get; }
        public bool Armed { get; private set; }
        public float Deadline { get; private set; }
        public BossSide LastStriker { get; private set; }
        public int Strikes { get; private set; }

        public BossFuse(float duration, float strikePenalty, float minRemaining)
        {
            if (!Finite(duration) || duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
            if (!Finite(strikePenalty) || strikePenalty < 0) throw new ArgumentOutOfRangeException(nameof(strikePenalty));
            if (!Finite(minRemaining) || minRemaining < 0 || minRemaining > duration) throw new ArgumentOutOfRangeException(nameof(minRemaining));
            Duration = duration; StrikePenalty = strikePenalty; MinRemaining = minRemaining;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public float Remaining(float now) => Armed ? Math.Max(0, Deadline - now) : 0;
        // 1 at the moment of lighting, 0 at detonation; drives how hot the bomb looks.
        public float Heat(float now) => Armed ? 1 - Math.Min(1, Remaining(now) / Duration) : 0;

        public void Arm(float now)
        {
            if (!Finite(now)) return;
            Armed = true; Deadline = now + Duration; LastStriker = BossSide.None; Strikes = 0;
        }

        public void Disarm()
        {
            Armed = false; Deadline = 0; LastStriker = BossSide.None; Strikes = 0;
        }

        // Returns false when nothing was armed (a strike on a dead cue changes nothing).
        public bool Strike(float now, BossSide side)
        {
            if (!Armed || side == BossSide.None || !Finite(now)) return false;
            LastStriker = side; Strikes++;
            Deadline = Math.Min(Deadline, Math.Max(now + MinRemaining, Deadline - StrikePenalty)); // never extends
            return true;
        }

        // A dynamite crate burns the fuse by a fixed amount. Never below MinRemaining, never negative.
        public bool Shorten(float now, float seconds)
        {
            if (!Armed || !Finite(now) || !Finite(seconds) || seconds <= 0) return false;
            Deadline = Math.Min(Deadline, Math.Max(now + MinRemaining, Deadline - seconds));
            return true;
        }

        public bool Due(float now) => Armed && now >= Deadline;
    }
}
