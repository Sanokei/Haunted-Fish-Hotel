using System;

namespace Monologue.Dialogue
{
    // Deterministic animation rules. Camera Y is positive up; push is positive toward layers.
    public enum StoryEase { Linear, Smooth, Smoother, EaseIn, EaseOut }

    public static class StoryAnimationMath
    {
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static StoryEase ParseEase(string name)
        {
            switch (name)
            {
                case "linear": return StoryEase.Linear;
                case "smooth": return StoryEase.Smooth;
                case "smoother": return StoryEase.Smoother;
                case "ease_in": return StoryEase.EaseIn;
                case "ease_out": return StoryEase.EaseOut;
                default: throw new ArgumentException("Unknown story easing: " + name);
            }
        }
        public static float Ease(float progress, StoryEase ease)
        {
            float t = Math.Max(0, Math.Min(1, progress));
            switch (ease)
            {
                case StoryEase.Linear: return t;
                case StoryEase.Smoother: return t * t * t * (t * (t * 6 - 15) + 10);
                case StoryEase.EaseIn: return t * t;
                case StoryEase.EaseOut: return 1 - (1 - t) * (1 - t);
                default: return t * t * (3 - 2 * t);
            }
        }
        public static float Magnification(float depth, float push)
        {
            if (!Finite(depth) || depth <= 0 || !Finite(push) || depth - push < .05f)
                throw new ArgumentException("Parallax push must stay at least 0.05 units in front of every positive layer depth.");
            return depth / (depth - push);
        }
        public static float Project(float position, float origin, float camera, float depth, float push) =>
            origin + ((position - origin) - camera / depth) * Magnification(depth, push);
    }
}
