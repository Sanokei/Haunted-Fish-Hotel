using System;
using UnityEngine;

namespace HauntedFish.InputPrompts
{
    public enum InputPromptFamily { KeyboardMouse, Xbox, PlayStation, GenericGamepad }

    [CreateAssetMenu(menuName = "Haunted Fish/Input Prompt Catalog")]
    public sealed class InputPromptCatalog : ScriptableObject
    {
        [Serializable]
        public struct Glyph
        {
            public InputPromptFamily Family;
            public string ControlPath;
            public Sprite Sprite;
            public string SpriteName;
        }
        [SerializeField] Glyph[] _Glyphs = Array.Empty<Glyph>();

        public string FindSpriteName(InputPromptFamily family, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            foreach (var glyph in _Glyphs)
                if (glyph.Family == family && string.Equals(glyph.ControlPath, path, StringComparison.OrdinalIgnoreCase))
                    return glyph.SpriteName;
            if (family != InputPromptFamily.KeyboardMouse && family != InputPromptFamily.GenericGamepad)
                return FindSpriteName(InputPromptFamily.GenericGamepad, path);
            return null;
        }

        public Sprite Find(InputPromptFamily family, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            foreach (var glyph in _Glyphs)
                if (glyph.Family == family && string.Equals(glyph.ControlPath, path, StringComparison.OrdinalIgnoreCase))
                    return glyph.Sprite;
            // Never mislabel an unknown controller with Xbox face letters.
            if (family != InputPromptFamily.KeyboardMouse && family != InputPromptFamily.GenericGamepad)
                return Find(InputPromptFamily.GenericGamepad, path);
            return null;
        }
    }
}
