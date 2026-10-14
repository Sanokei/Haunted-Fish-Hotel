using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HauntedFish.InputPrompts
{
    // All image/text slots are authored. This presenter never builds or discovers UI at runtime.
    public sealed class InputGlyphPrompt : MonoBehaviour
    {
        [SerializeField] InputPromptCatalog _Catalog;
        [SerializeField] InputActionAsset _ActionAsset;
        [SerializeField] string _ActionPath;
        [Tooltip("For controls outside an action asset. Leave empty when an action is assigned.")]
        [SerializeField] string _KeyboardPath, _GamepadPath;
        [SerializeField] Image _Glyph;
        [SerializeField] Image[] _AdditionalGlyphs = Array.Empty<Image>();
        [SerializeField] Text _Fallback;
        InputAction _RuntimeAction;
        bool _Refreshing;

        public bool HasBindings => _Catalog && _Glyph && _Fallback &&
            ((_ActionAsset && !string.IsNullOrEmpty(_ActionPath)) || !string.IsNullOrEmpty(_KeyboardPath) || !string.IsNullOrEmpty(_GamepadPath));
        void OnEnable() { InputPromptDevices.Listen(Refresh); Refresh(); }
        void OnDisable() => InputPromptDevices.Unlisten(Refresh);

        // A gameplay owner can inject its actual cloned/rebound action without coupling this
        // reusable view to the player, scene, networking layer or action-map enable state.
        public void SetAction(InputAction action)
        {
            if (_RuntimeAction == action) return;
            _RuntimeAction = action; Refresh();
        }
        public void Refresh()
        {
            if (_Refreshing) return;
            _Refreshing = true;
            try
            {
                var action = _RuntimeAction ?? InputPromptActions.Find(_ActionPath, _ActionAsset);
                var prompt = action != null || _ActionAsset
                    ? InputPromptResolver.Resolve(action, InputPromptDevices.Family)
                    : InputPromptResolver.ResolvePaths(_KeyboardPath, _GamepadPath, InputPromptDevices.Family);
                int count = prompt.GlyphPaths.Length;
                bool drawable = prompt.Available && _Catalog && count > 0 && count <= 1 + _AdditionalGlyphs.Length;
                for (int i = 0; drawable && i < count; ++i)
                    drawable = Slot(i) && _Catalog.Find(InputPromptDevices.Family, prompt.GlyphPaths[i]);
                for (int i = 0; i <= _AdditionalGlyphs.Length; ++i)
                {
                    var slot = Slot(i);
                    if (!slot) continue;
                    slot.sprite = drawable && i < count ? _Catalog.Find(InputPromptDevices.Family, prompt.GlyphPaths[i]) : null;
                    slot.enabled = drawable && i < count;
                    slot.preserveAspect = true;
                }
                if (_Fallback) { _Fallback.text = drawable ? "" : prompt.DisplayText; _Fallback.enabled = !drawable; }
            }
            finally { _Refreshing = false; }
        }
        Image Slot(int index) => index == 0 ? _Glyph : _AdditionalGlyphs[index - 1];
    }
}
