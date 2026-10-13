using System;
using System.Collections.Generic;

namespace HauntedFish.Multiplayer
{
    // Serialized values are append-only. Existing traps retain their damage policy.
    public enum TrapImpact { DamageOverTime = 0, KnockbackStun = 1, Lethal = 2 }

    public static class TrapImpactRules
    {
        public static float Damage(TrapImpact impact, float rate, float seconds)
        {
            if (!Finite(seconds) || seconds <= 0) return 0;
            if (impact == TrapImpact.Lethal) return float.MaxValue;
            if (impact != TrapImpact.DamageOverTime || !Finite(rate) || rate <= 0) return 0;
            return (float)Math.Min(float.MaxValue, (double)rate * seconds);
        }

        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    // One strike per continuous contact episode, independent of collider count.
    public sealed class TrapContactEpisodes<T>
    {
        readonly HashSet<T> _Struck = new HashSet<T>();
        readonly List<T> _Separated = new List<T>();
        public bool Contains(T target) => _Struck.Contains(target);
        public bool TryStrike(T target) => _Struck.Add(target);
        public void RetainContacts(Predicate<T> touches)
        {
            _Separated.Clear();
            foreach (var target in _Struck) if (!touches(target)) _Separated.Add(target);
            foreach (var target in _Separated) _Struck.Remove(target);
        }
        public void Clear() { _Struck.Clear(); _Separated.Clear(); }
    }

    public readonly struct TrapStunStep
    {
        public readonly bool BlocksInput;
        public readonly float HorizontalDisplacement;
        public TrapStunStep(bool blocksInput, float horizontalDisplacement)
        { BlocksInput = blocksInput; HorizontalDisplacement = horizontalDisplacement; }
    }

    // Authority-owned status; collision/clamping remain the movement adapter's job.
    public sealed class TrapStunState
    {
        double _Remaining, _ImpulseAge, _ImpulseDuration, _ImpulseDistance;
        public bool Active => _Remaining > 0;
        public bool Apply(float direction, float distance, float impulseSeconds, float stunSeconds)
        {
            if (!TrapImpactRules.Finite(direction) || direction == 0 || !TrapImpactRules.Finite(distance) || distance < 0 ||
                !TrapImpactRules.Finite(impulseSeconds) || impulseSeconds <= 0 || !TrapImpactRules.Finite(stunSeconds) || stunSeconds <= 0)
                return false;
            _Remaining = Math.Max(_Remaining, stunSeconds);
            _ImpulseAge = 0;
            _ImpulseDuration = Math.Min(impulseSeconds, stunSeconds);
            _ImpulseDistance = Math.Sign(direction) * distance;
            return true;
        }
        public TrapStunStep Advance(float seconds, bool paused = false)
        {
            bool blocked = Active;
            if (paused || !TrapImpactRules.Finite(seconds) || seconds <= 0) return new TrapStunStep(blocked, 0);
            double before = Math.Min(1, _ImpulseDuration > 0 ? _ImpulseAge / _ImpulseDuration : 1);
            _ImpulseAge = Math.Min(_ImpulseDuration, _ImpulseAge + seconds);
            double after = Math.Min(1, _ImpulseDuration > 0 ? _ImpulseAge / _ImpulseDuration : 1);
            // Integral of a linearly decaying velocity; identical displacement at any tick rate.
            float displacement = (float)(_ImpulseDistance * ((2 * after - after * after) - (2 * before - before * before)));
            _Remaining = Math.Max(0, _Remaining - seconds);
            return new TrapStunStep(blocked, displacement);
        }
        public void Clear() { _Remaining = _ImpulseAge = _ImpulseDuration = _ImpulseDistance = 0; }
    }
}
