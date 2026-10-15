using System;

namespace HauntedFish.BossFight
{
    // Where a detonation lands and who pays for it. Pure rules; the arena applies the outcome.
    public readonly struct BossBlastOutcome
    {
        public readonly BossSide Victim;   // the side whose half of the table the cue was on
        public readonly float StunSeconds;
        public BossBlastOutcome(BossSide victim, float stun) { Victim = victim; StunSeconds = stun; }
    }

    public static class BossBlast
    {
        public const float DefaultRadius = 1.9f;
        public const float DefaultStunSeconds = 1.6f;

        // Fish defend z < 0 and the ghost defends z > 0. A cue exactly on the line belongs to whoever struck it last:
        // you were holding the potato when it went off.
        public static BossBlastOutcome Resolve(float cueZ, BossSide lastStriker, float stunSeconds = DefaultStunSeconds)
        {
            if (float.IsNaN(cueZ) || float.IsInfinity(cueZ)) throw new ArgumentOutOfRangeException(nameof(cueZ));
            BossSide victim = cueZ < -.05f ? BossSide.Fish : cueZ > .05f ? BossSide.Ghost : lastStriker;
            return new BossBlastOutcome(victim, victim == BossSide.None ? 0 : Math.Max(0, stunSeconds));
        }
    }
}
