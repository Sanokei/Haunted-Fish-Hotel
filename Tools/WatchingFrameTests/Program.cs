using System;
using HauntedFish.Presentation;
class Program
{
    static int checks;
    static void Assert(bool condition, string label) { checks++; if (!condition) throw new Exception(label); }
    static void Main()
    {
        float x, y;
        WatchingFrameGaze.Project(0, 0, 5, .35f, 4, .23f, .12f, out x, out y);
        Assert(x == 0 && y == 0, "centered camera");
        WatchingFrameGaze.Project(1, 0, 5, .35f, 4, .23f, .12f, out x, out y);
        Assert(x > 0 && y == 0, "viewer right moves pupil right");
        WatchingFrameGaze.Project(-1, 1, -5, .35f, 4, .23f, .12f, out x, out y);
        Assert(x < 0 && y > 0, "left/up and front negative depth");
        WatchingFrameGaze.Project(1000, -5000, 0, .35f, 4, .23f, .12f, out x, out y);
        Assert(Math.Abs(x) <= .23f && Math.Abs(y) <= .12f, "extreme displacement bounded");
        Assert(x*x/(.23f*.23f)+y*y/(.12f*.12f) < 1.00001, "elliptical travel limit");
        WatchingFrameGaze.Project(1, 1, 0, .35f, 0, 0, 0, out x, out y);
        Assert(x == 0 && y == 0, "zero limits");
        WatchingFrameGaze.Project(float.NaN, 1, 0, .35f, 4, .23f, .12f, out x, out y);
        Assert(x == 0 && y == 0, "NaN rejected");
        WatchingFrameGaze.Project(1, float.PositiveInfinity, 0, .35f, 4, .23f, .12f, out x, out y);
        Assert(x == 0 && y == 0, "infinity rejected");
        Assert(WatchingFrameGaze.BeamContains(.5f,.5f,.5f,.5f,1.78f,.18f,.12f), "beam center lit");
        Assert(!WatchingFrameGaze.BeamContains(.9f,.5f,.5f,.5f,1.78f,.18f,.12f), "beam does not illuminate distant screen points");
        Assert(!WatchingFrameGaze.BeamContains(-.01f,.5f,0,.5f,1,.18f,.12f), "offscreen eye excluded");
        Assert(!WatchingFrameGaze.BeamContains(.5f,.5f,.5f,.5f,0,.18f,.12f), "invalid aspect rejected");
        float c = 0;
        for (int i=0;i<4;i++) c=WatchingFrameGaze.AdvanceClosure(c,true,.05f,.16f,.24f);
        Assert(c==1, "beam closes in authored duration");
        c=WatchingFrameGaze.AdvanceClosure(c,false,.05f,.16f,.24f);
        Assert(c>0 && c<1, "beam off reopens smoothly");
        for (int i=0;i<5;i++) c=WatchingFrameGaze.AdvanceClosure(c,false,.05f,.16f,.24f);
        Assert(c==0, "beam off fully reopens");
        c=WatchingFrameGaze.AdvanceClosure(.5f,true,float.NaN,.16f,.24f);
        Assert(c==.5f, "invalid elapsed does not corrupt closure");
        c=WatchingFrameGaze.AdvanceClosure(.5f,true,0,.16f,.24f);
        Assert(c==.5f, "zero elapsed stable");
        c=WatchingFrameGaze.AdvanceClosure(.5f,false,100,.16f,.24f);
        Assert(c==0, "long pause cannot overshoot open");
        c=WatchingFrameGaze.AdvanceClosure(.5f,true,100,.16f,.24f);
        Assert(c==1, "long pause cannot overshoot closed");
        for(int i=0;i<1000;i++)
        {
            c=WatchingFrameGaze.AdvanceClosure(c,i%3==0,.011f,.16f,.24f);
            Assert(c>=0 && c<=1,"repeated beam edges stay bounded");
        }
        WatchingFrameGaze.Project(float.MaxValue,float.MaxValue,0,float.MaxValue,.01f,.23f,.12f,out x,out y);
        Assert(!float.IsNaN(x) && !float.IsInfinity(x) && !float.IsNaN(y) && !float.IsInfinity(y), "finite extreme inputs cannot overflow gaze");
        var random = new Random(14);
        for (int i = 0; i < 10000; i++)
        {
            WatchingFrameGaze.Project((float)(random.NextDouble()*200-100),
                (float)(random.NextDouble()*200-100), (float)(random.NextDouble()*200-100),
                .35f, 4, .23f, .12f, out x, out y);
            Assert(x*x/(.23f*.23f)+y*y/(.12f*.12f) < 1.00001, "sample stays bounded");
        }
        Console.WriteLine("PASS " + checks + " gaze, beam and eyelid checks. Native Unity rendering not exercised.");
    }
}
