using System;
using System.Collections.Generic;
using Ink.Runtime;

namespace Monologue.Dialogue
{
    public enum StorySequenceStepKind { SetVisible, SetVariant, PushTo, MoveUI, Knock, Wait, ExpandPanel, FlashFrames, DollyTo, CreateUI }

    public readonly struct StorySequenceStep
    {
        public readonly StorySequenceStepKind Kind;
        public readonly string Target, Variant, Parent;
        public readonly float X, Y, Seconds;
        public readonly int Count;
        public readonly bool Visible;

        public StorySequenceStep(StorySequenceStepKind kind, string target = "", string variant = "",
            float seconds = 0, float x = 0, float y = 0, int count = 0, bool visible = false, string parent = "")
        {
            Kind = kind; Target = target; Variant = variant; Parent = parent; Seconds = seconds;
            X = x; Y = y; Count = count; Visible = visible;
        }
    }

    public partial class StoryFunctions
    {
        public delegate void OnSetVisible(string objectTag, bool visible);
        public static event OnSetVisible OnSetVisibleEvent;
        public delegate void OnSetVariant(string objectTag, string variantTag);
        public static event OnSetVariant OnSetVariantEvent;
        public delegate void OnPushTo(string panelTag, float seconds);
        public static event OnPushTo OnPushToEvent;
        public delegate void OnMoveUI(string objectTag, float x, float y, float seconds);
        public static event OnMoveUI OnMoveUIEvent;
        public delegate void OnKnock(string objectTag, int count, float interval);
        public static event OnKnock OnKnockEvent;
        public delegate void OnWait(float seconds);
        public static event OnWait OnWaitEvent;
        public delegate void OnExpandPanel(string panelTag, float seconds);
        public static event OnExpandPanel OnExpandPanelEvent;
        public delegate void OnFlashFrames(int frames);
        public static event OnFlashFrames OnFlashFramesEvent;
        public delegate void OnDollyTo(string sceneTag, float amount, float seconds);
        public static event OnDollyTo OnDollyToEvent;
        public delegate void OnCreateUI(string prefabTag, string instanceTag, string parentTag);
        public static event OnCreateUI OnCreateUIEvent;

        // Ordinary stories emit the same typed events as the other StoryFunctions.
        // An automatic local sequence supplies a queue so operations run in Ink order.
        public static void BindAnimationFunctions(Story story, Action<StorySequenceStep> queue = null)
        {
            story.BindExternalFunction("CreateUI", (string prefab, string instance, string parent) =>
            {
                if (queue != null) queue(new StorySequenceStep(StorySequenceStepKind.CreateUI, instance, prefab, parent: parent));
                else OnCreateUIEvent?.Invoke(prefab, instance, parent);
            });
            story.BindExternalFunction("ExpandPanel", (string tag, float seconds) =>
            {
                if (queue != null) queue(new StorySequenceStep(StorySequenceStepKind.ExpandPanel, tag, seconds: seconds));
                else OnExpandPanelEvent?.Invoke(tag, seconds);
            });
            story.BindExternalFunction("FlashFrames", (int frames) =>
            {
                if (queue != null) queue(new StorySequenceStep(StorySequenceStepKind.FlashFrames, count: frames));
                else OnFlashFramesEvent?.Invoke(frames);
            });
            story.BindExternalFunction("DollyTo", (string tag, float amount, float seconds) =>
            {
                if (queue != null) queue(new StorySequenceStep(StorySequenceStepKind.DollyTo, tag, seconds: seconds, x: amount));
                else OnDollyToEvent?.Invoke(tag, amount, seconds);
            });
            story.BindExternalFunction("SetVisible", (string objectTag, bool visible) =>
            {
                if (queue != null) queue(new StorySequenceStep(StorySequenceStepKind.SetVisible, objectTag, visible: visible));
                else OnSetVisibleEvent?.Invoke(objectTag, visible);
            });
            story.BindExternalFunction("SetVariant", (string objectTag, string variantTag) =>
            {
                if (queue != null) queue(new StorySequenceStep(StorySequenceStepKind.SetVariant, objectTag, variantTag));
                else OnSetVariantEvent?.Invoke(objectTag, variantTag);
            });
            story.BindExternalFunction("PushTo", (string panelTag, float seconds) =>
            {
                if (queue != null) queue(new StorySequenceStep(StorySequenceStepKind.PushTo, panelTag, seconds: seconds));
                else OnPushToEvent?.Invoke(panelTag, seconds);
            });
            story.BindExternalFunction("MoveUI", (string objectTag, float x, float y, float seconds) =>
            {
                if (queue != null) queue(new StorySequenceStep(StorySequenceStepKind.MoveUI, objectTag, seconds: seconds, x: x, y: y));
                else OnMoveUIEvent?.Invoke(objectTag, x, y, seconds);
            });
            story.BindExternalFunction("Knock", (string objectTag, int count, float interval) =>
            {
                if (queue != null) queue(new StorySequenceStep(StorySequenceStepKind.Knock, objectTag, seconds: interval, count: count));
                else OnKnockEvent?.Invoke(objectTag, count, interval);
            });
            story.BindExternalFunction("Wait", (float seconds) =>
            {
                if (queue != null) queue(new StorySequenceStep(StorySequenceStepKind.Wait, seconds: seconds));
                else OnWaitEvent?.Invoke(seconds);
            });
        }
        public static void UnbindAnimationFunctions(Story story)
        {
            story.UnbindExternalFunction("CreateUI");
            story.UnbindExternalFunction("ExpandPanel");
            story.UnbindExternalFunction("FlashFrames");
            story.UnbindExternalFunction("DollyTo");
            story.UnbindExternalFunction("SetVisible");
            story.UnbindExternalFunction("SetVariant");
            story.UnbindExternalFunction("PushTo");
            story.UnbindExternalFunction("MoveUI");
            story.UnbindExternalFunction("Knock");
            story.UnbindExternalFunction("Wait");
        }

