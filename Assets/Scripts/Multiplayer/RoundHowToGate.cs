using System;
namespace HauntedFish.Multiplayer
{
    // Presentation-only completion latch; never changes replicated round timing or movement.
    public sealed class RoundHowToGate
    {
        public bool Showing { get; private set; }
        public bool GhostPage { get; private set; }
        public bool Submitted { get; private set; }
        float _Elapsed;
        public float ReadSeconds => _Elapsed;
        Action _Finished;
        public void Begin(uint local, uint ghost, Action finished)
        {
            Cancel(); GhostPage = local == ghost; Showing = true; _Finished = finished;
        }
        public bool Tick(float delta, bool skip, float minimum)
        {
            if (!Showing) return false;
            if (!GameplayPresentationPolicy.Finite(delta) || delta < 0) delta = 0;
            _Elapsed += delta;
            minimum = GameplayPresentationPolicy.Finite(minimum) ? Math.Max(0, minimum) : 1.5f;
            if (!skip || _Elapsed < minimum) return false;
            var finished = _Finished;
            Cancel(); Submitted = true; finished?.Invoke(); return true;
        }
        public void Cancel() { Showing = false; Submitted = false; _Elapsed = 0; _Finished = null; }
    }
}
