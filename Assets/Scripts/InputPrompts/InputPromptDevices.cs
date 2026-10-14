using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.LowLevel;

namespace HauntedFish.InputPrompts
{
    // One shared event subscription, regardless of the number of authored prompt slots.
    // No scene searches, GameObjects, PlayerInput ownership changes or per-prompt Update loops.
    public static class InputPromptDevices
    {
        static event Action _Changed;
        static readonly List<InputDevice> _Recent = new List<InputDevice>();
        static bool _Listening, _TrackSession;
        static readonly Dictionary<StickControl, bool> _StickArmed = new Dictionary<StickControl, bool>();
        public static InputDevice ActiveDevice { get; private set; }
        public static InputPromptFamily Family => Classify(ActiveDevice);

        // Keep the last meaningful device current even while every prompt is hidden.
        // Session initialization subscribes only to input events; no runtime objects are created.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void TrackSession()
        {
            _TrackSession = true;
            Listen(null);
        }

        public static void Listen(Action changed)
        {
            if (!_Listening)
            {
                _Listening = true;
                InputSystem.onEvent += OnEvent;
                InputSystem.onDeviceChange += OnDeviceChange;
                InputSystem.onActionChange += OnActionChange;
                if (!Available(ActiveDevice)) ActiveDevice = Fallback();
            }
            _Changed += changed;
        }
        public static void Unlisten(Action changed)
        {
            _Changed -= changed;
            if (_Changed != null || !_Listening || _TrackSession) return;
            InputSystem.onEvent -= OnEvent;
            InputSystem.onDeviceChange -= OnDeviceChange;
            InputSystem.onActionChange -= OnActionChange;
            _Listening = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            InputSystem.onEvent -= OnEvent;
            InputSystem.onDeviceChange -= OnDeviceChange;
            InputSystem.onActionChange -= OnActionChange;
            _Changed = null;
            _Listening = _TrackSession = false;
            ActiveDevice = null;
            _Recent.Clear();
            _StickArmed.Clear();
        }

