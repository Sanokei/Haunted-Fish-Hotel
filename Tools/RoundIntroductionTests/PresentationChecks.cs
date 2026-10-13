using System;
using HauntedFish.Multiplayer;
static class PresentationChecks
{
    public static int Run()
    {
        int count = 0;
        void Check(bool result, string message) { if (!result) throw new Exception(message); count++; }
        int Number(float time, bool intro = true, bool released = false) => GameplayPresentationPolicy.CountdownSecond(intro, released, time);
        Check(Number(5) == 5, "Countdown begins at authoritative five seconds");
        Check(Number(4.001f) == 5 && Number(4) == 4 && Number(3.999f) == 4, "Countdown rounds up across four-second boundary");
        Check(Number(1.001f) == 2 && Number(1) == 1 && Number(.001f) == 1, "Countdown preserves one until authoritative zero");
        Check(Number(0) == 0 && Number(-1) == 0, "Zero and expired deadlines hide countdown");
        Check(Number(5.001f) == 0 && Number(10) == 0, "Earlier setup does not show a spurious six");
        Check(Number(4, false) == 0 && Number(4, true, true) == 0, "Before intro and after release hide countdown");
        Check(Number(float.NaN) == 0 && Number(float.PositiveInfinity) == 0 && Number(float.NegativeInfinity) == 0, "Invalid replicated times hide countdown");
        const float duration = .45f;
        float CameraY(float elapsed) => -2160 * GameplayPresentationPolicy.DescentProgress(elapsed, duration, 1);
        Check(CameraY(0) == 0 && CameraY(duration) == -2160, "Descent covers authored downward distance");
        float early = CameraY(.11f) - CameraY(.10f);
        float late = CameraY(duration) - CameraY(duration - .01f);
        Check(early < 0 && late < early && late < -90, "Downward velocity accelerates and remains nonzero at cut");
        foreach (float fps in new[] { 24f, 30f, 60f, 144f })
        {
            float previous = 0, priorVelocity = 0, elapsed = 1 / fps;
            int frames = 0;
            while (elapsed < duration)
            {
                float position = CameraY(elapsed);
                float velocity = (position - previous) * fps;
                Check(velocity < 0 && (frames == 0 || velocity < priorVelocity), "Sampled descent never slows/stops before cut at " + fps + " FPS");
                previous = position; priorVelocity = velocity; elapsed += 1 / fps; frames++;
            }
            Check(previous > -2160 && priorVelocity < 0 && elapsed >= duration, "Final displayed sample moves, then duration cuts without a held bottom pose");
        }
        Check(GameplayPresentationPolicy.DescentProgress(-1, duration, 1) == 0 && GameplayPresentationPolicy.DescentProgress(1, duration, 1) == 1, "Curve clamps sampling time");
        bool Clip(float x, float y, float z, out float cx, out float cy) => GameplayPresentationPolicy.ClipProjection(x, y, z, .3f, 0, 0, 1280, 720, out cx, out cy);
        Check(Clip(300, 400, 10, out float px, out float py) && px == 300 && py == 400, "Visible projections retain screen position");
        Check(!Clip(300, 400, -1, out _, out _) && !Clip(300, 400, 0, out _, out _), "Behind-camera projections reject overlay");
        Check(!Clip(300, 400, .3f, out _, out _) && !Clip(300, 400, .299f, out _, out _), "Near-plane and closer corners reject overlay");
        Check(Clip(float.MaxValue, -float.MaxValue, .301f, out px, out py) && px == 1280 && py == 0, "Extreme near-plane coordinates stay inside viewport");
        Check(!Clip(float.NaN, 0, 1, out _, out _) && !Clip(0, float.PositiveInfinity, 1, out _, out _) && !Clip(0, 0, float.NaN, out _, out _), "Nonfinite projections reject overlay");
        Check(!GameplayPresentationPolicy.ClipProjection(0, 0, 1, .3f, 0, 0, 0, 720, out _, out _), "Degenerate viewport rejects overlay");
        Console.WriteLine("Passed " + count + " focused presentation boundary checks (pure policy; no Unity rendering/lifecycle simulation).");
        return count;
    }
}