// Deliberately small source-test boundary doubles. NOT native Input System, hardware, or UI evidence.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace UnityEngine
{
    public class Object { public static implicit operator bool(Object value) => value != null; }
    public class MonoBehaviour : Object { }
    public class ScriptableObject : Object { }
    public class Sprite : Object { public string name; }
    public class SerializeField : Attribute { }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    public class TextAreaAttribute : Attribute { }
    public class CreateAssetMenuAttribute : Attribute { public string menuName; }
    public enum RuntimeInitializeLoadType { SubsystemRegistration, BeforeSceneLoad }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { } }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float sqrMagnitude => x*x+y*y;
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x-b.x,a.y-b.y);
    }
}
namespace UnityEngine.UI
{
    public class Image : Object { public Sprite sprite; public bool enabled, preserveAspect; }
    public class Text : Object { public string text; public bool enabled; }
}
namespace TMPro
{
    public class TMP_SpriteAsset : UnityEngine.Object
    {
        public readonly List<string> names = new List<string>();
        public int GetSpriteIndexFromName(string name) => names.IndexOf(name);
    }
    public class TMP_Text : UnityEngine.Object { public TMP_SpriteAsset spriteAsset; public bool richText; public string text; }
}
namespace UnityEngine.InputSystem.Utilities
{
    public struct ReadOnlyArray<T> : IEnumerable<T>
    {
        readonly T[] items;
        public ReadOnlyArray(T[] items) { this.items = items; }
        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(items ?? Array.Empty<T>())).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public static implicit operator ReadOnlyArray<T>(T[] items) => new ReadOnlyArray<T>(items);
    }
}
namespace UnityEngine.InputSystem.LowLevel
{
    public struct StateEvent { }
    public struct DeltaStateEvent { }
    public sealed class InputEventPtr
    {
        public readonly Dictionary<InputControl,object> values = new Dictionary<InputControl,object>();
        public Type eventType = typeof(StateEvent);
        public bool IsA<T>() => eventType == typeof(T);
        public IEnumerable<InputControl> EnumerateChangedControls(InputDevice device) => values.Keys.Where(c => !Equals(c.current,values[c]));
    }
}
namespace UnityEngine.InputSystem.Controls
{
    using UnityEngine.InputSystem.LowLevel;
    public class ButtonControl : InputControl
    {
        public ButtonControl() { current = 0f; }
        public float ReadValue() => (float)current;
        public bool ReadValueFromEvent(InputEventPtr e, out float value)
        { if(e.values.TryGetValue(this,out object raw)) {value=(float)raw;return true;} value=0;return false; }
    }
    public class Vector2Control : InputControl
    {
        public Vector2Control() { current = new Vector2(); }
        public Vector2 ReadValue() => (Vector2)current;
        public bool ReadValueFromEvent(InputEventPtr e, out Vector2 value)
        { if(e.values.TryGetValue(this,out object raw)) {value=(Vector2)raw;return true;} value=default;return false; }
    }
    public class StickControl : Vector2Control { }
    public class DpadControl
    { public ButtonControl up=new ButtonControl(), down=new ButtonControl(),left=new ButtonControl(),right=new ButtonControl(); }
}
namespace UnityEngine.InputSystem
{
    using Controls; using LowLevel; using Utilities;
    public class InputControl { public bool noisy, synthetic; public object current; }
    public class InputDevice : InputControl { public bool added=true,enabled=true; public string layout="Unknown"; }
    public class Keyboard : InputDevice
    {
        public new static Keyboard current;
        public ButtonControl spaceKey=new ButtonControl(),enterKey=new ButtonControl(),eKey=new ButtonControl();
        public Keyboard() { layout="Keyboard"; }
    }
    public class Mouse : InputDevice
    {
        public new static Mouse current;
        public Vector2Control delta=new Vector2Control(),scroll=new Vector2Control();
        public ButtonControl leftButton=new ButtonControl(),rightButton=new ButtonControl(),middleButton=new ButtonControl(),forwardButton=new ButtonControl(),backButton=new ButtonControl();
        public Mouse() { layout="Mouse"; }
    }
    public class Gamepad : InputDevice
    {
        public StickControl leftStick=new StickControl(),rightStick=new StickControl();
        public ButtonControl buttonSouth=new ButtonControl(),buttonEast=new ButtonControl(),buttonWest=new ButtonControl(),buttonNorth=new ButtonControl(),leftShoulder=new ButtonControl(),rightShoulder=new ButtonControl(),leftTrigger=new ButtonControl(),rightTrigger=new ButtonControl(),leftStickButton=new ButtonControl(),rightStickButton=new ButtonControl(),startButton=new ButtonControl(),selectButton=new ButtonControl();
        public DpadControl dpad=new DpadControl();
        public Gamepad() { layout="Gamepad"; }
    }
    public enum InputDeviceChange { Added, Removed, Disconnected, Reconnected, Disabled, Enabled, ConfigurationChanged }
    public enum InputActionChange { BoundControlsChanged, ActionEnabled, ActionDisabled }
    public static class InputSystem
    {
        public static event Action<InputEventPtr,InputDevice> onEvent;
        public static event Action<InputDevice,InputDeviceChange> onDeviceChange;
        public static event Action<object,InputActionChange> onActionChange;
        public static readonly List<InputDevice> devices=new List<InputDevice>();
        public static int EventListeners => onEvent?.GetInvocationList().Length ?? 0;
        public static int DeviceListeners => onDeviceChange?.GetInvocationList().Length ?? 0;
        public static int ActionListeners => onActionChange?.GetInvocationList().Length ?? 0;
        public static bool IsFirstLayoutBasedOnSecond(string layout,string parent)
        {
            if(layout==parent)return true;
            if(parent=="Gamepad")return new[]{"DualShockGamepad","DualShock4GamepadHID","DualSenseGamepadHID","XInputController","XInputControllerWindows","XboxGamepad","CustomXInput","CustomPad"}.Contains(layout);
            if(parent=="DualShockGamepad")return layout=="DualShock4GamepadHID" || layout=="DualSenseGamepadHID";
            if(parent=="XInputController")return layout=="XInputControllerWindows" || layout=="CustomXInput";
            return false;
        }
        public static T Add<T>(T device) where T:InputDevice
        {
            devices.Add(device); if(device is Keyboard k)Keyboard.current=k;if(device is Mouse m)Mouse.current=m;
            Change(device,InputDeviceChange.Added);return device;
        }
        public static void Change(InputDevice device,InputDeviceChange change)
        {
            if(change==InputDeviceChange.Removed || change==InputDeviceChange.Disconnected)device.added=false;
            if(change==InputDeviceChange.Disabled)device.enabled=false;
            if(change==InputDeviceChange.Enabled)device.enabled=true;
            if(change==InputDeviceChange.Added || change==InputDeviceChange.Reconnected)device.added=true;
            onDeviceChange?.Invoke(device,change);
        }
        public static void BindingChanged(object owner) => onActionChange?.Invoke(owner,InputActionChange.BoundControlsChanged);
        public static void ActionChanged(object owner,InputActionChange kind) => onActionChange?.Invoke(owner,kind);
        public static void Emit(InputDevice device,params object[] controlsAndValues)
        {
            var e=new InputEventPtr {eventType=typeof(DeltaStateEvent)};
            for(int i=0;i<controlsAndValues.Length;i+=2)e.values[(InputControl)controlsAndValues[i]]=controlsAndValues[i+1];
            Emit(device,e);
        }
        public static void Emit(InputDevice device,InputEventPtr e)
        { onEvent?.Invoke(e,device);foreach(var p in e.values)p.Key.current=p.Value; }
        public static void Clear() { devices.Clear();Keyboard.current=null;Mouse.current=null; }
    }
    public struct InputBinding
    {
        public string name,path,overridePath,groups,display;
        public bool isComposite,isPartOfComposite;
        public string effectivePath=>overridePath ?? path;
        public bool Matches(InputBinding candidate)
        {
            if(!string.IsNullOrEmpty(groups) && !(candidate.groups ?? "").Split(';').Intersect(groups.Split(';')).Any())return false;
            return string.IsNullOrEmpty(path)||path==candidate.path;
        }
        public static InputBinding MaskByGroup(string group) => new InputBinding{groups=group};
    }
    public class InputAction
    {
        public readonly List<InputBinding> bindings=new List<InputBinding>();
        public InputBinding? bindingMask;
        public InputActionMap actionMap;
        public string name;
        public InputAction(string name="Action") {this.name=name;}
        public string GetBindingDisplayString(int index,out string layout,out string control)
        { layout="";control="";return bindings[index].display ?? InputControlPath.ToHumanReadableString(bindings[index].effectivePath,0); }
        public void Override(int index,string path)
        { var b=bindings[index];b.overridePath=path;bindings[index]=b;InputSystem.BindingChanged(this); }
    }
    public class InputActionMap
    {
        public string name;
        public InputActionAsset asset;
        public InputBinding? bindingMask;
        public ReadOnlyArray<InputDevice>? devices;
        public readonly List<InputAction> actions=new List<InputAction>();
        public InputAction FindAction(string name,bool throwIfNotFound=false)=>actions.FirstOrDefault(a=>a.name==name);
        public InputAction Add(InputAction action) {actions.Add(action);action.actionMap=this;return action;}
    }
    public class InputActionAsset : Object
    {
        public InputBinding? bindingMask;
        public readonly List<InputActionMap> maps=new List<InputActionMap>();
        public InputAction FindAction(string path,bool throwIfNotFound=false)
        {var p=path.Split('/');return p.Length==2?maps.FirstOrDefault(m=>m.name==p[0])?.FindAction(p[1]):maps.SelectMany(m=>m.actions).FirstOrDefault(a=>a.name==path);}
    }
    public static class InputControlPath
    {
        // Narrow declared layouts for these source fixtures only, not Unity's layout engine.
        public static string TryGetControlLayout(string path)
        {
            if(string.IsNullOrEmpty(path)||!path.StartsWith("<")||path.IndexOf(">/")<0)return null;
            int end=path.IndexOf('>');string layout=path.Substring(1,end-1),control=path.Substring(end+2);
            if(layout=="Keyboard")return (control.Length==1&&char.IsLetterOrDigit(control[0]))||new[]{"space","enter","leftShift","upArrow","f13"}.Contains(control)?"Key":null;
            if(layout=="Mouse"||layout=="Pointer")return new[]{"leftButton","rightButton","middleButton","scroll","delta","position"}.Contains(control)?"Button":null;
            if(InputSystem.IsFirstLayoutBasedOnSecond(layout,"Gamepad"))return new[]{"buttonSouth","buttonNorth","buttonEast","buttonWest","leftStick","rightStick","leftShoulder","rightShoulder","leftTrigger","rightTrigger","start","select"}.Contains(control)?"Button":null;
            return null;
        }
        public static InputControl TryFindControl(InputDevice device,string path)
        {
            if(TryGetControlLayout(path)==null)return null;
            int end=path.IndexOf('>');string layout=path.Substring(1,end-1);
            if(layout=="Pointer"&&device is Mouse)return device;
            return InputSystem.IsFirstLayoutBasedOnSecond(device.layout,layout)?device:null;
        }
        public enum HumanReadableStringOptions { OmitDevice=0 }
        public static string ToHumanReadableString(string path,HumanReadableStringOptions options)
        {
            if(string.IsNullOrEmpty(path))return "";
            string s=path.Substring(path.IndexOf('/')+1);
            return s.Length==1?s.ToUpperInvariant():s=="space"?"Space":s=="leftButton"?"Left Button":s;
        }
    }
}
namespace UnityEngine.InputSystem.DualShock
{
    public class DualShockGamepad : UnityEngine.InputSystem.Gamepad {public DualShockGamepad(){layout="DualShockGamepad";}}
}
