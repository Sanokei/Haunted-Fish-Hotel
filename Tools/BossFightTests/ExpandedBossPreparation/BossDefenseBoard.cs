using System;
using UnityEngine;
namespace HauntedFish.BossFight
{
    [Serializable] public sealed class BossDefenseSnapshot
    {public int Slot,Health;public bool Placed;public uint Sequence;public Vector3 Position;}
    public sealed class BossDefenseBoard : MonoBehaviour
    {
        [SerializeField] BossDefensePiece[] _Pieces;
        [SerializeField] Rigidbody _Puck,_FishPaddle,_GhostPaddle;
        [SerializeField] Vector3 _Centre=new Vector3(1000,1.6f,0);
        [SerializeField] Vector2 _HalfSize=new Vector2(3,5);
        BossDefenseState _State;bool _Authority,_Practice,_Initialized;
        readonly BossDefenseFootprint[] _Blockers=new BossDefenseFootprint[3];
        public event Action Changed;public Rigidbody Puck=>_Puck;
        public bool Authority=>_Authority;public uint Epoch=>_State?.Epoch??0;public bool Playable=>_State!=null&&_State.Active&&_State.Ready;
        public BossDefensePiece Piece(int slot)=>slot>=0&&slot<6?_Pieces[slot]:null;
        public bool ValidateAuthoredBindings(out string error)
        {
            error=null;if(_Pieces==null||_Pieces.Length!=6||!_Puck||!_FishPaddle||!_GhostPaddle){error="Author six defense pieces and three existing bodies.";return false;}
            for(int i=0;i<6;i++)if(!_Pieces[i]||_Pieces[i].Slot!=i||!_Pieces[i].ValidateAuthoredBindings(out error))return false;return true;
        }
        void Initialize(){if(_Initialized)return;if(!ValidateAuthoredBindings(out string error))throw new InvalidOperationException(error);foreach(var piece in _Pieces)piece.Bind(this);_Initialized=true;}
        public void SetAuthority(bool authority){Initialize();_Authority=authority;foreach(var piece in _Pieces)piece.SetAuthority(authority);}
        public void Begin(uint epoch,uint fish,uint ghost,bool practice=false){Initialize();_Practice=practice;_State=new BossDefenseState(epoch,fish,ghost,_HalfSize.x,_HalfSize.y);PresentState();}
        public void SetPlayable(bool active,bool ready){if(_State!=null){_State.Active=active;_State.Ready=ready;}}
        public bool Owns(uint actor,int slot)=>_State!=null&&slot>=0&&slot<6&&_State.Slot(slot).Owner==actor&&!_State.Slot(slot).Broken;
        public Vector3 TablePosition(Vector2 relative)=>_Centre+new Vector3(relative.x,.02f,relative.y);
        void Blockers(){_Blockers[0]=new BossDefenseFootprint(_Puck.position.x-_Centre.x,_Puck.position.z-_Centre.z,.3f);_Blockers[1]=new BossDefenseFootprint(_FishPaddle.position.x-_Centre.x,_FishPaddle.position.z-_Centre.z,.48f);_Blockers[2]=new BossDefenseFootprint(_GhostPaddle.position.x-_Centre.x,_GhostPaddle.position.z-_Centre.z,.48f);}
        public bool Place(uint actor,uint epoch,int slot,uint sequence,Vector2 target)
        {
            if(!_Authority||_State==null)return false;Blockers();if(!_State.TryPlace(actor,epoch,slot,sequence,target.x,target.y,_Blockers))return false;PresentState();Changed?.Invoke();return true;
        }
        public bool Preview(uint actor,int slot,Vector2 target)
        {if(_State==null||!Owns(actor,slot))return false;Blockers();return _State.CanPlace(actor,slot,target.x,target.y,_Blockers);}
        public void CueBallContact(int slot,int episode){if(_Authority&&_State!=null&&_State.Hit(Epoch,slot,episode,true)){PresentState();Changed?.Invoke();}}
        public void ContactEnded(int slot,int episode)=>_State?.ContactEnded(slot,episode);
        public void ServeStarted(){_State?.ServeStarted();foreach(var piece in _Pieces)piece.ResetContacts();}
        void PresentState(){for(int i=0;i<6;i++){var slot=_State.Slot(i);_Pieces[i].Present(slot,TablePosition(new Vector2(slot.X,slot.Z)));}}
        public BossDefenseSnapshot[] Snapshot()
        {
            if(_State==null||_Practice)return null;var result=new BossDefenseSnapshot[6];for(int i=0;i<6;i++){var state=_State.Slot(i);result[i]=new BossDefenseSnapshot{Slot=i,Health=state.Health,Placed=state.Placed,Sequence=state.LastSequence,Position=TablePosition(new Vector2(state.X,state.Z))};}return result;
        }
        public bool ValidateSnapshot(uint epoch,uint fish,uint ghost,BossDefenseSnapshot[] entries)
        {
            if(entries==null)return true;if(entries.Length!=6||epoch==0||fish==0||ghost==0||fish==ghost)return false;
            for(int i=0;i<6;i++){var s=entries[i];if(s==null||s.Slot!=i||s.Health<0||s.Health>i%3+1||!float.IsFinite(s.Position.x)||!float.IsFinite(s.Position.y)||!float.IsFinite(s.Position.z))return false;
                if(s.Placed){float r=i%3==0?.28f:i%3==1?.4f:.45f;if(i>=3?s.Position.z-_Centre.z<.55f+r:s.Position.z-_Centre.z>-.55f-r)return false;if(Mathf.Abs(s.Position.y-(_Centre.y+.02f))>.03f||Mathf.Abs(s.Position.x-_Centre.x)>_HalfSize.x-r-.10f||Mathf.Abs(s.Position.z-_Centre.z)>_HalfSize.y-1.25f-r+.02f)return false;}}
            return true;
        }
        public void ApplySnapshot(uint epoch,uint fish,uint ghost,BossDefenseSnapshot[] entries,bool active,bool ready)
        {
            if(_Authority||entries==null)return;Initialize();if(_State==null||_State.Epoch!=epoch)Begin(epoch,fish,ghost);
            for(int i=0;i<6;i++){var s=entries[i];var state=_State.Slot(i);state.Health=s.Health;state.Placed=s.Placed;state.LastSequence=s.Sequence;state.X=s.Position.x-_Centre.x;state.Z=s.Position.z-_Centre.z;_Pieces[i].Present(state,s.Position);}
            // Keep a full observed rule state for preview/ownership without inferring authority.
            for(int i=0;i<6;i++)_State.Observe(i,entries[i].Health,entries[i].Placed,entries[i].Sequence,entries[i].Position.x-_Centre.x,entries[i].Position.z-_Centre.z);_State.Active=active;_State.Ready=ready;
        }
        public Vector3 ClampStriker(Vector3 from,Vector3 to,float radius)
        {
            if(_State==null||_State.ClearStrikerPath(from.x-_Centre.x,from.z-_Centre.z,to.x-_Centre.x,to.z-_Centre.z,radius))return to;
            var xOnly=new Vector3(to.x,from.y,from.z);if(_State.ClearStrikerPath(from.x-_Centre.x,from.z-_Centre.z,xOnly.x-_Centre.x,xOnly.z-_Centre.z,radius))return xOnly;
            var zOnly=new Vector3(from.x,from.y,to.z);if(_State.ClearStrikerPath(from.x-_Centre.x,from.z-_Centre.z,zOnly.x-_Centre.x,zOnly.z-_Centre.z,radius))return zOnly;return from;
        }
        public void HidePreviews(){foreach(var piece in _Pieces)piece.HidePreview();}
    }
}