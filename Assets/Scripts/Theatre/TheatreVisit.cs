using System;
namespace HauntedFish.Theatre
{
    // Client-local visit; never changes session role, round or network travel.
    public sealed class TheatreVisit
    {
        public bool Active {get;private set;}
        public bool Loading {get;private set;}
        public uint Owner {get;private set;}
        public int Version {get;private set;}
        double _ReenterAfter; bool _MustLeaveDoor;
        public bool Begin(uint owner,bool eligible,bool inDoor,double now)
        {
            if(owner==0||!eligible||!inDoor||Active||Loading||_MustLeaveDoor||now<_ReenterAfter)return false;
            Owner=owner;Loading=true;Version++;return true;
        }
        public bool Loaded(uint owner,int version)
        {
            if(!Loading||Owner!=owner||Version!=version)return false;
            Loading=false;Active=true;return true;
        }
        public bool Exit(uint owner,double now)
        {
            if(!Active||owner!=Owner)return false;
            Cancel(now);return true;
        }
        public void Cancel(double now)
        {
            Active=Loading=false;Owner=0;Version++;_ReenterAfter=now+1;_MustLeaveDoor=true;
        }
        public void ObserveDoor(bool inside){if(!inside)_MustLeaveDoor=false;}
        public static float Move(float x,float axis,float delta,float min,float max,float speed)
        {
            if(float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(axis)||float.IsInfinity(axis)||float.IsNaN(delta)||float.IsInfinity(delta))return min;
            return Math.Max(min,Math.Min(max,x+Math.Max(-1,Math.Min(1,axis))*Math.Max(0,delta)*Math.Max(0,speed)));
        }
        public static int Countdown(double elapsed)=>elapsed<0?3:elapsed<1?3:elapsed<2?2:elapsed<3?1:0;
    }
}