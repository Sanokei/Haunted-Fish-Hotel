namespace HauntedFish.BossFight
{
    public static class BossGhostLifeStage
    {
        public static int VariantIndex(int fishGoals)=>fishGoals<=0?0:fishGoals==1?1:2;
        public static int RemainingLives(int fishGoals)=>fishGoals<=0?BossMatch.FishGoalsToWin:fishGoals>=BossMatch.FishGoalsToWin?0:BossMatch.FishGoalsToWin-fishGoals;
    }
}