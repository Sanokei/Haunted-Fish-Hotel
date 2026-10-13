using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Monologue.Dialogue
{
    // Per-StoryUI playback state. No discovery, subscriptions, GameObjects or Update loop.
    public sealed class StoryParallaxPlayer
    {
        sealed class LayerState
        {
            public StoryParallaxRig.Layer Layer;
            public Vector2 Position;
            public Vector3 Scale;
        }
        sealed class RigState
        {
            public LayerState[] Layers;
            public Vector2 Camera;
            public float Push;
        }
        readonly Dictionary<StoryParallaxRig, RigState> _States = new Dictionary<StoryParallaxRig, RigState>();
        int _Version;

        static StoryParallaxRig Find(StoryParallaxRig[] rigs, string tag) =>
            Array.Find(rigs, rig => rig != null && rig.Tag == tag);
        static StoryParallaxRig.Pose Pose(StoryParallaxRig rig, string tag) =>
            rig == null || rig.Poses == null ? null : Array.Find(rig.Poses, pose => pose != null && pose.Tag == tag);
        public bool Supports(StoryParallaxRig[] rigs, StorySequenceStep step) => Pose(Find(rigs, step.Target), step.Variant) != null;
        public void Validate(StoryParallaxRig[] rigs, StorySequenceStep step)
        {
            StoryFunctions.ValidateAnimationStep(step);
            var rig = Find(rigs, step.Target);
            var pose = Pose(rig, step.Variant);
            if (pose == null) throw new InvalidOperationException("Unbound parallax rig/pose: " + step.Target + "/" + step.Variant);
            var tags = new HashSet<string>();
            foreach (var candidate in rigs)
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.Tag) || !tags.Add(candidate.Tag))
                    throw new InvalidOperationException("Parallax rig tags must be nonempty and unique.");
            if (!StoryAnimationMath.Finite(rig.ProjectionOrigin.x) || !StoryAnimationMath.Finite(rig.ProjectionOrigin.y))
                throw new ArgumentException("Parallax projection origin must be finite.");
            if (rig.Layers == null || rig.Layers.Length == 0) throw new InvalidOperationException("Parallax rig has no authored layers: " + rig.Tag);
            var targets = new HashSet<RectTransform>();
            var parent = rig.Layers[0]?.Target ? rig.Layers[0].Target.parent : null;
            foreach (var layer in rig.Layers)
            {
                if (layer == null || !layer.Target || !targets.Add(layer.Target) || layer.Target.parent != parent)
                    throw new InvalidOperationException("Parallax layers must be unique authored sibling transforms: " + rig.Tag);
                if (layer.Target.anchorMin != rig.Layers[0].Target.anchorMin || layer.Target.anchorMax != rig.Layers[0].Target.anchorMax)
                    throw new InvalidOperationException("Parallax layers must share their sibling anchor frame: " + rig.Tag);
                if (layer.Target.anchorMin != layer.Target.anchorMax && layer.Target.pivot != rig.Layers[0].Target.pivot)
                    throw new InvalidOperationException("Stretched parallax layers must share the same anchor-reference pivot: " + rig.Tag);
                StoryAnimationMath.Magnification(layer.Depth, 0);
                foreach (var other in rigs)
                    if (other != rig && other.Layers != null && Array.Exists(other.Layers, item => item != null && item.Target == layer.Target))
                        throw new InvalidOperationException("A parallax layer cannot belong to multiple rigs.");
            }
            tags.Clear();
            foreach (var authored in rig.Poses)
            {
                if (authored == null || string.IsNullOrWhiteSpace(authored.Tag) || !tags.Add(authored.Tag))
                    throw new InvalidOperationException("Parallax pose tags must be nonempty and unique: " + rig.Tag);
                if (!StoryAnimationMath.Finite(authored.Camera.x) || !StoryAnimationMath.Finite(authored.Camera.y))
                    throw new ArgumentException("Parallax camera coordinates must be finite.");
                foreach (var layer in rig.Layers) StoryAnimationMath.Magnification(layer.Depth, authored.Push);
            }
        }
        RigState Remember(StoryParallaxRig rig)
        {
            if (_States.TryGetValue(rig, out var state)) return state;
            state = new RigState { Layers = new LayerState[rig.Layers.Length] };
            for (int i = 0; i < rig.Layers.Length; i++)
            {
                var layer = rig.Layers[i];
                state.Layers[i] = new LayerState { Layer = layer, Position = layer.Target.anchoredPosition, Scale = layer.Target.localScale };
            }
            _States.Add(rig, state);
            return state;
        }
        static void Apply(StoryParallaxRig rig, RigState state, Vector2 camera, float push)
        {
            state.Camera = camera; state.Push = push;
            foreach (var item in state.Layers)
            {
                var target = item.Layer.Target;
                if (!target) continue;
                float depth = item.Layer.Depth;
                float scale = StoryAnimationMath.Magnification(depth, push);
                target.anchoredPosition = new Vector2(
                    StoryAnimationMath.Project(item.Position.x, rig.ProjectionOrigin.x, camera.x, depth, push),
                    StoryAnimationMath.Project(item.Position.y, rig.ProjectionOrigin.y, camera.y, depth, push));
                target.localScale = new Vector3(item.Scale.x * scale, item.Scale.y * scale, item.Scale.z);
            }
        }
        public IEnumerator Play(StoryParallaxRig[] rigs, StorySequenceStep step)
        {
            // Capture cancellation before returning the iterator, including callers that have not advanced it yet.
            int version = _Version;
            return PlayCurrent(rigs, step, version);
        }
        IEnumerator PlayCurrent(StoryParallaxRig[] rigs, StorySequenceStep step, int version)
        {
            if (version != _Version) yield break;
            Validate(rigs, step);
            var rig = Find(rigs, step.Target);
            var pose = Pose(rig, step.Variant);
            var state = Remember(rig);
            var camera = state.Camera;
            float push = state.Push;
            var ease = StoryAnimationMath.ParseEase(step.Easing);
            for (float elapsed = 0; elapsed < step.Seconds; elapsed += Time.unscaledDeltaTime)
            {
                if (version != _Version) yield break;
                float t = StoryAnimationMath.Ease(elapsed / step.Seconds, ease);
                Apply(rig, state, Vector2.Lerp(camera, pose.Camera, t), Mathf.Lerp(push, pose.Push, t));
                yield return null;
            }
            if (version == _Version) Apply(rig, state, pose.Camera, pose.Push);
        }
        public void Close()
        {
            _Version++;
            foreach (var state in _States.Values)
                foreach (var item in state.Layers)
                    if (item.Layer.Target)
                    {
                        item.Layer.Target.anchoredPosition = item.Position;
                        item.Layer.Target.localScale = item.Scale;
                    }
            _States.Clear();
        }
    }
}
