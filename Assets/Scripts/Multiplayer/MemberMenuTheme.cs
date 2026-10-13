using TMPro;
using UnityEngine;
namespace HauntedFish.Multiplayer
{
    // Optional licensed art hook, configured after the source/license handoff is verified.
    [CreateAssetMenu(menuName = "Haunted Hotel/Menu theme")]
    public sealed class MemberMenuTheme : ScriptableObject
    {
        public TMP_FontAsset HeadingFont;
        public Sprite Panel, Control, LeftDecoration, RightDecoration;
    }
}
