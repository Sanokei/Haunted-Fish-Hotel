using System;
using System.Reflection;
using HauntedFish.Multiplayer;
using UnityEngine;
static class Program
{
    static int checks;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    static void Main()
    {
        var radio = new LobbyRadioPlayback();
        var music = new AudioClip(); var adA = new AudioClip(); var adB = new AudioClip();
        var source = new AudioSource();
        void Set(string name, object value) => typeof(LobbyRadioPlayback).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(radio, value);
        Set("_Source", source); Set("_Music", music); Set("_Advertisements", new[] { adA, adB });
        Check(!radio.PlayAdvertisement(), "Radio cannot tune before lobby readiness.");
        radio.SetReady(true); source.timeSamples = 1234;
        UnityEngine.Random.value = .99f;
        Check(radio.PlayAdvertisement() && radio.PlayingAdvertisement && source.clip != music && !source.loop, "Music always tunes to an ad even on a high random roll.");
        var previous = source.clip;
        UnityEngine.Random.value = .1f;
        Check(radio.PlayAdvertisement() && radio.PlayingAdvertisement && source.clip != previous, "Low roll selects a different ad.");
        UnityEngine.Random.value = .9f;
        Check(radio.PlayAdvertisement() && !radio.PlayingAdvertisement && source.clip == music && source.loop, "High roll resumes looping music.");
        Check(source.timeSamples == 1234, "Replacement ads preserve the interrupted music position.");
        Check(RadioAdvertisementSelection.DialPlaysAdvertisement(true, 2.0/3.0 - 1e-9), "Two-thirds boundary selects an ad below threshold.");
        Check(!RadioAdvertisementSelection.DialPlaysAdvertisement(true, 2.0/3.0), "Two-thirds boundary selects music at threshold.");
        int ads = 0;
        for (int i=0; i<3000; i++) if (RadioAdvertisementSelection.DialPlaysAdvertisement(true, (i+.5)/3000)) ads++;
        Check(ads == 2000, "Evenly distributed samples produce exactly two-thirds ads.");
        UnityEngine.Random.value = 0;
        radio.PlayAdvertisement(); source.isPlaying = false;
        typeof(LobbyRadioPlayback).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(radio, null);
        Check(!radio.PlayingAdvertisement && source.clip == music, "Finished ad automatically restores music.");
        Console.WriteLine($"{checks} radio behavior checks passed (production playback logic with audio stubs).");
    }
}
namespace UnityEngine
{
    public class Object { public static implicit operator bool(Object value) => value != null; public static bool operator !(Object value) => value == null; }
    public class MonoBehaviour : Object { public bool isActiveAndEnabled = true; }
    public class SerializeField : Attribute { }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string value) { } }
    public enum AudioDataLoadState { Loaded, Failed }
    public class AudioClip : Object { public float length = 10; public int samples = 100000; public AudioDataLoadState loadState; }
    public class AudioSource : Object
    {
        public bool isActiveAndEnabled = true, isPlaying, loop, playOnAwake, ignoreListenerPause;
        public AudioClip clip; public int timeSamples;
        public void Play() => isPlaying = true;
        public void Stop() { isPlaying = false; timeSamples = 0; }
    }
    public static class Random { public static float value; }
    public static class Time { public static double realtimeSinceStartupAsDouble; }
    public static class AudioListener { public static bool pause; }
    public static class Mathf { public static int Clamp(int x, int min, int max) => Math.Clamp(x,min,max); public static int Max(int a,int b) => Math.Max(a,b); }
}
