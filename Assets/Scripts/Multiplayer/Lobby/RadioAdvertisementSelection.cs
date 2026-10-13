using System;
using System.Collections.Generic;

namespace HauntedFish.Multiplayer
{
    // Plain selection policy: missing entries and every copy of the previous clip are skipped.
    public static class RadioAdvertisementSelection
    {
        // Music always tunes to an ad; an ad has a two-in-three chance of another ad.
        public static bool DialPlaysAdvertisement(bool playingAdvertisement, double sample)
            => !playingAdvertisement || sample < 2.0 / 3.0;

        public static T Choose<T>(IReadOnlyList<T> clips, T previous, Func<T, bool> available, double sample) where T : class
        {
            if (clips == null || clips.Count == 0) return null;
            bool hasAlternative = false;
            for (int i = 0; i < clips.Count; i++)
                if (available(clips[i]) && !EqualityComparer<T>.Default.Equals(clips[i], previous)) { hasAlternative = true; break; }
            int count = 0;
            for (int i = 0; i < clips.Count; i++)
                if (available(clips[i]) && (!hasAlternative || !EqualityComparer<T>.Default.Equals(clips[i], previous))) count++;
            if (count == 0) return null;
            if (double.IsNaN(sample)) sample = 0;
            int target = (int)(Math.Max(0, Math.Min(.999999999, sample)) * count);
            for (int i = 0; i < clips.Count; i++)
                if (available(clips[i]) && (!hasAlternative || !EqualityComparer<T>.Default.Equals(clips[i], previous)) && target-- == 0) return clips[i];
            return null;
        }
    }
}
