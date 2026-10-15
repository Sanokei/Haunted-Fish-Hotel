namespace HauntedFish.BossFight
{
    // Pure per-kind rules for the pieces each side can place. Every contestant owns one of each of six kinds.
    // Slot layout keeps the original six indices intact so older arena copies still bind:
    //   0-2 fish bottle/tuna/building    3-5 ghost bottle/tuna/building
    //   6-8 fish bumper/ramp/crate       9-11 ghost bumper/ramp/crate
    public static class BossDefenseCatalog
    {
        public const int KindCount = 6;
        public const int SlotCount = KindCount * 2;
        public const int OriginalSlotCount = 6;

        public static bool IsValidSlot(int slot) => slot >= 0 && slot < SlotCount;
        public static bool IsFishSlot(int slot) => (slot / 3) % 2 == 0;
        public static BossDefenseKind KindOf(int slot) => (BossDefenseKind)((slot / OriginalSlotCount) * 3 + slot % 3);

        // Hits the piece survives. Soft pieces die in one touch; the building and bumper are the stubborn ones.
        public static int MaxHealth(BossDefenseKind kind)
        {
            switch (kind)
            {
                case BossDefenseKind.Bottle: return 1;
                case BossDefenseKind.TunaCan: return 2;
                case BossDefenseKind.Building: return 3;
                case BossDefenseKind.Bumper: return 3;
                case BossDefenseKind.Ramp: return 2;
                default: return 1; // Dynamite crate
            }
        }

        // Footprint radius in table metres, used for placement overlap and blast reach.
        public static float Radius(BossDefenseKind kind)
        {
            switch (kind)
            {
                case BossDefenseKind.Bottle: return .28f;
                case BossDefenseKind.TunaCan: return .40f;
                case BossDefenseKind.Building: return .45f;
                case BossDefenseKind.Bumper: return .42f;
                case BossDefenseKind.Ramp: return .48f;
                default: return .34f;
            }
        }

        // A struck dynamite crate burns the cue bomb's fuse down by this much (seconds). Other kinds do nothing to it.
        public static float FuseShortening(BossDefenseKind kind) => kind == BossDefenseKind.Crate ? 2.5f : 0f;
    }
}