        public static InputPromptFamily Classify(InputDevice device)
        {
            if (!(device is Gamepad)) return InputPromptFamily.KeyboardMouse;
            if (device is DualShockGamepad || BasedOn(device.layout, "DualShockGamepad") ||
                device.layout.IndexOf("DualSense", StringComparison.OrdinalIgnoreCase) >= 0)
                return InputPromptFamily.PlayStation;
            if (BasedOn(device.layout, "XInputController") || device.layout.IndexOf("Xbox", StringComparison.OrdinalIgnoreCase) >= 0)
                return InputPromptFamily.Xbox;
            // Generic Gamepad is intentionally generic. Do not guess from OS or attachment.
            return InputPromptFamily.GenericGamepad;
        }
        static bool BasedOn(string layout, string parent) =>
            string.Equals(layout, parent, StringComparison.OrdinalIgnoreCase) || InputSystem.IsFirstLayoutBasedOnSecond(layout, parent);
        static bool Available(InputDevice device) => device != null && device.added && device.enabled;
        static bool Supported(InputDevice device) => device is Keyboard || device is Mouse || device is Gamepad;
        static InputDevice Fallback()
        {
            for (int i = _Recent.Count - 1; i >= 0; --i)
                if (Available(_Recent[i])) return _Recent[i];
            if (Available(Keyboard.current)) return Keyboard.current;
            if (Available(Mouse.current)) return Mouse.current;
            foreach (var device in InputSystem.devices) if (Supported(device) && Available(device)) return device;
            return null;
        }
        static void Use(InputDevice device)
        {
            _Recent.Remove(device); _Recent.Add(device);
            if (ActiveDevice == device) return;
            ActiveDevice = device;
            _Changed?.Invoke();
        }
        static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected || change == InputDeviceChange.Disabled)
            {
                _Recent.Remove(device);
                if (device is Gamepad pad) { _StickArmed.Remove(pad.leftStick); _StickArmed.Remove(pad.rightStick); }
                if (ActiveDevice == device) { ActiveDevice = Fallback(); _Changed?.Invoke(); }
            }
            else if (change == InputDeviceChange.ConfigurationChanged)
                _Changed?.Invoke(); // Includes keyboard layout changes.
            else if (ActiveDevice == null && Supported(device) && Available(device))
            { ActiveDevice = Fallback(); _Changed?.Invoke(); }
        }
        public static void RefreshBindings() => _Changed?.Invoke();
        static void OnActionChange(object changed, InputActionChange change)
        {
            if (change == InputActionChange.BoundControlsChanged) _Changed?.Invoke();
        }
        static bool Pressed(ButtonControl control, InputEventPtr inputEvent) => control != null &&
            control.ReadValueFromEvent(inputEvent, out float value) && value >= .5f && control.ReadValue() < .5f;
        static bool StickMoved(StickControl stick, InputEventPtr inputEvent)
        {
            if (!stick.ReadValueFromEvent(inputEvent, out Vector2 value)) return false;
            // Hysteresis accepts even a slow deliberate tilt, but not a held stick's tiny noise.
            bool armed = !_StickArmed.TryGetValue(stick, out bool wasArmed) || wasArmed;
            if (value.sqrMagnitude <= .0625f) armed = true;
            bool meaningful = value.sqrMagnitude >= .3025f && (armed || (value - stick.ReadValue()).sqrMagnitude >= .1225f);
            _StickArmed[stick] = meaningful ? false : armed;
            return meaningful;
        }
        static void OnEvent(InputEventPtr inputEvent, InputDevice device)
        {
            if (!Available(device) || !Supported(device) || (!inputEvent.IsA<StateEvent>() && !inputEvent.IsA<DeltaStateEvent>())) return;
            if (device is Gamepad pad)
            {
                // Evaluate both to keep their hysteresis state current even when one already moved.
                bool leftMoved = StickMoved(pad.leftStick, inputEvent);
                bool rightMoved = StickMoved(pad.rightStick, inputEvent);
                if (leftMoved || rightMoved ||
                    Pressed(pad.buttonSouth, inputEvent) || Pressed(pad.buttonEast, inputEvent) ||
                    Pressed(pad.buttonWest, inputEvent) || Pressed(pad.buttonNorth, inputEvent) ||
                    Pressed(pad.leftShoulder, inputEvent) || Pressed(pad.rightShoulder, inputEvent) ||
                    Pressed(pad.leftTrigger, inputEvent) || Pressed(pad.rightTrigger, inputEvent) ||
                    Pressed(pad.leftStickButton, inputEvent) || Pressed(pad.rightStickButton, inputEvent) ||
                    Pressed(pad.startButton, inputEvent) || Pressed(pad.selectButton, inputEvent) ||
                    Pressed(pad.dpad.up, inputEvent) || Pressed(pad.dpad.down, inputEvent) ||
                    Pressed(pad.dpad.left, inputEvent) || Pressed(pad.dpad.right, inputEvent)) Use(device);
                return;
            }
            if (device is Mouse mouse)
            {
                bool moved = mouse.delta.ReadValueFromEvent(inputEvent, out Vector2 delta) && delta.sqrMagnitude >= 16;
                bool scrolled = mouse.scroll.ReadValueFromEvent(inputEvent, out Vector2 scroll) && scroll.sqrMagnitude >= 1;
                if (moved || scrolled || Pressed(mouse.leftButton, inputEvent) || Pressed(mouse.rightButton, inputEvent) ||
                    Pressed(mouse.middleButton, inputEvent) || Pressed(mouse.forwardButton, inputEvent) || Pressed(mouse.backButton, inputEvent)) Use(device);
                return;
            }
            foreach (var control in inputEvent.EnumerateChangedControls(device))
                if (!control.noisy && !control.synthetic && control is ButtonControl button && Pressed(button, inputEvent))
                { Use(device); return; }
        }
    }
}
