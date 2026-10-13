using System;

namespace HauntedFish.Multiplayer
{
    // Horizontal state only. Grounding, jump acceptance and authority belong to
    // the existing motor; vertical velocity/gravity are deliberately unchanged.
    public sealed class RunningJumpState
    {
        int _LaunchDirection;
        public bool Active => _LaunchDirection != 0;

        public bool Begin(bool grounded, bool fish, bool runAtPress, float launchAxis, float configuredSpeed)
        {
            Clear();
            launchAxis = ClampAxis(launchAxis);
            if (!grounded || !fish || !runAtPress || Math.Abs(launchAxis) <= .01f ||
                !Finite(configuredSpeed) || configuredSpeed <= 0) return false;
            _LaunchDirection = Math.Sign(launchAxis);
            return true;
        }

        public float HorizontalVelocity(float axis, bool running, bool blocked,
            float walkingSpeed, float runningSpeed, float configuredSpeed)
        {
            axis = ClampAxis(axis);
            if (blocked || !running || Math.Abs(axis) <= .01f || Math.Sign(axis) != _LaunchDirection ||
                !Finite(configuredSpeed) || configuredSpeed <= runningSpeed) Clear();
            if (blocked) return 0;
            float speed = Active ? configuredSpeed : running ? runningSpeed : walkingSpeed;
            return Finite(speed) && speed >= 0 ? axis * speed : 0;
        }

        public void Clear() => _LaunchDirection = 0;
        public static float ClampAxis(float value) => Finite(value) ? Math.Max(-1, Math.Min(1, value)) : 0;
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
