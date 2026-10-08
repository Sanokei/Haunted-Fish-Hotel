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
            AutomaticReleaseAndCadence();
            ManualControlAndPause();
            PeriodicRuns();
            IntervalEdits();
            SpawnBacklogAndReset();
            InvalidTiming();
            WeightedFamilies();
            Console.WriteLine($"PASS: {_Checks} trap supply checks (production helpers, no Unity dependencies).");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    static ConveyorTiming Timing(ConveyorRunPolicy policy = ConveyorRunPolicy.AfterRoundRelease,
        float delay = 0, float interval = .5f, float duration = 1, float gap = 2)
        => new ConveyorTiming(policy, delay, interval, duration, gap);

    static void AutomaticReleaseAndCadence()
    {
        Check((int)ConveyorRunPolicy.AfterRoundRelease == 0 && (int)ConveyorRunPolicy.Periodic == 1 &&
            (int)ConveyorRunPolicy.Manual == 2, "Serialized run policy values remain stable");
        var schedule = new ConveyorSchedule();
        var timing = Timing(delay: 1);
        var step = schedule.Advance(false, false, .5f, timing);
        Check(!step.Running && !step.AdvancePackages && !step.Start.Started, "Release waits for the start delay");
        step = schedule.Advance(false, true, 20, timing);
        Check(!step.Running && !step.AdvancePackages, "Pause freezes a pending automatic start");
        step = schedule.Advance(false, false, .5f, timing);
        Check(step.Running && step.Start.Started && step.Start.SeedInitialPackages, "Delay boundary starts and seeds once");
        Near(step.ActiveSeconds, 0, "No motion is credited before the release boundary");
        Check(step.Spawns.Count == 0, "Initial seed does not also produce an interval spawn");
        step = schedule.Advance(true, false, .25f, timing);
        Near(step.ActiveSeconds, .25f, "Active run consumes the elapsed simulation time");
        Check(step.Spawns.Count == 0 && !step.Start.Started, "Cadence waits while a run continues");
        step = schedule.Advance(true, false, .25f, timing);
        Offsets(step, 0);
        step = schedule.Advance(true, false, 1.25f, timing);
        Offsets(step, .75f, .25f);
        Check(step.Running && !step.Start.SeedInitialPackages, "Release policy continues running without repeated seeds");

        schedule.Reset();
        step = schedule.Advance(false, false, 1.4f, timing);
        Near(step.ActiveSeconds, .4f, "A tick crossing the start delay moves only for its remaining time");
        Check(step.Spawns.Count == 0, "Crossing a delay retains the full spawn interval");
    }

    static void ManualControlAndPause()
    {
        var schedule = new ConveyorSchedule();
        var timing = Timing(ConveyorRunPolicy.Manual);
        var step = schedule.Advance(false, false, 100, timing);
        Check(!step.Running && !step.Start.Started, "Manual policy never starts automatically");
        var start = schedule.Start(false, false, .5f);
        Check(start.Started && start.SeedInitialPackages, "Explicit start seeds a stopped run");
        step = schedule.Advance(true, false, .2f, timing);
        Check(step.Spawns.Count == 0, "Manual start uses the configured cadence");
        Check(!schedule.Start(true, false, .5f).Started, "Starting an active run is idempotent");
        step = schedule.Advance(true, true, 10, timing);
        Check(step.Running && !step.AdvancePackages && step.Spawns.Count == 0, "Pause freezes motion and cadence");
        step = schedule.Advance(true, false, .3f, timing);
        Offsets(step, 0);

        // TrapManager's explicit Start while paused resets a run; Resume only unpauses it.
        start = schedule.Start(true, true, .5f);
        Check(start.Started && !start.SeedInitialPackages, "Starting a paused run restarts timing without duplicate seeds");
        step = schedule.Advance(true, false, .25f, timing);
        Check(step.Spawns.Count == 0, "Explicit restart resets the spawn countdown");

        schedule.SuppressAutomaticStarts();
        step = schedule.Advance(false, false, 100, Timing(ConveyorRunPolicy.Periodic));
        Check(!step.Running && !step.AdvancePackages, "Explicit stop suppresses periodic restarts");
        schedule.AllowAutomaticStarts();
        start = schedule.Start(false, false, .5f);
        Check(start.Started && !start.SeedInitialPackages, "Explicit restart clears suppression without replacing inventory");
    }

    static void PeriodicRuns()
    {
        var schedule = new ConveyorSchedule();
        var timing = Timing(ConveyorRunPolicy.Periodic, delay: .25f, interval: .25f, duration: .5f, gap: 1);
        var step = schedule.Advance(false, false, 1, timing);
        Check(step.Start.Started && step.Start.SeedInitialPackages && !step.Running, "One large tick starts and finishes the initial burst");
        Near(step.ActiveSeconds, .5f, "Periodic motion is capped at the run duration");
        Offsets(step, .25f, 0);
        step = schedule.Advance(false, false, .75f, timing);
        Check(!step.Running && !step.AdvancePackages, "Unused burst time is not credited toward the next gap");
        step = schedule.Advance(false, true, 10, timing);
        Check(!step.Running && !step.AdvancePackages, "Pause also freezes the periodic gap");
        step = schedule.Advance(false, false, .25f, timing);
        Check(step.Running && step.Start.Started && !step.Start.SeedInitialPackages, "Gap boundary restarts without seeding again");
        Near(step.ActiveSeconds, 0, "A gap-boundary restart begins without retroactive motion");
        step = schedule.Advance(true, false, .125f, timing);
        Check(step.Running && step.Spawns.Count == 0, "A periodic burst waits for its own cadence");
        step = schedule.Advance(true, true, 10, timing);
        Check(step.Running && !step.AdvancePackages, "Pause does not consume burst duration");
        step = schedule.Advance(true, false, .375f, timing);
        Check(!step.Running, "Resumed burst stops at its original duration boundary");
        Offsets(step, .25f, 0);

        // Zero gap still starts on the next tick, matching the scene's established policy.
        timing = Timing(ConveyorRunPolicy.Periodic, interval: .25f, duration: .5f, gap: 0);
        step = schedule.Advance(false, false, 0, timing);
        Check(step.Running && !step.Start.SeedInitialPackages, "A zero gap permits restart on the next simulation tick");
    }

    static void IntervalEdits()
    {
        var schedule = new ConveyorSchedule();
        schedule.Start(false, false, 1);
        var step = schedule.Advance(true, false, .25f, Timing(interval: 1));
        Check(step.Spawns.Count == 0, "Original interval is pending before edits");
        schedule.ClampSpawnDue(.25f);
        step = schedule.Advance(true, false, .25f, Timing(interval: .25f));
        Offsets(step, 0);
        step = schedule.Advance(true, false, .125f, Timing(interval: .25f));
        Check(step.Spawns.Count == 0, "Shorter interval starts the next countdown");
        schedule.ClampSpawnDue(2);
        step = schedule.Advance(true, false, .125f, Timing(interval: 2));
        Offsets(step, 0);
        step = schedule.Advance(true, false, 1, Timing(interval: 2));
        Check(step.Spawns.Count == 0, "A longer edit keeps the pending spawn and extends subsequent intervals");
    }

    static void SpawnBacklogAndReset()
    {
        var schedule = new ConveyorSchedule();
        schedule.Start(false, false, 1);
        var timing = Timing(interval: 1);
        var step = schedule.Advance(true, false, 130, timing);
        Check(step.Spawns.Count == 64, "A long stall is limited to 64 spawn attempts per tick");
        var offsets = Collect(step.Spawns);
        Near(offsets[0], 129, "The oldest delayed spawn receives its full elapsed time");
        Near(offsets[63], 66, "Capped backlog preserves spawn ordering");
        step = schedule.Advance(true, false, 0, timing);
        Check(step.Spawns.Count == 64, "A later tick can continue draining the retained backlog");
        offsets = Collect(step.Spawns);
        Near(offsets[0], 65, "Backlog resumes from the next unconsumed interval");
        Near(offsets[63], 2, "Backlog remains in chronological order");
        step = schedule.Advance(true, false, 0, timing);
        Offsets(step, 1, 0);
        step = schedule.Advance(true, false, .5f, timing);
        Check(step.Spawns.Count == 0, "Drained backlog resumes normal cadence");
        schedule.SuppressAutomaticStarts();
        schedule.Reset();
        step = schedule.Advance(false, false, 0, timing);
        Check(step.Running && step.Start.SeedInitialPackages && step.Spawns.Count == 0,
            "Round reset clears suppression, seed history and spawn backlog");
    }

    static void InvalidTiming()
    {
        foreach (float seconds in new[] { -1f, float.NaN, float.PositiveInfinity })
        {
            var schedule = new ConveyorSchedule();
            var step = schedule.Advance(false, false, seconds, Timing(delay: 1));
            Check(!step.Running && !step.AdvancePackages, "Invalid elapsed time cannot start or advance the schedule");
            step = schedule.Advance(false, false, .5f, Timing(delay: 1));
            Check(!step.Running, "Rejected elapsed time does not consume the start delay");
        }

        foreach (float interval in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            var schedule = new ConveyorSchedule();
            schedule.Start(false, false, interval);
            var step = schedule.Advance(true, false, .05f, Timing(interval: interval));
            Offsets(step, 0);
            Near(ConveyorSchedule.SafeInterval(interval), .05f, "Invalid cadence is bounded at fifty milliseconds");
        }

        var bounded = new ConveyorSchedule();
        var periodic = bounded.Advance(false, false, 1,
            Timing(ConveyorRunPolicy.Periodic, float.NaN, float.NaN, float.PositiveInfinity, float.NaN));
        Check(periodic.Start.SeedInitialPackages && !periodic.Running, "Invalid start delay and duration retain bounded periodic behavior");
        Near(periodic.ActiveSeconds, .05f, "Invalid burst duration uses the minimum interval");
        Offsets(periodic, 0);
        periodic = bounded.Advance(false, false, 0,
            Timing(ConveyorRunPolicy.Periodic, gap: float.NaN));
        Check(periodic.Running && !periodic.Start.SeedInitialPackages, "Invalid gap safely allows a subsequent restart");
    }

    static void WeightedFamilies()
    {
        var cart = new Family("cart", 5);
        var click = new Family("click", 2);
        var chandelier = new Family("chandelier", 3);
        var families = new[] { cart, click, chandelier };
        Check(Choose(families, -.1) == cart && Choose(families, 0) == cart, "Negative samples clamp to the first weighted entry");
        Check(Choose(families, .499) == cart && Choose(families, .5) == click, "A weight boundary belongs to the next entry");
        Check(Choose(families, .699) == click && Choose(families, .7) == chandelier, "Relative weights define contiguous sample ranges");
        Check(Choose(families, 1) == chandelier && Choose(families, 10) == chandelier, "Upper samples clamp inside the final weighted range");
        Check(Choose(families, double.NaN) == null && Choose(families, double.PositiveInfinity) == null &&
            Choose(families, double.NegativeInfinity) == null, "Nonfinite samples cannot select supplies");
        Check(Choose(Array.Empty<Family>(), .5) == null, "An empty catalog emits no supplies");
        var disabled = new[] { new Family("zero", 0), new Family("negative", -1), new Family("nan", float.NaN),
            new Family("infinite", float.PositiveInfinity), new Family("negative infinite", float.NegativeInfinity) };
        Check(Choose(disabled, .5) == null, "Nonpositive and nonfinite weights cannot supply a family");
        var mixed = new List<Family>(disabled) { cart };
        Check(Choose(mixed, 0) == cart && Choose(mixed, 1) == cart, "Disabled entries do not occupy any sample range");
        var large = new[] { new Family("first", float.MaxValue), new Family("second", float.MaxValue) };
        Check(Choose(large, .25) == large[0] && Choose(large, .75) == large[1], "Double accumulation avoids float weight overflow");

        int carts = 0, clicks = 0, chandeliers = 0;
        for (int i = 0; i < 10000; i++)
        {
            var chosen = Choose(families, (i + .5) / 10000);
            if (chosen == cart) carts++;
            else if (chosen == click) clicks++;
            else if (chosen == chandelier) chandeliers++;
        }
        Check(carts == 5000 && clicks == 2000 && chandeliers == 3000, "Weighted families occupy exactly their configured proportions");
    }

    static Family Choose(IEnumerable<Family> families, double sample)
        => WeightedSelection.Select(families, family => family.Weight, sample);

    sealed class Family
    {
        public readonly string Name;
        public readonly float Weight;
        public Family(string name, float weight) { Name = name; Weight = weight; }
    }

    static List<float> Collect(ConveyorSpawnBatch batch)
    {
        var result = new List<float>();
        foreach (float elapsed in batch)
            result.Add(elapsed);
        return result;
    }

    static void Offsets(ConveyorStep step, params float[] expected)
    {
        var actual = Collect(step.Spawns);
        Check(actual.Count == expected.Length && step.Spawns.Count == actual.Count, "Spawn count matches its enumerated due times");
        for (int i = 0; i < expected.Length; i++)
            Near(actual[i], expected[i], $"Spawn {i} uses the expected elapsed time");
    }

    static void Near(float actual, float expected, string label)
        => Check(Math.Abs(actual - expected) < .0001f, $"{label}: expected {expected}, got {actual}");

    static void Check(bool condition, string label)
    {
        _Checks++;
        if (!condition)
            throw new InvalidOperationException($"FAIL: {label}");
    }
}
