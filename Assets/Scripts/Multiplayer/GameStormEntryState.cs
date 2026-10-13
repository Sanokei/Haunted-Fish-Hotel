namespace HauntedFish.Multiplayer
{
    // Entry latch only; Boss owns ordinary suspension/resumption, Lightning owns the strike clock.
    public sealed class GameStormEntryState
    {
        public bool Entered { get; private set; }
        bool _PendingEntry;
        public bool TryEnter(bool bossWatching)
        {
            if (Entered) return false;
            Entered = true; _PendingEntry = bossWatching;
            return !bossWatching;
        }
        public bool TryResumeDeferredEntry(bool bossWatching)
        {
            if (!Entered || !_PendingEntry || bossWatching) return false;
            _PendingEntry = false; return true;
        }
        public void Exit() { Entered = _PendingEntry = false; }
    }
}
