using System;
namespace HauntedFish.BossFight
{
    public enum BossViewerSide { Fish, Ghost, Spectator }
    public static class BossPerspectiveFraming
    {
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        public static bool RequiredDistance(float fov,float aspect,float near,float halfWidth,float minY,float maxY,float halfLength,float pitch,out float distance)
        {
            distance=0;
            if(!Finite(fov)||!Finite(aspect)||!Finite(near)||!Finite(halfWidth)||!Finite(minY)||!Finite(maxY)||!Finite(halfLength)||!Finite(pitch)||
                fov<20||fov>100||aspect<.1f||aspect>5||near<=0||halfWidth<=0||halfLength<=0||minY>=maxY||pitch<10||pitch>75)return false;
            double radians=pitch*Math.PI/180,sin=Math.Sin(radians),cos=Math.Cos(radians),tan=Math.Tan(fov*Math.PI/360),required=1;
            const double edge=.92; // HUD has its own side strip; playable goals keep a four-percent margin.
            foreach(float y in new[]{minY,maxY}) foreach(float z in new[]{-halfLength,halfLength})
            {
                double depth=-y*sin+z*cos,up=y*cos+z*sin;
                required=Math.Max(required,halfWidth/(tan*aspect*edge)-depth);
                required=Math.Max(required,Math.Abs(up)/(tan*edge)-depth);
                required=Math.Max(required,near+.3-depth);
            }
            distance=(float)required;return Finite(distance);
        }
        // Exact projected corner of the far opponent; do not pretend it is a tall near-table corner.
        public static bool ProjectedPointDistance(float right,float up,float depth,float fov,float aspect,float near,out float distance)
        {
            distance=0;if(!Finite(right)||!Finite(up)||!Finite(depth)||!Finite(fov)||!Finite(aspect)||!Finite(near)||fov<20||fov>100||aspect<.1f||aspect>5||near<=0)return false;
            double tan=Math.Tan(fov*Math.PI/360);const double edge=.92;
            distance=(float)Math.Max(1,Math.Max(Math.Max(Math.Abs(right)/(tan*aspect*edge)-depth,Math.Abs(up)/(tan*edge)-depth),near+.3-depth));return Finite(distance);
        }
        public static bool CornerVisible(float x,float y,float z,float distance,float pitch,float fov,float aspect,float near,float far)
        {
            double radians=pitch*Math.PI/180,sin=Math.Sin(radians),cos=Math.Cos(radians),depth=distance-y*sin+z*cos;
            double up=y*cos+z*sin,tan=Math.Tan(fov*Math.PI/360);
            return depth>near&&depth<far&&Math.Abs(x/(depth*tan*aspect))<=.92001&&Math.Abs(up/(depth*tan))<=.92001;
        }
    }
}