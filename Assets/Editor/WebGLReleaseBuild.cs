using System;
using System.Linq;
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

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        // Mirage assigns scene ids in NetworkIdentity.OnValidate outside of builds; resaving
        // each scene gives any newly added identities a valid id before the player build.
        foreach (var path in scenes) {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
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
