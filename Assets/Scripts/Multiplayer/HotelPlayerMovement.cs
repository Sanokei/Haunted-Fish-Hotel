using UnityEngine;
using HauntedFish.InputPrompts;
using UnityEngine.InputSystem;

namespace HauntedFish.Multiplayer
{
    public enum HotelControlMode { Lobby, Selection, Fish, Ghost, Spectator }
    // fixme epsilon
    [RequireComponent(typeof(CharacterController), typeof(GameSideScrollMotor))]  
    public sealed class HotelPlayerMovement : MonoBehaviour
    {
        public InputActionAsset InputActions;
        public float WalkingSpeed = 1.8f;
        public float RunningSpeed = 4.5f;
        public float LobbyWalkingSpeed = 6f;
        public float Gravity = 25f;
        public bool CanMove = true;

        InputActionAsset _OwnedActions;
        InputActionMap _LobbyMap, _GameMap, _BossMap;
        HotelPlayer _Actor;
        InputAction _LobbyMove, _GameMove, _GameJump, _GameRun, _GameInteract, _BossMove;
        CharacterController _Controller;
        GameSideScrollMotor _GameMotor;
        readonly LobbyMovementMotor _LobbyMotor = new LobbyMovementMotor();
        readonly TrapStunState _TrapStun = new TrapStunState();
        public bool IsStunned => _TrapStun.Active;
        bool StunBlocksInput => IsStunned || _Actor && _Actor.IsStunned;
        int _ResumeAfterFrame;
        bool _AuthorityInitialized, _SimulationAuthority, _InputBlocked;
        bool _ControlStateInitialized, _LocalControl, _GameControl;

        public HotelControlMode Mode { get; private set; }
        public void SetMode(HotelControlMode mode)
        {
            if (Mode == mode) return;
            Mode = mode;
            ClearTrapStun();
            ResetMotion();
            _ControlStateInitialized = false;
        }
        public bool ControlsReady { get; private set; }
        public bool SharedDialogueLocked { get; set; }
        public bool Walking { get; private set; }
        public bool Running => GameActive && Mode == HotelControlMode.Fish && Motor.Running;
        public bool InteractPressed => _GameInteract != null && _GameInteract.enabled && _GameInteract.WasPressedThisFrame();
        public Vector2 BossAxis => _BossMove != null && _BossMove.enabled ? Vector2.ClampMagnitude(_BossMove.ReadValue<Vector2>(), 1) : Vector2.zero;
        [Header("Sprint stamina")]
        [Tooltip("Seconds of continuous sprint from a full bar.")]
        [SerializeField, Min(.1f)] float _SprintSeconds = 1.2f;
        [Tooltip("Seconds to refill an empty bar while not sprinting.")]
        [SerializeField, Min(.1f)] float _SprintRecoverSeconds = 2.5f;
        [Tooltip("After running dry, sprint unlocks again once the bar refills to this share.")]
        [SerializeField, Range(0, 1)] float _SprintRestart = .35f;
        bool _Winded;
        public float Stamina { get; private set; } = 1;
        public bool Winded => _Winded;
        // Local owner only: spends stamina while sprinting and refills it otherwise.
        public bool GateSprint(bool requested, bool moving)
        {
            float dt = Time.deltaTime;
            bool sprint = requested && moving && !_Winded && Stamina > 0;
            if (sprint)
            {
                Stamina = Mathf.Max(0, Stamina - dt / _SprintSeconds);
                if (Stamina <= 0) _Winded = true;
            }
            else
            {
                Stamina = Mathf.Min(1, Stamina + dt / _SprintRecoverSeconds);
                if (_Winded && Stamina >= _SprintRestart) _Winded = false;
            }
            return sprint;
        }
        public bool RunHeld => Mode == HotelControlMode.Fish && ControlsReady && !_InputBlocked && !StunBlocksInput && _GameRun != null && _GameRun.enabled && _GameRun.IsPressed();
        public bool FacingLeft { get; private set; }
        public Vector3 LampPosition => Motor.LampPosition;
        public bool GameActive => Motor.Active;
        public string ActiveInputMap => _BossMap != null && _BossMap.enabled ? "BossFight" : _GameMap != null && _GameMap.enabled ? "Game" :
            _LobbyMap != null && _LobbyMap.enabled ? "Lobby" : "";

