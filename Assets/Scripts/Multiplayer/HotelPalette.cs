using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HauntedFish.Multiplayer
{
    // Hidden Library palette by Pixzel Hoo, supplied as the visual reference.
    public static class HotelPalette
    {
        static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }
        public static readonly Color Night = Hex("100B2A"), Plum = Hex("2A213A"), Pine = Hex("314743"),
            Moss = Hex("65856D"), Sage = Hex("ADCA9A"), Light = Hex("ECFECA"), Wood = Hex("582D27"),
            Rust = Hex("974133"), Clay = Hex("D67654"), Mulberry = Hex("5D3441"), Rose = Hex("A3685B"),
            Peach = Hex("FFB379"), Paper = Hex("FFE6A9");

    }
}
