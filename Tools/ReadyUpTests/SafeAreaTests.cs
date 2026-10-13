using System;
using System.Reflection;
using HauntedFish.Multiplayer;
using UnityEngine;

static class SafeAreaTests
{
    static int checks;
    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
    static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    static void Call(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    static bool Near(float a, float b) => Math.Abs(a - b) < .01f;
    static void Main()
    {
        var owner = new RoundHowToSafeArea(); var viewport = new RectTransform();
        var safe = new RectTransform(); var page = new RectTransform { sizeDelta = new Vector2(1280, 720) };
        Set(owner, "_Viewport", viewport); Set(owner, "_SafeArea", safe); Set(owner, "_Page", page);
        Check(owner.HasBindings, "Safe layout retains all authored references");
        Screen.width = 1280; Screen.height = 720; Screen.safeArea = new Rect(0, 0, 1280, 720); viewport.rect = Screen.safeArea;
        Call(owner, "OnEnable"); Check(Canvas.Subscribers == 1, "Enable subscribes once to render geometry");
        foreach (var display in new[] { new[] {1280,720,0,0,1280,720}, new[] {1920,1080,0,0,1920,1080}, new[] {1024,768,0,0,1024,768}, new[] {1280,800,0,0,1280,800}, new[] {3440,1440,0,0,3440,1440}, new[] {2340,1080,108,40,2124,1000}, new[] {800,1280,20,40,760,1180} })
        {
            Screen.width = display[0]; Screen.height = display[1]; Screen.safeArea = new Rect(display[2], display[3], display[4], display[5]);
            float canvasScale = Math.Min(display[0] / 1280f, display[1] / 720f);
            viewport.rect = new Rect(0, 0, display[0] / canvasScale, display[1] / canvasScale);
            Canvas.Render();
            Check(Near(safe.anchorMin.x, display[2] / (float)display[0]) && Near(safe.anchorMin.y, display[3] / (float)display[1]), "Safe area minimum follows display inset");
            Check(Near(safe.anchorMax.x, (display[2] + display[4]) / (float)display[0]) && Near(safe.anchorMax.y, (display[3] + display[5]) / (float)display[1]), "Safe area maximum follows display inset");
            Check(page.localScale.x > 0 && page.localScale.x <= 1 && Near(page.localScale.x, page.localScale.y), "Uniform page scale preserves authored proportions");
            Check(1280 * page.localScale.x * canvasScale <= display[4] + .01f && 720 * page.localScale.y * canvasScale <= display[5] + .01f, "Entire guide fits safe width and height");
            Check(safe.offsetMin == Vector2.zero && safe.offsetMax == Vector2.zero, "No leftover safe-area offsets");
            var previous = page.localScale; Canvas.Render(); Check(page.localScale == previous, "Unchanged display does not drift layout");
        }
        var before = page.localScale;
        Call(owner, "OnDisable"); Check(Canvas.Subscribers == 0, "Disable removes render callback");
        Screen.width = 2560; Screen.height = 1440; Screen.safeArea = new Rect(0,0,2560,1440); viewport.rect = new Rect(0,0,1280,720);
        Canvas.Render(); Check(page.localScale == before, "Disabled layout leaves authored transforms alone");
        Call(owner, "OnEnable"); Check(Canvas.Subscribers == 1 && Near(page.localScale.x, 1), "Re-enable refreshes changed display once");
        Screen.width = 0; Canvas.Render(); Check(Near(page.localScale.x, 1), "Transient zero display cannot collapse the guide");
        Screen.width = 2560; Screen.safeArea = new Rect(0,0,0,0); Canvas.Render(); Check(Near(page.localScale.x,1), "Transient empty safe area cannot collapse the guide");
        Call(owner, "OnDisable");
        Console.WriteLine("Passed " + checks + " actual RoundHowToSafeArea adapter checks using Unity geometry doubles; no native render claimed.");
    }
}

namespace UnityEngine
{
    public class Object { public static implicit operator bool(Object value) => value != null; }
    public class MonoBehaviour : Object { }
    public sealed class SerializeField : Attribute { }
    public struct Vector2
    {
        public float x, y; public Vector2(float x, float y) { this.x=x; this.y=y; }
        public static Vector2 zero => new Vector2(0,0);
        public static Vector2 Scale(Vector2 a, Vector2 b) => new Vector2(a.x*b.x,a.y*b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x-b.x,a.y-b.y);
        public static bool operator ==(Vector2 a, Vector2 b) => a.x==b.x && a.y==b.y;
        public static bool operator !=(Vector2 a, Vector2 b) => !(a==b);
        public override bool Equals(object o) => o is Vector2 v && this==v;
        public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode();
    }
    public struct Vector3
    {
        public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 one => new Vector3(1,1,1);
        public static Vector3 operator *(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);
        public static bool operator ==(Vector3 a,Vector3 b)=>a.x==b.x&&a.y==b.y&&a.z==b.z;
        public static bool operator !=(Vector3 a,Vector3 b)=>!(a==b);
        public override bool Equals(object o)=>o is Vector3 v&&this==v;
        public override int GetHashCode()=>x.GetHashCode()^y.GetHashCode()^z.GetHashCode();
    }
    public struct Rect
    {
        public float x,y,width,height; public Rect(float x,float y,float width,float height){this.x=x;this.y=y;this.width=width;this.height=height;}
        public float xMin=>x;public float xMax=>x+width;public float yMin=>y;public float yMax=>y+height;
        public Vector2 size=>new Vector2(width,height);
        public static bool operator ==(Rect a,Rect b)=>a.x==b.x&&a.y==b.y&&a.width==b.width&&a.height==b.height;
        public static bool operator !=(Rect a,Rect b)=>!(a==b);
        public override bool Equals(object o)=>o is Rect r&&this==r;
        public override int GetHashCode()=>x.GetHashCode()^y.GetHashCode()^width.GetHashCode()^height.GetHashCode();
    }
    public sealed class RectTransform : Object { public Rect rect; public Vector2 sizeDelta, anchorMin, anchorMax, offsetMin, offsetMax; public Vector3 localScale=Vector3.one; }
    public static class Screen { public static int width,height;public static Rect safeArea; }
    public static class Canvas { public static event Action willRenderCanvases; public static int Subscribers=>willRenderCanvases?.GetInvocationList().Length??0;public static void Render()=>willRenderCanvases?.Invoke(); }
    public static class Mathf { public static float Clamp01(float x)=>Math.Max(0,Math.Min(1,x));public static float Min(float x,float y)=>Math.Min(x,y); }
}
