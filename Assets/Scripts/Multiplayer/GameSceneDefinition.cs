using UnityEngine;
namespace HauntedFish.Multiplayer
{
    // An authored marker scopes the extra side-scroll behavior to Game, leaving Lobby cameras and controls intact.
    public sealed class GameSceneDefinition : MonoBehaviour
    {
        // Persistent players consult the current scene marker instead of carrying obsolete scene references.
        public static GameSceneDefinition Current { get; private set; }
        // Server scene travel places admitted players at this authored origin after Game loads.
        public Vector3 SpawnOrigin=new Vector3(-10,1.2f,0);
        // Separate spawn points avoid overlapping the players' CharacterControllers.
        public float SpawnSpacing=2;
        // The server bounds mouse requests to this authored playable region; client coordinates are not trusted.
        public Rect MouseBounds=new Rect(-15,-2,30,18);
        // Put the point light in front of the platform faces so its radial illumination is visible to the side camera.
        public float MouseLightDepth=-3;
        // Optional explicit visual overrides leave the original authored rig/marker materials unchanged when empty.
        public Material PlayerMaterial, LampMaterial;
        // Unity activates the marker when the serialized Game scene loads.
        void Awake() { Current=this; }
        // Removing Game releases the marker so persistent characters switch back to Lobby input and hide their lights.
        void OnDestroy() { if (Current==this) Current=null; }
        // Only the server selects the slot index and teleports the owned network character.
        public Vector3 Spawn(int index)=>SpawnOrigin+Vector3.right*(index*SpawnSpacing);
        // Clamp XY independently of the caller's Z; peers cannot move the light behind geometry or outside the scene bounds.
        public Vector3 ClampMouse(Vector3 point)=>new Vector3(Mathf.Clamp(point.x,MouseBounds.xMin,MouseBounds.xMax),Mathf.Clamp(point.y,MouseBounds.yMin,MouseBounds.yMax),MouseLightDepth);
    }
}
