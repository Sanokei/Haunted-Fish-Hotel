using System;
using HauntedFish.BossFight;
static class LifeStageTests
{
    static int checks;
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
    static void Main()
    {
        for(uint id=1;id<=100;id++)
        {
            var match=new BossMatch();Check(match.TryBegin(id,id+1000),"real match begins");
            Check(BossGhostLifeStage.VariantIndex(match.FishScore)==0&&BossGhostLifeStage.RemainingLives(match.FishScore)==3,"first life uses neutral v1");
            Check(match.Score(match.Epoch,true)&&match.Active,"first fish goal leaves real match active");
            Check(BossGhostLifeStage.VariantIndex(match.FishScore)==1&&BossGhostLifeStage.RemainingLives(match.FishScore)==2,"second life uses angry v2");
            Check(match.Score(match.Epoch,true)&&match.Active,"second fish goal leaves real match active");
            Check(BossGhostLifeStage.VariantIndex(match.FishScore)==2&&BossGhostLifeStage.RemainingLives(match.FishScore)==1,"third life uses god v3");
            Check(match.Score(match.Epoch,true)&&!match.Active,"third goal ends real match");
            Check(BossGhostLifeStage.VariantIndex(match.FishScore)==2&&BossGhostLifeStage.RemainingLives(match.FishScore)==0,"final loss stays v3 for terminal reaction");
            Check(!match.Score(match.Epoch,true),"duplicate collision cannot create fourth variant");
            Check(match.TryBegin(id,id+1000)&&BossGhostLifeStage.VariantIndex(match.FishScore)==0,"new match resets first-life art");
            Check(match.Score(match.Epoch,false)&&!match.Active&&BossGhostLifeStage.RemainingLives(match.FishScore)==3,"one ghost goal beats fish without consuming ghost lives");
            Check(match.TryBegin(id,id+1000)&&match.Score(match.Epoch,true)&&match.Cancel(id),"departure cancels second-life match");
            Check(match.TryBegin(id,id+1000)&&BossGhostLifeStage.VariantIndex(match.FishScore)==0,"cancelled reentry resets neutral variant");
        }
        Check(BossGhostLifeStage.VariantIndex(int.MinValue)==0&&BossGhostLifeStage.RemainingLives(int.MinValue)==3,"malformed negative score cannot select invalid art");
        Check(BossGhostLifeStage.VariantIndex(int.MaxValue)==2&&BossGhostLifeStage.RemainingLives(int.MaxValue)==0,"large score cannot overflow life selection");
        Console.WriteLine(checks+" prepared life-stage/match progression checks passed.");
    }
}