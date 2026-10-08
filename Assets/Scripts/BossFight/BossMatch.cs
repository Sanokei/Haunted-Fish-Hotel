using System;

namespace HauntedFish.BossFight
{
    public readonly struct BossMatchResult
    {
        public readonly uint Epoch, FishId, GhostId;
        public readonly bool WinnerFish, Cancelled;
        public BossMatchResult(uint epoch, uint fish, uint ghost, bool win, bool cancel)
        {
            Epoch = epoch;
            FishId = fish;
            GhostId = ghost;
            WinnerFish = win;
            Cancelled = cancel;
        }
    }

    public sealed class BossMatch
    {
        public uint Epoch { get; private set; }
        public uint FishId { get; private set; }
        public uint GhostId { get; private set; }
        public int FishScore { get; private set; }
        public int GhostScore { get; private set; }
        public bool Active { get; private set; }
        public bool ResultIssued { get; private set; }

        public event Action<BossMatchResult> Completed;
        public bool TryBegin(uint fish, uint ghost)
        {
            if (Active || fish == 0 || ghost == 0 || fish == ghost)
                return false;
            Epoch++;
            FishId = fish;
            GhostId = ghost;
            FishScore = GhostScore = 0;
            ResultIssued = false;
            Active = true;
            return true;
        }

        public bool Score(uint epoch, bool fish)
        {
            if (!Active || ResultIssued || epoch != Epoch)
                return false;
            if (fish)
                FishScore++;
            else
                GhostScore++;
            if (FishScore >= 1 || GhostScore >= 2)
                Finish(FishScore >= 1, false);
            return true;
        }

        public bool Cancel(uint id)
        {
            if (!Active || (id != FishId && id != GhostId))
                return false;
            Finish(false, true);
            return true;
        }

        void Finish(bool win, bool cancel)
        {
            Active = false;
            ResultIssued = true;
            Completed?.Invoke(new BossMatchResult(Epoch, FishId, GhostId, win, cancel));
        }
    }
}
