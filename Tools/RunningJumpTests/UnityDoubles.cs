// Deterministic adapter doubles only. This is not Unity CharacterController physics.
using System;
namespace UnityEngine
{
    public class Object { public static implicit operator bool(Object value) => value != null; }
    public class SerializeField : Attribute { }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    public class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public class MonoBehaviour : Object { public Transform transform = new Transform(); public T GetComponent<T>() where T : class => null; }
    public class Transform { public Vector3 position; }
    public class GameObject { public bool activeSelf; public void SetActive(bool value) => activeSelf = value; }
    public class Light : Object { public GameObject gameObject = new GameObject(); }
    public class MeshRenderer : Object { public bool enabled; }
    public class CharacterController : Object { public bool isGrounded; }
    [Flags] public enum CollisionFlags { None = 0, Sides = 1, Above = 2, Below = 4 }
    public static class Time { public static float unscaledTime; }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => default;
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => default;
        public static Vector3 right => new Vector3(1, 0, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator *(Vector3 a, float value) => new Vector3(a.x * value, a.y * value, a.z * value);
    }
    public static class Mathf { public static float Abs(float value) => Math.Abs(value); }
    public class Camera : Object { public Ray ScreenPointToRay(Vector2 value) => default; }
    public struct Ray { public Vector3 GetPoint(float value) => default; }
    public struct Plane { public Plane(Vector3 a, Vector3 b) { } public bool Raycast(Ray ray, out float distance) { distance = 0; return false; } }
}
namespace UnityEngine.InputSystem
{
    public class Mouse { public static Mouse current; public Position position; }
    public class Position { public UnityEngine.Vector2 ReadValue() => default; }
}
namespace HauntedFish.Multiplayer
{
    public enum HotelControlMode { Fish, Lobby }
    public class HotelPlayerMovement : UnityEngine.Object
    {
        public bool SimulationReady = true, IsStunned, CanMove = true, SharedDialogueLocked;
        public float Gravity = 25, WalkingSpeed = 1.8f, RunningSpeed = 4.5f;
        public HotelControlMode Mode;
        public UnityEngine.CollisionFlags LastCollisionFlags;
        public UnityEngine.Vector3 Displacement;
        public UnityEngine.Transform Transform;
        public UnityEngine.CharacterController Controller;
        public int JumpSounds;
        public bool FailMove;
        public float Ceiling = float.PositiveInfinity;
        public void PlayJumpSound() => JumpSounds++;
        public bool TryMove(UnityEngine.Vector3 value)
        {
            Displacement = value;
            if (FailMove) return false;
            var position = Transform.position + value;
            LastCollisionFlags = UnityEngine.CollisionFlags.None;
            if (position.y >= Ceiling) { position.y = Ceiling; LastCollisionFlags |= UnityEngine.CollisionFlags.Above; }
            if (position.y <= 0) { position.y = 0; LastCollisionFlags |= UnityEngine.CollisionFlags.Below; }
            Controller.isGrounded = (LastCollisionFlags & UnityEngine.CollisionFlags.Below) != 0;
            Transform.position = position;
            return true;
        }
        public void FaceDirection(UnityEngine.Vector3 value) { }
    }
    public static class HotelViewCamera { public static UnityEngine.Camera Current; }
    public class GameSceneController : UnityEngine.Object, IGameMovementEnvironment
    {
        public UnityEngine.Vector3 ClampMouse(UnityEngine.Vector3 point) => point;
        public UnityEngine.Vector3 ClampFish(HotelPlayerMovement movement, UnityEngine.Vector3 point) => point;
    }
}
