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

    // itch.io rejects any file over 200 MB. DXT needs block-aligned (multiple of 4) sizes, so most of the
    // hand-painted art silently fell back to RGBA32. Give unaligned textures ASTC, which browsers without
    // native ASTC decode at load time, and crunch the aligned ones. Manual WebGL overrides are kept.
    const int TextureBudgetPixels = 512 * 512;

    static void ApplyWebGLTextureBudget() {
        int changed = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" })) {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
            if (importer.textureType != TextureImporterType.Sprite && importer.textureType != TextureImporterType.Default) continue;
            var current = importer.GetPlatformTextureSettings("WebGL");
            bool ours = current.format == TextureImporterFormat.DXT5Crunched || current.format == TextureImporterFormat.DXT1Crunched || current.format == TextureImporterFormat.ASTC_6x6;
            if (current.overridden && !ours) continue;
            importer.GetSourceTextureWidthAndHeight(out var width, out var height);
            if (width * height < TextureBudgetPixels) continue;
            int maxSize = Mathf.Min(importer.maxTextureSize, 2048);
            float scale = Mathf.Min(1f, (float)maxSize / Mathf.Max(width, height));
            int w = Mathf.RoundToInt(width * scale), h = Mathf.RoundToInt(height * scale);
            bool aligned = w % 4 == 0 && h % 4 == 0;
            var format = aligned
                ? (importer.DoesSourceTextureHaveAlpha() ? TextureImporterFormat.DXT5Crunched : TextureImporterFormat.DXT1Crunched)
                : TextureImporterFormat.ASTC_6x6;
            if (current.overridden && current.format == format && current.maxTextureSize == maxSize) continue;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
                name = "WebGL", overridden = true, maxTextureSize = maxSize, format = format,
                compressionQuality = 70, crunchedCompression = aligned, textureCompression = TextureImporterCompression.CompressedHQ,
            });
            importer.SaveAndReimport();
            changed++;
        }
        Debug.Log($"WEBGL_TEXTURE_BUDGET changed={changed}");
    }

    [MenuItem("Haunted Fish/Build WebGL Release")]
    public static void Run() {
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;
        // Gzip with the JS fallback loads on hosts that don't send Content-Encoding headers (e.g. itch.io).
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;

        ApplyWebGLTextureBudget();

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
