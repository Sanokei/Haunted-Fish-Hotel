namespace HauntedFish.BossFight
{
    // Show the opponent's lives: fish goals remove boss lives; ghost goals remove fish lives.
    public readonly struct BossLivesState
    {
        public readonly bool Visible;
        public readonly int Remaining;
        BossLivesState(bool visible, int remaining) { Visible=visible; Remaining=remaining; }

        public static BossLivesState ForViewer(BossViewerSide side, bool watching, bool hasSnapshot,
            bool cancelled, int fishScore, int ghostScore)
        {
            // Spectators use their existing overview camera and have no opponent life row.
            if (!watching || !hasSnapshot || cancelled || side==BossViewerSide.Spectator)
                return new BossLivesState(false,0);
            if (side==BossViewerSide.Fish)
                return new BossLivesState(true,BossGhostLifeStage.RemainingLives(fishScore));
            if (side==BossViewerSide.Ghost)
                return new BossLivesState(true,ghostScore<=0?BossMatch.GhostGoalsToWin:0);
            return new BossLivesState(false,0);
        }
    }
}
