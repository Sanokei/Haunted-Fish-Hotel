using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HauntedFish.InputPrompts
{
    // Explicit registration of local runtime maps keeps authored views aligned with cloned
    // actions/rebinding, without searching for players or enabling their input maps.
    public static class InputPromptActions
    {
        static readonly Dictionary<string, InputActionMap> _Maps = new Dictionary<string, InputActionMap>(StringComparer.Ordinal);
        public static void Register(InputActionMap map)
        {
            if (map == null || _Maps.TryGetValue(map.name, out var current) && current == map) return;
            _Maps[map.name] = map;
            InputPromptDevices.RefreshBindings();
        }
        public static void Unregister(InputActionMap map)
        {
            if (map == null || !_Maps.TryGetValue(map.name, out var current) || current != map) return;
            _Maps.Remove(map.name);
            InputPromptDevices.RefreshBindings();
        }
        public static InputAction Find(string path, InputActionAsset authored)
        {
            if (string.IsNullOrEmpty(path)) return null;
            int slash = path.IndexOf('/');
            if (slash > 0 && _Maps.TryGetValue(path.Substring(0, slash), out var map))
                return map.FindAction(path.Substring(slash + 1), false);
            return authored ? authored.FindAction(path, false) : null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => _Maps.Clear();
    }
}
