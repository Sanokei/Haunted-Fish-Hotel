using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Monologue.Dialogue;
using HauntedFish.Multiplayer;
using UnityEngine;

static class DoorPushTests
{
    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
        void Near(float value, float target, string why) => Check(Math.Abs(value - target) < .01f, why);
        var ui = new StoryUI();
        var rect = new RectTransform { anchoredPosition = new Vector2(0, 0), sizeDelta = new Vector2(1392, 590),
            localScale = Vector3.one, pivot = new Vector2(.5f, .5f) };
        var shape = new IntroductionPanelShape { Slant = 0 };
        rect.Shape = shape;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(StoryUI).GetField("_Content", flags).SetValue(ui, new GameObject());
        typeof(StoryUI).GetField("_Elements", flags).SetValue(ui, new[] { new StoryUI.Element { Tag = "door_panel", Target = rect } });
        typeof(StoryUI).GetField("_Layouts", flags).SetValue(ui, new[] {
            new StoryUI.Layout { Tag = "letter_under_door", Poses = new[] {
                new StoryUI.Pose { Target = rect, Position = new Vector2(-320, 0), Size = new Vector2(752, 590),
                    Scale = Vector3.one, Slant = 120 } } } });
        var step = new StorySequenceStep(StorySequenceStepKind.PushTo, "letter_under_door", seconds: .8f);
        ui.ValidateSequence(new[] { step });
        ui.Prepare();
        Near(shape.Slant, 0, "Door background is rectangular at authored rest.");
        Time.unscaledDeltaTime = .2f;
        IEnumerator Begin(out IEnumerator parent)
        {
            parent = ui.Play(step);
            Check(parent.MoveNext() && parent.Current is IEnumerator, "Actual StoryUI push supplies its authored tween.");
            return (IEnumerator)parent.Current;
        }
        var tween = Begin(out var play);
        tween.MoveNext(); tween.MoveNext(); tween.MoveNext();
        Near(shape.Slant, 60, "Door background smoothly reaches half skew at the middle of the push.");
        Near(rect.sizeDelta.x, 1072, "Mid-push size preserves the existing pose interpolation.");
        ui.Prepare();
        Near(shape.Slant, 60, "Repeated Prepare during an active sequence does not reset the animation.");
        while (tween.MoveNext()) { }
        play.MoveNext();
        Near(shape.Slant, 120, "Door background reaches the authored rhombus at the end of the actual push.");
        Near(rect.anchoredPosition.x, -320, "Existing push destination is preserved.");
        Near(rect.pivot.x, .5f, "Push preserves the authored door pivot.");
        ui.Close();
        Near(shape.Slant, 0, "Close restores rectangle after a complete push.");
        Near(rect.sizeDelta.x, 1392, "Close restores authored rest dimensions.");
        Near(rect.anchoredPosition.x, 0, "Close restores authored rest position.");
        ui.Prepare();
        tween = Begin(out play);
        tween.MoveNext(); tween.MoveNext();
        Check(shape.Slant > 0 && shape.Slant < 120, "Replay starts a fresh partial skew.");
        (tween as IDisposable)?.Dispose(); (play as IDisposable)?.Dispose();
        ui.Close();
        Near(shape.Slant, 0, "Skipping a partial push restores the rectangle.");
        ui.Prepare();
        tween = Begin(out play);
        while (tween.MoveNext()) { }
        play.MoveNext();
        Near(shape.Slant, 120, "Repeated play never accumulates extra skew.");
        ui.Close();
        Near(shape.Slant, 0, "Repeated close restores rectangle again.");
        string prefab = File.ReadAllText(Path.Combine(root, "Assets/Resources/LobbyIntroductionUI.prefab"));
        int shapeStart = prefab.IndexOf("--- !u!114 &3720855840725677550");
        int next = prefab.IndexOf("--- !u!", shapeStart + 10);
        Check(prefab.Substring(shapeStart, next - shapeStart).Contains("_Slant: 0"), "Production Door section background starts rectangular.");
        int pushStart = prefab.IndexOf("  - Tag: letter_under_door\n    Poses:");
        if (pushStart < 0) pushStart = prefab.IndexOf("  - Tag: letter_under_door\r\n    Poses:");
        int reading = prefab.IndexOf("  - Tag: letter_read", pushStart);
        Check(prefab.Substring(pushStart, reading - pushStart).Contains("Slant: 120"), "Production push still targets its rhombus.");
        return checks;
    }
}

namespace HauntedFish.Multiplayer
{
    public class IntroductionPanelShape : UnityEngine.Object { public float Slant { get; set; } }
    public class IntroductionSkipCircle : UnityEngine.Object { public float Progress; }
}
namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)] public class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    public partial class MonoBehaviour
    {
        public static RectTransform Instantiate(RectTransform prefab, RectTransform parent, bool world) => new RectTransform();
        public static void Destroy(Object target) { }
    }
    public class GameObject : Object
    {
        public bool activeSelf = true;
        public RectTransform transform;
        public void SetActive(bool value) => activeSelf = value;
    }
    public class RectTransform : Object
    {
        public readonly GameObject gameObject;
        public Vector2 anchoredPosition, sizeDelta, pivot;
        public Vector3 localScale = Vector3.one;
        public Quaternion localRotation = Quaternion.identity;
        public HauntedFish.Multiplayer.IntroductionPanelShape Shape;
        public RectTransform() { gameObject = new GameObject { transform = this }; }
        public T GetComponent<T>() where T : class => Shape as T;
    }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float a, float b) { x = a; y = b; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 one => new Vector2(1, 1);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => new Vector2(Mathf.Lerp(a.x,b.x,t), Mathf.Lerp(a.y,b.y,t));
    }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float a,float b,float c) { x=a;y=b;z=c; }
        public static Vector3 one => new Vector3(1,1,1);
        public static Vector3 Lerp(Vector3 a,Vector3 b,float t) => new Vector3(Mathf.Lerp(a.x,b.x,t),Mathf.Lerp(a.y,b.y,t),Mathf.Lerp(a.z,b.z,t));
    }
    public struct Quaternion
    {
        public static Quaternion identity => new Quaternion();
        public static Quaternion Euler(float x,float y,float z) => identity;
        public static Quaternion operator *(Quaternion a,Quaternion b) => identity;
    }
    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public static float Min(float a,float b) => Math.Min(a,b);
        public static float Clamp01(float value) => Math.Max(0,Math.Min(1,value));
        public static float Lerp(float a,float b,float t) => a+(b-a)*t;
        public static float Sin(float value) => (float)Math.Sin(value);
    }
    public static class Time { public static float unscaledDeltaTime; }
    public class WaitForSecondsRealtime { public WaitForSecondsRealtime(float seconds) { } }
}
