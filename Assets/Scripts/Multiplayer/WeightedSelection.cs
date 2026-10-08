using System;
using System.Collections.Generic;

namespace HauntedFish.Multiplayer
{
    internal static class WeightedSelection
    {
        // The caller supplies randomness; the same unit sample always selects the same entry.
        // Double accumulation allows multiple valid float.MaxValue weights.
        public static T Select<T>(IEnumerable<T> entries, Func<T, float> weight, double sample) where T : class
        {
            if (double.IsNaN(sample) || double.IsInfinity(sample))
                return null;
            double total = 0;
            foreach (var entry in entries)
            {
                float value = weight(entry);
                if (ValidWeight(value))
                    total += value;
            }

            if (total <= 0)
                return null;
            double cursor = Math.Max(0, Math.Min(.9999999999999999, sample)) * total;
            T last = null;
            foreach (var entry in entries)
            {
                float value = weight(entry);
                if (!ValidWeight(value))
                    continue;
                last = entry;
                if (cursor < value)
                    return entry;
                cursor -= value;
            }

            return last;
        }

        static bool ValidWeight(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0;
    }
}
