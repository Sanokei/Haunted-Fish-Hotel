using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // Optional authored binding for scene/prefab owners; keep one radio voice on Music.
    public sealed class HotelAudioSourceRouting : MonoBehaviour
    {
        [SerializeField] AudioSource[] _Music = new AudioSource[0];
        [SerializeField] AudioSource[] _SoundEffects = new AudioSource[0];
        void Awake()
        {
            foreach (var source in _Music) HotelAudioRouting.RouteMusic(source);
            foreach (var source in _SoundEffects) HotelAudioRouting.RouteEffects(source);
        }
    }
}
