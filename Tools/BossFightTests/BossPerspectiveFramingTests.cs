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
        OffsetFraming(check);
        check(!BossPerspectiveFraming.RequiredDistance(55,0,.1f,3.65f,-.4f,.3f,5.8f,35,out _),"invalid aspect refused");
        check(!BossPerspectiveFraming.RequiredDistance(float.NaN,1,.1f,3.65f,-.4f,.3f,5.8f,35,out _),"invalid FOV refused");
        check(!BossPerspectiveFraming.RequiredDistance(55,1,.1f,3.65f,-.4f,.3f,5.8f,0,out _),"level camera pitch refused");
        check(BossMatch.FishGoalsToWin==3&&BossMatch.GhostGoalsToWin==1,"three ghost lives and one fish life");
    }
    static void OffsetFraming(Action<bool,string> check)
    {
        // Actual fish-view authored board, tray and head envelope, at the lowered 12-degree pitch.
        foreach(float aspect in new[]{9f/16,3f/4,1,4f/3,16f/9,21f/9,32f/9})
        {
            var right=new float[16];var up=new float[16];var depth=new float[16];int i=0;
            double sin=Math.Sin(12*Math.PI/180),cos=Math.Cos(12*Math.PI/180);
            Action<float,float,float> add=(x,y,z)=>{right[i]=x;up[i]=(float)(y*cos+z*sin);depth[i]=(float)(-y*sin+z*cos);i++;};
            foreach(float x in new[]{-3.65f,3.65f})foreach(float y in new[]{-.8f,.7f})foreach(float z in new[]{-6.325f,6.325f})add(x,y,z);
            foreach(float x in new[]{-5.05f,5.05f})foreach(float z in new[]{-4.2f,4.2f})add(x,1.45f,z);
            foreach(float x in new[]{-5.8f,5.8f})foreach(float y in new[]{-2.1f,9f})add(x,y,13f);
            CheckFit(check,55,aspect,.1f,right,up,depth,i);
        }
        var random=new Random(131026);
        for(int sample=0;sample<500;sample++)
        {
            int count=1+random.Next(40);var right=new float[count];var up=new float[count];var depth=new float[count];
            for(int i=0;i<count;i++){right[i]=(float)(random.NextDouble()*60-30);up[i]=(float)(random.NextDouble()*50-15);depth[i]=(float)(random.NextDouble()*50-25);}
            CheckFit(check,20+(float)random.NextDouble()*80,.1f+(float)random.NextDouble()*4.9f,.01f+(float)random.NextDouble(),right,up,depth,count);
        }
        var one=new[]{0f};
        check(!BossPerspectiveFraming.RequiredOffsetDistance(55,1,.1f,null,one,one,1,out _,out _),"offset null points rejected");
        check(!BossPerspectiveFraming.RequiredOffsetDistance(55,1,.1f,one,one,one,2,out _,out _),"offset excessive count rejected");
        check(!BossPerspectiveFraming.RequiredOffsetDistance(55,1,.1f,one,one,one,0,out _,out _),"offset empty points rejected");
        check(!BossPerspectiveFraming.RequiredOffsetDistance(55,1,.1f,new[]{float.NaN},one,one,1,out _,out _),"offset nonfinite point rejected");
        check(!BossPerspectiveFraming.RequiredOffsetDistance(55,0,.1f,one,one,one,1,out _,out _),"offset invalid aspect rejected");
    }
    static void CheckFit(Action<bool,string> check,float fov,float aspect,float near,float[] right,float[] up,float[] depth,int count)
    {
        check(BossPerspectiveFraming.RequiredOffsetDistance(fov,aspect,near,right,up,depth,count,out float d,out float offset),"offset fit valid");
        double tan=Math.Tan(fov*Math.PI/360),k=tan*.92,lower=double.NegativeInfinity,upper=double.PositiveInfinity;bool horizontalOrNear=false;
        double closer=d-.001;
        for(int i=0;i<count;i++)
        {
            double z=d+depth[i];check(z>near&&Math.Abs(right[i]/(z*tan*aspect))<=.92001&&Math.Abs((up[i]-offset)/(z*tan))<=.92001,"offset point keeps four-percent edge and near-plane margin");
            horizontalOrNear|=closer+depth[i]<near+.3-1e-5||Math.Abs(right[i])>(closer+depth[i])*k*aspect+1e-5;
            lower=Math.Max(lower,up[i]-k*(closer+depth[i]));upper=Math.Min(upper,up[i]+k*(closer+depth[i]));
        }
        check(d<=1.00001||horizontalOrNear||lower>upper+1e-5,"offset fit does not retreat unnecessarily");
    }
}