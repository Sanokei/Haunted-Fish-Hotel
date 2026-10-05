using UnityEngine;
using UnityEngine.InputSystem;

namespace HauntedFish.Multiplayer
{
    // fixme epsilon
    [RequireComponent(typeof(CharacterController), typeof(GameSideScrollMotor))]  
    public sealed class HotelPlayerMovement : MonoBehaviour
    {
        public InputActionAsset InputActions;
        public float WalkingSpeed = 4.5f;
        public float Gravity = 25f;
        public bool CanMove = true;

        InputActionAsset _OwnedActions;
        InputActionMap _LobbyMap, _GameMap;
        InputAction _LobbyMove, _GameMove, _GameJump;
        CharacterController _Controller;
        GameSideScrollMotor _GameMotor;
        Vector2 _Input;
        float _VerticalSpeed, _LastInput;
        int _ResumeAfterFrame;
        bool _AuthorityInitialized, _SimulationAuthority, _InputBlocked;

        public bool ControlsReady { get; private set; }
        public bool SharedDialogueLocked { get; set; }
        public bool Walking { get; private set; }
        public bool FacingLeft { get; private set; }
        public Vector3 LampPosition => Motor.LampPosition;
        public bool GameActive => Motor.Active;
        public string ActiveInputMap => _GameMap != null && _GameMap.enabled ? "Game" :
            _LobbyMap != null && _LobbyMap.enabled ? "Lobby" : "";

        CharacterController Controller => _Controller ? _Controller : (_Controller = GetComponent<CharacterController>());
        GameSideScrollMotor Motor => _GameMotor ? _GameMotor : (_GameMotor = GetComponent<GameSideScrollMotor>());

        // Collision state needs a frame to settle after activation or teleport.
        public bool SimulationReady => ControlsReady && _SimulationAuthority && isActiveAndEnabled &&
            Controller.enabled && Controller.gameObject.activeInHierarchy &&
            Time.frameCount > _ResumeAfterFrame && Controller.bounds.size.sqrMagnitude > .000001f;

        void Awake()
        {
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
        }

        void OnEnable() => _ResumeAfterFrame = Time.frameCount + 1;

        void OnDisable()
        {
            if (_OwnedActions) _OwnedActions.Disable();
            ControlsReady = false;
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
            var wasReady = ControlsReady;
            var wasBlocked = _InputBlocked;
            ControlsReady = ready;
            _InputBlocked = local && inputBlocked;
            SetMap(_LobbyMap, ready && local && !inputBlocked && !GameActive);
            SetMap(_GameMap, ready && local && !inputBlocked && GameActive);
            if ((wasReady && !ready) || (!wasBlocked && _InputBlocked)) ResetMotion();
        }

        static void SetMap(InputActionMap map, bool enabled)
        {
            if (map == null || map.enabled == enabled) return;
            if (enabled) map.Enable();
            else map.Disable();
        }

        public void ReadOwnedInput(out Vector2 move, out bool jump, out Vector3 mouse)
        {
            bool canMove = CanMove && ControlsReady && !_InputBlocked && !SharedDialogueLocked;
            var action = GameActive ? _GameMove : _LobbyMove;
            move = canMove && action != null ? Vector2.ClampMagnitude(action.ReadValue<Vector2>(), 1) : Vector2.zero;
            jump = GameActive && canMove && _GameJump != null && _GameJump.WasPressedThisFrame();
            mouse = GameActive ? Motor.ReadMouse() : Vector3.zero;
        }

        public void AcceptInput(Vector2 move, bool jump, Vector3 mouse)
        {
            if (!SimulationReady) return;
            _LastInput = Time.unscaledTime;
            _Input = Vector2.ClampMagnitude(move, 1);
            if (GameActive) Motor.Accept(move.x, jump, mouse);
        }

        public void Simulate(float delta)
        {
            if (!SimulationReady)
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

            if (Time.unscaledTime - _LastInput > .3f || SharedDialogueLocked) _Input = Vector2.zero;
            if (Controller.isGrounded && _VerticalSpeed < 0) _VerticalSpeed = -2;
            _VerticalSpeed -= Gravity * delta;
            var direction = Vector3.forward * _Input.x - Vector3.right * _Input.y;
            if (!TryMove((direction * (CanMove ? WalkingSpeed : 0) + Vector3.up * _VerticalSpeed) * delta))
            {
                ResetMotion();
                return;
            }
            Walking = direction.sqrMagnitude > .001f && CanMove;
            if (Walking) FaceDirection(direction);
        }

        public void FaceDirection(Vector3 direction)
        {
            // Lobby movement is on XZ; choose left/right relative to the active view.
            float horizontal = GameActive || !Camera.main ? direction.x :
                Vector3.Dot(direction, Camera.main.transform.right);
            if (Mathf.Abs(horizontal) > .01f) FacingLeft = horizontal < 0;
        }

        public bool TryMove(Vector3 displacement)
        {
            if (!SimulationReady) return false;
            var scale = Controller.transform.lossyScale;
            if (Mathf.Abs(scale.x) < .0001f || Mathf.Abs(scale.y) < .0001f || Mathf.Abs(scale.z) < .0001f) return false;
            Controller.Move(displacement);
            return true;
        }

        public void ResetMotion()
        {
            _Input = Vector2.zero;
            _VerticalSpeed = 0;
            Walking = false;
            Motor.ResetMotion();
        }

        public void Teleport(Vector3 point)
        {
            bool wasEnabled = Controller.enabled;
            Controller.enabled = false;
            transform.position = point;
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
