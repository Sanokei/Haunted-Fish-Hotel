using System;
using System.Collections.Generic;
using System.Reflection;
using HauntedFish.Multiplayer;
class Program
{
    static int checks;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static void Load() => typeof(HotelSettings).GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
    static void Main()
    {
        Load(); Check(HotelSettings.MasterVolume == 1 && UnityEngine.AudioListener.volume == 1, "fresh startup volume");
        HotelSettings.Save(.37f, UnityEngine.FullScreenMode.Windowed);
        Check(UnityEngine.PlayerPrefs.Saves == 1 && UnityEngine.AudioListener.volume == .37f, "Apply persists and changes real audio API");
        UnityEngine.AudioListener.volume = 1; UnityEngine.Screen.fullScreenMode = UnityEngine.FullScreenMode.FullScreenWindow;
        Load(); Check(HotelSettings.MasterVolume == .37f && UnityEngine.AudioListener.volume == .37f, "reload saved volume");
        Check(HotelSettings.DisplayMode == UnityEngine.FullScreenMode.Windowed && UnityEngine.Screen.fullScreenMode == UnityEngine.FullScreenMode.Windowed, "reload saved display choice");
        HotelSettings.Save(2f, UnityEngine.FullScreenMode.ExclusiveFullScreen);
        Check(HotelSettings.MasterVolume == 1 && HotelSettings.DisplayMode == UnityEngine.FullScreenMode.FullScreenWindow, "normalize invalid preferences");
        HotelSettings.Save(-1f, UnityEngine.FullScreenMode.Windowed);
        Check(HotelSettings.MasterVolume == 0 && UnityEngine.AudioListener.volume == 0, "mute at lower boundary");
        UnityEngine.PlayerPrefs.Values["HauntedHotel.Settings.DisplayMode"] = 999;
        UnityEngine.Screen.fullScreenMode = UnityEngine.FullScreenMode.Windowed; Load();
        Check(HotelSettings.DisplayMode == UnityEngine.FullScreenMode.Windowed, "ignore corrupt stored display enum");
        UnityEngine.Application.isEditor = true; UnityEngine.Screen.fullScreenMode = UnityEngine.FullScreenMode.Windowed;
        HotelSettings.Save(.6f, UnityEngine.FullScreenMode.FullScreenWindow);
        Check(UnityEngine.Screen.fullScreenMode == UnityEngine.FullScreenMode.Windowed, "editor does not change display");
        UnityEngine.PlayerPrefs.Values["HauntedHotel.Settings.MasterVolume"] = float.NaN; Load();
        Check(HotelSettings.MasterVolume == 1, "corrupt NaN preference defaults safely");
        HotelSettings.Save(float.PositiveInfinity, UnityEngine.FullScreenMode.Windowed);
        Check(HotelSettings.MasterVolume == 1, "invalid applied volume defaults safely");
        var draft = new MemberMenuSettingsDraft(); draft.Reopen(.37f, true);
        float appliedVolume = .37f; bool appliedWindowed = true; int writes = 0;
        Action<float, bool> apply = (v, w) => { appliedVolume = v; appliedWindowed = w; writes++; };
        draft.Volume = .8f; draft.Windowed = false;
        Check(writes == 0 && appliedVolume == .37f && appliedWindowed, "editing draft does not affect game or persistence");
        draft.Cancel(); Check(draft.Volume == .37f && draft.Windowed && writes == 0, "cancel restores both values without writes");
        draft.Reopen(.37f, true); Check(draft.Volume == .37f && draft.Windowed, "reopen reads persisted values");
        draft.Volume = .62f; draft.Windowed = false; draft.Apply(apply);
        Check(writes == 1 && appliedVolume == .62f && !appliedWindowed, "apply atomically invokes settings effect");
        draft.Volume = .2f; draft.Windowed = true; draft.Cancel();
        Check(draft.Volume == .62f && !draft.Windowed && writes == 1, "cancel after Apply preserves the latest applied values");
        draft.Reopen(appliedVolume, appliedWindowed); Check(draft.Volume == .62f && !draft.Windowed, "applied settings survive tab reopen");
        draft.Volume = float.NegativeInfinity; Check(draft.Volume == 1, "draft handles non-finite values");
        var capture = new MemberMenuInputCapture();
        Check(!capture.IsOpen && !capture.Captured && !capture.TryRelease(1, null), "fresh menu and missing device are safe");
        Check(MemberMenuInputCapture.CanOpen(true, false, false), "ready scene can open menu");
        Check(!MemberMenuInputCapture.CanOpen(false, false, false), "disconnected scene cannot open menu");
        Check(!MemberMenuInputCapture.CanOpen(true, true, false), "transition prevents menu reopening");
        Check(MemberMenuInputCapture.CanOpen(true, false, true), "Escape menu takes priority over introduction UI");
        capture.SetOpen(true, 10); Check(capture.IsOpen && capture.Captured && !capture.TryRelease(11, false), "open menu captures even without a held click");
        capture.SetOpen(false, 12); Check(!capture.IsOpen && capture.Captured && !capture.TryRelease(12, false), "Resume does not release in the closing frame");
        Check(!capture.TryRelease(13, true) && capture.Captured, "held Resume click remains captured");
        Check(capture.TryRelease(14, false) && !capture.Captured && !capture.TryRelease(15, false), "click release clears focus exactly once on a later frame");
        capture.SetOpen(true, 20); capture.SetOpen(false, 21);
        Check(capture.TryRelease(22, null), "missing pointer device releases a closed menu safely");
        capture.SetOpen(true, 30); capture.SetOpen(false, 31); capture.SetOpen(true, 32);
        Check(capture.IsOpen && capture.Captured && !capture.TryRelease(33, false), "reopen cancels a pending input release");
        capture.SetOpen(false, 34); Check(capture.Captured && capture.TryRelease(35, null), "scene transition closes and releases when no device exists");
        capture.SetOpen(true, 40); capture.Reset(); Check(!capture.IsOpen && !capture.Captured && !capture.TryRelease(41, true), "disable/destroy clears capture without a release dependency");

        HotelSettings.Save(.9f, .25f, .8f, UnityEngine.FullScreenMode.Windowed);
        Check(HotelSettings.MusicVolume == .25f && HotelSettings.SoundEffectsVolume == .8f && UnityEngine.AudioListener.volume == .9f, "separate category gains leave master independent");
        Load(); Check(HotelSettings.MusicVolume == .25f && HotelSettings.SoundEffectsVolume == .8f, "both category preferences persist through load");
        HotelSettings.Save(.6f, UnityEngine.FullScreenMode.Windowed);
        Check(HotelSettings.MusicVolume == .25f && HotelSettings.SoundEffectsVolume == .8f, "legacy master caller preserves independent categories");
        var audioDraft = new MemberMenuSettingsDraft(); audioDraft.Reopen(.6f, .25f, .8f, true);
        audioDraft.MusicVolume = 0; audioDraft.SoundEffectsVolume = .33f; audioDraft.Volume = .1f;
        Check(HotelSettings.MusicVolume == .25f && HotelSettings.SoundEffectsVolume == .8f && HotelSettings.MasterVolume == .6f, "editing all sliders has no audio or preference effects");
        audioDraft.Cancel(); Check(audioDraft.Volume == .6f && audioDraft.MusicVolume == .25f && audioDraft.SoundEffectsVolume == .8f, "cancel restores all audio categories");
        audioDraft.MusicVolume = 0; audioDraft.SoundEffectsVolume = .33f;
        audioDraft.Apply((v,m,s,w) => HotelSettings.Save(v,m,s,UnityEngine.FullScreenMode.Windowed));
        Check(HotelSettings.MusicVolume == 0 && HotelSettings.SoundEffectsVolume == .33f && UnityEngine.AudioListener.volume == .6f, "Music zero leaves Effects and Master alone");
        audioDraft.Reopen(HotelSettings.MasterVolume,HotelSettings.MusicVolume,HotelSettings.SoundEffectsVolume,true);
        Check(audioDraft.MusicVolume == 0 && audioDraft.SoundEffectsVolume == .33f, "Apply categories survive reopen");
        audioDraft.SoundEffectsVolume = 0; audioDraft.MusicVolume = 1;
        audioDraft.Apply((v,m,s,w) => HotelSettings.Save(v,m,s,UnityEngine.FullScreenMode.Windowed));
        Check(HotelSettings.MusicVolume == 1 && HotelSettings.SoundEffectsVolume == 0, "Effects zero leaves Music independent");
        Check(MemberMenuSettingsDraft.ToDecibels(0) == -80 && MemberMenuSettingsDraft.ToDecibels(1) == 0 && Math.Abs(MemberMenuSettingsDraft.ToDecibels(.5f) + 6.0206f) < .0001f, "linear sliders convert to appropriate independent mixer attenuation");
        UnityEngine.PlayerPrefs.Values["HauntedHotel.Settings.MusicVolume"] = float.NaN; UnityEngine.PlayerPrefs.Values["HauntedHotel.Settings.SoundEffectsVolume"] = -1f; Load();
        Check(HotelSettings.MusicVolume == 1 && HotelSettings.SoundEffectsVolume == 0, "corrupt category preferences normalize independently");
        var tabs = new MemberMenuNavigation(); Check(tabs.Current == MemberMenuTab.Room && !tabs.Back(), "Room Escape delegates to modal close");
        tabs.Select(MemberMenuTab.HowTo); Check(tabs.Back() && tabs.Current == MemberMenuTab.Room, "How To Escape returns to Room without closing modal");
        tabs.Select(MemberMenuTab.Settings); Check(tabs.Back() && tabs.Current == MemberMenuTab.Room, "Settings Escape returns to Room");
        tabs.Select(MemberMenuTab.HowTo); tabs.Close(); Check(tabs.Current == MemberMenuTab.Room, "close/reopen resets tutorial navigation");

        Console.WriteLine($"PASS: {checks} production settings/draft/input assertions (simulated Unity preference effects).");
    }
}
namespace UnityEngine
{
    public enum FullScreenMode { ExclusiveFullScreen, FullScreenWindow, MaximizedWindow, Windowed }
    public enum RuntimeInitializeLoadType { BeforeSceneLoad }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) {} }
    public static class Mathf { public static float Clamp01(float v) => Math.Clamp(v, 0, 1); }
    public static class Screen { public static FullScreenMode fullScreenMode = FullScreenMode.FullScreenWindow; }
    public static class AudioListener { public static float volume; }
    public static class Application { public static bool isEditor; }
    public static class PlayerPrefs
    {
        public static Dictionary<string, object> Values = new(); public static int Saves;
        public static bool HasKey(string key) => Values.ContainsKey(key);
        public static int GetInt(string key) => (int)Values[key];
        public static float GetFloat(string key, float fallback) => Values.TryGetValue(key, out var v) ? (float)v : fallback;
        public static void SetFloat(string key, float v) => Values[key] = v;
        public static void SetInt(string key, int v) => Values[key] = v;
        public static void Save() => Saves++;
    }
}
