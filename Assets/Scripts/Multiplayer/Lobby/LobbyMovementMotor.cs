using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // Lobby locomotion owns XZ movement and gravity independently of game roles.
    public sealed class LobbyMovementMotor
    {
        Vector2 _Input;
        float _VerticalSpeed, _LastInput;
        public void Accept(Vector2 input) { _Input = Vector2.ClampMagnitude(input, 1); _LastInput = Time.unscaledTime; }
        public void Reset() { _Input = Vector2.zero; _VerticalSpeed = 0; }
        public bool Simulate(HotelPlayerMovement movement, CharacterController controller, float delta)
        {
            if (Time.unscaledTime - _LastInput > .3f || movement.SharedDialogueLocked) _Input = Vector2.zero;
            if (controller.isGrounded && _VerticalSpeed < 0) _VerticalSpeed = -2;
            _VerticalSpeed -= movement.Gravity * delta;
            var direction = Vector3.right * _Input.x + Vector3.forward * _Input.y;
            if (!movement.TryMove((direction * (movement.CanMove ? movement.LobbyWalkingSpeed : 0) + Vector3.up * _VerticalSpeed) * delta))
            { Reset(); return false; }
            bool walking = direction.sqrMagnitude > .001f && movement.CanMove;
            if (walking) movement.FaceDirection(direction);
            return walking;
        }
    }
}
