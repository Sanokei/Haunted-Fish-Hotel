using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace HauntedFish.Multiplayer
{
    // Only on the newly authored Game scaffold. Never changes Lobby or user gameplay cameras.
    // This adapter is scoped to the new Game scene. It assigns the local player to the authored Cinemachine camera without touching Lobby cameras.
    public sealed class GameCameraOwner : MonoBehaviour
    {
        // This adapter is scoped to the new Game scene. It assigns the local player to the authored Cinemachine camera without touching Lobby cameras.
        public CinemachineCamera Camera;
        void OnEnable()
        {
            HotelPlayer.LocalPlayerChanged += BindPlayer;
            SceneManager.sceneLoaded += OnSceneLoaded;
            BindPlayer(HotelPlayer.LocalPlayer);
        }
        void OnDisable()
        {
            HotelPlayer.LocalPlayerChanged -= BindPlayer;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Camera) Camera.Follow = null;
        }
        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => BindPlayer(HotelPlayer.LocalPlayer);
        void BindPlayer(HotelPlayer player)
        {
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            if (!Camera || !GameSceneDefinition.Current || gameObject.scene!=GameSceneDefinition.Current.gameObject.scene) return;
            Camera.Follow = player ? player.transform : null;
        }
    }
}
