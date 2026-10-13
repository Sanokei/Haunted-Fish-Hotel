using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HauntedFish.Multiplayer;
using Monologue.Dialogue;
using UnityEngine;

static class PunchlineAudioTests
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
        var audio = new GrandmaFishPunchlineAudio();
        var source = new AudioSource();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(GrandmaFishPunchlineAudio).GetField("_Source", flags).SetValue(audio, source);
        typeof(GrandmaFishPunchlineAudio).GetField("_Rimshot", flags).SetValue(audio, new AudioClip());
        void Invoke(string name) => typeof(GrandmaFishPunchlineAudio).GetMethod(name, flags).Invoke(audio, null);
        Ink.Runtime.Story NewStory() => new Ink.Compiler("She was quite the catch.\n-> END").Compile();
        var manager = new DialogueManager { CurrentStory = NewStory(), ActiveDialoguePanel = true, _DialoguePanel = new Panel() };
        DialogueManager.Instance = manager;
        manager._DialoguePanel.DialogueText = "She was quite the catch.";
        manager._DialoguePanel.SpeechText.textInfo.characterCount = 23;
        Invoke("OnEnable");
        StoryFunctions.EmitAnimation("other");
        Check(audio.Routines.Count == 0, "Unrelated animation creates no cue.");
        StoryFunctions.EmitAnimation("grandma_rimshot");
        audio.Tick();
        audio.Tick();
        Check(source.Plays == 0, "Normal typing waits until the punchline is visible.");
        StoryFunctions.EmitAnimation("grandma_rimshot");
        Check(audio.Routines.Count == 1, "Repeated same-line tags do not create duplicate playback.");
        manager._DialoguePanel.SpeechText.maxVisibleCharacters = 23;
        audio.Tick();
        Check(source.Plays == 1, "Completing reveal triggers exactly one rimshot.");
        audio.Tick();
        Check(source.Plays == 1, "Further frames cannot repeat the cue.");

        manager.CurrentStory = NewStory();
        manager._DialoguePanel.SpeechText.maxVisibleCharacters = 0;
        StoryFunctions.EmitAnimation("grandma_rimshot");
        audio.Tick();
        manager._DialoguePanel.DialogueText = "Grandma fish watches over the lobby";
        audio.Tick();
        Check(source.Plays == 1, "Skipping the unrevealed punchline cancels its pending cue.");

        manager.CurrentStory = NewStory();
        manager._DialoguePanel.DialogueText = "She was quite the catch.";
        StoryFunctions.EmitAnimation("grandma_rimshot");
        DialogueManager.EmitEnd();
        audio.Tick();
        Check(source.Plays == 1 && audio.Routines.Count == 0, "Closing dialogue cancels pending playback.");
        manager.CurrentStory = NewStory();
        manager._DialoguePanel.SpeechText.maxVisibleCharacters = 23;
        StoryFunctions.EmitAnimation("grandma_rimshot");
        audio.Tick(); audio.Tick();
        Check(source.Plays == 2, "Replaying the story permits one fresh cue.");
        Invoke("OnDisable");
        Check(audio.Routines.Count == 0 && source.Stops > 0, "Disable releases pending routines and stops the owned source.");
        return checks;
    }
}

// Compile and execute the real cue component without a Unity Editor or added packages.
namespace UnityEngine
{
    public class Object
    {
        public static implicit operator bool(Object value) => value != null;
        public static bool operator !(Object value) => value == null;
    }
    [AttributeUsage(AttributeTargets.Field)] public class SerializeField : Attribute { }
    public class Coroutine { public IEnumerator Iterator; }
    public partial class MonoBehaviour : Object
    {
        public readonly List<Coroutine> Routines = new();
        public Coroutine StartCoroutine(IEnumerator routine) { var c = new Coroutine { Iterator = routine }; Routines.Add(c); return c; }
        public void StopCoroutine(Coroutine c) => Routines.Remove(c);
        public void Tick() { foreach (var c in Routines.ToArray()) if (!c.Iterator.MoveNext()) Routines.Remove(c); }
    }
    public class AudioClip : Object { }
    public class AudioSource : Object
    {
        public int Plays, Stops;
        public void PlayOneShot(AudioClip clip) => Plays++;
        public void Stop() => Stops++;
    }
}
namespace Monologue.Dialogue
{
    public class Speech : UnityEngine.Object { public int maxVisibleCharacters; public Info textInfo = new(); }
    public class Info { public int characterCount; }
    public class Panel : UnityEngine.Object { public string DialogueText; public Speech SpeechText = new(); }
    public class DialogueManager : UnityEngine.Object
    {
        public static DialogueManager Instance;
        public Ink.Runtime.Story CurrentStory;
        public bool ActiveDialoguePanel;
        public Panel _DialoguePanel;
        public static event Action OnDialogueEndEvent;
        public static void EmitEnd() => OnDialogueEndEvent?.Invoke();
    }
    public partial class StoryFunctions
    {
        public static event Action<string> OnAnimationEvent;
        public static void EmitAnimation(string tag) => OnAnimationEvent?.Invoke(tag);
    }
}
