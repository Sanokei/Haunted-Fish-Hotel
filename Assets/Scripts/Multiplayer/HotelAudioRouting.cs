using UnityEngine;
using UnityEngine.Audio;

namespace HauntedFish.Multiplayer
{
    // Routing never changes a source's calibrated volume, pitch, timing or spatial mix.
    public static class HotelAudioRouting
    {
        static AudioMixer _Mixer;
        static AudioMixerGroup _Music, _Effects;
        public const string ResourcePath = "HotelAudio";
        static AudioMixerGroup Group(bool music)
        {
            if (!_Mixer) _Mixer = Resources.Load<AudioMixer>(ResourcePath);
            if (!_Mixer) return null;
            var cached = music ? _Music : _Effects;
            if (cached) return cached;
            foreach (var group in _Mixer.FindMatchingGroups(music ? "Music" : "Sound Effects"))
                if (group.name == (music ? "Music" : "Sound Effects"))
                {
                    if (music) _Music = group; else _Effects = group;
                    return group;
                }
            return null;
        }
        public static bool RouteMusic(AudioSource source) => Route(source, true);
        public static bool RouteEffects(AudioSource source) => Route(source, false);
        static bool Route(AudioSource source, bool music)
        {
            var group = Group(music);
            if (!source || !group) return false;
            source.outputAudioMixerGroup = group;
            return true;
        }
    }
}
