using UnityEngine;
using UnityEngine.InputSystem;

namespace HauntedFish.Multiplayer
{
    public sealed class GameSideScrollMotor : MonoBehaviour
    {
        public float JumpSpeed = 12;
        [SerializeField] Light _Lamp;
        [SerializeField] MeshRenderer _Marker;

        HotelPlayerMovement _Movement;
        CharacterController _Controller;
        float _Axis, _Vertical, _LastCommand;
        bool _PendingJump, _WasActive;
        Vector3 _LocalMouse;

        public bool Active => GameSceneDefinition.Current;
        public Vector3 LampPosition { get; private set; }
        public bool Walking => _Movement.SimulationReady && Mathf.Abs(_Axis) > .01f &&
            _Movement.CanMove && !_Movement.SharedDialogueLocked;

        void Awake()
        {
            _Movement = GetComponent<HotelPlayerMovement>();
            _Controller = GetComponent<CharacterController>();
            if (!_Lamp || !_Marker)
                Debug.LogError("Assign the authored mouse-light child in the player prefab.", this);
            if (_Lamp) _Lamp.gameObject.SetActive(false);
        }

        public void ResetMotion()
        {
            _Axis = _Vertical = 0;
            _PendingJump = false;
            _LastCommand = Time.unscaledTime;
            _LocalMouse = transform.position + Vector3.right;
        }

        public Vector3 ReadMouse()
        {
            if (Mouse.current != null && Camera.main)
            {
                var ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out var distance))
                    _LocalMouse = ray.GetPoint(distance);
            }
            return GameSceneDefinition.Current.ClampMouse(_LocalMouse);
        }

        public void Accept(float horizontal, bool jump, Vector3 mouse)
        {
            if (!Active || !_Movement.SimulationReady) return;
            _Axis = Mathf.Clamp(horizontal, -1, 1);
            _PendingJump |= jump;
            _LastCommand = Time.unscaledTime;
            LampPosition = GameSceneDefinition.Current.ClampMouse(mouse);
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
                _PendingJump = false;
            }
            if (_Controller.isGrounded)
            {
                if (_Vertical < 0) _Vertical = -2;
                if (_PendingJump && _Movement.CanMove) _Vertical = JumpSpeed;
            }
            _PendingJump = false;
            _Vertical -= _Movement.Gravity * delta;
            float horizontal = _Movement.CanMove ? _Axis * _Movement.WalkingSpeed : 0;
            if (!_Movement.TryMove(new Vector3(horizontal, _Vertical, 0) * delta))
            {
                ResetMotion();
                return;
            }
            var point = transform.position;
            point.z = 0;
            transform.position = point;
            if (Walking) _Movement.FaceDirection(Vector3.right * _Axis);
        }

        public void ShowLamp(Vector3 worldPosition, uint playerId)
        {
            if (!_Lamp) return;
            bool active = Active && _Movement.ControlsReady;
            if (_Lamp.gameObject.activeSelf != active) _Lamp.gameObject.SetActive(active);
            if (active)
            {
                if (!_WasActive)
                {
                    var definition = GameSceneDefinition.Current;
                    if (_Marker && definition.LampMaterial) _Marker.sharedMaterial = definition.LampMaterial;
                    _Lamp.color = Color.HSVToRGB((playerId * .23f) % 1, .45f, 1);
                }
                _Lamp.transform.position = worldPosition;
            }
            _WasActive = active;
        }
    }
}
