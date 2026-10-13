using System;
namespace HauntedFish.BossFight
{
    // Shared by mouse/gamepad control and local practice; no Unity state or transport.
    public static class BossPaddleInputPolicy
    {
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        public static bool InputAllowed(bool blocked, float timeScale) => !blocked && Finite(timeScale) && timeScale > 0;
        public static bool OwnsPaddle(uint id, uint fish, uint ghost, bool active, bool ready) =>
            active && ready && id != 0 && fish != 0 && ghost != 0 && fish != ghost && (id == fish || id == ghost);
        public static bool TryAxis(float targetX, float targetZ, float positionX, float positionZ,
            float centreX, float centreZ, float halfX, float halfZ, float radius,
            float speed, float interval, bool ghost, out float axisX, out float axisZ)
        {
            axisX = axisZ = 0;
            if (!Finite(targetX) || !Finite(targetZ) || !Finite(positionX) || !Finite(positionZ) ||
                !Finite(centreX) || !Finite(centreZ) || !Finite(halfX) || !Finite(halfZ) ||
                !Finite(radius) || !Finite(speed) || !Finite(interval) || radius <= 0 || halfX <= radius ||
                halfZ <= radius + .55f || speed <= 0 || interval <= 0) return false;
            targetX = Math.Clamp(targetX, centreX - halfX + radius, centreX + halfX - radius);
            targetZ = Math.Clamp(targetZ, centreZ + (ghost ? .55f : -halfZ + radius), centreZ + (ghost ? halfZ - radius : -.55f));
            double dx = targetX - (double)positionX, dz = targetZ - (double)positionZ;
            double divisor = Math.Max(.001, speed * (double)interval);
            double length = Math.Sqrt(dx * dx + dz * dz);
            divisor = Math.Max(divisor, length);
            axisX = (float)(dx / divisor); axisZ = (float)(dz / divisor);
            return true;
        }
    }
}