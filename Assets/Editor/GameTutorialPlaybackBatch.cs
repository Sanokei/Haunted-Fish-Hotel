#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using HauntedFish.Multiplayer;

// The historical entry point now validates the single-page ready guides.
// Theatre playback has its own independent TheatreNativeValidation suite.
public static class GameTutorialPlaybackBatch
{
    public static void Run()
    {
        if (!Application.isBatchMode || !Environment.GetCommandLineArgs().Contains("--game-tutorial-playback"))
            throw new InvalidOperationException("Explicit owned --game-tutorial-playback batch required.");
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Requires an idle validation project in Edit mode.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Refusing a dirty scene.");
        string evidence = System.IO.Path.GetFullPath("Temp/ReadyUpNativeValidation");
        Directory.CreateDirectory(evidence);
        int checks = 0;
        try
        {
            foreach (string role in new[] { "Fish", "Ghost" })
            {
                string path = "Assets/Resources/" + role + "RoundHowTo.prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var page = root.GetComponent<RoundHowToPage>();
                    Require(page && page.ValidateAuthoredBindings(out _), role + " authored bindings"); checks++;
                    Require(!root.GetComponent<UnityEngine.Video.VideoPlayer>() && !root.GetComponentInChildren<RawImage>(true),
                        role + " has no movie/video substitute"); checks++;
                    Require(root.GetComponentsInChildren<Canvas>(true).Length == 1 && root.GetComponentsInChildren<Button>(true).Length == 1,
                        role + " has one canvas/page and one Ready action"); checks++;
                    Canvas.ForceUpdateCanvases();
                    foreach (var text in root.GetComponentsInChildren<Text>(true))
                    {
                        Require(text.font && text.gameObject.activeSelf, role + " visible imported text " + text.name);
                        Require(text.preferredHeight <= text.rectTransform.rect.height + 1,
                            role + " overflowing text " + text.name); checks += 2;
                    }
                    int sent = 0;
                    page.Begin(1, role == "Ghost" ? 1u : 2u, () => sent++);
                    Require(page.Showing && sent == 0, role + " begins without automatic readiness"); checks++;
                    page.SetVisible(false);
                    Require(!root.GetComponentInChildren<Button>(true).interactable, role + " hidden guide blocks input"); checks++;
                    page.Cancel();
                    Require(!page.Showing && sent == 0, role + " cancellation never readies"); checks++;
                    page.Begin(1, role == "Ghost" ? 1u : 2u, () => sent++);
                    Require(page.Showing && root.GetComponent<Canvas>().enabled && sent == 0, role + " replay restores one visible page"); checks++;
                    page.Cancel();
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            File.WriteAllText(System.IO.Path.Combine(evidence, "result.txt"), "PASS " + checks +
                " native imported ready-page binding, font-height and lifecycle checks. No screenshot, play-mode input or live multiplayer pass claimed.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(System.IO.Path.Combine(evidence, "result.txt"), "FAIL " + error);
            EditorApplication.Exit(1);
        }
    }
    static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
#endif