        public static IReadOnlyList<StorySequenceStep> ReadAnimationSequence(string compiledInk)
        {
            var story = new Story(compiledInk);
            var steps = new List<StorySequenceStep>();
            BindAnimationFunctions(story, step =>
            {
                if (steps.Count >= 1024) throw new InvalidOperationException("Introduction exceeds 1024 steps.");
                if (float.IsNaN(step.Seconds) || float.IsInfinity(step.Seconds) || step.Seconds < 0)
                    throw new ArgumentException("Introduction durations must be finite and nonnegative.");
                if (step.Kind != StorySequenceStepKind.Wait && step.Kind != StorySequenceStepKind.FlashFrames && string.IsNullOrWhiteSpace(step.Target))
                    throw new ArgumentException("Introduction object tags must not be empty.");
                if (step.Kind == StorySequenceStepKind.SetVariant && string.IsNullOrWhiteSpace(step.Variant))
                    throw new ArgumentException("Variant tags must not be empty.");
                if (step.Kind == StorySequenceStepKind.CreateUI && (string.IsNullOrWhiteSpace(step.Variant) || string.IsNullOrWhiteSpace(step.Parent)))
                    throw new ArgumentException("UI prefab and parent tags must not be empty.");
                if (step.Kind == StorySequenceStepKind.Knock && (step.Count < 1 || step.Count > 32))
                    throw new ArgumentException("Knock count must be between 1 and 32.");
                if (step.Kind == StorySequenceStepKind.FlashFrames && (step.Count < 1 || step.Count > 120))
                    throw new ArgumentException("Flash duration must be between 1 and 120 rendered frames.");
                if (step.Kind == StorySequenceStepKind.DollyTo && (step.X < 0 || step.X > 1))
                    throw new ArgumentException("Dolly amount must be between 0 and 1.");
                if (float.IsNaN(step.X) || float.IsInfinity(step.X) || float.IsNaN(step.Y) || float.IsInfinity(step.Y))
                    throw new ArgumentException("UI coordinates must be finite.");
                steps.Add(step);
            });
            try
            {
                while (story.canContinue)
                    if (!string.IsNullOrWhiteSpace(story.Continue()))
                        throw new InvalidOperationException("Animation Ink uses story functions; dialogue belongs in a dialogue story.");
                if (story.currentChoices.Count > 0)
                    throw new InvalidOperationException("Automatic introductions cannot wait for an Ink choice.");
                if (story.hasError) throw new InvalidOperationException(string.Join("\n", story.currentErrors));
                return steps;
            }
            finally { UnbindAnimationFunctions(story); }
        }
    }
}
