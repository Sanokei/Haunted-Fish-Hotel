using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace HauntedFish.BossFight
{
    // One captured pointer owns an existing 3D defense. Strikers are keyboard-only (WASD, shift to boost); the cue ball
    // and both strikers still block the pick so a defense under them cannot be grabbed through them.
    public sealed class BossDragInput : MonoBehaviour
    {
        [SerializeField] BossArenaManager _Arena;
        [SerializeField] BossDefenseBoard _Board;
        [SerializeField] Rigidbody _FishStriker,_GhostStriker;
        [SerializeField] LayerMask _PickMask=~0;
        readonly RaycastHit[] _Hits=new RaycastHit[32];
        uint _Owner,_Epoch,_Sequence;int _Slot=-1;bool _Striker;Vector2 _GrabOffset;
        float _NextPlacement,_PendingUntil;uint _PendingSequence;int _PendingSlot=-1;
        public event Action<int,uint,Vector2> DefenseMoveRequested;
        public bool DefenseCaptured=>_Slot>=0;public bool StrikerCaptured=>_Striker;
        public bool ValidateAuthoredBindings(out string error){error=null;if(!_Arena||!_Board||!_FishStriker||!_GhostStriker){error="Assign arena, authored defense board and both strikers.";return false;}return true;}
        public Vector2 Tick(uint actor,uint epoch,bool practicing,bool allowed,Vector2 screen,bool pressed,bool held,bool released,float interval)
        {
            if(_Owner!=actor||_Epoch!=epoch){Cancel();_Owner=actor;_Epoch=epoch;_Sequence=0;}
            bool participant=practicing?(actor==1||actor==2):BossPaddleInputPolicy.OwnsPaddle(actor,_Arena.FishId,_Arena.GhostId,_Arena.Active,_Arena.ParticipantsReady);
            if(!allowed||!participant||!_Arena.Camera){Cancel();return Vector2.zero;}
            if(_PendingSlot>=0)
            {
                var observed=_Board.Piece(_PendingSlot).State;
                if(observed.LastSequence>=_PendingSequence||observed.Broken||Time.unscaledTime>=_PendingUntil){_Board.Piece(_PendingSlot).HidePreview();_PendingSlot=-1;}
            }
            bool ui=EventSystem.current&&EventSystem.current.IsPointerOverGameObject();
            if(pressed&&!ui&&_PendingSlot<0&&TryPlane(screen,out Vector2 target))Capture(actor,practicing,screen,target);
            if(!held||released){_Striker=false;_Slot=-1;return Vector2.zero;}
            if(!TryPlane(screen,out Vector2 plane))return Vector2.zero;var desired=plane+_GrabOffset;
            bool ghost=practicing?actor==2:actor==_Arena.GhostId;
            if(_Striker){var body=ghost?_GhostStriker:_FishStriker;return _Arena.AxisToTarget(body.position,new Vector3(desired.x,body.position.y,desired.y),interval,ghost);}
            if(_Slot>=0)
            {
                var relative=new Vector2(desired.x-_Arena.Bounds.center.x,desired.y-_Arena.Bounds.center.z);var piece=_Board.Piece(_Slot);uint sideOwner=actor;
                bool valid=_Board.Playable&&_Board.Preview(sideOwner,_Slot,relative);piece.ShowPreview(_Board.TablePosition(relative),valid);
                if(valid&&Time.unscaledTime>=_NextPlacement){_NextPlacement=Time.unscaledTime+.10f;uint sequence=++_Sequence;_PendingSequence=sequence;_PendingSlot=_Slot;_PendingUntil=Time.unscaledTime+1.2f;DefenseMoveRequested?.Invoke(_Slot,sequence,relative);}
            }
            return Vector2.zero;
        }
        bool TryPlane(Vector2 screen,out Vector2 point)
        {
            point=default;var camera=_Arena.Camera;if(!_Arena.TryPointerTarget(screen,out Vector3 world))return false;point=new Vector2(world.x,world.z);return true;
        }
        void Capture(uint actor,bool practicing,Vector2 screen,Vector2 plane)
        {
            _Striker=false;_Slot=-1;var ray=_Arena.Camera.ScreenPointToRay(screen);int count=Physics.RaycastNonAlloc(ray,_Hits,1000,_PickMask,QueryTriggerInteraction.Collide);float distance=float.MaxValue;
            Rigidbody owned=(practicing?actor==2:actor==_Arena.GhostId)?_GhostStriker:_FishStriker;
            for(int i=0;i<count;i++)
            {
                var hit=_Hits[i];if(hit.distance>=distance||!hit.collider)continue;
                var piece=hit.collider.GetComponentInParent<BossDefensePiece>();
                bool striker=false;bool defense=piece&&_Board.Owns(actor,piece.Slot);
                // Cue ball and opponent are real blockers to pointer capture; cannot select through them.
                bool known=piece||hit.rigidbody==_Arena.Puck||hit.rigidbody==_FishStriker||hit.rigidbody==_GhostStriker;
                if(!known)continue;distance=hit.distance;_Striker=striker;_Slot=defense?piece.Slot:-1;
                var position=striker?owned.position:defense?piece.transform.position:new Vector3(plane.x,0,plane.y);_GrabOffset=new Vector2(position.x-plane.x,position.z-plane.y);
            }
        }
        public void Reply(int slot,uint sequence,uint epoch,bool accepted)
        {
            if(_Epoch!=epoch||_PendingSlot!=slot||_PendingSequence!=sequence)return;
            if(!accepted){_Board.Piece(slot).HidePreview();_PendingSlot=-1;_Slot=-1;}
        }
        public void Cancel(){_Striker=false;_Slot=-1;_PendingSlot=-1;if(_Board)_Board.HidePreviews();}
        void OnDisable()=>Cancel();
    }
}