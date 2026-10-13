using System;
using System.Collections.Generic;
using UnityEngine;
namespace HauntedFish.BossFight
{
    public sealed class BossDefensePiece : MonoBehaviour
    {
        [SerializeField] int _Slot;
        [SerializeField] Rigidbody _Body;
        [SerializeField] Collider _Collision,_Pickup;
        [SerializeField] GameObject _Healthy,_Crack1,_Crack2,_Broken,_ValidPreview,_InvalidPreview;
        Vector3 _Rack;BossDefenseBoard _Board;BossDefenseSlot _State;bool _Bound,_Authority;
        float _FractureUntil;readonly HashSet<Collider> _CueContacts=new HashSet<Collider>();
        public int Slot=>_Slot;public BossDefenseSlot State=>_State;public Collider Pickup=>_Pickup;
        public Vector3 RackPosition=>_Rack;
        public bool ValidateAuthoredBindings(out string error){error=null;if(_Slot<0||_Slot>=6||!_Body||!_Collision||!_Pickup||!_Healthy||!_Crack1||!_Crack2||!_Broken||!_ValidPreview||!_InvalidPreview){error="Assign all authored 3D body/pickup/health/preview refs for slot "+_Slot;return false;}return true;}
        public void Bind(BossDefenseBoard board){if(_Bound)return;if(!ValidateAuthoredBindings(out string error))throw new InvalidOperationException(error);_Board=board;_Rack=_Body.position;_Bound=true;HidePreview();}
        public void SetAuthority(bool authority){_Authority=authority;_Body.isKinematic=true;_Body.detectCollisions=true;UpdateCollision();}
        public void Present(BossDefenseSlot state,Vector3 tablePosition)
        {
            bool broke=!_State.Broken&&state.Broken&&state.Placed;_State=state;
            if(broke)_FractureUntil=Time.unscaledTime+.18f;
            _Body.position=state.Placed?tablePosition:_Rack;
            UpdateCollision();UpdateArtwork();if(!state.Placed||state.Broken)_CueContacts.Clear();
        }
        void UpdateCollision(){if(_Collision)_Collision.enabled=_Authority&&_State.Placed&&!_State.Broken;if(_Pickup)_Pickup.enabled=!_State.Broken;}
        void UpdateArtwork()
        {
            bool fracture=_State.Broken&&Time.unscaledTime<_FractureUntil;bool live=!_State.Broken||fracture;
            _Healthy.SetActive(live);_Crack1.SetActive(live&&(_State.Health<=(int)_State.Kind||fracture));_Crack2.SetActive(live&&(_State.Kind==BossDefenseKind.Building&&_State.Health==1||fracture));_Broken.SetActive(_State.Broken&&!fracture);
        }
        void Update(){if(_Bound&&_State.Broken&&_FractureUntil>0&&Time.unscaledTime>=_FractureUntil){_FractureUntil=0;UpdateArtwork();}}
        public void ShowPreview(Vector3 position,bool valid){_ValidPreview.transform.position=_InvalidPreview.transform.position=position;_ValidPreview.SetActive(valid);_InvalidPreview.SetActive(!valid);}
        public void HidePreview(){if(_ValidPreview)_ValidPreview.SetActive(false);if(_InvalidPreview)_InvalidPreview.SetActive(false);}
        void OnCollisionEnter(Collision collision)
        {
            if(!_Authority||!_Board||collision.rigidbody!=_Board.Puck||!collision.collider)return;
            bool first=_CueContacts.Count==0;_CueContacts.Add(collision.collider);
            if(first)_Board.CueBallContact(_Slot,collision.rigidbody.GetInstanceID());
        }
        void OnCollisionExit(Collision collision)
        {
            if(!_Board||collision.rigidbody!=_Board.Puck||!collision.collider)return;
            _CueContacts.Remove(collision.collider);if(_CueContacts.Count==0)_Board.ContactEnded(_Slot,collision.rigidbody.GetInstanceID());
        }
        public void ResetContacts(){_CueContacts.Clear();}
    }
}