using System;
using System.Collections.Generic;
using UnityEngine;
namespace HauntedFish.Multiplayer
{
    // Project/network boundaries only. Engine transforms, UI and material APIs are real assemblies.
    public sealed class HotelPlayer : MonoBehaviour
    {
        public uint NetId;
        public bool BossWatching;
        public static HotelPlayer LocalPlayer;
        public static readonly List<HotelPlayer> ActivePlayers = new List<HotelPlayer>();
    }
    public sealed class HotelFishSprite : MonoBehaviour { }
    public sealed class GameSceneController : MonoBehaviour
    {
        public static GameSceneController Current;
        public float HallwayCeilingY;
        public Bounds HallwayBounds;
    }
    public sealed class RoundHowToPage : MonoBehaviour
    {
        public void Begin(uint local, uint ghost, Action ready) { }
        public void Cancel() { }
        public void SetVisible(bool visible) { }
    }
}
