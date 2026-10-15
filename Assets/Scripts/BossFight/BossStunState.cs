using System;

namespace HauntedFish.BossFight
{
    // Per-side paddle stun with injected time: callers ask "is this side stunned now?" when input arrives,
    // so no component has to tick a countdown.
    public sealed class BossStunState
    {
        float _FishUntil, _GhostUntil;

        public void Stun(BossSide side, float now, float seconds)
        {
            if (side == BossSide.None || float.IsNaN(now) || float.IsNaN(seconds) || seconds <= 0) return;
            float until = now + seconds;
            if (side == BossSide.Fish) _FishUntil = Math.Max(_FishUntil, until);
            else _GhostUntil = Math.Max(_GhostUntil, until);
        }

        public bool IsStunned(BossSide side, float now) =>
            side == BossSide.Fish ? now < _FishUntil : side == BossSide.Ghost && now < _GhostUntil;

        public float Remaining(BossSide side, float now) =>
            Math.Max(0, (side == BossSide.Fish ? _FishUntil : side == BossSide.Ghost ? _GhostUntil : 0) - now);

        public void Clear() { _FishUntil = _GhostUntil = 0; }
    }
}
