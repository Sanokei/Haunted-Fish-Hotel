// Compile-only stand-ins for the UnityEngine surface used by the boss-fight scripts. Behaviour is deliberately
// absent: this project exists to prove the production sources type-check, not to run them.
using System;
using System.Collections;
namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class MinAttribute : Attribute { public MinAttribute(float min) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } }
    public class Object
    {
        public static implicit operator bool(Object value) => !ReferenceEquals(value, null);
        public int GetInstanceID() => 0;
        public string name;
    }
    public class Component : Object { public GameObject gameObject; public Transform transform; public T GetComponent<T>() => default; }
    public class Behaviour : Component { public bool enabled; public bool isActiveAndEnabled => enabled; }
    public class Coroutine { }
    public class WaitForSecondsRealtime : IEnumerator { public WaitForSecondsRealtime(float seconds) { } public object Current => null; public bool MoveNext() => false; public void Reset() { } }
    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator routine) => null;
        public void StopCoroutine(Coroutine routine) { }
        public void StopAllCoroutines() { }
    }
    public class GameObject : Object { public bool activeSelf; public Transform transform; public void SetActive(bool value) { } public T GetComponent<T>() => default; }
    public class Transform : Component { public Vector3 position, localPosition, localScale; public Quaternion localRotation; }
    public struct Quaternion { public static float Angle(Quaternion a, Quaternion b) => 0; }
    public class Sprite : Object { }
    public class Renderer : Component { public bool enabled, forceRenderingOff; }
    public class SpriteRenderer : Renderer { public Sprite sprite; }
    public class AnimationClip : Object { public float length; }
    public class RuntimeAnimatorController : Object { public AnimationClip[] animationClips; }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => default;
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 ClampMagnitude(Vector2 v, float max) => v;
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => default;
        public static float Distance(Vector3 a, Vector3 b) => 0;
        public static Vector3 up => new Vector3(0, 1, 0);
        public Vector3 normalized => this;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float s) => new Vector3(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator *(float s, Vector3 a) => a * s;
        public static Vector3 ClampMagnitude(Vector3 v, float max) => v;
    }
    public struct Rect { public bool Contains(Vector2 point) => true; }
    public struct Ray { public Vector3 origin, direction; public Vector3 GetPoint(float distance) => origin; }
    public struct Plane { public Plane(Vector3 normal, Vector3 point) { } public bool Raycast(Ray ray, out float enter) { enter = 0; return true; } }
    public struct Bounds { public Vector3 center; public Bounds(Vector3 center, Vector3 size) { this.center = center; } }
    public static class Mathf
    {
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
        public static float Clamp01(float v) => Clamp(v, 0, 1);
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Abs(float a) => a < 0 ? -a : a;
    }
    public static class Time { public static float unscaledTime, unscaledDeltaTime, fixedDeltaTime, timeScale; }
    public static class Random { public static float Range(float a, float b) => a; }
    public static class JsonUtility { public static string ToJson(object value) => ""; public static T FromJson<T>(string json) => default; }
    public class TextMesh : Component { public string text; }
    public class Camera : Behaviour { public Rect pixelRect; public Ray ScreenPointToRay(Vector2 point) => default; }
    public class Collider : Component { public new bool enabled; public Rigidbody attachedRigidbody; }
    public class Collision { public Rigidbody rigidbody; public Collider collider; }
    public class Rigidbody : Component
    {
        public Vector3 position, linearVelocity, angularVelocity;
        public bool isKinematic, detectCollisions;
        public void MovePosition(Vector3 target) { }
    }
    public class Animator : Behaviour
    {
        public float speed;
        public RuntimeAnimatorController runtimeAnimatorController;
        public void SetFloat(int id, float value) { }
        public static int StringToHash(string name) => 0;
        public void Play(int stateNameHash, int layer, float normalizedTime) { }
    }
}
