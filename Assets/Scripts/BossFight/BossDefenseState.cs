using System;
using System.Collections.Generic;
namespace HauntedFish.BossFight
{
    public enum BossDefenseKind { Bottle=0,TunaCan=1,Building=2 }
    public struct BossDefenseFootprint { public float X,Z,Radius;public BossDefenseFootprint(float x,float z,float radius){X=x;Z=z;Radius=radius;} }
    public struct BossDefenseSlot
    {
        public uint Owner,LastSequence;public BossDefenseKind Kind;public int Health;public bool Placed;public float X,Z,Radius;
        public bool Broken=>Health==0;
    }
    // Server state only; Unity adapters supply real cue-ball contact episodes and current physical blockers.
    public sealed class BossDefenseState
    {
        readonly BossDefenseSlot[] _Slots=new BossDefenseSlot[6];
        readonly HashSet<(int slot,int contact)> _Contacts=new HashSet<(int,int)>();
        readonly float _HalfWidth,_HalfLength;
        uint _Fish,_Ghost;public uint Epoch{get;private set;}public uint Revision{get;private set;}
        public bool Ready{get;set;}public bool Active{get;set;}
        public BossDefenseState(uint epoch,uint fish,uint ghost,float halfWidth=3,float halfLength=5)
        {if(!Finite(halfWidth)||!Finite(halfLength)||halfWidth<2||halfLength<3)throw new ArgumentOutOfRangeException();_HalfWidth=halfWidth;_HalfLength=halfLength;Reset(epoch,fish,ghost);}
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public BossDefenseSlot Slot(int index)=>_Slots[index];
        public void Reset(uint epoch,uint fish,uint ghost)
        {
            if(epoch==0||fish==0||ghost==0||fish==ghost)throw new ArgumentException("Distinct scoped contestants required.");
            Epoch=epoch;_Fish=fish;_Ghost=ghost;Revision=0;Ready=false;Active=true;_Contacts.Clear();
            for(int i=0;i<6;i++)_Slots[i]=new BossDefenseSlot{Owner=i<3?fish:ghost,Kind=(BossDefenseKind)(i%3),Health=i%3+1,Radius=i%3==0?.28f:i%3==1?.40f:.45f};
        }
        public bool TryPlace(uint actor,uint epoch,int index,uint sequence,float x,float z,IReadOnlyList<BossDefenseFootprint> dynamicBlockers)=>ValidatePlace(actor,epoch,index,sequence,x,z,dynamicBlockers,true);
        public bool CanPlace(uint actor,int index,float x,float z,IReadOnlyList<BossDefenseFootprint> blockers)=>index>=0&&index<6&&ValidatePlace(actor,Epoch,index,_Slots[index].LastSequence+1,x,z,blockers,false);
        bool ValidatePlace(uint actor,uint epoch,int index,uint sequence,float x,float z,IReadOnlyList<BossDefenseFootprint> dynamicBlockers,bool commit)
        {
            if(!Active||!Ready||epoch!=Epoch||index<0||index>=6||sequence==0||!Finite(x)||!Finite(z))return false;
            var slot=_Slots[index];if(slot.Owner!=actor||actor==0||slot.Broken||sequence<=slot.LastSequence)return false;
            const float margin=.12f;float radius=slot.Radius;
            if(Math.Abs(x)>_HalfWidth-radius-margin||Math.Abs(z)>_HalfLength-1.25f-radius)return false;
            if(actor==_Fish?z>-.55f-radius:z<.55f+radius)return false;
            for(int i=0;i<6;i++)if(i!=index&&_Slots[i].Placed&&!_Slots[i].Broken&&Overlap(x,z,radius,new BossDefenseFootprint(_Slots[i].X,_Slots[i].Z,_Slots[i].Radius),margin))return false;
            if(dynamicBlockers==null)return false;
            foreach(var blocker in dynamicBlockers)if(!Finite(blocker.X)||!Finite(blocker.Z)||!Finite(blocker.Radius)||blocker.Radius<0||Overlap(x,z,radius,blocker,margin))return false;
            if(slot.Placed){foreach(var blocker in dynamicBlockers)if(Sweep(slot.X,slot.Z,x,z,radius,blocker,margin))return false;for(int i=0;i<6;i++)if(i!=index&&_Slots[i].Placed&&!_Slots[i].Broken&&Sweep(slot.X,slot.Z,x,z,radius,new BossDefenseFootprint(_Slots[i].X,_Slots[i].Z,_Slots[i].Radius),margin))return false;}
            if(commit){slot.Placed=true;slot.X=x;slot.Z=z;slot.LastSequence=sequence;_Slots[index]=slot;Revision++;}return true;
        }
        public bool Observe(int index,int health,bool placed,uint sequence,float x,float z)
        {
            if(index<0||index>=6||health<0||health>index%3+1||!Finite(x)||!Finite(z))return false;
            var slot=_Slots[index];slot.Health=health;slot.Placed=placed;slot.LastSequence=sequence;slot.X=x;slot.Z=z;_Slots[index]=slot;return true;
        }
        public bool ClearStrikerPath(float fromX,float fromZ,float toX,float toZ,float radius)
        {
            if(!Finite(fromX)||!Finite(fromZ)||!Finite(toX)||!Finite(toZ)||!Finite(radius)||radius<0)return false;foreach(var slot in _Slots)if(slot.Placed&&!slot.Broken&&Sweep(fromX,fromZ,toX,toZ,radius,new BossDefenseFootprint(slot.X,slot.Z,slot.Radius),.02f))return false;return true;
        }
        static bool Sweep(float fromX,float fromZ,float toX,float toZ,float radius,BossDefenseFootprint other,float margin)
        {
            double dx=toX-(double)fromX,dz=toZ-(double)fromZ,length=dx*dx+dz*dz;
            double t=length<.000001?0:Math.Max(0,Math.Min(1,((other.X-fromX)*dx+(other.Z-fromZ)*dz)/length));
            double x=fromX+t*dx-other.X,z=fromZ+t*dz-other.Z,min=radius+other.Radius+margin;return x*x+z*z<min*min;
        }
        static bool Overlap(float x,float z,float radius,BossDefenseFootprint other,float margin)
        {double dx=x-(double)other.X,dz=z-(double)other.Z,min=radius+other.Radius+margin;return dx*dx+dz*dz<min*min;}
        public bool Hit(uint epoch,int index,int contactEpisode,bool verifiedCueBall)
        {
            if(!Active||!Ready||epoch!=Epoch||index<0||index>=6||contactEpisode==0||!verifiedCueBall)return false;
            var slot=_Slots[index];if(!slot.Placed||slot.Broken||!_Contacts.Add((index,contactEpisode)))return false;
            slot.Health--;_Slots[index]=slot;Revision++;return true;
        }
        public void ContactEnded(int index,int episode)=>_Contacts.Remove((index,episode));
        public void ServeStarted(){_Contacts.Clear();}
    }
}