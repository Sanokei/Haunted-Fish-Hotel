using System;
using HauntedFish.BossFight;
static class BossPerspectiveFramingTests
{
    public static void Run(Action<bool,string> check)
    {
        foreach(float aspect in new[]{9f/16,3f/4,1,4f/3,16f/9,21f/9,32f/9})
        foreach(float pitch in new[]{35f,50f})
        {
            check(BossPerspectiveFraming.RequiredDistance(55,aspect,.1f,3.65f,-.4f,.3f,5.8f,pitch,out float d),"perspective fit valid "+aspect+" pitch "+pitch);
            foreach(bool ghost in new[]{false,true})foreach(float x in new[]{-3.65f,3.65f})foreach(float y in new[]{-.4f,.3f})foreach(float z in new[]{-5.8f,5.8f})
                check(BossPerspectiveFraming.CornerVisible(ghost?-x:x,y,ghost?-z:z,d,pitch,55,aspect,.1f,d+35),"full playable board/goal bounds inside perspective frustum from "+(ghost?"ghost":"fish")+" end aspect "+aspect);
        }
        check(!BossPerspectiveFraming.RequiredDistance(55,0,.1f,3.65f,-.4f,.3f,5.8f,35,out _),"invalid aspect refused");
        check(!BossPerspectiveFraming.RequiredDistance(float.NaN,1,.1f,3.65f,-.4f,.3f,5.8f,35,out _),"invalid FOV refused");
        check(!BossPerspectiveFraming.RequiredDistance(55,1,.1f,3.65f,-.4f,.3f,5.8f,0,out _),"level camera pitch refused");
        check(BossMatch.FishGoalsToWin==3&&BossMatch.GhostGoalsToWin==1,"three ghost lives and one fish life");
    }
}