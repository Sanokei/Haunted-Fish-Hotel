using System;
namespace UnityEngine
{
 [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField:Attribute{}
 [AttributeUsage(AttributeTargets.Field)] public sealed class HideInInspector:Attribute{}
 public class Object { public static implicit operator bool(Object value)=>value!=null; }
 public class MonoBehaviour:Object{}
 public class GameObject:Object { public bool activeSelf=true; public void SetActive(bool active){activeSelf=active;} }
 public class Canvas:Object { public bool enabled; public static event Action willRenderCanvases; public static void Render()=>willRenderCanvases?.Invoke(); }
 public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} public static Vector2 zero=>new Vector2(0,0); }
 public struct Rect { public float xMin,yMin,xMax,yMax; public Rect(float x,float y,float w,float h){xMin=x;yMin=y;xMax=x+w;yMax=y+h;} public static bool operator==(Rect a,Rect b)=>a.xMin==b.xMin&&a.yMin==b.yMin&&a.xMax==b.xMax&&a.yMax==b.yMax;public static bool operator!=(Rect a,Rect b)=>!(a==b); public override bool Equals(object other)=>other is Rect r&&this==r;public override int GetHashCode()=>xMin.GetHashCode(); }
 public class RectTransform:Object { public Vector2 anchorMin,anchorMax,offsetMin,offsetMax; }
 public static class Screen { public static int width=1920,height=1080;public static Rect safeArea=new Rect(0,0,1920,1080); }
 public static class Mathf { public static float Clamp01(float v)=>v<0?0:v>1?1:v; }
}
namespace UnityEngine.UI { public class Text:UnityEngine.Object{} public class Image:UnityEngine.Object{public bool enabled=true;} }
namespace TMPro { public class TMP_Text:UnityEngine.Object{} }
namespace HauntedFish.BossFight { public sealed class BossArenaSnapshot { public int FishScore,GhostScore; public bool Cancelled; } }
