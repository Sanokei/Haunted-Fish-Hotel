using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HauntedFish.InputPrompts
{
    // Inline variant for already-authored TMP instruction blocks and contextual HUDs.
    // Templates contain {input:Game/Jump}, or {input:<Mouse>/leftButton} for fixed controls.
    public sealed class InputPromptSpriteText : MonoBehaviour
    {
        [SerializeField] InputPromptCatalog _Catalog;
        [SerializeField] TMP_SpriteAsset _SpriteAsset;
        [SerializeField] InputActionAsset _ActionAsset;
        [SerializeField] TMP_Text _Text;
        [SerializeField, TextArea] string _Template;
        InputActionMap _RuntimeMap;
        bool _Refreshing;
        static readonly Regex Token = new Regex(@"\{input:([^}]+)\}");
        public bool HasBindings => _Catalog && _SpriteAsset && _Text;
        void OnEnable() { InputPromptDevices.Listen(Refresh); Refresh(); }
        void OnDisable() => InputPromptDevices.Unlisten(Refresh);
        public void SetTemplate(string template)
        {
            if (_Template == template) return;
            _Template = template; Refresh();
        }
        public void SetActionMap(InputActionMap map) { _RuntimeMap = map; Refresh(); }
        public void Refresh()
        {
            if (_Refreshing || !_Text) return;
            _Refreshing = true;
            try
            {
                _Text.spriteAsset = _SpriteAsset;
                _Text.richText = true;
                _Text.text = Token.Replace(_Template ?? "", ResolveToken);
            }
            finally { _Refreshing = false; }
        }
        string ResolveToken(Match token)
        {
            var path = token.Groups[1].Value;
            var family = InputPromptDevices.Family;
            ResolvedInputPrompt prompt;
            if (path.StartsWith("<"))
            {
                var paths = new List<string>(); var labels = new List<string>();
                prompt = InputPromptResolver.ResolvePaths(path.Split(';')[0], "", family);
                if (prompt.Available)
                {
                    foreach (var fixedPath in path.Split(';'))
                    {
                        var part = InputPromptResolver.ResolvePaths(fixedPath, "", family);
                        paths.AddRange(part.GlyphPaths); labels.Add(part.DisplayText);
                    }
                    prompt = new ResolvedInputPrompt(paths.ToArray(), string.Join(" / ", labels));
                }
            }
            else
            {
                InputAction action = null;
                int slash = path.IndexOf('/');
                if (_RuntimeMap != null && slash > 0 && _RuntimeMap.name == path.Substring(0, slash))
                    action = _RuntimeMap.FindAction(path.Substring(slash + 1), false);
                if (action == null) action = InputPromptActions.Find(path, _ActionAsset);
                prompt = InputPromptResolver.Resolve(action, family);
            }
            var text = new StringBuilder();
            foreach (var glyph in prompt.GlyphPaths)
            {
                string name = _Catalog ? _Catalog.FindSpriteName(family, glyph) : null;
                if (!_SpriteAsset || string.IsNullOrEmpty(name) || _SpriteAsset.GetSpriteIndexFromName(name) < 0)
                    return Escape(prompt.DisplayText);
                text.Append("<sprite name=\"").Append(name).Append("\" tint=0>");
            }
            return text.Length > 0 ? text.ToString() : Escape(prompt.DisplayText);
        }
        static string Escape(string value) => (value ?? "").Replace("<", "\u2039").Replace(">", "\u203a");
    }
}
