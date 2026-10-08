// Only engine-facing dependencies are doubled. The Ink compiler/runtime and all
// dialogue code under test are linked directly from production source.
using System;
using HauntedFish.Multiplayer;
using Ink.Runtime;
using Monologue.Dialogue;

namespace UnityEngine
{
    public class Object
    {
        bool destroyed;
        public static implicit operator bool(Object value) => value != null && !value.destroyed;
        public static void Destroy(Object value) { if (value != null) value.destroyed = true; }
        public static T Instantiate<T>(T prefab, Transform parent) where T : Component
        {
            if (prefab is OptionPrefab)
                return (T)(Component)new OptionPrefab { OptionTextGO = new TMPro.TMP_Text() };
            throw new NotSupportedException(typeof(T).Name);
        }
    }
    public class GameObject : Object
    {
        public bool activeSelf = true;
        public bool activeInHierarchy => activeSelf;
        public readonly Transform transform;
        public GameObject() { transform = new RectTransform(this); }
        public void SetActive(bool active) => activeSelf = active;
    }
    public class Component : Object
    {
        GameObject owner;
        public GameObject gameObject => owner ??= new GameObject();
        protected Component(GameObject owner = null) { this.owner = owner; }
        public Transform transform => gameObject.transform;
        public T GetComponentInChildren<T>(bool includeInactive) where T : Component => null;
    }
    public class MonoBehaviour : Component { }
    public class Transform : Component
    {
        public Transform(GameObject owner) : base(owner) { }
        public Vector3 localScale;
    }
    public class RectTransform : Transform
    {
        public RectTransform(GameObject owner) : base(owner) { }
        public Vector3 anchoredPosition3D;
    }
    public class TextAsset : Object
    {
        public readonly string text;
        public TextAsset(string text) { this.text = text; }
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new(0, 0, 0);
        public static Vector3 one => new(1, 1, 1);
    }
    public struct Color { }
    public struct Color32
    {
        public Color32(byte r, byte g, byte b, byte a) { }
        public static implicit operator Color(Color32 color) => new();
    }
    public class Canvas { public static void ForceUpdateCanvases() { } }
    [AttributeUsage(AttributeTargets.Field)] public class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public class HideInInspector : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public class HeaderAttribute : Attribute
    {
        public HeaderAttribute(string name) { }
    }
}
namespace UnityEngine.UI
{
    public class Image : UnityEngine.Component { public UnityEngine.Color color; }
    public class LayoutGroup : UnityEngine.Component { }
    public static class LayoutRebuilder
    {
        public static void ForceRebuildLayoutImmediate(UnityEngine.RectTransform transform) { }
    }
}
namespace TMPro { public class TMP_Text : UnityEngine.Component { public string text; } }
namespace UnityEngine.EventSystems
{
    public interface IPointerClickHandler { void OnPointerClick(PointerEventData data); }
    public interface IPointerEnterHandler { void OnPointerEnter(PointerEventData data); }
    public interface IPointerExitHandler { void OnPointerExit(PointerEventData data); }
    public class PointerEventData
    {
        public enum InputButton { Left, Right, Middle }
        public InputButton button;
    }
}
namespace UnityEngine.InputSystem
{
    public class ButtonControl { public bool wasPressedThisFrame; }
    public class Keyboard
    {
        public static Keyboard current;
        public readonly ButtonControl spaceKey = new(), eKey = new(), fKey = new();
    }
    public class Mouse
    {
        public static Mouse current;
        public readonly ButtonControl leftButton = new();
    }
}
namespace HauntedFish.Multiplayer
{
    public enum DialogueAudience { RelevantPlayer, Everyone }
    public class SharedDialogue : UnityEngine.MonoBehaviour
    {
        public static SharedDialogue Instance;
        public static bool Applying;
        public bool RouteEnabled;
        public readonly System.Collections.Generic.List<(int Action, int Index)> Routed = new();
        public bool Route(int action, int index = -1)
        {
            if (!RouteEnabled) return false;
            Routed.Add((action, index));
            return true;
        }
        public bool Open(UnityEngine.TextAsset asset, UnityEngine.Vector3 anchor) => false;
    }
    public class DialogueSnapshot
    {
        public bool active, inputActive;
        public UnityEngine.Vector3 anchor;
        public string storyState, text, speaker, inputQuestion, inputKey;
        public string[] choices;
    }
    public struct SharedWorldCue { public int Type; public string First; }
    public static class WorldDialogueCanvas
    {
        public static void Place(object panel, UnityEngine.Vector3 anchor) { }
    }
}
namespace Monologue.StoryInput
{
    public class StoryInputTextFieldManager : UnityEngine.MonoBehaviour
    {
        public static StoryInputTextFieldManager Instance;
        public static event Action OnStoryInputStartEvent, OnStoryInputEndEvent;
        public bool ActiveInputPanel;
        public object InputPanel;
        public void EnterInputMode()
        {
            OnStoryInputStartEvent?.Invoke();
            ActiveInputPanel = true;
        }
        public void ExitInputMode()
        {
            ActiveInputPanel = false;
            OnStoryInputEndEvent?.Invoke();
        }
        public void ApplySharedInput(bool active, string question, string key)
        {
            if (active && !ActiveInputPanel) EnterInputMode();
            else if (!active && ActiveInputPanel) ExitInputMode();
        }
    }
}
namespace Monologue.Dialogue
{
    public static class StoryFunctions
    {
        public static int BindCount, UnbindCount, Effects, Tags, Inputs;
        public static void BindFunctions(Story story)
        {
            BindCount++;
            story.BindExternalFunction("TestEffect", () => Effects++);
            story.BindExternalFunction("AskInput", () =>
            {
                Inputs++;
                Monologue.StoryInput.StoryInputTextFieldManager.Instance.EnterInputMode();
            });
        }
        public static void UnbindFunctions(Story story)
        {
            UnbindCount++;
            story.UnbindExternalFunction("TestEffect");
            story.UnbindExternalFunction("AskInput");
        }
        public static void HandleTags(Story story) => Tags++;
        public static void ApplyNetworkCue(SharedWorldCue cue) { }
    }
}
public static class DontDestroyHelper
{
    public static event Action NotDestroyedHelperEvent;
    public static void Notify() => NotDestroyedHelperEvent?.Invoke();
}
public class ChangeSceneOnLoadDontDestroy : UnityEngine.MonoBehaviour
{
    public static ChangeSceneOnLoadDontDestroy Instance;
    public void NextScene() { }
}
