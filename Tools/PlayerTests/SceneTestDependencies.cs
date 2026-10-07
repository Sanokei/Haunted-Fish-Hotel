// Network and camera endpoints only; scene, movement and trap collision code remain production code.
using System;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    public sealed class HauntedHotelMultiplayer : MonoBehaviour { public int PlayerCount; }
    public sealed class GameCameraOwner : MonoBehaviour { public Transform FollowOverride; }
    public sealed class GameMouseSpotlight : MonoBehaviour { }
    public sealed class TestSceneOwner { public bool SceneIsReady = true; }
    public sealed class TestIdentity { public TestSceneOwner Owner; }
    public sealed class HotelPlayer : MonoBehaviour
    {
        public static HotelPlayer LocalPlayer;
        public static event Action<HotelPlayer> PlayerEnabled;
        public static event Action<HotelPlayer> PlayerDisabled;
        public bool Networked, IsServer, ControlsReady, InputBlocked, RoundReleased, GhostPlacementAccepted;
        public uint NetId, GhostId;
        public int RoundVersion, GhostPlacementReply, ControlledCube = -1;
        public string RoundRoster = "", PlacedObjectsJson = "";
        public string HeldTrapFamily="",RoundStateKey="",InventoryScope="editor-preview",ConveyorJson="";
        public int PossessionEffectVersion;
        public Vector3 PossessionEffectPosition;
        public void ClearRoundInventory(){HeldTrapFamily="";RoundStateKey="";PlacedObjectsJson="";ControlledCube=-1;RoundRoster="";RoundReleased=false;PossessionEffectVersion=0;}
        public void RequestConveyorPackage(int id,Vector3 position,string key,int version){}
        public void RequestHeldTrap(string family,Vector3 position,string key,int version){}
        public void RequestPlaceHeldTrap(Vector3 position,string family,string key,int version){}
        public void RequestDisposeTrap(int id,string key,int version){}
        public void RequestScopedTrapPossession(int id,Vector3 position,string key,int version){}
        public void SendScopedTrapAction(int id,int kind,float axis,Vector3 point,string key,int version){}
        public TestIdentity Identity = new TestIdentity();
        public HotelControlMode ControlMode => GetComponent<HotelPlayerMovement>().Mode;
        public void SwitchEditorRole() { }
        public void ResetEditorRole() { }
        public void FinishGhostSelection(int version) { }
        void OnEnable() => PlayerEnabled?.Invoke(this);
        void OnDisable() => PlayerDisabled?.Invoke(this);
    }
}