        public CharacterController BodyController => Controller;
        public void PlayJumpSound() { if (_Actor) _Actor.PlayJumpSound(); }
        CharacterController Controller => _Controller ? _Controller : (_Controller = GetComponent<CharacterController>());
        GameSideScrollMotor Motor => _GameMotor ? _GameMotor : (_GameMotor = GetComponent<GameSideScrollMotor>());

        // Collision state needs a frame to settle after activation or teleport.
        public bool SimulationReady => ControlsReady && _SimulationAuthority && isActiveAndEnabled &&
            Controller.enabled && Controller.gameObject.activeInHierarchy &&
            Time.frameCount > _ResumeAfterFrame;

        void Awake()
        {
            _Actor = GetComponent<HotelPlayer>();
            _Controller = GetComponent<CharacterController>();
            _GameMotor = GetComponent<GameSideScrollMotor>();
            if (!InputActions) return;
            // Private maps prevent one avatar from enabling another avatar's controls.
            _OwnedActions = Instantiate(InputActions);
            _LobbyMap = _OwnedActions.FindActionMap("Lobby", true);
            _GameMap = _OwnedActions.FindActionMap("Game", true);
            _LobbyMove = _LobbyMap.FindAction("Move", true);
            _GameMove = _GameMap.FindAction("Move", true);
            _GameJump = _GameMap.FindAction("Jump", true);
            _GameRun = _GameMap.FindAction("Run", true);
            _GameInteract = _GameMap.FindAction("Interact", true);
            _BossMap = _OwnedActions.FindActionMap("BossFight", true);
            _BossMove = _BossMap.FindAction("Move", true);

        }

        void OnEnable()
        {
            _ControlStateInitialized = false;
            _ResumeAfterFrame = Time.frameCount + 1;
        }

        void OnDisable()
        {
            UnregisterPromptMaps();
            if (_OwnedActions) _OwnedActions.Disable();
            ControlsReady = false;
            ClearTrapStun();
            _ControlStateInitialized = false;
            ResetMotion();
        }

        void OnDestroy()
        {
            UnregisterPromptMaps();
            if (_OwnedActions) Destroy(_OwnedActions);
        }

        void UnregisterPromptMaps()
        {
            InputPromptActions.Unregister(_LobbyMap); InputPromptActions.Unregister(_GameMap); InputPromptActions.Unregister(_BossMap);
        }

        public void SetSimulationAuthority(bool authority)
        {
            if (_AuthorityInitialized && _SimulationAuthority == authority) return;
            _AuthorityInitialized = true;
            _SimulationAuthority = authority;
            if (!authority) { ClearTrapStun(); ResetMotion(); }
            Controller.enabled = authority;
            if (authority) SettleController();
        }

        public void SetControlState(bool ready, bool local, bool inputBlocked)
        {
            var game = GameActive;
            var blocked = local && inputBlocked;
            if (_ControlStateInitialized && ControlsReady == ready && _LocalControl == local &&
                _InputBlocked == blocked && _GameControl == game) return;
            _ControlStateInitialized = true;
            _LocalControl = local;
            if (local) { InputPromptActions.Register(_LobbyMap); InputPromptActions.Register(_GameMap); InputPromptActions.Register(_BossMap); }
            else UnregisterPromptMaps();
            _GameControl = game;
            var wasReady = ControlsReady;
            var wasBlocked = _InputBlocked;
            ControlsReady = ready;
            if (!ready) ClearTrapStun();
            _InputBlocked = blocked;
            SetMap(_LobbyMap, ready && local && !blocked && !game);
            SetMap(_GameMap, ready && local && !blocked && Mode == HotelControlMode.Fish);
            if ((wasReady && !ready) || (!wasBlocked && _InputBlocked)) ResetMotion();
        }

        public void SetBossControlState(bool enabled) => SetMap(_BossMap, enabled);

        static void SetMap(InputActionMap map, bool enabled)
        {
            if (map == null || map.enabled == enabled) return;
            if (enabled) map.Enable();
            else map.Disable();
        }

