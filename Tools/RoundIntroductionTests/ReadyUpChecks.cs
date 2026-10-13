using System;
using HauntedFish.Multiplayer;
static class ReadyUpChecks
{
    static int count;
    static void Check(bool ok, string reason) { if (!ok) throw new Exception(reason); count++; }
    public static void Run()
    {
        for (int size = 1; size <= 16; size++)
        {
            var roster = new uint[size]; for (int i=0;i<size;i++) roster[i]=(uint)i+1;
            var gate = new GameReadyGate(); var cinematic = new GameRoundGate();
            gate.Begin(roster,"round-a",1); cinematic.Begin(roster,1,0);
            Check(!gate.Complete,"Cinematic creation never means ready");
            for (int i=0;i<size;i++)
            {
                uint id=roster[i];
                Check(!gate.Ready(id,id,"round-a",1),"Cannot ready before intro");
                Check(!cinematic.Finish(id,1,8.99f),"Early cinematic rejected");
                Check(cinematic.Finish(id,1,9),"Minimum cinematic accepted");
                Check(gate.FinishIntro(id,"round-a",1),"Verified intro recorded");
                Check(!gate.Ready(id,id+100,"round-a",1),"Another owner cannot ready participant");
                Check(!gate.Ready(id,id,"wrong-key",1),"Stale/wrong key rejected");
                Check(!gate.Ready(id,id,"round-a",0),"Stale version rejected");
                Check(gate.Ready(id,id,"round-a",1),"Deliberate participant ready accepted");
                Check(gate.Ready(id,id,"round-a",1) && gate.ReadyCount==i+1,"Duplicate ready is idempotent");
                Check(gate.Complete==(i==size-1),"Partial roster, including ghost, cannot start countdown");
            }
            Check(cinematic.Complete && gate.Complete,"Only all cinematic+ready can begin authoritative setup");
            Check(!gate.Ready(1000,1000,"round-a",1),"Nonparticipant rejected");
            gate.Join(1000,"round-a",1); cinematic.Join(1000,1,9);
            Check(!gate.Complete && !cinematic.Complete,"Late arrival has independent locked gate");
            Check(!gate.Ready(1000,1000,"round-a",1),"Late arrival must finish intro first");
            Check(cinematic.Finish(1000,1,18) && gate.FinishIntro(1000,"round-a",1) && gate.Ready(1000,1000,"round-a",1),"Late arrival completes both gates");
            gate.Remove(1000); cinematic.Remove(1000);
            Check(gate.Complete,"Disconnected newcomer removed without deadlock");
            gate.Remove(1); gate.Join(1,"round-a",1);
            Check(!gate.IsReady(1) && !gate.Complete,"Rejoin does not carry ready");
            gate.Begin(roster,"round-b",2);
            Check(gate.ReadyCount==0 && !gate.Complete,"Replay resets all readiness/countdown prerequisite");
            Check(!gate.FinishIntro(1,"round-a",1) && !gate.Ready(1,1,"round-a",1),"Old in-flight messages cannot unlock replay");
        }
        var empty=new GameReadyGate(); empty.Begin(Array.Empty<uint>(),"x",1); Check(!empty.Complete,"Empty roster never releases");
        var page=new RoundHowToGate(); int ready=0;
        for (int round=0;round<50;round++)
        {
            page.Begin(1,1,()=>ready++); Check(page.GhostPage,"Ghost sees ghost page from authoritative ID");
            Check(!page.Tick(100,false,1.5f),"Waiting never auto readies");
            Check(page.Tick(0,true,1.5f),"Deliberate ready after read beat");
            Check(!page.Tick(10,true,1.5f) && ready==round+1,"Single completion callback");
            page.Begin(1,2,()=>throw new Exception("Cancelled replay callback fired")); Check(!page.GhostPage,"Fish sees fish page");
            Check(!page.Tick(.5f,true,1.5f),"Early skip cannot bypass read beat");
            page.Cancel(); Check(!page.Tick(10,true,0),"Disconnect/disable cancel callback");
        }
        int previous=ready; page.Begin(1,2,()=>ready++); page.Begin(2,2,()=>ready+=2); page.Tick(2,true,1.5f);
        Check(ready==previous+2,"Replay retires old callback");
        page.Begin(1,2,null); Check(!page.Tick(float.NaN,true,1.5f),"Invalid delta cannot bypass read beat"); page.Cancel();
        Check(page.Submitted == false, "Cancel clears local pending submission");
        page.Begin(4, 5, null); page.Tick(2, true, 1.5f);
        Check(page.Submitted && !page.Showing, "Ready request is latched while the same page remains visible");
        page.Begin(4, 4, null);
        Check(!page.Submitted && page.GhostPage, "Replay resets pending state and changes role from current authoritative ID");
        Check(RoundHowToState.Status(false, false, false, false, false, true, 10, 1.5f) == RoundHowToStatus.WaitingForPlayer, "Disconnected page cannot ready");
        Check(RoundHowToState.Status(true, false, false, false, false, true, 10, 1.5f) == RoundHowToStatus.FinishingIntroduction, "Server cinematic acknowledgement is mandatory");
        Check(RoundHowToState.Status(true, true, false, false, false, true, 1.49f, 1.5f) == RoundHowToStatus.Reading, "Read beat disables the single Ready button");
        Check(RoundHowToState.Status(true, true, false, false, false, true, 1.5f, 1.5f) == RoundHowToStatus.CanReady, "Read boundary enables deliberate Ready");
        Check(RoundHowToState.Status(true, true, false, false, true, true, 10, 1.5f) == RoundHowToStatus.MenuOpen, "Open menu blocks ready input");
        Check(RoundHowToState.Status(true, true, false, false, false, false, 10, 1.5f) == RoundHowToStatus.MenuOpen, "Hidden canvas blocks ready input");
        Check(RoundHowToState.Status(true, true, false, true, false, true, 10, 1.5f) == RoundHowToStatus.SendingReady, "Submission awaits replicated acknowledgement");
        Check(RoundHowToState.Status(true, true, true, true, false, true, 10, 1.5f) == RoundHowToStatus.Ready, "Acknowledged ready stays visible for the remaining participants");
        Check(RoundHowToState.Status(true, true, false, false, false, true, float.NaN, 1.5f) == RoundHowToStatus.Reading, "Invalid elapsed time cannot unlock Ready");
        Check(RoundHowToState.Progress(1, 2) == "1 / 2 PLAYERS READY", "Live count does not imply all-ready for one ready member");
        Check(RoundHowToState.Progress(3, 2) == "2 / 2 PLAYERS READY", "Out-of-order count is bounded to roster size");
        Check(RoundHowToState.Progress(-1, -2) == "0 / 0 PLAYERS READY", "Invalid counters are bounded");
        float natural=GameplayPresentationPolicy.NaturalHallwayFishHeight(2,4.11f,486);
        Check(natural>230 && natural<240,"Current authored proportions enlarge fish naturally");
        Check(GameplayPresentationPolicy.NaturalHallwayFishHeight(1,4,400)==100,"Scaling comes from body/clearance ratio");
        Check(GameplayPresentationPolicy.NaturalHallwayFishHeight(float.NaN,4,400)==1,"Invalid scale bounded");
        Check(GameplayPresentationPolicy.NaturalHallwayFishHeight(20,4,400)==320,"Fish cannot exceed authored clearance");
        Console.WriteLine("Passed "+count+" explicit ready-up, role-page and natural scale checks.");
    }
}
