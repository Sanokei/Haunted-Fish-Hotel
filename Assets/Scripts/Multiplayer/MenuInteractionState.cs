using System;
namespace HauntedFish.Multiplayer
{
    // Ordinary state objects: Unity supplies frame/device state and preference effects.
    public sealed class MemberMenuInputCapture
    {
        int _CloseFrame = -1;
        public bool IsOpen { get; private set; }
        public bool Captured { get; private set; }
        public static bool CanOpen(bool ready, bool transitioning, bool introduction) => ready && !transitioning;
        public void SetOpen(bool open, int frame)
        {
            if (IsOpen == open) return;
            IsOpen = open;
            if (open) { Captured = true; _CloseFrame = -1; }
            else _CloseFrame = frame;
        }
        public bool TryRelease(int frame, bool? primaryPointerPressed)
        {
            if (IsOpen || !Captured || _CloseFrame < 0 || frame <= _CloseFrame || primaryPointerPressed == true) return false;
            Captured = false; _CloseFrame = -1; return true;
        }
        public void Reset() { IsOpen = Captured = false; _CloseFrame = -1; }
    }
    public sealed class MemberMenuSettingsDraft
    {
        float _Volume, _SavedVolume, _MusicVolume, _SavedMusicVolume = 1, _SoundEffectsVolume, _SavedEffectsVolume = 1;
        bool _SavedWindowed;
        public float Volume { get => _Volume; set => _Volume = NormalizeVolume(value); }
        public float MusicVolume { get => _MusicVolume; set => _MusicVolume = NormalizeVolume(value); }
        public float SoundEffectsVolume { get => _SoundEffectsVolume; set => _SoundEffectsVolume = NormalizeVolume(value); }
        public bool Windowed { get; set; }
        public static float ToDecibels(float value) { value = NormalizeVolume(value); return value <= .0001f ? -80f : (float)(20 * Math.Log10(value)); }
        public static float NormalizeVolume(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Math.Max(0f, Math.Min(1f, value));
        public void Reopen(float savedVolume, bool savedWindowed)
        {
            Reopen(savedVolume, 1, 1, savedWindowed);
        }
        public void Reopen(float master, float music, float effects, bool windowed)
        {
            _SavedVolume = NormalizeVolume(master); _SavedMusicVolume = NormalizeVolume(music);
            _SavedEffectsVolume = NormalizeVolume(effects); _SavedWindowed = windowed; Cancel();
        }
        public void Cancel() { _Volume = _SavedVolume; _MusicVolume = _SavedMusicVolume; _SoundEffectsVolume = _SavedEffectsVolume; Windowed = _SavedWindowed; }
        public void Apply(Action<float, float, float, bool> save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            save(Volume, MusicVolume, SoundEffectsVolume, Windowed);
            _SavedVolume = Volume; _SavedMusicVolume = MusicVolume; _SavedEffectsVolume = SoundEffectsVolume; _SavedWindowed = Windowed;
        }
        public void Apply(Action<float, bool> save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            save(Volume, Windowed);
            _SavedVolume = Volume; _SavedWindowed = Windowed;
        }
    }

    public enum MemberMenuTab { Room, Settings, HowTo }
    public sealed class MemberMenuNavigation
    {
        public MemberMenuTab Current { get; private set; } = MemberMenuTab.Room;
        public void Select(MemberMenuTab tab) { Current = tab; }
        public bool Back() { if (Current == MemberMenuTab.Room) return false; Current = MemberMenuTab.Room; return true; }
        public void Close() { Current = MemberMenuTab.Room; }
    }
}