        public void ReadOwnedInput(out Vector2 move, out bool jump, out Vector3 mouse)
        {
            bool canMove = Mode != HotelControlMode.Ghost && Mode != HotelControlMode.Selection && Mode != HotelControlMode.Spectator && CanMove && ControlsReady && !_InputBlocked && !SharedDialogueLocked && !StunBlocksInput;
            var action = GameActive ? _GameMove : _LobbyMove;
            move = canMove && action != null ? Vector2.ClampMagnitude(action.ReadValue<Vector2>(), 1) : Vector2.zero;
            if (!GameActive && move != Vector2.zero)
            {
                var camera = HotelViewCamera.Current;
                var right = camera ? Vector3.ProjectOnPlane(camera.transform.right, Vector3.up) : Vector3.right;
                right = right.sqrMagnitude > .0001f ? right.normalized : Vector3.right;
                var forward = Vector3.Cross(right, Vector3.up);
                var direction = right * move.x + forward * move.y;
                // Send world XZ input; the server must not reinterpret another player's camera axes.
                move = new Vector2(direction.x, direction.z);
            }
            jump = Mode == HotelControlMode.Fish && canMove && _GameJump != null && _GameJump.WasPressedThisFrame();
            mouse = GameActive ? Motor.ReadMouse() : Vector3.zero;
        }

        public void AcceptInput(Vector2 move, bool jump, Vector3 mouse, bool run = false)
        {
            if (!SimulationReady) return;
            if (StunBlocksInput) { move = Vector2.zero; jump = run = false; }
            if (GameActive) Motor.Accept(move, jump, mouse, run);
            else _LobbyMotor.Accept(move);
        }

        public void Simulate(float delta)
        {
            if (!SimulationReady)
            {
                ResetMotion();
                return;
            }
            if (Mode == HotelControlMode.Ghost || Mode == HotelControlMode.Selection || Mode == HotelControlMode.Spectator)
            {
                ResetMotion();
                return;
            }
            if (GameActive)
            {
                // Global gameplay/dialogue locks pause the authority's impulse and
                // status clock. Local menus never pause or cancel a server stun.
                var stun = _TrapStun.Advance(delta, !CanMove || SharedDialogueLocked);
                Motor.Simulate(delta, stun.BlocksInput, stun.HorizontalDisplacement);
                Walking = Motor.Walking;
                return;
            }

            Walking = _LobbyMotor.Simulate(this, Controller, delta);
        }

        public void FaceDirection(Vector3 direction)
        {
            // Lobby movement is on XZ; choose left/right relative to the active view.
            var camera = HotelViewCamera.Current;
            float horizontal = GameActive || !camera ? direction.x :
                Vector3.Dot(direction, camera.transform.right);
            if (Mathf.Abs(horizontal) > .01f) FacingLeft = horizontal < 0;
        }

        public CollisionFlags LastCollisionFlags { get; private set; }

        public bool TryMove(Vector3 displacement) => TryMove(displacement, out _);

        public bool TryMove(Vector3 displacement, out Vector3 applied)
        {
            applied = Vector3.zero;
            if (!SimulationReady) return false;
            var scale = Controller.transform.lossyScale;
            if (Mathf.Abs(scale.x) < .0001f || Mathf.Abs(scale.y) < .0001f || Mathf.Abs(scale.z) < .0001f) return false;
            var before = transform.position;
            var flags = Controller.Move(displacement);
            LastCollisionFlags = flags;
            if (GameActive && (!_Actor || !_Actor.InBossFight) && GameSceneController.Current)
                transform.position = GameSceneController.Current.ClampFish(this, transform.position);
            applied = transform.position - before;
            // Keep the original return contract (valid controller), while callers
            // that push bodies can inspect the displacement collisions allowed.
            return true;
        }

        public void ResetMotion()
        {
            _LobbyMotor.Reset();
            Walking = false;
            Motor.ResetMotion();
        }

        internal bool ApplyTrapStun(float direction, float distance, float impulseSeconds, float stunSeconds)
        {
            if (!SimulationReady || Mode != HotelControlMode.Fish || !_TrapStun.Apply(direction, distance, impulseSeconds, stunSeconds)) return false;
            // Reject already queued movement/jumps, while preserving airborne gravity.
            Motor.ClearInput();
            Walking = false;
            return true;
        }
        public void ClearTrapStun() => _TrapStun.Clear();

        public void Teleport(Vector3 point)
        {
            ClearTrapStun();
            bool wasEnabled = Controller.enabled;
            Controller.enabled = false;
            transform.position = GameActive && (!_Actor || !_Actor.InBossFight) && GameSceneController.Current ? GameSceneController.Current.ClampFish(this, point) : point;
            Controller.enabled = wasEnabled;
            ResetMotion();
            SettleController();
        }

        public void SettleController()
        {
            _ResumeAfterFrame = Time.frameCount + 1;
            if (Controller.enabled && gameObject.activeInHierarchy) Physics.SyncTransforms();
        }

        public void ShowLamp(Vector3 point, uint playerId) => Motor.ShowLamp(point, playerId);
    }
}
