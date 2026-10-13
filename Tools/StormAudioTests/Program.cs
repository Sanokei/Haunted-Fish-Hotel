using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HauntedFish.Multiplayer;
using UnityEngine;
using Monologue.Dialogue;

static class Program
{
    static int checks;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static void Near(float actual, float target, string message) => Check(Math.Abs(actual - target) < .001f, message);
    static void Main()
    {
        Check(Math.Abs(LobbyStormAudioState.PlaybackDelay(.5, .06, 1) - .44) < .00001, "Cue lead-in is subtracted from audible distance delay.");
        Check(Math.Abs(LobbyStormAudioState.PlaybackDelay(.5, .06, 2) - .47) < .00001, "Fast pitch shortens authored lead-in.");
        Check(Math.Abs(LobbyStormAudioState.PlaybackDelay(.5, .06, .5) - .38) < .00001, "Slow pitch lengthens authored lead-in.");
        Check(LobbyStormAudioState.PlaybackDelay(.02, .06, 1) == 0, "Near/immediate thunder never schedules in the past.");
        Check(LobbyStormAudioState.PlaybackDelay(0, .1, 0) == 0, "Zero pitch and immediate distance remain safely clamped.");
        Check(Math.Abs(LobbyStormAudioState.PlaybackDelay(1.8, .1, 1) - 1.7) < .00001, "Far-rumble metadata preserves the desired acoustic delay.");
        var state = new LobbyStormAudioState();
        state.Strike(1, 0, .5, 0, 1);
        Check(state.PendingCount == 0, "No cues before the storm is started.");
        state.Start();
        state.Strike(1, 0, 2, 0, 1);
        state.Strike(2, .1, .2, 1, .96f);
        state.Strike(2, .1, .2, 1, .96f);
        Check(state.PendingCount == 2, "Every new actual strike gets one cue; duplicate event is ignored.");
        Check(!state.TryTakeDue(.29, out _), "Thunder cannot precede its propagation delay.");
        Check(state.TryTakeDue(.31, out var earliest) && earliest.Clip == 1, "Rapid strikes are ordered by due time, not input order.");
        Check(!state.TryTakeDue(1.9, out _), "Earlier long-delay cue remains pending.");
        Check(state.TryTakeDue(2, out var later) && later.Clip == 0, "Earlier strike is retained after a rapid retrigger.");
        Check(!state.TryTakeDue(10, out _), "Dequeued cues cannot repeat.");
        state.Advance(10, .16f, .42f, .55f, .65f, .5f);
        Near(state.RainGain, .16f, "Rain approaches its comfortable base gain.");
        Near(state.ThunderGain, .42f, "Thunder approaches its base gain.");
        state.SetDialogue(true);
        state.Advance(10, .16f, .42f, .55f, .65f, .5f);
        Near(state.RainGain, .088f, "Dialogue ducks rain.");
        Near(state.ThunderGain, .273f, "Dialogue ducks thunder.");
        state.SetDialogue(false);
        state.Advance(10, .16f, .42f, .55f, .65f, .5f);
        Near(state.RainGain, .16f, "Dialogue completion restores rain.");
        state.Strike(3, 10, 1, 0, 1);
        state.Stop();
        Check(state.PendingCount == 0 && !state.TryTakeDue(99, out _), "Scene stop cancels all pending thunder.");
        Near(state.RainGain, 0, "Scene stop clears rain gain.");
        Near(state.ThunderGain, 0, "Scene stop clears thunder gain.");
        state.Start(); state.Strike(1, 20, .5, 0, 1);
        Check(state.TryTakeDue(20.5, out _), "Reactivation creates a fresh storm lifetime.");

        var storm = new LobbyStormAudio();
        var lightning = new LightningEffectManager();
        var rain = new AudioSource();
        var thunder = new AudioSource();
        var thunderB = new AudioSource();
        var rainClip = new AudioClip { length = 60 };
        var medium = new AudioClip { length = 10.04f };
        var soft = new AudioClip { length = 8.4f };
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Set(string field, object value) => typeof(LobbyStormAudio).GetField(field, flags).SetValue(storm, value);
        void Call(string method) => typeof(LobbyStormAudio).GetMethod(method, flags).Invoke(storm, null);
        Set("_Lightning", lightning); Set("_RainSource", rain); Set("_ThunderSource", thunder);
        Set("_ThunderSourceB", thunderB); Set("_RainLoop", rainClip);
        Set("_ThunderClips", new[] { medium, soft });
        DialogueManager.Instance = new DialogueManager();
        Time.unscaledTimeAsDouble = 0; Time.unscaledDeltaTime = .1f;
        Call("OnEnable");
        Check(!storm.Running && rain.Plays == 0, "Rain waits for the existing light-owner intro/ambient start.");
        Check(lightning.StrikeSubscribers == 1 && DialogueManager.StartSubscribers == 1, "Enable subscribes once to light and dialogue.");
        lightning.Begin(); lightning.Strike(1); Call("Update");
        Check(rain.Plays == 1 && rain.clip == rainClip && rain.loop, "Intro/ambient start launches the actual rain loop once.");
        Check(rain.volume > 0 && rain.volume < 1, "Rain fades in without normalizing the quiet master.");
        Check(thunder.Schedules == 1 && thunder.LastClip == medium, "Actual strike reserves one dedicated DSP voice.");
        Check(Math.Abs(thunder.ScheduledAt - .94) < .001, "Medium cue .060-second lead-in compensates its 1-second acoustic delay.");
        Check(thunderB.Schedules == 0, "One strike never double-fires the second voice.");
        lightning.Begin(); lightning.Strike(1); Call("Update");
        Check(rain.Plays == 1 && thunder.Schedules == 1, "Repeated ambient start and duplicate strike do not restart audio.");
        Time.unscaledTimeAsDouble = .1;
        lightning.Strike(2); Call("Update");
        Check(thunderB.Schedules == 1 && thunderB.LastClip == soft, "Rapid second strike uses a separate voice and avoids the same cue.");
        Check(Math.Abs(thunderB.ScheduledAt - .64) < .001, "Soft cue keeps its own distance-delay metadata.");
        Time.unscaledTimeAsDouble = .2;
        lightning.Strike(3); Call("Update");
        Check(storm.PendingThunder == 1 && thunder.Schedules == 1 && thunderB.Schedules == 1,
            "An occupied two-voice pool queues additional thunder without cutting either tail.");
        DialogueManager.Begin(); Time.unscaledDeltaTime = 10; Call("Update");
        Near(rain.volume, .55f, "Actual rain source smoothly ducks during dialogue.");
        Near(thunder.volume, .52f, "Medium cue retains .8 art-direction gain under .65 dialogue ducking.");
        Near(thunderB.volume, .455f, "Soft cue retains .7 art-direction gain under .65 dialogue ducking.");
        Check(storm.PendingThunder == 1 && rain.Stops == 0, "Dialogue preserves looping rain and queued strikes.");
        DialogueManager.End(); Call("Update");
        Near(rain.volume, 1, "Dialogue end restores package-recommended unity rain gain.");
        Near(thunder.volume, .8f, "Dialogue end restores medium cue gain without loud normalization.");
        Near(thunderB.volume, .7f, "Dialogue end restores soft cue gain.");
        Time.unscaledTimeAsDouble = 9.2; Call("Update");
        Check(storm.PendingThunder == 0 && thunderB.Schedules == 2, "A free voice eventually plays the retained third strike.");
        Check(thunderB.ScheduledAt >= 9.25, "Late queued thunder receives a safe DSP lead rather than scheduling in the past.");
        lightning.Strike(4); lightning.End();
        Check(!storm.Running && storm.PendingThunder == 0 && thunder.Stops > 0 && thunderB.Stops > 0,
            "Light-owner stop cancels scheduled and queued thunder.");
        Time.unscaledDeltaTime = .5f; Call("Update");
        Check(rain.volume > 0 && rain.volume < 1 && rain.Stops == 0, "Ambient stop begins the package's two-second rain fade.");
        Time.unscaledDeltaTime = 1.5f; Call("Update");
        Check(rain.Stops == 1 && rain.volume == 0, "Rain fade finishes and releases playback.");
        Time.unscaledTimeAsDouble = 20;
        lightning.Begin(); lightning.Strike(1); Call("Update");
        Check(rain.Plays == 2 && thunder.Schedules == 2, "Replay/reactivation starts a fresh rain and thunder lifetime.");
        Call("OnDisable");
        Check(lightning.StrikeSubscribers == 0 && lightning.AmbientSubscribers == 0 &&
            DialogueManager.StartSubscribers == 0 && DialogueManager.EndSubscribers == 0,
            "Disable detaches every light and dialogue subscription.");
        lightning.Strike(6); DialogueManager.Begin();
        Check(storm.PendingThunder == 0 && !storm.Running, "Disabled adapter cannot receive stale events.");
        DialogueManager.Instance.ActiveDialoguePanel = true;
        Time.unscaledDeltaTime = 10;
        Call("OnEnable"); Call("Update");
        Near(rain.volume, .55f, "Enabling during active dialogue applies ducking.");
        Check(lightning.StrikeSubscribers == 1 && rain.Plays == 3, "Re-enable subscribes and starts exactly once.");
        Call("OnDisable");
        thunder.pitch = 2; Time.unscaledTimeAsDouble = 40;
        Call("OnEnable"); lightning.Strike(7); Call("Update");
        Check(Math.Abs(thunder.ScheduledAt - 40.97) < .001 && thunderB.pitch == 2,
            "Both dedicated voices share positive constant pitch and compensate onset at pitch two.");
        Call("OnDisable");
        Set("_ThunderCueDelays", new[] { new Vector2(.01f, .01f), new Vector2(.01f, .01f) });
        thunder.pitch = .5f; Time.unscaledTimeAsDouble = 60;
        Call("OnEnable"); lightning.Strike(8); Call("Update");
        Check(Math.Abs(thunder.ScheduledAt - 60.05) < .001,
            "Immediate near thunder safely clamps DSP scheduling to fifty milliseconds ahead.");
        Call("OnDisable");

        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var lightSource = File.ReadAllText(Path.Combine(root, "Assets/Scripts/LightningEffectManager.cs"));
        Check(lightSource.Split("StrikeStarted?.Invoke(++_StrikeId)").Length == 2, "Actual lightning source emits exactly one strike event.");
        Check(lightSource.IndexOf("StrikeStarted?.Invoke(++_StrikeId)") > lightSource.IndexOf("IEnumerator Strike(bool whiteFrame)"),
            "Event belongs to the actual strike coroutine, not a second timer.");
        Check(lightSource.Contains("for (int frame = 0; frame < 3; frame++) yield return null;") &&
            lightSource.Contains("float pulse = Mathf.Exp(-6 * t) + .45f * Mathf.Exp(-Mathf.Pow((t - .3f) / .055f, 2));"),
            "Original white-frame count and window light envelope are preserved.");
        Console.WriteLine($"Passed {checks} storm audio checks.");
    }
}

