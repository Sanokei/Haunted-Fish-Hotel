using System;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // Saved preferences are separate from the settings tab's editable draft.
    public static class HotelSettings
    {
        const string VolumeKey = "HauntedHotel.Settings.MasterVolume";
        const string MusicKey = "HauntedHotel.Settings.MusicVolume";
        const string EffectsKey = "HauntedHotel.Settings.SoundEffectsVolume";
        const string DisplayKey = "HauntedHotel.Settings.DisplayMode";
        public static float MasterVolume { get; private set; } = 1f;
        public static float MusicVolume { get; private set; } = 1f;
        public static float SoundEffectsVolume { get; private set; } = 1f;
        public static FullScreenMode DisplayMode { get; private set; }
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Load()
        {
            MasterVolume = MemberMenuSettingsDraft.NormalizeVolume(PlayerPrefs.GetFloat(VolumeKey, 1f));
            MusicVolume = MemberMenuSettingsDraft.NormalizeVolume(PlayerPrefs.GetFloat(MusicKey, 1f));
            SoundEffectsVolume = MemberMenuSettingsDraft.NormalizeVolume(PlayerPrefs.GetFloat(EffectsKey, 1f));
            DisplayMode = Screen.fullScreenMode;
            AudioListener.volume = MasterVolume;
            if (PlayerPrefs.HasKey(DisplayKey))
            {
                var saved = PlayerPrefs.GetInt(DisplayKey);
                if (saved == (int)FullScreenMode.Windowed || saved == (int)FullScreenMode.FullScreenWindow)
                    DisplayMode = (FullScreenMode)saved;
                ApplyDisplay();
            }
        }

        // Existing callers preserve both category settings.
        public static void Save(float volume, FullScreenMode displayMode) =>
            Save(volume, MusicVolume, SoundEffectsVolume, displayMode);

        public static void Save(float volume, float music, float effects, FullScreenMode displayMode)
        {
            MasterVolume = MemberMenuSettingsDraft.NormalizeVolume(volume);
            MusicVolume = MemberMenuSettingsDraft.NormalizeVolume(music);
            SoundEffectsVolume = MemberMenuSettingsDraft.NormalizeVolume(effects);
            DisplayMode = displayMode == FullScreenMode.Windowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
            // Master attenuation stays here exactly once; mixer Master remains at 0 dB.
            AudioListener.volume = MasterVolume;
            ApplyDisplay();
            PlayerPrefs.SetFloat(VolumeKey, MasterVolume);
            PlayerPrefs.SetFloat(MusicKey, MusicVolume);
            PlayerPrefs.SetFloat(EffectsKey, SoundEffectsVolume);
            PlayerPrefs.SetInt(DisplayKey, (int)DisplayMode);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        static void ApplyDisplay()
        {
            if (!Application.isEditor && Screen.fullScreenMode != DisplayMode)
                Screen.fullScreenMode = DisplayMode;
        }
    }
}
