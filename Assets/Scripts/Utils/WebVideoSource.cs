using UnityEngine;
using UnityEngine.Video;

// WebGL's VideoPlayer cannot decode VideoClip assets; it only streams URLs.
// WebVideoStaging copies each clip to StreamingAssets/Video/<clip name>.mp4 for WebGL builds.
public static class WebVideoSource
{
    public static void Assign(VideoPlayer player, VideoClip clip)
    {
        if (Application.platform == RuntimePlatform.WebGLPlayer && clip)
        {
            player.source = VideoSource.Url;
            player.url = Application.streamingAssetsPath + "/Video/" + clip.name + ".mp4";
        }
        else
        {
            player.source = VideoSource.VideoClip;
            player.clip = clip;
        }
    }
}
