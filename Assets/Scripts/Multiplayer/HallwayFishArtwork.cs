using UnityEngine;
using UnityEngine.UI;
namespace HauntedFish.Multiplayer
{
    // All render nodes live in the authored visual-only prefab; never clone a network actor.
    public sealed class HallwayFishArtwork : MonoBehaviour
    {
        public RectTransform Root;
        public Image Shadow;
        public Image[] Pieces;
    }
}
