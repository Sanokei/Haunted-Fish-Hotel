namespace HauntedFish.Theatre
{
    // Owned return request waits for both a scoped server acknowledgement and replicated outside position.
    public sealed class TheatreReturnState
    {
        public bool Waiting{get;private set;}
        public bool Acknowledged{get;private set;}
        public uint Owner{get;private set;}
        public int Version{get;private set;}
        double _Deadline;
        public bool Begin(uint owner,int version,double now,double timeout)
        {
            if(Waiting||owner==0||timeout<=0)return false;
            Owner=owner;Version=version;_Deadline=now+timeout;Waiting=true;Acknowledged=false;return true;
        }
        public bool Matches(uint owner,int version)=>Waiting&&Owner==owner&&Version==version;
        public bool Acknowledge(uint owner,int version,bool accepted)
        {
            if(!Matches(owner,version))return false;
            if(!accepted){Cancel();return true;}
            Acknowledged=true;return true;
        }
        public bool Ready(uint owner,int version,bool replicatedOutside)=>Matches(owner,version)&&Acknowledged&&replicatedOutside;
        public bool Expired(double now)=>Waiting&&now>=_Deadline;
        public void Cancel(){Waiting=Acknowledged=false;Owner=0;Version++;}
    }
}