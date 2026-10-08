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
        [SerializeField] Light _Lamp;
        [SerializeField] MeshRenderer _Marker;

        HotelPlayerMovement _Movement;
        CharacterController _Controller;
        float _Axis, _Vertical, _LastCommand;
        bool _PendingJump, _Run;
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
        public bool Walking => _Movement.SimulationReady && Mathf.Abs(_Axis) > .01f &&
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
            _PendingJump = _Run = false;
            _LastCommand = Time.unscaledTime;
            _LocalMouse = transform.position + Vector3.right;
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
            _Axis = Mathf.Clamp(move.x, -1, 1);
            _PendingJump |= jump;
            _Run = run && _Movement.Mode == HotelControlMode.Fish;
            _LastCommand = Time.unscaledTime;
            LampPosition = _Environment.ClampMouse(mouse);
        }

        public void Simulate(float delta)
        {
            if (!Active || !_Movement.SimulationReady)
            {
                ResetMotion();
                return;
            }
            if (Time.unscaledTime - _LastCommand > .3f || _Movement.SharedDialogueLocked)
            {
                _Axis = 0;
                _PendingJump = _Run = false;
            }
            if (_Controller.isGrounded)
            {
                if (_Vertical < 0) _Vertical = -2;
                if (_PendingJump && _Movement.CanMove) _Vertical = JumpSpeed;
            }
            _PendingJump = false;
            _Vertical -= _Movement.Gravity * delta;
            float horizontal = _Movement.CanMove ? _Axis * (_Run ? _Movement.RunningSpeed : _Movement.WalkingSpeed) : 0;
            if (!_Movement.TryMove(new Vector3(horizontal, _Vertical, 0) * delta))
            {
                ResetMotion();
                return;
            }
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
