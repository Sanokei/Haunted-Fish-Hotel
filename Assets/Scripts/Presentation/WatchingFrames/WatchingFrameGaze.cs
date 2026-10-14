using System;

namespace HauntedFish.Presentation
{
    /// <summary>Pure gaze projection. The texture mask, not an approximate ellipse, clips the pupil.</summary>
    public static class WatchingFrameGaze
    {
        public static void Project(float horizontal, float vertical, float depth, float gain,
            float minimumDistance, float limitX, float limitY, out float x, out float y)
        {
            x = y = 0;
            if (!Finite(horizontal) || !Finite(vertical) || !Finite(depth) || !Finite(gain) ||
                !Finite(minimumDistance) || !Finite(limitX) || !Finite(limitY)) return;
            limitX = Math.Max(0, Math.Min(.45f, limitX));
            limitY = Math.Max(0, Math.Min(.45f, limitY));
            double divisor = Math.Max(.01f, Math.Max(minimumDistance, Math.Abs(depth)));
            double px = limitX > 0 ? (double)horizontal * Math.Max(0, gain) / divisor : 0;
            double py = limitY > 0 ? (double)vertical * Math.Max(0, gain) / divisor : 0;
            double distance = (limitX > 0 ? px * px / ((double)limitX * limitX) : 0) +
                (limitY > 0 ? py * py / ((double)limitY * limitY) : 0);
            if (distance > 1)
            {
                double scale = 1 / Math.Sqrt(distance);
                px *= scale;
                py *= scale;
            }
            x = (float)px;
            y = (float)py;
        }

        // Matches GameMouseSpotlight's aspect-corrected circular falloff at its half-light edge.
        public static bool BeamContains(float x, float y, float centerX, float centerY,
            float aspect, float radius, float softness)
        {
            if (!Finite(x) || !Finite(y) || !Finite(centerX) || !Finite(centerY) ||
                !Finite(aspect) || !Finite(radius) || !Finite(softness) || aspect <= 0 || radius <= 0 ||
                x < 0 || x > 1 || y < 0 || y > 1) return false;
            double dx = (x - centerX) * aspect, dy = y - centerY;
            double edge = radius + Math.Max(.001f, softness) * .5;
            return dx * dx + dy * dy <= edge * edge;
        }

        public static float AdvanceClosure(float current, bool illuminated, float elapsed,
            float closeSeconds, float openSeconds)
        {
            if (!Finite(current)) current = 0;
            current = Math.Max(0, Math.Min(1, current));
            if (!Finite(elapsed) || elapsed <= 0) return current;
            float duration = illuminated ? closeSeconds : openSeconds;
            if (!Finite(duration)) return current;
            float step = elapsed / Math.Max(.01f, duration);
            return illuminated ? Math.Min(1, current + step) : Math.Max(0, current - step);
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
