using UnityEngine;
using UnityEngine.Audio;

namespace HauntedFish.Multiplayer
{
    // Authored on the persistent menu root, outside the modal that opens/closes.
    public sealed class HotelAudioSettings : MonoBehaviour
    {
        [SerializeField] AudioMixer _Mixer;
        bool _Started, _Dirty = true;
        public const string MusicParameter = "MusicVolumeDb";
        public const string EffectsParameter = "SoundEffectsVolumeDb";
        void OnEnable() { HotelSettings.Changed += SettingsChanged; _Dirty = true; }
        void Start() { _Started = true; Apply(); }
        void SettingsChanged() => _Dirty = true;
        void LateUpdate() { if (_Started && _Dirty) Apply(); }
        void Apply()
        {
            if (!_Mixer) _Mixer = Resources.Load<AudioMixer>(HotelAudioRouting.ResourcePath);
            // Unity requires SetFloat in Start or later; never in Awake/OnEnable/load hooks.
            bool music = _Mixer && _Mixer.SetFloat(MusicParameter, MemberMenuSettingsDraft.ToDecibels(HotelSettings.MusicVolume));
            bool effects = _Mixer && _Mixer.SetFloat(EffectsParameter, MemberMenuSettingsDraft.ToDecibels(HotelSettings.SoundEffectsVolume));
            if (!music || !effects) Debug.LogError("Assign the authored HotelAudio mixer with both exposed category volumes.", this);
            _Dirty = false;
        }
        void OnDisable() => HotelSettings.Changed -= SettingsChanged;
    }
}

