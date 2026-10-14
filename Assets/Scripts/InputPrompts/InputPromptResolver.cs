using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace HauntedFish.InputPrompts
{
    public readonly struct ResolvedInputPrompt
    {
        public readonly string[] GlyphPaths;
        public readonly string DisplayText;
        public readonly bool Available;
        public ResolvedInputPrompt(string[] paths, string display, bool available = true)
        { GlyphPaths = paths; DisplayText = display; Available = available; }
    }

    // Binding resolution is separate from device detection and presentation. Uses effectivePath,
    // so overrides, disabled bindings and keyboard layout names remain truthful after rebinding.
    public static class InputPromptResolver
    {
        public static ResolvedInputPrompt Resolve(InputAction action, InputPromptFamily family)
        {
            if (action == null || !PermittedDevices(action, family)) return Missing("Unbound");
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; ++i)
            {
                var binding = bindings[i];
                if (binding.isPartOfComposite) continue;
                if (binding.isComposite)
                {
                    if (string.IsNullOrEmpty(binding.effectivePath)) continue;
                    var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var parts = new Dictionary<string, ResolvedInputPrompt>(StringComparer.OrdinalIgnoreCase);
                    for (int p = i + 1; p < bindings.Count && bindings[p].isPartOfComposite; ++p)
                    {
                        required.Add(bindings[p].name);
                        if (!PermittedBinding(action, bindings[p]) || !Matches(bindings[p].effectivePath, family) || parts.ContainsKey(bindings[p].name)) continue;
                        parts[bindings[p].name] = ResolveBinding(action, p, family);
                    }
                    if (parts.Count == 0 || parts.Count != required.Count) continue;
                    var keys = new List<string>(); var labels = new List<string>();
                    foreach (string name in new[] { "up", "left", "down", "right", "negative", "positive", "modifier", "button" })
                        if (parts.TryGetValue(name, out var part)) { keys.AddRange(part.GlyphPaths); labels.Add(part.DisplayText); parts.Remove(name); }
                    foreach (var part in parts.Values) { keys.AddRange(part.GlyphPaths); labels.Add(part.DisplayText); }
                    return new ResolvedInputPrompt(keys.ToArray(), string.Join(" / ", labels));
                }
                if (PermittedBinding(action, binding) && Matches(binding.effectivePath, family)) return ResolveBinding(action, i, family);
            }
            bool mouse = false, keyboard = false;
            foreach (var binding in bindings)
            {
                if (!PermittedBinding(action, binding)) continue;
                mouse |= IsMouse(binding.effectivePath);
                keyboard |= Layout(binding.effectivePath).Equals("Keyboard", StringComparison.OrdinalIgnoreCase);
            }
            if (family != InputPromptFamily.KeyboardMouse)
                return Missing(mouse ? "Mouse required" : keyboard ? "Keyboard required" : "Unbound");
            return Missing("Unbound");
        }

        static bool PermittedBinding(InputAction action, InputBinding binding)
        {
            if (string.IsNullOrEmpty(binding.effectivePath) || string.IsNullOrEmpty(InputControlPath.TryGetControlLayout(binding.effectivePath))) return false;
            var map = action.actionMap;
            var devices = map?.devices;
            if (devices.HasValue)
            {
                bool supported = false;
                foreach (var device in devices.Value)
                    if (device != null && device.added && device.enabled && InputControlPath.TryFindControl(device, binding.effectivePath) != null)
                    { supported = true; break; }
                if (!supported) return false;
            }
            return (!action.bindingMask.HasValue || action.bindingMask.Value.Matches(binding)) &&
                (map == null || !map.bindingMask.HasValue || map.bindingMask.Value.Matches(binding)) &&
                (map == null || map.asset == null || !map.asset.bindingMask.HasValue || map.asset.bindingMask.Value.Matches(binding));
        }
        static bool PermittedDevices(InputAction action, InputPromptFamily family)
        {
            var devices = action.actionMap?.devices;
            if (!devices.HasValue) return true;
            foreach (var device in devices.Value)
                if (device != null && InputPromptDevices.Classify(device) == family) return true;
            return false;
        }

        public static ResolvedInputPrompt ResolvePaths(string keyboardPath, string gamepadPath, InputPromptFamily family)
        {
            var path = family == InputPromptFamily.KeyboardMouse ? keyboardPath : gamepadPath;
            if (string.IsNullOrEmpty(path)) return Missing(family != InputPromptFamily.KeyboardMouse && !string.IsNullOrEmpty(keyboardPath)
                ? IsMouse(keyboardPath) ? "Mouse required" : "Keyboard required" : "Unbound");
            if (string.IsNullOrEmpty(InputControlPath.TryGetControlLayout(path))) return Missing("Unbound");
            string label = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            return FromPath(path, label, family);
        }

        static ResolvedInputPrompt ResolveBinding(InputAction action, int index, InputPromptFamily family)
        {
            // Do not use display text as the path for gamepads: Xbox X and PlayStation cross
            // share a letter but refer to different physical controls.
            string display = action.GetBindingDisplayString(index, out _, out _);
            return FromPath(action.bindings[index].effectivePath, display, family);
        }
        static ResolvedInputPrompt FromPath(string path, string display, InputPromptFamily family)
        {
            var layout = Layout(path); var control = Control(path);
            string key = family == InputPromptFamily.KeyboardMouse ? "<" + layout + ">/" + control : control;
            if (layout.Equals("Keyboard", StringComparison.OrdinalIgnoreCase) && display.Length == 1 && char.IsLetterOrDigit(display[0]))
                key = "<Keyboard>/" + display.ToLowerInvariant();
            if (family != InputPromptFamily.KeyboardMouse) display = GamepadLabel(control, family, display);
            return new ResolvedInputPrompt(new[] { key }, display);
        }
        public static string GamepadLabel(string path, InputPromptFamily family, string fallback)
        {
            bool ps = family == InputPromptFamily.PlayStation, xbox = family == InputPromptFamily.Xbox;
            switch (path.ToLowerInvariant())
            {
                case "buttonsouth": return ps ? "Cross" : xbox ? "A" : "South button";
                case "buttoneast": return ps ? "Circle" : xbox ? "B" : "East button";
                case "buttonwest": return ps ? "Square" : xbox ? "X" : "West button";
                case "buttonnorth": return ps ? "Triangle" : xbox ? "Y" : "North button";
                case "leftstick": return "Left stick";
                case "rightstick": return "Right stick";
                case "leftstickpress": return ps ? "L3" : "Left stick press";
                case "rightstickpress": return ps ? "R3" : "Right stick press";
                case "leftshoulder": return ps ? "L1" : xbox ? "LB" : "Left shoulder";
                case "rightshoulder": return ps ? "R1" : xbox ? "RB" : "Right shoulder";
                case "lefttrigger": return ps ? "L2" : xbox ? "LT" : "Left trigger";
                case "righttrigger": return ps ? "R2" : xbox ? "RT" : "Right trigger";
                case "start": return ps ? "Options" : "Menu";
                case "select": return ps ? "Share" : "View";
                default: return fallback;
            }
        }
        static ResolvedInputPrompt Missing(string reason) => new ResolvedInputPrompt(Array.Empty<string>(), reason, false);
        static bool IsMouse(string path) => Layout(path).Equals("Mouse", StringComparison.OrdinalIgnoreCase) || Layout(path).Equals("Pointer", StringComparison.OrdinalIgnoreCase);
        static string Layout(string path)
        {
            if (string.IsNullOrEmpty(path) || path[0] != '<') return "";
            int end = path.IndexOf('>'); return end > 1 ? path.Substring(1, end - 1) : "";
        }
        static string Control(string path)
        {
            int slash = path.IndexOf('/'); return slash >= 0 ? path.Substring(slash + 1) : path;
        }
        static bool Matches(string path, InputPromptFamily family)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string layout = Layout(path);
            if (family == InputPromptFamily.KeyboardMouse)
                return layout.Equals("Keyboard", StringComparison.OrdinalIgnoreCase) || IsMouse(path);
            if (layout.Equals("Gamepad", StringComparison.OrdinalIgnoreCase)) return true;
            if (!InputSystem.IsFirstLayoutBasedOnSecond(layout, "Gamepad")) return false;
            bool ps = layout.IndexOf("Dual", StringComparison.OrdinalIgnoreCase) >= 0 || InputSystem.IsFirstLayoutBasedOnSecond(layout, "DualShockGamepad");
            bool xbox = layout.IndexOf("Xbox", StringComparison.OrdinalIgnoreCase) >= 0 || InputSystem.IsFirstLayoutBasedOnSecond(layout, "XInputController");
            return family == InputPromptFamily.PlayStation ? ps : family == InputPromptFamily.Xbox ? xbox : !ps && !xbox;
        }
    }
}
