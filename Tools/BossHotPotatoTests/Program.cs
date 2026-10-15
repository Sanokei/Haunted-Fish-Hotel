using System;
using System.Collections.Generic;
using System.Linq;
using HauntedFish.BossFight;

// Pure-rule checks for the hot-potato cue bomb and the twelve-slot defense catalog. No Unity dependency.
static class Program
{
    static int checks;
    static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception("FAILED: " + message);
    }
    static bool Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return true; } catch { return false; }
        return false;
    }
    static readonly BossDefenseFootprint[] None = new BossDefenseFootprint[0];

    static BossDefenseState NewState()
    {
        var state = new BossDefenseState(1, 10, 20) { Ready = true };
        return state;
    }

    static void Catalog()
    {
        Check(BossDefenseCatalog.KindCount == 6 && BossDefenseCatalog.SlotCount == 12, "six kinds per side, twelve slots");
        for (int slot = 0; slot < 12; slot++)
        {
            Check(BossDefenseCatalog.IsValidSlot(slot), "slot " + slot + " valid");
            Check(BossDefenseCatalog.IsFishSlot(slot) == (slot < 3 || slot >= 6 && slot < 9), "fish owns slots 0-2 and 6-8");
            Check((int)BossDefenseCatalog.KindOf(slot) == (slot < 6 ? slot % 3 : 3 + slot % 3), "kinds follow the original-then-new layout");
        }
        Check(!BossDefenseCatalog.IsValidSlot(-1) && !BossDefenseCatalog.IsValidSlot(12), "out of range slots rejected");
        // Older arena copies author only the first six slots; their indices and kinds must not move.
        Check(BossDefenseCatalog.OriginalSlotCount == 6, "six original slots");
        for (int slot = 0; slot < 6; slot++)
            Check((int)BossDefenseCatalog.KindOf(slot) == slot % 3 && BossDefenseCatalog.IsFishSlot(slot) == (slot < 3), "original slot " + slot + " keeps its meaning");
        // The original three pieces keep their exact health and footprints.
        Check(BossDefenseCatalog.MaxHealth(BossDefenseKind.Bottle) == 1 && BossDefenseCatalog.Radius(BossDefenseKind.Bottle) == .28f, "bottle unchanged");
        Check(BossDefenseCatalog.MaxHealth(BossDefenseKind.TunaCan) == 2 && BossDefenseCatalog.Radius(BossDefenseKind.TunaCan) == .40f, "tuna can unchanged");
        Check(BossDefenseCatalog.MaxHealth(BossDefenseKind.Building) == 3 && BossDefenseCatalog.Radius(BossDefenseKind.Building) == .45f, "building unchanged");
        foreach (BossDefenseKind kind in Enum.GetValues(typeof(BossDefenseKind)))
        {
            Check(BossDefenseCatalog.MaxHealth(kind) >= 1, kind + " has health");
            Check(BossDefenseCatalog.Radius(kind) > .2f && BossDefenseCatalog.Radius(kind) < .6f, kind + " footprint fits the table");
        }
        Check(BossDefenseCatalog.FuseShortening(BossDefenseKind.Crate) > 0, "dynamite crate burns the fuse");
        Check(Enum.GetValues(typeof(BossDefenseKind)).Cast<BossDefenseKind>().Where(k => BossDefenseCatalog.FuseShortening(k) > 0).Count() == 1, "only the crate burns the fuse");
    }

    static void State()
    {
        var state = NewState();
        for (int slot = 0; slot < 12; slot++)
        {
            var piece = state.Slot(slot);
            Check(piece.Owner == (BossDefenseCatalog.IsFishSlot(slot) ? 10u : 20u), "owner of slot " + slot);
            Check(piece.Kind == BossDefenseCatalog.KindOf(slot) && piece.Health == BossDefenseCatalog.MaxHealth(piece.Kind), "kind and health of slot " + slot);
            Check(!piece.Placed && !piece.Broken, "slot " + slot + " starts racked");
        }
        // Each side can stand all six pieces on its own half at once, in two rows of three.
        for (int side = 0; side < 2; side++)
        {
            uint actor = side == 0 ? 10u : 20u;
            for (int k = 0; k < 6; k++)
            {
                int slot = k < 3 ? side * 3 + k : 6 + side * 3 + (k - 3);
                float x = -1.5f + (k % 3) * 1.5f;
                float z = (k < 3 ? 1.5f : 2.7f) * (side == 0 ? -1 : 1);
                Check(state.TryPlace(actor, 1, slot, 1, x, z, None), "side " + side + " places kind " + k);
            }
        }
        Check(state.Revision == 12, "twelve placements, twelve revisions");
        Check(!state.TryPlace(10, 1, 3, 2, 0, -2, None) && !state.TryPlace(10, 1, 9, 2, 0, -2, None), "cannot move the opponent's piece");
        Check(!state.TryPlace(10, 1, 0, 1, 0, -2, None), "stale sequence rejected");
        Check(!state.TryPlace(10, 1, 12, 1, 0, -2, None) && !state.TryPlace(10, 1, -1, 1, 0, -2, None), "out of range slots rejected");
        Check(!state.TryPlace(10, 2, 0, 5, 0, -2, None), "wrong epoch rejected");

        // Placement respects the half and the table.
        var fresh = NewState();
        Check(!fresh.TryPlace(10, 1, 0, 1, 0, .8f, None), "fish cannot place on the ghost half");
        Check(!fresh.TryPlace(20, 1, 6, 1, 0, -.8f, None), "ghost cannot place on the fish half");
        Check(!fresh.TryPlace(10, 1, 0, 1, 2.95f, -2, None), "wall margin respected");
        var notReady = new BossDefenseState(1, 10, 20);
        Check(!notReady.TryPlace(10, 1, 0, 1, 0, -2, None), "placement waits for readiness");
        // Pieces cannot overlap, including the bigger new footprints.
        Check(fresh.TryPlace(10, 1, 7, 1, 0, -2, None), "ramp placed");
        Check(!fresh.TryPlace(10, 1, 6, 1, .3f, -2, None), "bumper cannot overlap ramp");
        Check(fresh.TryPlace(10, 1, 6, 1, 1.5f, -2, None), "bumper placed clear of the ramp");

        // Hits: new kinds take their authored health and die on the last point.
        var hit = NewState();
        hit.TryPlace(10, 1, 6, 1, 0, -2, None);   // bumper, 3 hp
        hit.TryPlace(10, 1, 8, 1, 1.5f, -2, None); // crate, 1 hp
        Check(hit.Hit(1, 6, 1, true) && hit.Slot(6).Health == 2, "bumper loses one health");
        Check(!hit.Hit(1, 6, 1, true), "same contact episode counts once");
        hit.ContactEnded(6, 1);
        Check(hit.Hit(1, 6, 2, true) && hit.Hit(1, 6, 3, true) && hit.Slot(6).Broken, "bumper breaks after three contacts");
        Check(hit.Hit(1, 8, 1, true) && hit.Slot(8).Broken, "crate breaks in one contact");
        Check(!hit.Hit(1, 8, 2, true), "broken pieces cannot be hit again");
        Check(!hit.Hit(1, 0, 1, true), "racked pieces cannot be hit");
        Check(!hit.Hit(1, 6, 9, false), "only verified cue contact counts");

        // Observed (client) state is bounded by each kind's own health.
        var observed = NewState();
        Check(observed.Observe(8, 1, true, 1, 0, 0) && !observed.Observe(8, 2, true, 1, 0, 0), "crate cannot report more than one health");
        Check(observed.Observe(9, 3, true, 1, 0, 0) && !observed.Observe(9, 4, true, 1, 0, 0), "ghost bumper bounded by three health");
        Check(!observed.Observe(12, 1, true, 1, 0, 0), "observe rejects slot 12");
    }

    static void Blast()
    {
        var state = NewState();
        state.TryPlace(10, 1, 0, 1, -1.0f, -1.5f, None);  // fish bottle
        state.TryPlace(10, 1, 2, 1, 1.0f, -1.5f, None);   // fish building (3 hp)
        state.TryPlace(20, 1, 3, 1, -1.0f, 1.5f, None);    // ghost bottle
        state.TryPlace(20, 1, 11, 1, 2.0f, 3.0f, None);    // ghost crate, far from the blast
        int damaged = state.ApplyBlast(0, 0, BossBlast.DefaultRadius);
        Check(damaged == 4 - 1, "blast reaches both sides' pieces but not the far crate");
        Check(state.Slot(0).Broken && state.Slot(3).Broken, "bottles die in the blast");
        Check(state.Slot(2).Health == 2, "building survives with one point lost");
        Check(state.Slot(11).Health == 1 && !state.Slot(11).Broken, "far crate untouched");
        Check(state.ApplyBlast(0, 0, BossBlast.DefaultRadius) == 1 && state.Slot(2).Health == 1, "broken pieces ignored on the next blast");
        uint revision = state.Revision;
        Check(state.ApplyBlast(100, 100, 1) == 0 && state.Revision == revision, "a miss changes nothing");
        Check(state.ApplyBlast(0, 0, 0) == 0 && state.ApplyBlast(0, 0, -1) == 0, "non-positive radius does nothing");
        Check(state.ApplyBlast(float.NaN, 0, 1) == 0 && state.ApplyBlast(0, float.PositiveInfinity, 1) == 0 && state.ApplyBlast(0, 0, float.NaN) == 0, "non-finite input does nothing");
        state.Ready = false;
        Check(state.ApplyBlast(0, 0, 10) == 0, "no blast damage while the arena is not live");
        state.Ready = true; state.Active = false;
        Check(state.ApplyBlast(0, 0, 10) == 0, "no blast damage after the match ends");
        // Reach includes the piece's own footprint: a piece whose edge is inside the radius is hit.
        var edge = NewState();
        edge.TryPlace(10, 1, 7, 1, 0, -2.0f, None);       // ramp radius .48
        Check(edge.ApplyBlast(0, 0, 1.6f) == 1, "edge of the footprint inside the radius is damaged");
        var outside = NewState();
        outside.TryPlace(10, 1, 7, 1, 0, -2.5f, None);
        Check(outside.ApplyBlast(0, 0, 1.9f) == 0, "footprint beyond the radius is safe");
    }

    static void Fuse()
    {
        Check(Throws<ArgumentOutOfRangeException>(() => new BossFuse(0, 0, 0)), "zero duration rejected");
        Check(Throws<ArgumentOutOfRangeException>(() => new BossFuse(float.NaN, 0, 0)), "NaN duration rejected");
        Check(Throws<ArgumentOutOfRangeException>(() => new BossFuse(5, -1, 0)), "negative penalty rejected");
        Check(Throws<ArgumentOutOfRangeException>(() => new BossFuse(5, 1, 6)), "minimum beyond the duration rejected");
        var fuse = new BossFuse(9, .6f, 1.5f);
        Check(!fuse.Armed && fuse.Remaining(100) == 0 && fuse.Heat(100) == 0 && !fuse.Due(100), "a new fuse is not lit");
        Check(!fuse.Strike(100, BossSide.Fish) && fuse.Strikes == 0, "striking a dead cue does nothing");
        fuse.Arm(100);
        Check(fuse.Armed && fuse.Remaining(100) == 9 && fuse.Heat(100) == 0, "lit fuse has its full time");
        Check(Math.Abs(fuse.Heat(104.5f) - .5f) < 1e-5f, "heat rises as the fuse burns");
        Check(!fuse.Due(108.99f) && fuse.Due(109), "due exactly at the deadline");
        Check(fuse.Remaining(120) == 0, "remaining never goes negative");
        Check(fuse.Strike(101, BossSide.Fish) && Math.Abs(fuse.Remaining(101) - 7.4f) < 1e-4f, "a strike burns the penalty off");
        Check(fuse.LastStriker == BossSide.Fish && fuse.Strikes == 1, "striker recorded");
        Check(fuse.Strike(102, BossSide.Ghost) && fuse.LastStriker == BossSide.Ghost && fuse.Strikes == 2, "the potato changes hands");
        Check(!fuse.Strike(102, BossSide.None), "a strike needs a side");
        Check(fuse.Shorten(102, 2.5f) && Math.Abs(fuse.Remaining(102) - 3.3f) < 1e-4f, "a dynamite crate burns 2.5 seconds");
        Check(!fuse.Shorten(102, 0) && !fuse.Shorten(102, -1) && !fuse.Shorten(102, float.NaN), "non-positive shortening ignored");
        Check(fuse.Shorten(102, 50) && Math.Abs(fuse.Remaining(102) - 1.5f) < 1e-4f, "never burns below the minimum");
        float before = fuse.Deadline;
        Check(fuse.Strike(102.2f, BossSide.Fish) && fuse.Deadline <= before, "a strike never extends the fuse");
        fuse.Arm(200);
        Check(fuse.Strikes == 0 && fuse.LastStriker == BossSide.None && fuse.Remaining(200) == 9, "re-arming resets everything");
        fuse.Disarm();
        Check(!fuse.Armed && fuse.Remaining(200) == 0 && !fuse.Due(1000) && !fuse.Shorten(200, 1), "disarmed fuse is inert");
        fuse.Arm(float.NaN);
        Check(!fuse.Armed, "a non-finite clock cannot light the fuse");

        // Below the minimum already: strikes must not push the deadline back out.
        var low = new BossFuse(9, .6f, 1.5f);
        low.Arm(0); low.Shorten(0, 50);
        float lowDeadline = low.Deadline;
        low.Strike(7.9f, BossSide.Fish);
        Check(low.Deadline == lowDeadline, "a late strike leaves an already-short fuse alone");

        // Randomised invariants: the deadline only ever moves earlier, remaining stays within [0, duration].
        var rng = new Random(11);
        for (int round = 0; round < 200; round++)
        {
            var f = new BossFuse(2 + (float)rng.NextDouble() * 10, (float)rng.NextDouble(), (float)rng.NextDouble() * 1.5f);
            float now = (float)rng.NextDouble() * 1000;
            f.Arm(now);
            float previous = f.Deadline;
            for (int step = 0; step < 40; step++)
            {
                now += (float)rng.NextDouble() * .5f;
                if (rng.Next(3) == 0) f.Shorten(now, (float)rng.NextDouble() * 3);
                else f.Strike(now, rng.Next(2) == 0 ? BossSide.Fish : BossSide.Ghost);
                Check(f.Deadline <= previous + 1e-4f, "deadline never moves later");
                Check(f.Remaining(now) >= 0 && f.Remaining(now) <= f.Duration + 1e-3f, "remaining stays within the duration");
                previous = f.Deadline;
            }
        }
    }

    static void Resolution()
    {
        var fishHalf = BossBlast.Resolve(-2, BossSide.Ghost);
        Check(fishHalf.Victim == BossSide.Fish && fishHalf.StunSeconds == BossBlast.DefaultStunSeconds, "blast on the fish half stuns the fish");
        var ghostHalf = BossBlast.Resolve(2, BossSide.Fish);
        Check(ghostHalf.Victim == BossSide.Ghost, "blast on the ghost half stuns the ghost");
        Check(BossBlast.Resolve(0, BossSide.Fish).Victim == BossSide.Fish && BossBlast.Resolve(.04f, BossSide.Ghost).Victim == BossSide.Ghost,
            "a cue on the centre line punishes whoever struck it last");
        Check(BossBlast.Resolve(0, BossSide.None).Victim == BossSide.None && BossBlast.Resolve(0, BossSide.None).StunSeconds == 0, "nobody to blame, nobody stunned");
        Check(Throws<ArgumentOutOfRangeException>(() => BossBlast.Resolve(float.NaN, BossSide.Fish)), "NaN position rejected");
        Check(BossBlast.Resolve(-2, BossSide.Fish, -5).StunSeconds == 0, "negative stun clamps to zero");

        var stun = new BossStunState();
        Check(!stun.IsStunned(BossSide.Fish, 0) && !stun.IsStunned(BossSide.None, 0), "nobody starts stunned");
        stun.Stun(BossSide.Fish, 10, 1.6f);
        Check(stun.IsStunned(BossSide.Fish, 11.5f) && !stun.IsStunned(BossSide.Fish, 11.6f), "stun lasts its duration");
        Check(!stun.IsStunned(BossSide.Ghost, 11), "stun is per side");
        Check(Math.Abs(stun.Remaining(BossSide.Fish, 10.6f) - 1.0f) < 1e-4f, "remaining stun reported");
        stun.Stun(BossSide.Fish, 10.5f, .1f);
        Check(stun.IsStunned(BossSide.Fish, 11.5f), "a shorter stun never cuts a longer one");
        stun.Stun(BossSide.Ghost, 0, 0); stun.Stun(BossSide.None, 0, 5); stun.Stun(BossSide.Ghost, float.NaN, 1);
        Check(!stun.IsStunned(BossSide.Ghost, 0.001f), "invalid stuns ignored");
        stun.Clear();
        Check(!stun.IsStunned(BossSide.Fish, 11), "clear releases both paddles");
    }

    static void Main()
    {
        Catalog();
        State();
        Blast();
        Fuse();
        Resolution();
        Console.WriteLine("Passed " + checks + " hot-potato checks.");
    }
}
