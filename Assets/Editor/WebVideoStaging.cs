using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

// Stages tutorial films as StreamingAssets for WebGL, which can only play videos by URL (see WebVideoSource).
public class WebVideoStaging : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    const string Source = "Assets/TutorialVideos";
    const string Target = "Assets/StreamingAssets/Video";
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL) return;
        Directory.CreateDirectory(Target);
        foreach (var file in Directory.GetFiles(Source, "*.mp4"))
            File.Copy(file, System.IO.Path.Combine(Target, System.IO.Path.GetFileName(file)), true);
        AssetDatabase.Refresh();
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL) return;
        Cleanup();
    }

    [InitializeOnLoadMethod]
    static void Cleanup()
    {
        // Also runs on domain reload so a failed build never leaves the copies in the project.
        if (!Directory.Exists(Target)) return;
        FileUtil.DeleteFileOrDirectory(Target);
        FileUtil.DeleteFileOrDirectory(Target + ".meta");
        if (Directory.Exists("Assets/StreamingAssets") && Directory.GetFileSystemEntries("Assets/StreamingAssets").Length == 0)
        {
            FileUtil.DeleteFileOrDirectory("Assets/StreamingAssets");
            FileUtil.DeleteFileOrDirectory("Assets/StreamingAssets.meta");
        }
        AssetDatabase.Refresh();
    }
}
