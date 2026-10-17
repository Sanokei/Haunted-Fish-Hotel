using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// Batch entry point: Unity.exe -batchmode -quit -projectPath . -executeMethod WebGLReleaseBuild.Run
public static class WebGLReleaseBuild {
    const string OutputPath = "Builds/WebGL";

    [MenuItem("Haunted Fish/Build WebGL Release")]
    public static void Run() {
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;
        // Gzip with the JS fallback loads on hosts that don't send Content-Encoding headers (e.g. itch.io).
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        // Mirage assigns scene ids in NetworkIdentity.OnValidate outside of builds; resaving
        // each scene gives any newly added identities a valid id before the player build.
        foreach (var path in scenes) {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            // OnValidate does not reliably run for inactive identities on load, so call it directly.
            var onValidate = typeof(Mirage.NetworkIdentity).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (var identity in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Mirage.NetworkIdentity>(true))) {
                onValidate.Invoke(identity, null);
                EditorUtility.SetDirty(identity);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = scenes,
            locationPathName = OutputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        });
        var summary = report.summary;
        Debug.Log($"WEBGL_BUILD_RESULT {summary.result} size={summary.totalSize} errors={summary.totalErrors} time={summary.totalTime}");
        if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
