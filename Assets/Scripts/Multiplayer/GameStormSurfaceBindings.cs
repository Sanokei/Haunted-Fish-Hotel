using UnityEngine;
namespace HauntedFish.Multiplayer
{
    // Authored Game-only geometry data for the shared storm owner; no independent strike clock.
    public sealed class GameStormSurfaceBindings : MonoBehaviour
    {
        public SpriteRenderer[] Windows;
        public MeshRenderer[] OppositeWallProjections;
        public Transform ReceiverRoot;
        public Vector3 LightDirection = new Vector3(0, -.45f, 1);
    }
}
