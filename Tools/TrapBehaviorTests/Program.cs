using System;
using System.Collections.Generic;
using HauntedFish.Multiplayer;

internal static class Program
{
    static int _Checks;
    static int Main()
    {
        try
        {
            ImpactPolicies();
            ContactEpisodes();
            StunIntegration();
            CapsuleGeometry();
            PassageSpeeds();
            PassageLifetime();
            TickInvariance();
            InvalidInputs();
            Console.WriteLine($"PASS: {_Checks} trap behavior checks (production rules; no Unity, physics or Mirage execution).");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    static void Check(bool valid, string message)
    { _Checks++; if (!valid) throw new Exception("FAIL: " + message); }
    static void Near(float actual, float expected, string message, float tolerance = .0001f)
        => Check(Math.Abs(actual - expected) <= tolerance, message + $" (actual {actual}, expected {expected})");
    static List<TrapPassageInterval> Bodies(params float[] endpoints)
    {
        var result = new List<TrapPassageInterval>();
        for (int i = 0; i < endpoints.Length; i += 2) result.Add(new TrapPassageInterval(endpoints[i], endpoints[i + 1]));
        return result;
    }
    static void ImpactPolicies()
    {
        Check((int)TrapImpact.DamageOverTime == 0 && (int)TrapImpact.KnockbackStun == 1 && (int)TrapImpact.Lethal == 2, "Serialized impact identities are stable");
        Near(TrapImpactRules.Damage(TrapImpact.KnockbackStun, 50, 1), 0, "Tablecart never damages health");
        Near(TrapImpactRules.Damage(TrapImpact.DamageOverTime, 50, .1f), 5, "Existing damage cadence remains unchanged");
        Check(TrapImpactRules.Damage(TrapImpact.Lethal, 0, .001f) == float.MaxValue, "Angler kills at contact independent of tick length or damage-rate tuning");
        Check(TrapImpactRules.Damage(TrapImpact.DamageOverTime, float.MaxValue, 2) == float.MaxValue, "Large valid damage cannot overflow to infinity");
        Near(TrapImpactRules.Damage((TrapImpact)100, 50, 1), 0, "Unknown impact policy is inert");
    }
    static void ContactEpisodes()
    {
        var episodes = new TrapContactEpisodes<int>();
        Check(episodes.TryStrike(1), "First direct contact strikes");
        Check(!episodes.TryStrike(1), "Duplicate collider contact is deduplicated");
        for (int i = 0; i < 120; i++) { episodes.RetainContacts(id => true); Check(!episodes.TryStrike(1), "Pinned continuous contact does not repeatedly stun"); }
        Check(episodes.TryStrike(2), "Second fish gets its own strike");
        episodes.RetainContacts(id => id == 2);
        Check(episodes.TryStrike(1) && !episodes.TryStrike(2), "Re-entry permits one new hit without rearming another fish");
        episodes.Clear();
        Check(episodes.TryStrike(2), "Reset starts a new contact lifetime");
    }
    static void StunIntegration()
    {
        var stun = new TrapStunState();
        Check(stun.Apply(1, 1.2f, .2f, 1), "Valid tablecart strike is accepted");
        Check(stun.Active, "Input blocks immediately");
        var step = stun.Advance(.1f);
        Check(step.BlocksInput && stun.Active, "Stun blocks input while impulse moves");
        Near(step.HorizontalDisplacement, .9f, "Knockback decelerates during first half");
        step = stun.Advance(.1f);
        Near(step.HorizontalDisplacement, .3f, "Knockback finishes its authored distance");
        step = stun.Advance(.6f);
        Check(step.BlocksInput && stun.Active, "Stun outlasts knockback");
        Near(step.HorizontalDisplacement, 0, "Impulse does not continue drifting");
        step = stun.Advance(.21f);
        Check(step.BlocksInput && !stun.Active && !stun.Advance(.01f).BlocksInput, "Stun expires and next tick accepts input");
        Check(stun.Apply(-1, 1.2f, .2f, .1f), "Opposite-direction hit is accepted");
        Near(stun.Advance(1).HorizontalDisplacement, -1.2f, "Short stun clamps impulse time but preserves distance");
        stun.Apply(1, 2, .5f, 2);
        stun.Advance(.2f);
        stun.Apply(-1, 1, .2f, .2f);
        Near(stun.Advance(.2f).HorizontalDisplacement, -1, "New independent strike replaces impulse direction");
        Check(stun.Active, "A short new strike never shortens an existing longer stun");
        stun.Clear();
        Check(!stun.Active && !stun.Advance(10).BlocksInput, "Death/teleport/round reset clears stun and pending impulse");
        Near(stun.Advance(10).HorizontalDisplacement, 0, "Cleared knockback cannot replay");
        stun.Apply(1, 1.2f, .2f, 1);
        var paused = stun.Advance(100, true);
        Check(paused.BlocksInput && stun.Active, "Global movement/dialogue lock pauses the stun clock");
        Near(paused.HorizontalDisplacement, 0, "Global movement/dialogue lock prevents knockback");
        Near(stun.Advance(.2f).HorizontalDisplacement, 1.2f, "Unlock resumes the complete stored impulse");
        Check(stun.Active, "Paused time cannot silently consume stun");
    }
    static void CapsuleGeometry()
    {
        Check(TrapPassageMotion.TryInterval(-1, 1, 0, 0, 5, .5f, out var body), "Standing capsule has a horizontal passage");
        Near(body.Start, 3.5f, "Entry includes the capsule's round front");
        Near(body.End, 6.5f, "Exit includes full trap and capsule widths");
        Check(!TrapPassageMotion.TryInterval(-1, 1, .51f, 0, 5, .5f, out _), "Jump fully clearing the trap is safe");
        Check(!TrapPassageMotion.TryInterval(-1, 1, .4f, .4f, 5, .5f, out _), "Empty capsule bounding-box corner is safe");
        Check(TrapPassageMotion.TryInterval(-1, 1, .3f, 0, 5, .5f, out body), "Partial airborne contact retains true capsule shape");
        Near(body.Start, 3.6f, "Vertical gap narrows horizontal contact radius");
        Near(body.End, 6.4f, "No contact margin is added upward into jump clearance");
        var reverse = body.Offset(10).Along(20, -1);
        Near(reverse.Start, 3.6f, "Leftward sweep mirrors world-space entry");
        Near(reverse.End, 6.4f, "Leftward sweep mirrors world-space exit");
    }
    static void PassageSpeeds()
    {
        Near(TrapPassageMotion.Travel(1, 10, 100, .25f, null), 10, "Empty corridor uses full speed");
        Near(TrapPassageMotion.Travel(1, 10, 4, .25f, null), 4, "Solid/room/grace distance caps horizontal travel");
        Near(TrapPassageMotion.Travel(.5f, 10, 100, .5f, Bodies(2, 4)), 3.5f, "One tick integrates fast approach then actual slow overlap");
        Near(TrapPassageMotion.Travel(1, 10, 100, .5f, Bodies(2, 4)), 8, "Angler resumes full speed after crossing a body");
        Near(TrapPassageMotion.Travel(1, 10, 100, .5f, Bodies(2, 4, 6, 8)), 7, "Angler continues through first victim into a second victim");
        Near(TrapPassageMotion.Travel(2, 10, 100, .5f, Bodies(2, 4, 6, 8, 10, 12)), 14, "A large step crosses every victim rather than stopping at the first");
        Near(TrapPassageMotion.Travel(1, 10, 100, .5f, Bodies(2, 5, 3, 6)), 6, "Clustered fish slow by their interval union");
        Near(TrapPassageMotion.Travel(1, 10, 100, .5f, Bodies(3, 6, 2, 5)), 6, "Victim registration order does not change travel");
        Near(TrapPassageMotion.Travel(1, 10, 100, .5f, Bodies(2, 4, 2, 4, 2, 4)), 8, "Multi-collider duplicate intervals never multiply slowdown");
        Near(TrapPassageMotion.Travel(.5f, 10, 100, .5f, Bodies(-2, 2)), 3, "Starting inside a body slows until its exit only");
        Near(TrapPassageMotion.Travel(1, 10, 100, .5f, Bodies(-3, -1)), 10, "Already crossed bodies cannot slow later movement");
        Near(TrapPassageMotion.Travel(1, 10, 100, .5f, Bodies(20, 30)), 10, "Unreached bodies do not slow the approach");
        Near(TrapPassageMotion.Travel(1, 10, 100, .5f, Bodies(2, 2)), 10, "Zero-width tangency consumes no extra time");
        Near(TrapPassageMotion.Travel(1, 10, 100, 1, Bodies(2, 4)), 10, "Full factor disables slowdown without changing hits");
    }
    static void PassageLifetime()
    {
        var memory = new TrapPassageMemory<string>();
        var original = Bodies(2, 4, 5, 6);
        Check(memory.Remember("victim", original), "First lethal contact remembers original body spans");
        Check(!memory.Remember("victim", Bodies(100, 200)), "Duplicate hit cannot change stored body geometry");
        original.Clear(); // Corresponds to a destroyed/disabled physics body.
        var intervals = new List<TrapPassageInterval>();
        memory.AppendIntervals(intervals, 3, 1);
        Near(TrapPassageMotion.Travel(.2f, 10, 10, .5f, intervals), 1, "Dead body's original span still slows traversal");
        memory.RetireExited(4.5f, 1);
        Check(memory.Contains("victim"), "Compound-body gap retains the later body part");
        memory.RetireExited(6, 1);
        Check(!memory.Contains("victim") && memory.Count == 0, "Exited corpse is cleaned up immediately");
        memory.Remember("left victim", Bodies(2, 4));
        intervals.Clear(); memory.AppendIntervals(intervals, 3, -1);
        Near(TrapPassageMotion.Travel(.2f, 10, 10, .5f, intervals), 1, "Reversing while inside retains slowdown");
        memory.RetireExited(2, -1);
        Check(memory.Count == 0, "Leftward exit cleans up a remembered victim");
        memory.Remember("old round", Bodies(2, 4)); memory.Clear();
        Check(memory.Count == 0, "Trap reset cannot keep prior-round victim geometry");
    }
    static void TickInvariance()
    {
        var bodies = Bodies(2, 4, 3, 5, 7, 8, 12, 15);
        float whole = TrapPassageMotion.Travel(2, 14, 100, .25f, bodies);
        foreach (int steps in new[] { 2, 10, 40, 120 })
        {
            float position = 0;
            for (int i = 0; i < steps; i++)
            {
                var shifted = new List<TrapPassageInterval>();
                foreach (var interval in bodies) shifted.Add(interval.Along(position, 1));
                position += TrapPassageMotion.Travel(2f / steps, 14, 100 - position, .25f, shifted);
            }
            Near(position, whole, "Body traversal is independent of simulation partitioning", .0002f);
            var stun = new TrapStunState(); stun.Apply(1, 2, .22f, 1);
            float moved = 0;
            for (int i = 0; i < steps; i++) moved += stun.Advance(2f / steps).HorizontalDisplacement;
            Near(moved, 2, "Knockback is independent of simulation partitioning");
            Check(!stun.Active, "Stun duration expires at each tick rate");
        }
    }
    static void InvalidInputs()
    {
        foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1, 0 })
        {
            Near(TrapImpactRules.Damage(TrapImpact.Lethal, 1, value), 0, "Invalid damage time is inert");
            Near(TrapPassageMotion.Travel(value, 10, 100, .25f, null), 0, "Invalid passage time is inert");
            Near(TrapPassageMotion.Travel(1, value, 100, .25f, null), 0, "Invalid passage speed is inert");
            Near(TrapPassageMotion.Travel(1, 10, value, .25f, null), 0, "Invalid travel limit is inert");
            Near(TrapPassageMotion.Travel(1, 10, 100, value, null), 0, "Invalid slowdown factor is inert");
            var stun = new TrapStunState();
            Check(!stun.Apply(1, 1, .2f, value) && !stun.Active, "Invalid stun duration is rejected");
            stun.Apply(1, 1, .2f, 1);
            Near(stun.Advance(value).HorizontalDisplacement, 0, "Invalid delta never mutates stun impulse");
            Near(stun.Advance(.2f).HorizontalDisplacement, 1, "Valid tick still delivers the impulse after invalid input");
        }
        Near(TrapPassageMotion.Travel(1, 10, 100, 2, null), 0, "Acceleration masquerading as slowdown is rejected");
        Near(TrapPassageMotion.Travel(1, 10, 100, .25f, Bodies(float.NaN, 5, 10, 2)), 10, "Malformed contact intervals are ignored");
        Check(!TrapPassageMotion.TryInterval(-1, 1, float.NaN, 0, 5, 1, out _), "Invalid capsule geometry is rejected");
        var memory = new TrapPassageMemory<int>();
        Check(!memory.Remember(1, Bodies(2, float.PositiveInfinity)), "Invalid remembered bodies cannot leak forever");
    }
}