public class LightningEffectManager : UnityEngine.Object
{
    public event Action<bool> AmbientStateChanged;
    public event Action<long> StrikeStarted;
    public bool AmbientActive { get; private set; }
    public int StrikeSubscribers => StrikeStarted?.GetInvocationList().Length ?? 0;
    public int AmbientSubscribers => AmbientStateChanged?.GetInvocationList().Length ?? 0;
    public void Begin() { AmbientActive = true; AmbientStateChanged?.Invoke(true); }
    public void End() { AmbientActive = false; AmbientStateChanged?.Invoke(false); }
    public void Strike(long id) => StrikeStarted?.Invoke(id);
}
namespace Monologue.Dialogue
{
    public class DialogueManager : UnityEngine.Object
    {
        public static DialogueManager Instance;
        public bool ActiveDialoguePanel;
        public static event Action OnDialogueStartEvent, OnDialogueEndEvent;
        public static int StartSubscribers => OnDialogueStartEvent?.GetInvocationList().Length ?? 0;
        public static int EndSubscribers => OnDialogueEndEvent?.GetInvocationList().Length ?? 0;
        public static void Begin() => OnDialogueStartEvent?.Invoke();
        public static void End() => OnDialogueEndEvent?.Invoke();
    }
}
namespace UnityEngine
{
    public class Object
    {
        public static implicit operator bool(Object value) => value != null;
        public static bool operator !(Object value) => value == null;
    }
    public class MonoBehaviour : Object { }
    [AttributeUsage(AttributeTargets.Field)] public class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    [AttributeUsage(AttributeTargets.Field)] public class HeaderAttribute : Attribute { public HeaderAttribute(string text) { } }
    [AttributeUsage(AttributeTargets.Field)] public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    [AttributeUsage(AttributeTargets.Field)] public class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public struct Vector2 { public float x, y; public Vector2(float a, float b) { x = a; y = b; } }
    public static class Mathf
    {
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Clamp01(float value) => Clamp(value, 0, 1);
        public static float MoveTowards(float value, float target, float delta) => value < target ? Math.Min(target, value + delta) : Math.Max(target, value - delta);
        public static float Clamp(float value, float a, float b) => Math.Max(a, Math.Min(value, b));
    }
    public static class Random
    {
        public static int Range(int minimum, int maximum) => minimum;
        public static float Range(float minimum, float maximum) => minimum;
    }
    public static class AudioSettings { public static double dspTime => Time.unscaledTimeAsDouble; }
    public static class Time { public static double unscaledTimeAsDouble; public static float unscaledDeltaTime; }
    public static class Debug { public static void LogError(string message, Object context) => throw new Exception(message); }
    public class AudioClip : Object { public float length = 1; }
    public class AudioSource : Object
    {
        public bool loop, playOnAwake;
        public float volume;
        public float pitch = 1;
        public AudioClip clip, LastClip;
        public int Plays, OneShots, Stops, Schedules;
        public double ScheduledAt;
        public void Play() => Plays++;
        public void PlayScheduled(double start) { Schedules++; ScheduledAt = start; LastClip = clip; }
        public void Stop() => Stops++;
        public void PlayOneShot(AudioClip audio, float gain) { OneShots++; LastClip = audio; }
    }
}
