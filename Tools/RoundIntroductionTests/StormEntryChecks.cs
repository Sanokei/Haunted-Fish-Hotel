using System;
using HauntedFish.Multiplayer;
static class StormEntryChecks
{
    public static void Run()
    {
        int checks=0;
        void Check(bool ok,string message) { if(!ok) throw new Exception(message); checks++; }
        var state=new GameStormEntryState();
        for(int i=0;i<100;i++)
        {
            Check(state.TryEnter(false),"Ordinary Game entry starts once");
            Check(!state.TryEnter(false),"Repeated entry does not reset strike schedule");
            for(int tick=0;tick<10;tick++)
            {
                Check(!state.TryEnter(true),"Unrelated entry during Boss viewing cannot restart Game storm");
                Check(!state.TryResumeDeferredEntry(true),"Game entry latch stays silent during Boss viewing");
            }
            Check(!state.TryResumeDeferredEntry(false),"Normal Boss return remains Boss-owned, avoiding second resume");
            state.Exit();
            Check(state.TryEnter(true)==false && state.Entered,"New Game entry during Boss watching is deferred");
            Check(!state.TryEnter(false),"Enable/entry tick cannot bypass deferred interval");
            Check(!state.TryResumeDeferredEntry(true),"Deferred storm cannot start while viewing");
            Check(state.TryResumeDeferredEntry(false),"Deferred entry resumes exactly once on return");
            Check(!state.TryResumeDeferredEntry(false),"Return ticks cannot add duplicate timers");
            state.Exit(); state.TryEnter(true); state.Exit();
            Check(!state.TryResumeDeferredEntry(false),"Scene exit cancels deferred entry");
        }
        Console.WriteLine("Passed "+checks+" production storm entry/suspension/resume latch checks (no native audio/timer execution).");
    }
}
