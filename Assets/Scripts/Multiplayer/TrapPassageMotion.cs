using System;
using System.Collections.Generic;

namespace HauntedFish.Multiplayer
{
    public readonly struct TrapPassageInterval
    {
        public readonly float Start, End;
        public TrapPassageInterval(float start, float end) { Start = start; End = end; }
        public bool IsValid => TrapImpactRules.Finite(Start) && TrapImpactRules.Finite(End) && End >= Start;
        public TrapPassageInterval Offset(float value) => new TrapPassageInterval(Start + value, End + value);
        public TrapPassageInterval Along(float origin, float direction) => direction > 0
            ? new TrapPassageInterval(Start - origin, End - origin)
            : new TrapPassageInterval(origin - End, origin - Start);
    }

    public static class TrapPassageMotion
    {
        // Capsule/box geometry reduces to an exact horizontal interval after the
        // vertical and depth gaps are removed from the rounded capsule radius.
        public static bool TryInterval(float boxMinX, float boxMaxX, float verticalGap, float depthGap,
            float capsuleX, float radius, out TrapPassageInterval interval)
        {
            interval = default;
            if (!TrapImpactRules.Finite(boxMinX) || !TrapImpactRules.Finite(boxMaxX) || boxMinX > boxMaxX ||
                !TrapImpactRules.Finite(verticalGap) || !TrapImpactRules.Finite(depthGap) ||
                !TrapImpactRules.Finite(capsuleX) || !TrapImpactRules.Finite(radius) || radius < 0) return false;
            double y = Math.Max(0, verticalGap), z = Math.Max(0, depthGap);
            double squared = (double)radius * radius - y * y - z * z;
            if (squared < 0) return false;
            float reach = (float)Math.Sqrt(squared);
            interval = new TrapPassageInterval(capsuleX - boxMaxX - reach, capsuleX - boxMinX + reach);
            return true;
        }

        // Intervals are supplied along travel. Slowdown applies only inside their
        // union, never stacks for overlapping fish, and consumes this tick's time.
        public static float Travel(float seconds, float speed, float maxDistance, float bodySpeedFactor,
            IReadOnlyList<TrapPassageInterval> bodies)
        {
            if (!TrapImpactRules.Finite(seconds) || seconds <= 0 || !TrapImpactRules.Finite(speed) || speed <= 0 ||
                !TrapImpactRules.Finite(maxDistance) || maxDistance <= 0 || !TrapImpactRules.Finite(bodySpeedFactor) ||
                bodySpeedFactor <= 0 || bodySpeedFactor > 1) return 0;
            double position = 0, remaining = seconds;
            while (position < maxDistance && remaining > 0)
            {
                double next = maxDistance;
                bool inside = false;
                if (bodies != null) foreach (var body in bodies)
                {
                    if (!body.IsValid || body.End <= position || body.Start >= maxDistance) continue;
                    if (body.Start <= position) { inside = true; next = Math.Min(next, body.End); }
                    else next = Math.Min(next, body.Start);
                }
                double currentSpeed = speed * (inside ? bodySpeedFactor : 1);
                double required = (next - position) / currentSpeed;
                if (required >= remaining) return (float)Math.Min(maxDistance, position + remaining * currentSpeed);
                position = next;
                remaining -= required;
            }
            return (float)position;
        }
    }

    // Cache the geometric body passage before lethal damage disables its collider.
    // A victim is retired only after the moving trap has exited the whole body.
    public sealed class TrapPassageMemory<T>
    {
        readonly Dictionary<T, List<TrapPassageInterval>> _Bodies = new Dictionary<T, List<TrapPassageInterval>>();
        readonly List<T> _Exited = new List<T>();
        public int Count => _Bodies.Count;
        public bool Contains(T target) => _Bodies.ContainsKey(target);
        public bool Remember(T target, IReadOnlyList<TrapPassageInterval> positions)
        {
            if (_Bodies.ContainsKey(target) || positions == null) return false;
            var copy = new List<TrapPassageInterval>();
            foreach (var interval in positions) if (interval.IsValid) copy.Add(interval);
            if (copy.Count == 0) return false;
            _Bodies.Add(target, copy);
            return true;
        }
        public void AppendIntervals(List<TrapPassageInterval> destination, float origin, float direction)
        {
            foreach (var body in _Bodies.Values)
                foreach (var interval in body) destination.Add(interval.Along(origin, direction));
        }
        public void RetireExited(float position, float direction)
        {
            _Exited.Clear();
            foreach (var pair in _Bodies)
            {
                bool remaining = false;
                foreach (var interval in pair.Value)
                    if (direction > 0 ? position < interval.End : position > interval.Start) { remaining = true; break; }
                if (!remaining) _Exited.Add(pair.Key);
            }
            foreach (var target in _Exited) _Bodies.Remove(target);
        }
        public void Clear() { _Bodies.Clear(); _Exited.Clear(); }
    }
}
