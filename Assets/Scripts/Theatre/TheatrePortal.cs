using System;
using System.Collections;
using HauntedFish.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace HauntedFish.Theatre
{
    // Lobby owner authors this adapter on GO524835299 and binds an outside return anchor.
    public sealed class TheatrePortal : MonoBehaviour
    {
        public static TheatrePortal Current{get;private set;}
        // True from the doorway load until the return completes; lobby ready zones ignore the theatre-goer meanwhile.
        public static bool Visiting=>Current&&(Current._Visit.Active||Current._Visit.Loading||Current._Leaving);
        [SerializeField] LobbyTrigger _Entry;
        [SerializeField] Transform _SafeReturn;
        [SerializeField] LobbyManager _Lobby;
        [SerializeField] HauntedHotelMultiplayer _Session;
        [SerializeField] string _Scene="theatre";
        readonly TheatreVisit _Visit=new TheatreVisit();
        HotelPlayer _Owner; bool _CanMove; Scene _Theatre; TheatrePresentation _Presentation; AsyncOperation _PendingLoad;
        bool _Leaving;
        readonly TheatreReturnState _Return=new TheatreReturnState();
        TheatreReturnRequest _ReturnRequest;
        static uint OwnerId(HotelPlayer player)=>player.NetId==0?1:player.NetId;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetStatic()=>Current=null;
        void OnEnable(){if(Current&&Current!=this)throw new InvalidOperationException("One theatre portal per Lobby.");Current=this;}
        void OnDisable(){if(Current==this)Current=null;Leave(false);}
        void Update()
        {
            var player=HotelPlayer.LocalPlayer;bool inside=_Entry&&_Entry.ContainsPosition(player);_Visit.ObserveDoor(inside);
            if((_Visit.Active||_Visit.Loading)&&(!_Owner||player!=_Owner||!_Session||!_Session.ReadyToPlay||_Session.Transitioning)){Leave(false);return;}
            if(_Leaving)
            {
                if(_Return.Expired(Time.unscaledTimeAsDouble)){if(_Session)_Session.SetStatus("Theatre return timed out; controls restored. Leave the doorway before entering again.");FinishLeave();}
                else if(_Owner&&_Return.Ready(OwnerId(_Owner),_Visit.Version,!_Entry.ContainsPosition(_Owner)&&Vector3.Distance(_Owner.transform.position,_SafeReturn.position)<.5f))FinishLeave();
                return;
            }
            if((_PendingLoad!=null&&!_PendingLoad.isDone)||!player||!_SafeReturn||!_Session||!_Lobby||!_Lobby.InspectionAllowed||!player.GetComponent<TheatreReturnRequest>())return;
            bool eligible=_Session.ReadyToPlay&&!_Session.Transitioning&&!_Session.MemberMenuOpen&&!player.Movement.SharedDialogueLocked;
            if(!_Visit.Begin(player.NetId==0?1:player.NetId,eligible,inside,Time.unscaledTimeAsDouble))return;
            _Owner=player;_CanMove=player.Movement.CanMove;player.Movement.CanMove=false;player.ResetSceneMotion();
            StartCoroutine(Load(_Visit.Version));
        }
        IEnumerator Load(int version)
        {
            if(!Application.CanStreamedLevelBeLoaded(_Scene)){Debug.LogError("Register theatre in build settings.",this);Leave(false);yield break;}
            AsyncOperation operation=null;try{operation=SceneManager.LoadSceneAsync(_Scene,LoadSceneMode.Additive);}catch(Exception error){Debug.LogException(error,this);}
            if(operation==null){Leave(false);yield break;}
            _PendingLoad=operation;
            operation.completed += ignored => { if(!this||!isActiveAndEnabled||version!=_Visit.Version){var late=SceneManager.GetSceneByName(_Scene);if(late.IsValid()&&late.isLoaded)SceneManager.UnloadSceneAsync(late);} };
            while(!operation.isDone)yield return null;
            _Theatre=SceneManager.GetSceneByName(_Scene);_PendingLoad=null;
            if(!_Owner||!_Visit.Loaded(_Owner.NetId==0?1:_Owner.NetId,version)){if(_Theatre.IsValid()&&_Theatre.isLoaded)SceneManager.UnloadSceneAsync(_Theatre);yield break;}
            foreach(var root in _Theatre.GetRootGameObjects()){
                var p=root.GetComponentInChildren<TheatrePresentation>(true);if(p){if(_Presentation){Debug.LogError("One theatre presentation required.",this);Leave(false);yield break;}_Presentation=p;}
            }
            if(!_Presentation){Debug.LogError("Assign authored theatre presentation.",this);Leave(false);yield break;}
            SceneManager.SetActiveScene(_Theatre);_Presentation.ExitRequested+=ExitRequested;
            try{_Presentation.Open(_Owner,_Session);}catch(Exception error){Debug.LogException(error,this);Leave(false);}
        }
        void ExitRequested()=>Leave(true);
        void Leave(bool requestReturn)
        {
            if(_Leaving){if(!requestReturn)FinishLeave();return;}
            _Leaving=true;
            if(requestReturn&&_Owner)
            {
                try
                {
                    _ReturnRequest=_Owner.GetComponent<TheatreReturnRequest>();
                    if(_ReturnRequest&&_Return.Begin(OwnerId(_Owner),_Visit.Version,Time.unscaledTimeAsDouble,8))
                    {
                        _ReturnRequest.ReturnCompleted+=OnReturnAcknowledged;
                        if(_Presentation)_Presentation.ShowReturnPending();
                        if(_ReturnRequest.ReturnToArchway(_Visit.Version))return;
                    }
                }
                catch(Exception error){Debug.LogException(error,this);}
            }
            FinishLeave();
        }
        void OnReturnAcknowledged(int version,bool accepted)
        {
            if(!_Owner||!_Return.Acknowledge(OwnerId(_Owner),version,accepted))return;
            if(!accepted){if(_Session)_Session.SetStatus("Theatre return was rejected; controls restored. Leave the doorway before entering again.");FinishLeave();}
        }
        void FinishLeave()
        {
            if(_ReturnRequest)_ReturnRequest.ReturnCompleted-=OnReturnAcknowledged;
            _ReturnRequest=null;_Return.Cancel();
            Restore();if(_Theatre.IsValid()&&_Theatre.isLoaded)SceneManager.UnloadSceneAsync(_Theatre);_Theatre=default;
            _Visit.Cancel(Time.unscaledTimeAsDouble);_Leaving=false;
        }
        void Restore()
        {
            if(_Presentation){_Presentation.ExitRequested-=ExitRequested;_Presentation.Close();}_Presentation=null;
            if(gameObject.scene.IsValid()&&gameObject.scene.isLoaded)SceneManager.SetActiveScene(gameObject.scene);
            if(_Owner&&_Owner.Movement){_Owner.Movement.CanMove=_CanMove;_Owner.ResetSceneMotion();}_Owner=null;
        }
        // Called only by owned RPC (or explicit nonnetwork practice) against server-authored anchor.
        public bool ReturnOwnedPlayer(HotelPlayer player)
        {
            if(!player||player.Networked&&!player.IsServer||!_Entry||!_SafeReturn||!_Session||!_Session.SceneTravel||!_Session.ReadyToPlay||_Session.Transitioning||_Session.SceneTravel.TargetScene!="Lobby")return false;
            if(!_Entry.ContainsPosition(player))return false;
            if((_Entry.GetComponent<Collider>().ClosestPoint(_SafeReturn.position)-_SafeReturn.position).sqrMagnitude<.25f)return false;
            player.Teleport(_SafeReturn.position);player.ResetSceneMotion();return true;
        }
    }
}