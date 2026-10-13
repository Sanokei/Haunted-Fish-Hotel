using System;
using UnityEngine;

namespace Monologue.Dialogue
{
    // Authored binding data only. Layer targets must share one parent/coordinate space.
    [Serializable]
    public sealed class StoryParallaxRig
    {
        [Serializable] public sealed class Layer
        {
            public RectTransform Target;
            [Tooltip("Positive camera depth. Smaller depths move/scale more; push must remain below this value.")]
            public float Depth = 1;
        }
        [Serializable] public sealed class Pose
        {
            public string Tag;
            [Tooltip("Camera translation in reference canvas units at depth 1. Negative Y cranes down.")]
            public Vector2 Camera;
            [Tooltip("Positive moves toward the layers; zero keeps their authored scale.")]
            public float Push;
        }
        public string Tag;
        public Vector2 ProjectionOrigin;
        public Layer[] Layers = Array.Empty<Layer>();
        public Pose[] Poses = Array.Empty<Pose>();
    }
}
