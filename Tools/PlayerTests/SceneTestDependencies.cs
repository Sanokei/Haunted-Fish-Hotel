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
    // Boss endpoints are outside this movement/trap fixture.
    public sealed class TestBossArena : MonoBehaviour { public bool Active; }
    public sealed class BossArenaCoordinator : MonoBehaviour
    {
        public static BossArenaCoordinator Current;
        public TestBossArena Arena;
        public void EditorGoToBoss() { }
    }
    public sealed class HotelPlayer : MonoBehaviour
    {
        static readonly System.Collections.Generic.List<HotelPlayer> _Active = new System.Collections.Generic.List<HotelPlayer>();
        public static System.Collections.Generic.IReadOnlyList<HotelPlayer> ActivePlayers => _Active;
        public HotelPlayerMovement Movement => GetComponent<HotelPlayerMovement>();
        public CharacterController BodyController => Movement ? Movement.BodyController : null;
        public bool IsRelevantPlayer => !Networked || LocalPlayer == this;
        public bool GhostFlightReady, RoundIntroComplete, InBossFight;
        public bool BossWatching, BossReturnLocked;
        public bool BossHallwayLocked => BossWatching || BossReturnLocked;
        public bool GhostSetupReady => RoundReleased || RoundIntroComplete;
        public float GhostSetupRemaining, Spook;
        public int GhostEmergenceVersion;
        public Vector3 GhostEmergencePosition;
        public Vector3 GhostFlightPosition;
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
        public void SendGhostFlightInput(Vector2 axis,string key,int version){}
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
        public void ResetSceneMotion() { if (Movement) Movement.ResetMotion(); }
        internal void TakeFishBody(HotelPlayer fish) { if(Movement) Movement.Teleport(fish.transform.position); }
        public void FinishGhostSelection(int version) { }
        IHotelGameCommands _GameCommands;
        public void BindGameCommands(IHotelGameCommands commands) => _GameCommands = commands;
        public void UnbindGameCommands(IHotelGameCommands commands)
        {
            if (ReferenceEquals(_GameCommands, commands)) _GameCommands = null;
        }
        void OnEnable() {if(!_Active.Contains(this))_Active.Add(this);PlayerEnabled?.Invoke(this); }
        void OnDisable() {_Active.Remove(this);PlayerDisabled?.Invoke(this); }
    }
}
