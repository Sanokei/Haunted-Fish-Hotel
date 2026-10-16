using System.Collections.Generic;
using Mirage;
using Monologue.Dialogue;
using Monologue.StoryInput;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // Optional deterministic input for authored, offline scenes. It never supplies
    // simulation authority or bypasses the normal readiness/status checks.
    public interface IHotelOfflineInputSource
    {
        void ReadInput(HotelPlayer actor, out Vector2 move, out bool jump, out Vector3 mouse, out bool run);
    }

    [RequireComponent(typeof(NetworkIdentity), typeof(HotelPlayerMovement))]
    public sealed class HotelPlayer : NetworkBehaviour
    {
        static readonly List<HotelPlayer> _ActivePlayers = new List<HotelPlayer>();
        public static IReadOnlyList<HotelPlayer> ActivePlayers { get; } = _ActivePlayers.AsReadOnly();
        public static HotelPlayer LocalPlayer { get; private set; }
        public HotelPlayerMovement Movement => _Movement;
        public CharacterController BodyController => _Movement ? _Movement.BodyController : null;
        public static event System.Action<HotelPlayer> LocalPlayerChanged;
        public static event System.Action<HotelPlayer> PlayerEnabled;
        public static event System.Action<HotelPlayer> PlayerDisabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLocalPlayer()
        {
            _ActivePlayers.Clear();
            LocalPlayer = null;
            LocalPlayerChanged = null;
            PlayerEnabled = null;
            PlayerDisabled = null;
        }

        void RegisterLocalPlayer()
        {
            if (!isActiveAndEnabled || !IsRelevantPlayer || LocalPlayer == this) return;
            LocalPlayer = this;
            LocalPlayerChanged?.Invoke(this);
        }

        void ReleaseLocalPlayer()
        {
            if (LocalPlayer != this) return;
            LocalPlayer = null;
            LocalPlayerChanged?.Invoke(null);
        }
        public bool Networked = true;

        [SyncVar] bool _Running;
        [SyncVar] public int RoundVersion;
        [SyncVar] public string RoundRoster = "";
        [SyncVar] public uint GhostId;
        [SyncVar] public bool RoundReleased;
        [SyncVar] public bool RoundIntroComplete;
        [SyncVar] public bool RoundCinematicComplete, RoundReady;
        [SyncVar] public int RoundReadyCount, RoundParticipantCount;
        [SyncVar] public float GhostSetupRemaining;
        [SyncVar] public bool InBossFight;
        [SyncVar] public bool BossWatching;
        [SyncVar] public bool BossReturnLocked;
        [SyncVar] public float BossReturnSetupRemaining;
        public bool BossHallwayLocked => BossWatching || BossReturnLocked;
        [SyncVar] public uint BossMatchEpoch;
        [SyncVar] public string BossArenaJson = "";
        [SyncVar] public Vector3 BossReturnPosition;
        [SyncVar] public int BossResultVersion;
        [SyncVar] public bool BossWon;
        // The boss fight ends the match: 0 still playing, 1 the fish won, 2 the ghost won.
        [SyncVar] public int MatchResult;
        // Seconds the ghost still has to stall to win; 0 when no countdown is running.
        [SyncVar] public float StallRemaining;
        [SyncVar] public float Health = 100;
        [SyncVar] public bool IsDead;
        [SyncVar] bool _Stunned;
        public bool IsStunned => _Stunned && !IsDead && ControlMode == HotelControlMode.Fish && !InBossFight && !BossHallwayLocked;
        [SyncVar] public int GhostEmergenceVersion;
        [SyncVar] public Vector3 GhostEmergencePosition;
        [SerializeField] AuthoredGhostEmergence _DeathEmergence;
        public AuthoredGhostEmergence DeathEmergence => _DeathEmergence;
        public bool GhostSetupReady => RoundIntroComplete || RoundReleased;
        [SyncVar] public string PlacedObjectsJson = "";
        [SyncVar] public int ControlledCube = -1;
        [SyncVar] public Vector3 GhostFlightPosition;
        [SyncVar] public bool GhostFlightReady;
        [SyncVar] public int GhostPlacementReply;
        [SyncVar] public bool GhostPlacementAccepted;
        [SyncVar] int _EditorRole = -1;
        public HotelControlMode ControlMode => !_Movement.GameActive ? HotelControlMode.Lobby :
            !GhostSetupReady ? HotelControlMode.Selection : IsDead ? HotelControlMode.Spectator : _EditorRole >= 0 ?
                (_EditorRole == 1 ? HotelControlMode.Ghost : HotelControlMode.Fish) :
                NetId == GhostId ? HotelControlMode.Ghost : HotelControlMode.Fish;

        public void SwitchEditorRole()
        {
#if UNITY_EDITOR
            if (IsDead || !IsRelevantPlayer || !RoundReleased || InBossFight || BossHallwayLocked || !_Movement.GameActive) return;
            bool ghost = ControlMode != HotelControlMode.Ghost;
            if (Networked) RequestEditorRole(ghost);
            else ApplyEditorRole(ghost);
#endif
        }
        [ServerRpc]
        void RequestEditorRole(bool ghost)
        {
#if UNITY_EDITOR
            if (!IsDead && RoundReleased && !InBossFight && !BossHallwayLocked && _Movement.GameActive) ApplyEditorRole(ghost);
#endif
        }
        void ApplyEditorRole(bool ghost)
        {
            ControlledCube = -1;
            _EditorRole = ghost ? 1 : 0;
            if (ghost) _GameCommands?.RefreshPlacement(this);
            ResetSceneMotion();
        }
        public void ResetEditorRole() => _EditorRole = -1;

        IHotelOfflineInputSource _OfflineInputSource;
        public void BindOfflineInput(IHotelOfflineInputSource source)
        {
            if (Networked) throw new System.InvalidOperationException("Authored offline input cannot control a networked actor.");
            _OfflineInputSource = source;
        }
        public void UnbindOfflineInput(IHotelOfflineInputSource source)
        {
            if (ReferenceEquals(_OfflineInputSource, source)) _OfflineInputSource = null;
        }

        IHotelGameCommands _GameCommands;
        IHotelBossCommands _BossCommands;

        public void BindGameCommands(IHotelGameCommands commands) => _GameCommands = commands;
        public void UnbindGameCommands(IHotelGameCommands commands)
        {
            if (ReferenceEquals(_GameCommands, commands)) _GameCommands = null;
        }
        public void BindBossCommands(IHotelBossCommands commands) => _BossCommands = commands;
        public void UnbindBossCommands(IHotelBossCommands commands)
        {
            if (ReferenceEquals(_BossCommands, commands)) _BossCommands = null;
        }

        [ServerRpc]
        public void RequestBossFight(string key, int version)
        {
            _BossCommands?.TryEnter(this, key, version);
        }
        [ServerRpc]
        public void ReadyBossScene(string key, int version, uint epoch)
        {
            _BossCommands?.AcceptReady(this, key, version, epoch);
        }
        [ServerRpc]
        public void SendBossInput(Vector2 axis, string key, int version, uint epoch)
        {
            _BossCommands?.AcceptInput(this, axis, key, version, epoch);
        }

        [ServerRpc]
        public void FinishGhostSelection(int version)
        {
            _GameCommands?.Acknowledge(this, version);
        }

        [ServerRpc(requireAuthority = true)]
        public void ReadyForGame(string key, int version)
        {
            _GameCommands?.Ready(this, key, version);
        }

        [SyncVar] public string HeldTrapFamily = "", RoundStateKey = "";
        [SyncVar] public string ConveyorJson = "";
        [SyncVar] public int PossessionEffectVersion;
        [SyncVar] public Vector3 PossessionEffectPosition;
        string _InventoryScope = "";
        int _InventoryGeneration;
        public string InventoryScope => Networked ? _InventoryScope : "editor-preview";
        public void ClearRoundInventory()
        {
            HeldTrapFamily = ConveyorJson = PlacedObjectsJson = RoundStateKey = RoundRoster = "";
            ControlledCube = -1;
            InBossFight = BossWatching = BossReturnLocked = false;
            BossReturnSetupRemaining = 0;
            BossMatchEpoch = 0;
            BossArenaJson = "";
            BossResultVersion = 0;
            MatchResult = 0;
            StallRemaining = 0;
            RoundReleased = RoundIntroComplete = RoundCinematicComplete = RoundReady = false;
            RoundReadyCount = RoundParticipantCount = 0;
            GhostSetupRemaining = 0;
            Health = 100;
            IsDead = false;
            _Stunned = false;
            if (_Movement) _Movement.ClearTrapStun();
            if (_Visual) _Visual.SetStunned(false);
            GhostEmergenceVersion = PossessionEffectVersion = 0;
            if (_DeathEmergence) _DeathEmergence.Stop();
            GhostFlightReady = false;
        }

        [ServerRpc]
        public void SendGhostFlightInput(Vector2 axis, string key, int version)
            => _GameCommands?.AcceptFlightInput(this, axis, key, version);

        [ServerRpc]
        public void RequestConveyorPackage(int id, Vector3 position, string key, int version)
            => ReplyToPlacement(_GameCommands?.TakePackage(this, id, position, key, version) ?? false);

        [ServerRpc]
        public void RequestPlaceHeldTrap(Vector3 position, string family, string key, int version)
            => ReplyToPlacement(_GameCommands?.PlaceTrap(this, position, family, key, version) ?? false);

        [ServerRpc]
        public void RequestDisposeTrap(int id, string key, int version)
            => ReplyToPlacement(_GameCommands?.DisposeTrap(this, id, key, version) ?? false);

        [ServerRpc]
        public void RequestScopedTrapPossession(int id, Vector3 position, string key, int version)
            => _GameCommands?.PossessTrap(this, id, position, key, version);

        [ServerRpc]
        public void SendScopedTrapAction(int id, int kind, float axis, Vector3 point, string key, int version)
            => _GameCommands?.ActOnTrap(this, id, kind, axis, point, key, version);

        void ReplyToPlacement(bool accepted)
        {
            GhostPlacementAccepted = accepted;
            GhostPlacementReply++;
        }

        [SyncVar] Vector3 _Position;
        [SyncVar] bool _FacingLeft;
        [SyncVar] bool _Walking;
        [SyncVar] bool _Talking;
        [SyncVar] Vector3 _MouseLight;
        public Vector3 LampWorldPosition => _MouseLight;

        HotelSessionContext _Session;
        HotelPlayerMovement _Movement;
        HotelFishSprite _Visual;
        HotelPlayerAudio _Audio;
        float _NextInteractionSound;
        float _NextSend;
        bool _QueuedJump;
        GameObject _TriggerSensor;

        bool Connected => Networked && Identity.IsSpawned;
        public bool IsRelevantPlayer => !Networked || (Identity.IsSpawned && IsLocalPlayer);
        public bool ControlsReady => isActiveAndEnabled && gameObject.activeInHierarchy &&
            (!Networked || (Identity.IsSpawned && (IsServer || IsClient))) &&
            (_Session == null ? !Networked : _Session.Connected && !_Session.Loading);
        public bool InputBlocked => IsRelevantPlayer && ((_Session != null && _Session.InputFocused) ||
            (DialogueManager.Instance && DialogueManager.Instance.ActiveDialoguePanel) ||
            (StoryInputTextFieldManager.Instance && StoryInputTextFieldManager.Instance.ActiveInputPanel));

        public void Configure(HotelSessionContext context)
        {
            if (_Session != null) _Session.InputFocusChanged -= OnInputFocusChanged;
            if(context==null || _InventoryScope!=context.RoomScope || _InventoryGeneration!=context.ConnectionGeneration) ClearRoundInventory();
            _InventoryScope=context?.RoomScope ?? "";_InventoryGeneration=context?.ConnectionGeneration ?? 0;
            _Session = context;
            if (_Session != null && isActiveAndEnabled)
                _Session.InputFocusChanged += OnInputFocusChanged;
            if (_Movement) ConfigureMovement();
        }

        void OnInputFocusChanged(bool focused)
        {
            if (!IsRelevantPlayer) return;
            if (focused) _QueuedJump = false;
            ConfigureMovement();
        }

        void Awake()
        {
            _Movement = GetComponent<HotelPlayerMovement>();
            _Visual = GetComponentInChildren<HotelFishSprite>(true);
            _Audio = GetComponent<HotelPlayerAudio>();
            _Movement.SetSimulationAuthority(!Networked);
            // Network avatars travel with the session. The authored offline Editor
            // preview belongs to Game and must unload with its scene and input maps.
            if (Networked) DontDestroyOnLoad(transform.root.gameObject);
            Identity.OnStartServer.AddListener(StartServer);
            Identity.OnStartClient.AddListener(StartClient);
            Identity.OnStopServer.AddListener(StopSimulation);
            Identity.OnStopClient.AddListener(StopSimulation);
            Identity.OnStartLocalPlayer.AddListener(RegisterLocalPlayer);
        }

        void StartServer()
        {
            RegisterActivePlayer();
            _Position = transform.position;
            _Movement.SetSimulationAuthority(true);
            ResetSceneMotion();
            _Movement.SettleController();
        }

        void StartClient()
        {
            RegisterActivePlayer();
            if (IsServer) return;
            _Movement.SetSimulationAuthority(false);
            transform.position = _Position;
            if (!_TriggerSensor)
            {
                var controller = GetComponent<CharacterController>();
                _TriggerSensor = new GameObject("Lobby trigger sensor");
                _TriggerSensor.tag = "Player";
                _TriggerSensor.layer = gameObject.layer;
                _TriggerSensor.transform.SetParent(transform, false);
                var sensor = _TriggerSensor.AddComponent<CapsuleCollider>();
                sensor.center = controller.center;
                sensor.height = controller.height;
                sensor.radius = controller.radius;
                sensor.isTrigger = true;
                var body = _TriggerSensor.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
            }
            _TriggerSensor.SetActive(true);
        }

        void OnEnable()
        {
            RegisterActivePlayer();
            if (_TriggerSensor && Connected && IsClient && !IsServer) _TriggerSensor.SetActive(true);
            RegisterLocalPlayer();
            if (_Session != null) _Session.InputFocusChanged += OnInputFocusChanged;
            StoryFunctions.OnSpeakerEvent += Speaker;
            DialogueManager.OnDialogueTryingToContinueEvent += StopTalking;
        }

        void OnDisable()
        {
            _OfflineInputSource = null;
            if (_DeathEmergence) _DeathEmergence.Stop();
            UnregisterActivePlayer();
            if (_Session != null) _Session.InputFocusChanged -= OnInputFocusChanged;
            StoryFunctions.OnSpeakerEvent -= Speaker;
            DialogueManager.OnDialogueTryingToContinueEvent -= StopTalking;
            StopSimulation();
        }

        void RegisterActivePlayer()
        {
            if (_ActivePlayers.Contains(this)) return;
            _ActivePlayers.Add(this);
            PlayerEnabled?.Invoke(this);
        }
        void UnregisterActivePlayer()
        {
            if (!_ActivePlayers.Remove(this)) return;
            PlayerDisabled?.Invoke(this);
            _GameCommands = null;
            _BossCommands = null;
        }
        void StopSimulation()
        {
            UnregisterActivePlayer();
            if (_TriggerSensor) _TriggerSensor.SetActive(false);
            ReleaseLocalPlayer();
            if (!_Movement) return;
            _Movement.SetControlState(false, false, true);
            _Movement.SetSimulationAuthority(false);
            ResetSceneMotion();
        }

        void Speaker(string speaker)
        {
            SetOwnedTalking(speaker == "player");
        }

        void StopTalking() => SetOwnedTalking(false);

        void SetOwnedTalking(bool talking)
        {
            if (!IsRelevantPlayer) return;
            if (Connected) SetTalking(talking);
            else _Talking = talking;
        }

        [ServerRpc]
        void SetTalking(bool value) => _Talking = !IsDead && value;

        void Update()
        {
            ConfigureMovement();
            if (!IsDead && IsRelevantPlayer && ControlsReady && !InBossFight && !BossHallwayLocked)
                SendOwnedInput();

            if (!Networked || (Connected && IsServer))
            {
                _Movement.Simulate(Time.deltaTime);
                _Stunned = _Movement.IsStunned;
                _Position = transform.position;
                _FacingLeft = _Movement.FacingLeft;
                _Walking = _Movement.Walking;
                _Running = _Movement.Running;
                _MouseLight = _Movement.LampPosition;
            }
            else if (Connected)
            {
                transform.position = Vector3.Lerp(transform.position, _Position,
                    1 - Mathf.Exp(-18 * Time.deltaTime));
            }

            if (_Visual) _Visual.gameObject.SetActive(ControlMode != HotelControlMode.Ghost && !InBossFight);
            _Movement.ShowLamp(_MouseLight, NetId);
            if (_TriggerSensor) _TriggerSensor.SetActive(!IsDead && Connected && IsClient && !IsServer);
            if (_Visual) _Visual.SetCorpse(IsDead);
            if (_Visual) _Visual.SetStunned(IsStunned && ControlsReady);
            if (_Visual) _Visual.Present(_FacingLeft, _Walking && ControlsReady, _Talking && ControlsReady, _Running && ControlsReady);
            if (_Audio) _Audio.Present(_Walking && ControlsReady && !InBossFight && ControlMode != HotelControlMode.Ghost, _Running);
            if (IsRelevantPlayer && ControlsReady && !InputBlocked && _Movement.InteractPressed) RequestInteractionSound();
        }

        void ConfigureMovement()
        {
            bool ownerReady = !Networked || Identity.Owner == null || Identity.Owner.SceneIsReady;
            bool sharedDialogue = DialogueManager.Instance && DialogueManager.Instance.IsSharedDialogue;
            bool inputBlocked = InputBlocked;
            var ready = !IsDead && ControlsReady && ownerReady && !InBossFight && !BossHallwayLocked && ControlMode != HotelControlMode.Selection &&
                (ControlMode != HotelControlMode.Fish || RoundReleased);
            _Movement.SetMode(ControlMode);
            _Movement.SetSimulationAuthority((!Networked || (Connected && IsServer)) && !IsDead && ControlMode != HotelControlMode.Ghost && !InBossFight && !BossHallwayLocked);
            _Movement.SetControlState(ready, IsRelevantPlayer, inputBlocked);
            _Movement.SetBossControlState(InBossFight && IsRelevantPlayer && ControlsReady && !inputBlocked);
            _Movement.SharedDialogueLocked = sharedDialogue;
            if (!ready || inputBlocked) _QueuedJump = false;
            if (IsStunned) _QueuedJump = false;
        }

        void SendOwnedInput()
        {
            Vector2 move; bool jump, run; Vector3 mouse;
            if (!Networked && _OfflineInputSource != null && !(_OfflineInputSource is Object owner && !owner) &&
                !(_OfflineInputSource is Behaviour behaviour && !behaviour.isActiveAndEnabled) &&
                !InputBlocked && _Movement.ControlsReady && _Movement.CanMove && !_Movement.SharedDialogueLocked &&
                ControlMode == HotelControlMode.Fish && !IsStunned && !_Movement.IsStunned)
            {
                _OfflineInputSource.ReadInput(this, out move, out jump, out mouse, out run);
                if (!Finite(move.x) || !Finite(move.y) || !Finite(mouse.x) || !Finite(mouse.y) || !Finite(mouse.z))
                { move = Vector2.zero; jump = run = false; mouse = transform.position; }
                else move = Vector2.ClampMagnitude(move, 1);
            }
            else
            {
                _Movement.ReadOwnedInput(out move, out jump, out mouse);
                run = _Movement.RunHeld;
            }
            run = _Movement.GateSprint(run, move.sqrMagnitude > .0001f);
            // Retain a one-frame jump until the next network send.
            _QueuedJump |= jump;
            if (Connected && Time.unscaledTime < _NextSend) return;

            if (!Connected)
                _Movement.AcceptInput(move, _QueuedJump, mouse, run);
            else
            {
                _NextSend = Time.unscaledTime + .05f;
                SetInput((int)ControlMode, move, _QueuedJump, mouse, run);
            }
            _QueuedJump = false;
        }

        [ServerRpc]
        void SetInput(int mode, Vector2 move, bool jump, Vector3 mouse, bool run)
        {
            ConfigureMovement();
            if (IsDead || !ControlsReady || InBossFight || BossHallwayLocked || (Identity.Owner != null && !Identity.Owner.SceneIsReady)) return;
            if (mode != (int)ControlMode || ControlMode == HotelControlMode.Selection) return;
            if (!Finite(move.x) || !Finite(move.y) || !Finite(mouse.x) || !Finite(mouse.y) || !Finite(mouse.z)) return;
            _Movement.AcceptInput(Vector2.ClampMagnitude(move, 1), jump, mouse, run && ControlMode == HotelControlMode.Fish);
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public void ResetSceneMotion()
        {
            ResetOwnedInput();
            _Movement.ClearTrapStun();
            if (!Networked || IsServer) _Stunned = false;
            if (_Visual) _Visual.SetStunned(false);
            _Movement.ResetMotion();
        }

        internal bool ApplyTrapStun(float direction, float distance, float impulseSeconds, float stunSeconds)
        {
            if (Networked && !IsServer || IsDead || !ControlsReady || !RoundReleased || InBossFight || BossHallwayLocked ||
                ControlMode != HotelControlMode.Fish || !_Movement.ApplyTrapStun(direction, distance, impulseSeconds, stunSeconds)) return false;
            _Stunned = true;
            ResetOwnedInput();
            return true;
        }

        void ResetOwnedInput()
        {
            _QueuedJump = false;
            _NextSend = 0;
            _Walking = _Running = false;
        }

        public void Teleport(Vector3 point)
        {
            _Movement.Teleport(point);
            if (!Networked || IsServer) _Stunned = false;
            if (_Visual) _Visual.SetStunned(false);
            _Position = transform.position;
            ResetOwnedInput();
            _MouseLight = point + Vector3.right;
        }

        internal void BecomeSpectator(float floorY)
        {
            if (IsDead || (Networked && !IsServer) || ControlMode != HotelControlMode.Fish) return;
            IsDead = true;
            ControlledCube = -1;
            HeldTrapFamily = "";
            ResetEditorRole();
            ResetSceneMotion();
            // Retain this network avatar as the corpse, resting on the hallway floor.
            var body = BodyController;
            float halfHeight = body ? Mathf.Max(body.radius, body.height * .5f) : .875f;
            float centerY = body ? body.center.y : 0;
            Teleport(new Vector3(transform.position.x, floorY + halfHeight - centerY, transform.position.z));
            _Movement.SetSimulationAuthority(false);
            if (_TriggerSensor) _TriggerSensor.SetActive(false);
        }

        [ServerRpc]
        public void RequestDialogue(int action, string story, Vector3 anchor, int choice, int revision,
            string inputKey, string inputValue)
        {
            if (!IsDead && SharedDialogue.Instance)
                SharedDialogue.Instance.HandleRequest(this, action, story, anchor, choice, revision, inputKey, inputValue);
        }

        [ServerRpc]
        public void RequestLobbyProp(int prop)
        {
            if (LobbyCursorManager.Instance && LobbyCursorManager.Instance.AcceptProp(this, prop)) RpcLobbyProp(prop);
        }

        public void PlayJumpSound()
        {
            if (Connected && IsServer) RpcFishSound(0);
            else if (!Networked && _Audio) _Audio.PlayEvent(0);
        }

        public void RequestInteractionSound()
        {
            if (Connected) ServerInteractionSound();
            else if (_Audio) _Audio.PlayEvent(1);
        }

        [ServerRpc]
        void ServerInteractionSound()
        {
            if (IsDead || !ControlsReady || ControlMode == HotelControlMode.Ghost || Time.unscaledTime < _NextInteractionSound) return;
            _NextInteractionSound = Time.unscaledTime + .15f;
            RpcFishSound(1);
        }

        [ClientRpc]
        void RpcFishSound(int kind) { if (_Audio) _Audio.PlayEvent(kind); }

        [ClientRpc]
        void RpcLobbyProp(int prop)
        {
            if (LobbyCursorManager.Instance) LobbyCursorManager.Instance.PlayProp(prop);
        }
    }
}
