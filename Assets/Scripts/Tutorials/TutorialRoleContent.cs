using System;
using UnityEngine;
using UnityEngine.Video;

namespace HauntedFish.Tutorials
{
    public enum TutorialRole { Fish, Ghost }
    [Serializable]
    public sealed class TutorialInstruction
    {
        public string Heading;
        [TextArea(2, 6)] public string Body;
    }
    // Shared authored content; consumers own their presentation, input and playback lifecycle.
    [CreateAssetMenu(menuName = "Haunted Fish/Tutorial role content")]
    public sealed class TutorialRoleContent : ScriptableObject
    {
        public TutorialRole Role;
        public string RoleLabel, Title;
        [TextArea(3, 12)] public string Summary;
        public TutorialInstruction[] Instructions = Array.Empty<TutorialInstruction>();
        public VideoClip Recording;
        public TextAsset RecordingInk;
        public bool Loop = true;
        public bool HasAudio;
        public int RecordedWidth, RecordedHeight;
        public double RecordedSeconds, RecordedFrameRate;
        public ulong RecordedFrames;
        public string CaptureProvenance;
        public double Duration => Recording ? Recording.length : RecordedSeconds;
        public float Aspect => Recording && Recording.height > 0 ? Recording.width / (float)Recording.height : 16f / 9f;
    }
}
