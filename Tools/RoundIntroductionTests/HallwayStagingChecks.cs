using System;
using System.Collections.Generic;
using HauntedFish.Multiplayer;
static class HallwayStagingChecks
{
    sealed class Visual { public uint Id; public bool Retired; }
    public static void Run()
    {
        int checks = 0;
        void Check(bool result, string message) { if (!result) throw new Exception(message); checks++; }
        var staging = new HallwayFishStaging<Visual>();
        var active = new HashSet<uint>();
        int created = 0, released = 0, requested = 0;
        Visual Create(uint id)
        {
            requested++;
            if (id == 99) return null;
            Check(active.Add(id), "Replay cannot create a second active copy of the same roster fish");
            created++;
            return new Visual { Id = id };
        }
        void Release(Visual visual)
        {
            Check(!visual.Retired && active.Remove(visual.Id), "Each owned copy retires once");
            visual.Retired = true;
            released++;
        }
        staging.Begin(new uint[] { 1, 1, 2, 99, 99 }, Create, Release);
        Check(staging.Visuals.Count == 2 && requested == 3, "Duplicate IDs and missing fish visuals are skipped");
        staging.End();
        Check(staging.Visuals.Count == 0 && active.Count == 0 && released == 2, "End retires every active copy");
        staging.End();
        Check(released == 2, "Repeated completion/cancellation is idempotent");
        for (int round = 0; round < 50; round++)
        {
            staging.Begin(new uint[] { 1, 2, 3, 3, 4 }, Create, Release);
            Check(staging.Visuals.Count == 4 && active.Count == 4, "Repeated transition has exactly one visual per roster ID");
            if (round % 3 == 0) { staging.End(); staging.End(); }
        }
        staging.End();
        Check(active.Count == 0 && released == created, "Replay and interruption leave no owned copies");
        staging.Begin(new uint[] { 99, 99 }, Create, Release);
        Check(staging.Visuals.Count == 0, "All-missing artwork remains safe and empty");
        staging.End();
        bool failed = false;
        try
        {
            staging.Begin(new uint[] { 1, 2, 3 }, id => id == 3 ? throw new InvalidOperationException("fixture creation failure") : Create(id), Release);
        }
        catch (InvalidOperationException) { failed = true; }
        Check(failed && active.Count == 0 && staging.Visuals.Count == 0 && released == created, "Partial construction failure cleans preceding copies");
        staging.Begin(new uint[] { 1, 2 }, Create, visual => { Release(visual); staging.End(); });
        staging.End();
        Check(active.Count == 0 && released == created, "Reentrant cleanup cannot double-release");
        staging.Begin(new uint[] { 1, 2 }, Create, visual => { Release(visual); if (visual.Id == 1) throw new InvalidOperationException("fixture release failure"); });
        bool releaseFailed = false;
        try { staging.End(); } catch (AggregateException error) { releaseFailed = error.InnerExceptions.Count == 1; }
        Check(releaseFailed && active.Count == 0 && staging.Visuals.Count == 0 && released == created, "One failing release cannot strand other copies");
        staging.End();
        Check(!GameplayPresentationPolicy.HallwayFishFacesLeft(0, 7) && GameplayPresentationPolicy.HallwayFishFacesLeft(1.3f, 7) &&
            !GameplayPresentationPolicy.HallwayFishFacesLeft(2.6f, 7), "A stationary hallway fish looks right, left, then right");
        for (uint id = 1; id <= 8; id++)
        {
            bool initial = GameplayPresentationPolicy.HallwayFishFacesLeft(0, id), turned = false;
            for (float time = .1f; time <= GameplayPresentationPolicy.DefaultHallwayLookSeconds; time += .1f)
                turned |= GameplayPresentationPolicy.HallwayFishFacesLeft(time, id) != initial;
            Check(turned, "Every staged fish has a visible turn during the default hallway beat");
        }
        for (int sample = 0; sample < 80; sample++)
        {
            float lean = GameplayPresentationPolicy.HallwayFishLean(sample * .1f, 2);
            Check(GameplayPresentationPolicy.Finite(lean) && Math.Abs(lean) <= 3, "Look-around lean stays small and finite");
        }
        Check(GameplayPresentationPolicy.HallwayFishLean(float.NaN, 1) == 0 && !GameplayPresentationPolicy.HallwayFishFacesLeft(float.PositiveInfinity, 1), "Invalid animation time yields a safe rest pose");
        const float wallWidth = 1641.12f;
        float first = GameplayPresentationPolicy.HallwayFishX(-10, -15, 15, wallWidth, 66);
        float next = GameplayPresentationPolicy.HallwayFishX(-8, -15, 15, wallWidth, 66);
        Check(first < next && first < 0 && next < 0, "Actual starting hallway positions map into the same left-side hallway order");
        Check(GameplayPresentationPolicy.HallwayFishX(-100, -15, 15, wallWidth, 66) >= -wallWidth / 2 + 66 &&
            GameplayPresentationPolicy.HallwayFishX(100, -15, 15, wallWidth, 66) <= wallWidth / 2 - 66, "Fish artwork stays inside authored hallway edges");
        Check(GameplayPresentationPolicy.HallwayFishX(0, 1, 1, wallWidth, 66) == 0 &&
            GameplayPresentationPolicy.HallwayFishX(float.NaN, -15, 15, wallWidth, 66) == 0, "Invalid geometry/position uses safe centered staging");
        Console.WriteLine("Passed " + checks + " hallway staging checks (production registry/look policy; no native Unity destruction/render simulation).");
    }
}