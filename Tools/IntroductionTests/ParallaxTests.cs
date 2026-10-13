using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Monologue.Dialogue;
using UnityEngine;

static class ParallaxTests
{
    static void Drain(IEnumerator routine)
    {
        while (routine.MoveNext()) if (routine.Current is IEnumerator child) Drain(child);
    }
    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
        void Near(float actual, float expected, string message) => Check(Math.Abs(actual - expected) < .001f, message);
        void Reject(Action action, string message)
        {
            bool rejected = false;
            try { action(); } catch (ArgumentException) { rejected = true; } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, message);
        }
        string Compile(string source) => new Ink.Compiler(source).Compile().ToJson();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Set(object target, string field, object value) => target.GetType().GetField(field, flags).SetValue(target, value);
        var parent = new RectTransform();
        var near = new RectTransform { parent = parent, anchoredPosition = new Vector2(60, 0), localScale = new Vector3(2, 3, 4) };
        var far = new RectTransform { parent = parent, anchoredPosition = new Vector2(60, 0) };
        var rig = new StoryParallaxRig { Tag = "landscape", Layers = new[] {
            new StoryParallaxRig.Layer { Target = near, Depth = 2 },
            new StoryParallaxRig.Layer { Target = far, Depth = 5 }
        }, Poses = new[] {
            new StoryParallaxRig.Pose { Tag = "wide" },
            new StoryParallaxRig.Pose { Tag = "arrive", Camera = new Vector2(20, -100), Push = .5f }
        }};
        var rigs = new[] { rig };
        var player = new StoryParallaxPlayer();
        var step = new StorySequenceStep(StorySequenceStepKind.ParallaxTo, "landscape", "arrive", seconds: 1, easing: "linear");
        var wide = new StorySequenceStep(StorySequenceStepKind.ParallaxTo, "landscape", "wide", easing: "linear");
        Time.unscaledDeltaTime = .25f;
        player.Validate(rigs, step);
        Check(player.Supports(rigs, step), "Arbitrary named rig/pose is supported without lobby tags.");
        Check(!player.Supports(rigs, new StorySequenceStep(StorySequenceStepKind.ParallaxTo, "missing", "arrive")), "Missing rigs are not supported.");
        Reject(() => player.Validate(rigs, new StorySequenceStep(StorySequenceStepKind.ParallaxTo, "landscape", "missing")), "Missing pose fails before playback.");
        var moving = player.Play(rigs, step);
        moving.MoveNext(); moving.MoveNext(); moving.MoveNext();
        Near(near.anchoredPosition.y, 50 / 1.75f, "Halfway camera cranes down and near layer moves up.");
        Near(far.anchoredPosition.y, 50 / 4.75f, "Far layer projects the same camera move at its own depth.");
        Near(near.localScale.x, 2 * 2 / 1.75f, "Push-in is simultaneous and respects authored nonuniform scale.");
        Near(near.localScale.z, 4, "2D parallax leaves the authored Z scale alone.");
        Drain(moving);
        Near(near.anchoredPosition.x, (60 * 2 - 20) / 1.5f, "Lateral camera travel uses the same reusable perspective projection.");
        Near(near.anchoredPosition.y, 100 / 1.5f, "Near layer reaches exact authored low-camera endpoint.");
        Near(far.anchoredPosition.y, 100 / 4.5f, "Far layer moves less than foreground.");
        float endX = near.anchoredPosition.x;
        Drain(player.Play(rigs, step));
        Near(near.anchoredPosition.x, endX, "Repeated absolute camera pose never accumulates displacement.");
        Drain(player.Play(rigs, wide));
        Near(near.anchoredPosition.x, 60, "Zero-second wide pose reverses the completed camera move.");
        Near(near.localScale.y, 3, "Zero-second pose restores original nonuniform scale.");
        moving = player.Play(rigs, step); moving.MoveNext(); moving.MoveNext();
        player.Close();
        Near(near.anchoredPosition.y, 0, "Close/skip restores a partially animated layer.");
        Near(near.localScale.x, 2, "Close restores original scale.");
        Drain(moving);
        Near(near.anchoredPosition.y, 0, "A stale external iterator cannot mutate after Close.");
        var notStarted = player.Play(rigs, step); player.Close(); Drain(notStarted);
        Near(near.anchoredPosition.y, 0, "Close also invalidates an iterator not yet advanced.");
        var stale = player.Play(rigs, step); stale.MoveNext(); stale.MoveNext(); player.Close();
        var replay = player.Play(rigs, step); replay.MoveNext(); replay.MoveNext();
        float replayY = near.anchoredPosition.y;
        Drain(stale);
        Near(near.anchoredPosition.y, replayY, "An old cancelled iterator cannot overwrite a newer replay.");
        Drain(replay); player.Close(); player.Close();
        Near(near.anchoredPosition.x, 60, "Repeated replay and close remain drift-free.");
        // Overshoot/easing mistakes must not move through the camera plane.
        foreach (string name in new[] { "linear", "smooth", "smoother", "ease_in", "ease_out" })
        {
            var ease = StoryAnimationMath.ParseEase(name);
            Near(StoryAnimationMath.Ease(-1, ease), 0, name + " clamps its start.");
            Near(StoryAnimationMath.Ease(2, ease), 1, name + " clamps its end.");
            float previous = 0;
            for (int i = 0; i <= 100; i++)
            {
                float sample = StoryAnimationMath.Ease(i / 100f, ease);
                if (sample < previous || sample < 0 || sample > 1) throw new Exception("Easing must be monotonic and bounded.");
                previous = sample;
            }
            checks++;
        }
        Near(StoryAnimationMath.Ease(.5f, StoryEase.Smoother), .5f, "Cinematic smoother ease retains the midpoint.");
        Near(StoryAnimationMath.Project(30, 10, 0, 2, 1), 50, "Authored projection origin controls push framing.");
        rig.Poses[1].Push = 2;
        Reject(() => player.Validate(rigs, step), "Push cannot cross the nearest layer plane."); rig.Poses[1].Push = .5f;
        rig.Poses[1].Camera = new Vector2(float.NaN, 0);
        Reject(() => player.Validate(rigs, step), "Nonfinite camera coordinates are rejected."); rig.Poses[1].Camera = new Vector2(20, -100);
        rig.Layers[0].Depth = 0;
        Reject(() => player.Validate(rigs, step), "Nonpositive depth is rejected."); rig.Layers[0].Depth = 2;
        rig.Layers[1].Target = near;
        Reject(() => player.Validate(rigs, step), "A duplicated layer target is rejected."); rig.Layers[1].Target = far;
        far.parent = new RectTransform();
        Reject(() => player.Validate(rigs, step), "Nested or unrelated parent spaces are rejected."); far.parent = parent;
        far.anchorMin = new Vector2(1, 1);
        Reject(() => player.Validate(rigs, step), "Different sibling anchor frames are rejected."); far.anchorMin = Vector2.zero;
        near.anchorMax = far.anchorMax = Vector2.one; far.pivot = Vector2.one;
        Reject(() => player.Validate(rigs, step), "Stretched siblings cannot mix anchor-reference pivots.");
        near.anchorMax = far.anchorMax = Vector2.zero; far.pivot = Vector2.zero;
        Reject(() => player.Validate(new[] { rig, rig }, step), "Duplicate rig names are rejected.");
        rig.Poses[1].Tag = "wide";
        Reject(() => player.Validate(rigs, wide), "Duplicate pose names are rejected."); rig.Poses[1].Tag = "arrive";
        var overlap = new StoryParallaxRig { Tag = "overlap", Layers = rig.Layers, Poses = rig.Poses };
        Reject(() => player.Validate(new[] { rig, overlap }, step), "Two rigs cannot claim the same mutable layer.");
        rig.Layers[1].Target = null;
        Reject(() => player.Validate(rigs, step), "Missing authored layer reference is rejected."); rig.Layers[1].Target = far;
        foreach (var bad in new[] {
            new StorySequenceStep(StorySequenceStepKind.ParallaxTo, "landscape", "arrive", seconds: -1),
            new StorySequenceStep(StorySequenceStepKind.ParallaxTo, "landscape", "arrive", seconds: float.NaN),
            new StorySequenceStep(StorySequenceStepKind.ParallaxTo, "landscape", "arrive", easing: "unknown"),
            new StorySequenceStep(StorySequenceStepKind.ParallaxTo, "landscape", "")
        }) Reject(() => player.Validate(rigs, bad), "Malformed direct camera step is rejected.");

        const string declaration = "EXTERNAL ParallaxTo(rig,pose,seconds,easing)\n";
        var json = Compile(declaration + "~ ParallaxTo(\"landscape\",\"arrive\",1,\"smoother\")\n-> END");
        var parsed = StoryFunctions.ReadAnimationSequence(json).Single();
        Check(parsed.Kind == StorySequenceStepKind.ParallaxTo && parsed.Target == "landscape" && parsed.Variant == "arrive" && parsed.Seconds == 1 && parsed.Easing == "smoother", "Vendored Ink compiler/runtime retain every reusable parallax argument.");
        Reject(() => StoryFunctions.ReadAnimationSequence(Compile(declaration + "~ ParallaxTo(\"landscape\",\"arrive\",1,\"typo\")\n-> END")), "Ink easing typo fails before any presentation.");
        var ui = new StoryUI();
        Set(ui, "_Content", new GameObject()); Set(ui, "_ParallaxRigs", rigs);
        ui.ValidateSequence(new[] { parsed }); ui.Prepare();
        var outer = ui.Play(step); outer.MoveNext(); var inner = (IEnumerator)outer.Current;
        inner.MoveNext(); inner.MoveNext();
        Check(near.anchoredPosition.y > 0, "The actual StoryUI routes parallax through its ordered coroutine.");
        typeof(StoryUI).GetMethod("OnDisable", flags).Invoke(ui, null);
        Drain(inner); Drain(outer);
        Near(near.anchoredPosition.y, 0, "StoryUI disable restores and invalidates a partial local parallax shot.");
        ui.Prepare(); Drain(ui.Play(parsed)); ui.Close();
        Near(near.anchoredPosition.x, 60, "StoryUI replay restores the authored camera basis.");
        typeof(StoryUI).GetMethod("OnEnable", flags).Invoke(ui, null);
        var eventStory = new Ink.Runtime.Story(Compile(declaration + "~ ParallaxTo(\"landscape\",\"wide\",0,\"linear\")\n~ ParallaxTo(\"landscape\",\"arrive\",1,\"smoother\")\n-> END"));
        int events = 0;
        StoryFunctions.OnParallaxTo onParallax = (tag, pose, seconds, easing) => events++;
        StoryFunctions.OnParallaxToEvent += onParallax;
        try
        {
            StoryFunctions.BindAnimationFunctions(eventStory);
            while (eventStory.canContinue) eventStory.Continue();
            StoryFunctions.UnbindAnimationFunctions(eventStory);
            Check(events == 2 && ui.Routines.Count == 1, "Ordinary Ink events enqueue both operations on one StoryUI runner.");
            Drain(ui.Routines[0].Iterator);
            Near(near.anchoredPosition.y, 100 / 1.5f, "Ordinary StoryFunctions event path plays the authored camera endpoint.");
            ui.Close();
            typeof(StoryUI).GetMethod("OnDisable", flags).Invoke(ui, null);
            int routinesBefore = ui.Routines.Count;
            var detached = new Ink.Runtime.Story(json);
            StoryFunctions.BindAnimationFunctions(detached);
            while (detached.canContinue) detached.Continue();
            StoryFunctions.UnbindAnimationFunctions(detached);
            Check(ui.Routines.Count == routinesBefore, "Disabled StoryUI does not enqueue camera events.");
            Near(near.anchoredPosition.y, 0, "Disabled StoryUI no longer receives camera events.");
        }
        finally { StoryFunctions.OnParallaxToEvent -= onParallax; }
        // JSON parity is checked by Program after optional compilation.
        Check(File.ReadAllText(Path.Combine(root, "Assets/Dialogue/LobbyIntroduction.ink")).Contains("ParallaxTo(\"exterior\", \"wide\", 0.0, \"linear\")"), "Lobby resets its explicit wide pose before the white reveal.");
        return checks;
    }
}
