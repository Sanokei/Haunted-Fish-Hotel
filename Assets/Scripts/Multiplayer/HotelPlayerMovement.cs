using UnityEngine;
using UnityEngine.InputSystem;

namespace HauntedFish.Multiplayer
{
    public enum HotelControlMode { Lobby, Selection, Fish, Ghost }
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
        int _ResumeAfterFrame;
        bool _AuthorityInitialized, _SimulationAuthority, _InputBlocked;
        bool _ControlStateInitialized, _LocalControl, _GameControl;

        public HotelControlMode Mode { get; private set; }
        public void SetMode(HotelControlMode mode)
        {
            if (Mode == mode) return;
            Mode = mode;
            ResetMotion();
            _ControlStateInitialized = false;
        }
        public bool ControlsReady { get; private set; }
        public bool SharedDialogueLocked { get; set; }
        public bool Walking { get; private set; }
        public bool Running => GameActive && Mode == HotelControlMode.Fish && Motor.Running;
        public bool InteractPressed => _GameInteract != null && _GameInteract.enabled && _GameInteract.WasPressedThisFrame();
        public Vector2 BossAxis => _BossMove != null && _BossMove.enabled ? Vector2.ClampMagnitude(_BossMove.ReadValue<Vector2>(), 1) : Vector2.zero;
        public bool RunHeld => Mode == HotelControlMode.Fish && ControlsReady && !_InputBlocked && _GameRun != null && _GameRun.enabled && _GameRun.IsPressed();
        public bool FacingLeft { get; private set; }
        public Vector3 LampPosition => Motor.LampPosition;
        public bool GameActive => Motor.Active;
        public string ActiveInputMap => _BossMap != null && _BossMap.enabled ? "BossFight" : _GameMap != null && _GameMap.enabled ? "Game" :
            _LobbyMap != null && _LobbyMap.enabled ? "Lobby" : "";

        public CharacterController BodyController => Controller;
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
            if (_OwnedActions) _OwnedActions.Disable();
            ControlsReady = false;
            _ControlStateInitialized = false;
            ResetMotion();
        }

        void OnDestroy()
        {
            if (_OwnedActions) Destroy(_OwnedActions);
        }

        public void SetSimulationAuthority(bool authority)
        {
            if (_AuthorityInitialized && _SimulationAuthority == authority) return;
            _AuthorityInitialized = true;
            _SimulationAuthority = authority;
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
            _GameControl = game;
            var wasReady = ControlsReady;
            var wasBlocked = _InputBlocked;
            ControlsReady = ready;
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
            bool canMove = Mode != HotelControlMode.Ghost && Mode != HotelControlMode.Selection && CanMove && ControlsReady && !_InputBlocked && !SharedDialogueLocked;
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
            if (Mode == HotelControlMode.Ghost || Mode == HotelControlMode.Selection)
            {
                ResetMotion();
                return;
            }
            if (GameActive)
            {
                Motor.Simulate(delta);
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

        public bool TryMove(Vector3 displacement) => TryMove(displacement, out _);

        public bool TryMove(Vector3 displacement, out Vector3 applied)
        {
            applied = Vector3.zero;
            if (!SimulationReady) return false;
            var scale = Controller.transform.lossyScale;
            if (Mathf.Abs(scale.x) < .0001f || Mathf.Abs(scale.y) < .0001f || Mathf.Abs(scale.z) < .0001f) return false;
            var before = transform.position;
            Controller.Move(displacement);
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

        public void Teleport(Vector3 point)
        {
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
