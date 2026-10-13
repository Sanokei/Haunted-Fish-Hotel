using UnityEngine;
using UnityEngine.UI;
namespace HauntedFish.Multiplayer
{
    public sealed class AtticTransitionAssets : MonoBehaviour
    {
        public RectTransform FishRoot;
        public HallwayFishArtwork FishPrefab;
        public HallwayFishArtwork[] IntroFish;
        // Four authored intro figures; these slots never become network participants.
        public float[] IntroFishWorldX = new[] { -10.5f, -8.5f, -6.5f, -4.5f };
        [Range(.1f, .8f)] public float IntroFishHeightFraction = .62f;
        public RectTransform StreakRoot;
        public Image[] Streaks;
        public RoundHowToPage FishHowToPrefab, GhostHowToPrefab;
    }
}
