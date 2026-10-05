using Mirage;
using Monologue.Dialogue;
using Monologue.StoryInput;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    [RequireComponent(typeof(NetworkIdentity), typeof(HotelPlayerMovement))]
    public sealed class HotelPlayer : NetworkBehaviour
    {
        public static HotelPlayer LocalPlayer { get; private set; }
        public static event System.Action<HotelPlayer> LocalPlayerChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLocalPlayer()
        {
            LocalPlayer = null;
            LocalPlayerChanged = null;
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

        [SyncVar] Vector3 _Position;
        [SyncVar] bool _FacingLeft;
        [SyncVar] bool _Walking;
        [SyncVar] bool _Talking;
        [SyncVar] Vector3 _MouseLight;

        HotelSessionContext _Session;
        HotelPlayerMovement _Movement;
        HotelFishSprite _Visual;
        float _NextSend;
        bool _QueuedJump;
        bool _ControlStateInitialized;
        (bool authority, bool ready, bool local, bool blocked, bool sharedDialogue, bool game) _ControlState;

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
            _Movement.SetSimulationAuthority(!Networked);
            DontDestroyOnLoad(transform.root.gameObject);
            Identity.OnStartServer.AddListener(StartServer);
            Identity.OnStartClient.AddListener(StartClient);
            Identity.OnStopServer.AddListener(StopSimulation);
            Identity.OnStopClient.AddListener(StopSimulation);
            Identity.OnStartLocalPlayer.AddListener(RegisterLocalPlayer);
        }

        void StartServer()
        {
            _Position = transform.position;
            _Movement.SetSimulationAuthority(true);
            ResetSceneMotion();
            _Movement.SettleController();
        }

        void StartClient()
        {
            if (IsServer) return;
            _Movement.SetSimulationAuthority(false);
            transform.position = _Position;
        }

        void OnEnable()
        {
            RegisterLocalPlayer();
            if (_Session != null) _Session.InputFocusChanged += OnInputFocusChanged;
            StoryFunctions.OnSpeakerEvent += Speaker;
            DialogueManager.OnDialogueTryingToContinueEvent += StopTalking;
        }

        void OnDisable()
        {
            if (_Session != null) _Session.InputFocusChanged -= OnInputFocusChanged;
            StoryFunctions.OnSpeakerEvent -= Speaker;
            DialogueManager.OnDialogueTryingToContinueEvent -= StopTalking;
            StopSimulation();
        }

        void StopSimulation()
        {
            ReleaseLocalPlayer();
            if (!_Movement) return;
            _ControlStateInitialized = false;
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
        void SetTalking(bool value) => _Talking = value;

        void Update()
        {
            ConfigureMovement();
            if (IsRelevantPlayer && ControlsReady)
                SendOwnedInput();

            if (!Networked || (Connected && IsServer))
            {
                _Movement.Simulate(Time.deltaTime);
                _Position = transform.position;
                _FacingLeft = _Movement.FacingLeft;
                _Walking = _Movement.Walking;
                _MouseLight = _Movement.LampPosition;
            }
            else if (Connected)
            {
                transform.position = Vector3.Lerp(transform.position, _Position,
                    1 - Mathf.Exp(-18 * Time.deltaTime));
            }

            _Movement.ShowLamp(_MouseLight, NetId);
            if (_Visual) _Visual.Present(_FacingLeft, _Walking && ControlsReady, _Talking && ControlsReady);
        }

        void ConfigureMovement()
        {
            bool ownerReady = !Networked || Identity.Owner == null || Identity.Owner.SceneIsReady;
            bool sharedDialogue = DialogueManager.Instance && DialogueManager.Instance.IsSharedDialogue;
            bool inputBlocked = InputBlocked;
            var state = (authority: !Networked || (Connected && IsServer), ready: ControlsReady && ownerReady,
                local: IsRelevantPlayer, blocked: inputBlocked, sharedDialogue, game: _Movement.GameActive);
            if (_ControlStateInitialized && _ControlState == state) return;
            _ControlStateInitialized = true;
            _ControlState = state;
            _Movement.SetSimulationAuthority(state.authority);
            _Movement.SetControlState(state.ready, state.local, state.blocked);
            _Movement.SharedDialogueLocked = sharedDialogue;
            if (!state.ready || inputBlocked) _QueuedJump = false;
        }

        void SendOwnedInput()
        {
            _Movement.ReadOwnedInput(out var move, out var jump, out var mouse);
            // Retain a one-frame jump until the next network send.
            _QueuedJump |= jump;
            if (Connected && Time.unscaledTime < _NextSend) return;

            if (!Connected)
                _Movement.AcceptInput(move, _QueuedJump, mouse);
            else
            {
                _NextSend = Time.unscaledTime + .05f;
                SetInput(_Movement.GameActive, move, _QueuedJump, mouse);
            }
            _QueuedJump = false;
        }

        [ServerRpc]
        void SetInput(bool game, Vector2 move, bool jump, Vector3 mouse)
        {
            ConfigureMovement();
            if (!ControlsReady || (Identity.Owner != null && !Identity.Owner.SceneIsReady)) return;
            if (game != _Movement.GameActive) return;
            if (!Finite(move.x) || !Finite(move.y) || !Finite(mouse.x) || !Finite(mouse.y) || !Finite(mouse.z)) return;
            _Movement.AcceptInput(Vector2.ClampMagnitude(move, 1), jump, mouse);
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public void ResetSceneMotion()
        {
            ResetOwnedInput();
            _Movement.ResetMotion();
        }

        void ResetOwnedInput()
        {
            _QueuedJump = false;
            _NextSend = 0;
            _Walking = false;
        }

        public void Teleport(Vector3 point)
        {
            _Movement.Teleport(point);
            _Position = point;
            ResetOwnedInput();
            _MouseLight = point + Vector3.right;
        }

        [ServerRpc]
        public void RequestDialogue(int action, string story, Vector3 anchor, int choice, int revision,
            string inputKey, string inputValue)
        {
            if (SharedDialogue.Instance)
                SharedDialogue.Instance.HandleRequest(this, action, story, anchor, choice, revision, inputKey, inputValue);
        }
    }
}
