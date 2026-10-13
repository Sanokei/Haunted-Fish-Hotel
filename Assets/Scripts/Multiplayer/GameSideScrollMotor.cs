using UnityEngine;
using UnityEngine.InputSystem;

namespace HauntedFish.Multiplayer
{
    public interface IGameMovementEnvironment
    {
        Vector3 ClampMouse(Vector3 point);
    }

    public sealed class GameSideScrollMotor : MonoBehaviour
    {
        public float JumpSpeed = 7;
        [Tooltip("Horizontal speed for an intentional grounded run-jump. Zero preserves ordinary running speed. Hold run and the launch direction; release or reverse cancels the boost.")]
        [SerializeField, Min(0)] float _RunningJumpSpeed;
        public float RunningJumpSpeed => _RunningJumpSpeed;
        [SerializeField] Light _Lamp;
        [SerializeField] MeshRenderer _Marker;

        HotelPlayerMovement _Movement;
        CharacterController _Controller;
        float _Axis, _Vertical, _LastCommand;
        bool _PendingJump, _Run, _JumpRun;
        float _JumpAxis;
        readonly RunningJumpState _RunningJump = new RunningJumpState();
        Vector3 _LocalMouse;

        IGameMovementEnvironment _Environment;
        public bool Active => _Environment != null &&
            !(_Environment is Object owner && !owner);

        public void Bind(IGameMovementEnvironment environment)
        {
            if (ReferenceEquals(_Environment, environment)) return;
            _Environment = environment;
            ResetMotion();
        }

        public void Unbind(IGameMovementEnvironment environment)
        {
            if (ReferenceEquals(_Environment, environment)) Bind(null);
        }
        public Vector3 LampPosition { get; private set; }
        public bool Running => Walking && _Run;
        public bool Walking => _Movement.SimulationReady && !_Movement.IsStunned && Mathf.Abs(_Axis) > .01f &&
            _Movement.CanMove && !_Movement.SharedDialogueLocked;

        void Awake()
        {
            _Movement = GetComponent<HotelPlayerMovement>();
            _Controller = GetComponent<CharacterController>();
            if (_Lamp) _Lamp.gameObject.SetActive(false);
            if (_Marker) _Marker.enabled = false;
        }

        public void ResetMotion()
        {
            _Axis = _Vertical = 0;
            ClearInput();
            _LastCommand = Time.unscaledTime;
            _LocalMouse = transform.position + Vector3.right;
        }

        public void ClearInput()
        {
            _Axis = _JumpAxis = 0;
            _PendingJump = _Run = _JumpRun = false;
            _RunningJump.Clear();
        }

        public Vector3 ReadMouse()
        {
            if (!Active) return Vector3.zero;
            var camera = HotelViewCamera.Current;
            if (Mouse.current != null && camera)
            {
                var ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out var distance))
                    _LocalMouse = ray.GetPoint(distance);
            }
            return _Environment.ClampMouse(_LocalMouse);
        }

        public void Accept(Vector2 move, bool jump, Vector3 mouse, bool run = false)
        {
            if (!Active || !_Movement.SimulationReady) return;
            if (_Movement.IsStunned || !_Movement.CanMove || _Movement.SharedDialogueLocked)
            { ClearInput(); move = Vector2.zero; jump = run = false; }
            _Axis = RunningJumpState.ClampAxis(move.x);
            _Run = run && _Movement.Mode == HotelControlMode.Fish;
            if (jump && !_PendingJump)
            {
                _PendingJump = true;
                // Retain the actual jump command's intent, rather than granting
                // a boost if a later airborne/movement packet happens to run.
                _JumpRun = _Run;
                _JumpAxis = _Axis;
            }
            _LastCommand = Time.unscaledTime;
            LampPosition = _Environment.ClampMouse(mouse);
        }

        public void Simulate(float delta, bool stunned = false, float knockback = 0)
        {
            if (!Active || !_Movement.SimulationReady)
            {
                ResetMotion();
                return;
            }
            if (Time.unscaledTime - _LastCommand > .3f || _Movement.SharedDialogueLocked || stunned || !_Movement.CanMove)
                ClearInput();
            if (!RunningJumpState.Finite(delta) || delta <= 0) return;
            if (_Controller.isGrounded)
            {
                _RunningJump.Clear();
                if (_Vertical < 0) _Vertical = -2;
                if (_PendingJump && _Movement.CanMove)
                {
                    _Vertical = JumpSpeed;
                    _RunningJump.Begin(true, _Movement.Mode == HotelControlMode.Fish, _JumpRun, _JumpAxis, _RunningJumpSpeed);
                    _Movement.PlayJumpSound();
                }
            }
            _PendingJump = _JumpRun = false;
            // Integrate constant acceleration over this step; using the final
            // velocity for the whole step made jump height depend on frame rate.
            float verticalStep = _Vertical * delta - .5f * _Movement.Gravity * delta * delta;
            _Vertical -= _Movement.Gravity * delta;
            float horizontal = _RunningJump.HorizontalVelocity(_Axis, _Run,
                !_Movement.CanMove || _Movement.SharedDialogueLocked || stunned,
                _Movement.WalkingSpeed, _Movement.RunningSpeed, _RunningJumpSpeed);
            if (!_Movement.CanMove || _Movement.SharedDialogueLocked) knockback = 0;
            if (!_Movement.TryMove(new Vector3(horizontal * delta + knockback, verticalStep, 0)))
            {
                ResetMotion();
                return;
            }
            if (_Vertical > 0 && (_Movement.LastCollisionFlags & CollisionFlags.Above) != 0) _Vertical = 0;
            if ((_Movement.LastCollisionFlags & CollisionFlags.Below) != 0) _RunningJump.Clear();
            var point = transform.position;
            if (_Environment is GameSceneController scene) point = scene.ClampFish(_Movement, point);
            else point.z = 0;
            transform.position = point;
            if (Walking) _Movement.FaceDirection(Vector3.right * _Axis);
        }

        public void ShowLamp(Vector3 worldPosition, uint playerId)
        {
            // The local screen shader owns the spotlight; network avatars have no light or marker.
            if (_Lamp && _Lamp.gameObject.activeSelf) _Lamp.gameObject.SetActive(false);
            if (_Marker) _Marker.enabled = false;
        }
    }
}
