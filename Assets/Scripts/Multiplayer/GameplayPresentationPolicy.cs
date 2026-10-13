using System;
namespace HauntedFish.Multiplayer
{
    // Stateless presentation calculations. The scene and replicated player retain all timing/rules.
    public static class GameplayPresentationPolicy
    {
        public const float DefaultHallwayLookSeconds = 1.4f;
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static int CountdownSecond(bool introComplete, bool released, float remaining)
        {
            if (!introComplete || released || !Finite(remaining) || remaining <= 0 || remaining > 5) return 0;
            return (int)Math.Ceiling(remaining);
        }
        public static float DescentProgress(float elapsed, float duration, float amount)
        {
            if (!Finite(elapsed) || !Finite(duration) || !Finite(amount)) return 0;
            float t = duration > 0 ? Math.Max(0, Math.Min(1, elapsed / duration)) : 1;
            return t * t * amount;
        }
        public static bool HallwayFishFacesLeft(float elapsed, uint id)
        {
            if (!Finite(elapsed)) return false;
            return (int)Math.Floor((Math.Max(0, elapsed) + id % 7 * .37f) / 1.2f) % 2 != 0;
        }
        public static float HallwayFishLean(float elapsed, uint id)
            => Finite(elapsed) ? (float)Math.Sin(elapsed * 2.1f + id % 7) * 3 : 0;
        public static float HallwayFishX(float playerX, float minimum, float maximum, float width, float halfVisualWidth)
        {
            if (!Finite(playerX) || !Finite(minimum) || !Finite(maximum) || !Finite(width) || !Finite(halfVisualWidth) ||
                maximum <= minimum || width <= 0 || halfVisualWidth < 0 || halfVisualWidth * 2 >= width) return 0;
            float x = (Math.Max(minimum, Math.Min(maximum, playerX)) - minimum) / (maximum - minimum) * width - width * .5f;
            float edge = width * .5f - halfVisualWidth;
            return Math.Max(-edge, Math.Min(edge, x));
        }
        public static float NaturalHallwayFishHeight(float playerHeight, float clearance, float artworkClearance)
        {
            if (!Finite(playerHeight) || !Finite(clearance) || !Finite(artworkClearance) || playerHeight <= 0 || clearance <= 0 || artworkClearance <= 0) return 1;
            return artworkClearance * Math.Max(.1f, Math.Min(.8f, playerHeight / clearance));
        }
        public static bool ClipProjection(float x, float y, float depth, float near,
            float minX, float minY, float maxX, float maxY, out float clippedX, out float clippedY)
        {
            clippedX = clippedY = 0;
            if (!Finite(x) || !Finite(y) || !Finite(depth) || !Finite(near) || near < 0 || depth <= near ||
                !Finite(minX) || !Finite(minY) || !Finite(maxX) || !Finite(maxY) || minX >= maxX || minY >= maxY) return false;
            clippedX = Math.Max(minX, Math.Min(maxX, x));
            clippedY = Math.Max(minY, Math.Min(maxY, y));
            return true;
        }
    }
}