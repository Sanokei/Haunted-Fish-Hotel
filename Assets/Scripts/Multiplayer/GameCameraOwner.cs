using Unity.Cinemachine;
using UnityEngine;
namespace HauntedFish.Multiplayer
{
    // Only on the newly authored Game scaffold. Never changes Lobby or user gameplay cameras.
    // This adapter is scoped to the new Game scene. It assigns the local player to the authored Cinemachine camera without touching Lobby cameras.
    public sealed class GameCameraOwner : MonoBehaviour
    {
        // This adapter is scoped to the new Game scene. It assigns the local player to the authored Cinemachine camera without touching Lobby cameras.
        public CinemachineCamera Camera;
        // Service local presentation and authoritative simulation each frame, with ownership/readiness checks inside the path.
        void Update()
        {
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            if (!Camera || !GameSceneDefinition.Current || gameObject.scene!=GameSceneDefinition.Current.gameObject.scene) return;
            // Look up objects already present in the loaded scenes; ownership and scene checks select the appropriate one.
            foreach (var player in FindObjectsByType<HotelPlayer>(FindObjectsSortMode.None))
                // Ownership restricts this path to the local player, so a remote avatar cannot take local input or camera focus.
                if (player.IsRelevantPlayer && (!player.Networked || player.Identity.IsSpawned)) {Camera.Follow=player.transform;return;}
            // Assign the local owned character as the follow target of this authored virtual camera.
            Camera.Follow=null;
        }
    }
}
