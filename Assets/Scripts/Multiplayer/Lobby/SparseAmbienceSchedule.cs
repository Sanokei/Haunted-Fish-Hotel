using System;
namespace HauntedFish.Multiplayer
{
    // One accent reservation for this scene; no relation to the lightning clock.
    public sealed class SparseAmbienceSchedule
    {
        public enum Accent { None, Swirl, Timber }
        public bool Running { get; private set; }
        public double NextSwirl { get; private set; }
        public double NextTimber { get; private set; }
        public double AccentEnds { get; private set; }
        Func<double,double,double> _Range;
        public void Begin(double now,Func<double,double,double> range)
        {
            Stop(); _Range = range ?? throw new ArgumentNullException(nameof(range));
            Running=true; NextSwirl=now+range(27,41); NextTimber=now+range(17,25); AccentEnds=now;
        }
        public Accent Take(double now,bool voiceBusy,double swirlSeconds,double timberSeconds)
        {
            if(!Running||voiceBusy||now<AccentEnds)return Accent.None;
            bool swirl=now>=NextSwirl, timber=now>=NextTimber;
            if(!swirl&&!timber)return Accent.None;
            if(swirl&&(!timber||NextSwirl<NextTimber))
            { AccentEnds=now+Math.Max(0,swirlSeconds);NextSwirl=AccentEnds+_Range(58,96);return Accent.Swirl; }
            AccentEnds=now+Math.Max(0,timberSeconds);NextTimber=AccentEnds+_Range(32,61);return Accent.Timber;
        }
        public void Stop(){Running=false;NextSwirl=NextTimber=double.PositiveInfinity;AccentEnds=0;_Range=null;}
    }
}
