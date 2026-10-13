using System;
using System.Reflection;
using HauntedFish.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

static class ReadyPageAdapterTests
{
    static int checks;
    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
    static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    static void Tick(RoundHowToPage page, float seconds = .5f)
    {
        Time.unscaledDeltaTime = seconds;
        page.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(page, null);
        Keyboard.current.enterKey.wasPressedThisFrame = false;
        Keyboard.current.spaceKey.wasPressedThisFrame = false;
        Gamepad.current.buttonSouth.wasPressedThisFrame = false;
    }
    static void Main()
    {
        ReadyUpChecks.Run();
        Keyboard.current = new Keyboard(); Gamepad.current = new Gamepad();
        var page = new RoundHowToPage(); var canvas = new Canvas(); var button = new Button();
        var label = new Text(); var status = new Text();
        Set(page, "_Canvas", canvas); Set(page, "_Continue", button); Set(page, "_ContinueLabel", label);
        Set(page, "_Status", status); Set(page, "_Layout", new RoundHowToSafeArea());
        Check(page.ValidateAuthoredBindings(out _), "All authored bindings accepted");
        var local = HotelPlayer.LocalPlayer = new HotelPlayer { RoundParticipantCount = 2 };
        int sent = 0;
        page.Begin(1, 2, () => sent++);
        Check(page.Showing && canvas.enabled && !button.interactable && sent == 0, "Begin shows one locked guide without readying");
        Tick(page, 100); Check(!button.interactable && sent == 0, "Elapsed time cannot bypass authoritative cinematic acknowledgement");
        local.RoundCinematicComplete = true;
        Keyboard.current.enterKey.wasPressedThisFrame = true; Tick(page, .5f);
        Check(sent == 0 && !button.interactable, "Early Enter does not queue readiness");
        Tick(page); Tick(page); Check(button.interactable, "Read beat enables the sole Ready button");
        Check(status.text == "0 / 2 PLAYERS READY" && label.text == "READY UP", "Group readiness and local ready action have distinct labels");
        page.SetVisible(false); Keyboard.current.spaceKey.wasPressedThisFrame = true; button.onClick.Invoke(); Tick(page, 10);
        Check(sent == 0 && !button.interactable, "Hidden guide rejects keyboard and click events");
        page.SetVisible(true); local.InputBlocked = true; button.onClick.Invoke(); Tick(page);
        Check(sent == 0 && !button.interactable, "Opening menu blocks a previously enabled button");
        local.InputBlocked = false; Tick(page); Check(sent == 0 && button.interactable, "Closing menu never replays a blocked click");
        button.onClick.Invoke(); page.SetVisible(true); button.onClick.Invoke(); page.SetVisible(true); Tick(page);
        Check(sent == 1 && !button.interactable && label.text == "READY SENT", "LateUpdate SetVisible(true) retains a queued click; repeated clicks send exactly once");
        Gamepad.current.buttonSouth.wasPressedThisFrame = true; Tick(page, 100);
        Check(sent == 1 && canvas.enabled, "Pending request remains on the same page without duplicate submit");
        Check(status.text == "0 / 2 PLAYERS READY" && label.text == "READY SENT", "Group counter does not duplicate local pending instruction or imply server acknowledgement");
        local.RoundReady = true; local.RoundReadyCount = 1; Tick(page);
        Check(label.text == "YOU ARE READY" && status.text == "1 / 2 PLAYERS READY" && canvas.enabled,
            "Replicated readiness keeps the guide visible while another participant is unready");
        local.RoundParticipantCount = 3; Tick(page);
        Check(status.text == "1 / 3 PLAYERS READY", "Late arrival refreshes the live roster counter");
        local.RoundParticipantCount = 2; Tick(page);
        Check(status.text == "1 / 2 PLAYERS READY", "Departure refreshes the counter without a stale participant");
        HotelPlayer.LocalPlayer = null; Tick(page);
        Check(status.text == "0 / 0 PLAYERS READY" && !button.interactable, "Lost player cannot leave stale counts or ready action");
        page.Cancel(); Check(!page.Showing && button.onClick.Count == 0 && sent == 1, "Cancellation releases the owned button listener");
        HotelPlayer.LocalPlayer = local; local.RoundReady = false; local.RoundReadyCount = 0;
        for (int i = 0; i < 20; i++)
        {
            page.SetVisible(false); page.Begin(1, 1, () => sent++);
            Check(canvas.enabled && button.onClick.Count == 1 && !button.interactable, "Replay restores one guide and exactly one listener");
            Tick(page, 2); Gamepad.current.buttonSouth.wasPressedThisFrame = true; Tick(page);
            Check(sent == i + 2 && !button.interactable, "Gamepad deliberately readies replay once");
            page.Cancel();
        }
        page.Begin(1, 2, () => sent++); Tick(page, 2); button.onClick.Invoke();
        page.GetType().GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(page, null);
        Tick(page, 2);
        Check(sent == 21 && button.onClick.Count == 0, "Disable/disconnect discards pending click without callback");
        Console.WriteLine("Passed " + checks + " actual RoundHowToPage adapter checks using Unity/input doubles; no native or network pass claimed.");
    }
}

namespace UnityEngine
{
    public class Object { public static implicit operator bool(Object value) => value != null; }
    public class MonoBehaviour : Object { }
    public class Canvas : Object { public bool enabled = true; }
    public sealed class SerializeField : Attribute { }
    public sealed class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public static class Mathf { public static float Max(float a, float b) => Math.Max(a, b); }
    public static class Time { public static float unscaledDeltaTime; }
}
namespace UnityEngine.UI
{
    public class Text : UnityEngine.Object { public string text; }
    public class Button : UnityEngine.Object
    {
        public bool interactable;
        public readonly Click onClick = new Click();
        public sealed class Click
        {
            event Action handlers;
            public int Count => handlers?.GetInvocationList().Length ?? 0;
            public void AddListener(Action action) => handlers += action;
            public void RemoveListener(Action action) => handlers -= action;
            public void Invoke() => handlers?.Invoke();
        }
    }
}
namespace UnityEngine.InputSystem
{
    public sealed class Press { public bool wasPressedThisFrame; }
    public sealed class Keyboard { public static Keyboard current; public readonly Press enterKey = new Press(), spaceKey = new Press(); }
    public sealed class Gamepad { public static Gamepad current; public readonly Press buttonSouth = new Press(); }
}
namespace HauntedFish.Multiplayer
{
    public sealed class HotelPlayer : UnityEngine.Object
    {
        public static HotelPlayer LocalPlayer;
        public bool RoundCinematicComplete, RoundReady, InputBlocked;
        public int RoundReadyCount, RoundParticipantCount;
    }
    public sealed class RoundHowToSafeArea : UnityEngine.Object { public bool HasBindings => true; }
}
