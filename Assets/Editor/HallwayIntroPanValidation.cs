#if UNITY_EDITOR
using System;
using System.Linq;
using HauntedFish.Multiplayer;
using Monologue.Dialogue;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Read-only native import/binding validation. Does not claim a runtime/render/network pass.
public static class HallwayIntroPanValidation
{
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    [MenuItem("Tools/Haunted Fish/Validate authored hallway intro pan")]
    public static void Run()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/AtticRoundCutscene.prefab");
        var ui = prefab ? prefab.GetComponent<StoryUI>() : null;
        Require(ui, "Authored attic cutscene did not import.");
        var settings = ui.GetComponent<RoundIntroductionSettings>();
        Require(settings && settings.Sequence, "Compiled round Ink is not assigned.");
        var steps = StoryFunctions.ReadAnimationSequence(settings.Sequence.text);
        ui.ValidateSequence(steps.Where(s => s.Target != "wheel_spin" && s.Target != "selected_slice" && s.Target != "attic_cutscene").ToArray());
        var poses = steps.Where(s => s.Kind == StorySequenceStepKind.ParallaxTo).ToArray();
        Require(poses.Select(s => s.Variant).SequenceEqual(new[] { "attic_top", "hotel_bottom", "hallway_end", "hallway_start" }), "Complete authored camera path was not imported.");
        Require(poses[1].Easing == "ease_in" && poses[3].Seconds == .85f, "Downward acceleration/horizontal timing changed.");
        var assets = ui.GetComponent<AtticTransitionAssets>();
        Require(assets && assets.IntroFish.Length == 4 && assets.IntroFish.All(f => f), "Four saved fish slots are required.");
        Require(assets.IntroFishWorldX.SequenceEqual(new[] { -10.5f, -8.5f, -6.5f, -4.5f }) && assets.IntroFishHeightFraction == .62f, "Starting fish layout changed.");
        var strip = ui.Find("hallway_wall").parent;
        var images = strip.GetComponentsInChildren<Image>(true);
        var door = images.SingleOrDefault(i => i.name == "Authored open attic door - hallway end");
        Require(door && AssetDatabase.GetAssetPath(door.sprite) == "Assets/Art/Hallway/HALLWAY-Attic-Door-Open-panel.png", "Actual open-door artwork is not assigned.");
        Require(Mathf.Abs(door.rectTransform.sizeDelta.x / door.rectTransform.sizeDelta.y - 1172f / 1054) < .0001f, "Door artwork is distorted.");
        foreach (string tag in new[] { "return_blur", "hallway_blur" })
        {
            var effect = ui.Find(tag);
            Require(effect && !effect.gameObject.activeSelf, "Blur must be saved disabled: " + tag);
            var blur = effect.GetComponent<AtticReturnBlur>();
            Require(blur, "Authored blur component missing: " + tag);
            var fields = new SerializedObject(blur);
            var material = fields.FindProperty("_Material").objectReferenceValue as Material;
            Require(material && material.shader && !ShaderUtil.ShaderHasError(material.shader), "Directional blur shader import/compile failed.");
            var axis = material.GetVector("_Direction");
            Require(tag == "return_blur" ? axis.x == 0 && axis.y == 1 : axis.x == 1 && axis.y == 0, "Blur direction is incorrect.");
            var targets = fields.FindProperty("_Graphics");
            Require(targets.arraySize == images.Length, "Blur target count does not cover the actual saved strip Images.");
            var bound = Enumerable.Range(0, targets.arraySize).Select(i => targets.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
            Require(bound.Distinct().Count() == bound.Length && images.All(i => bound.Contains(i)), "Blur target binding is missing or duplicated.");
        }
        Require(ui.Find("upward_streaks").GetComponent<CinematicStreaks>(), "The separate authored streak observer is missing.");
        Require(ui.Find("transition_flash") && !ui.Find("transition_flash").gameObject.activeSelf, "Flash overlay must be authored and initially hidden.");
        Debug.Log("HALLWAY_PAN_NATIVE_BINDINGS_PASS: actual prefab, Ink, four fish, source door, directions and shader import. Runtime playback/render/network validation is a separate required check.");
    }
}
#endif
