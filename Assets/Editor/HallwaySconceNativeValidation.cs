#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Run after Unity imports the authored lighting sources. Does not construct runtime objects.</summary>
public static class HallwaySconceNativeValidation
{
    [MenuItem("Tools/Haunted Fish/Validate authored hallway sconces")]
    public static void Run()
    {
        var fixture = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/HallwaySconce.prefab");
        Require(fixture != null, "Authored fixture imports");
        var bulbs = fixture.GetComponentsInChildren<Light>(true);
        Require(bulbs.Length == 3, "Exactly three actual Unity Lights per fixture");
        var expected = new[] { new Vector3(-.19521f,.47628f,-.22f), new Vector3(-.03807f,.47628f,-.22f), new Vector3(.16281f,.47628f,-.22f) };
        for (var i = 0; i < bulbs.Length; i++)
        {
            var bulb = bulbs[i];
            Require(bulb.enabled && bulb.type == LightType.Point, "Enabled point bulb " + i);
            Require(Vector3.Distance(bulb.transform.localPosition, expected[i]) < .00001f, "Original bulb registration " + i);
            Require(Mathf.Abs(bulb.intensity - .22f) < .00001f && Mathf.Abs(bulb.range - 2.15f) < .00001f, "Bounded realtime energy/range " + i);
            Require(bulb.shadows == LightShadows.None, "No cubemap shadow allocation " + i);
            Require(bulb.color.b > bulb.color.g && bulb.color.g > bulb.color.r && !bulb.useColorTemperature, "Cool-blue source color " + i);
        }
        var sr = fixture.GetComponentInChildren<SpriteRenderer>(true);
        Require(sr && sr.sprite && sr.sprite.texture.width == 1173 && sr.sprite.texture.height == 1714, "Original unlit source imports at full size");
        Require(sr.sharedMaterial && sr.sharedMaterial.shader.name == "HauntedFish/HallwaySpriteLit", "Actual light-receiving material");
        Require(!ShaderUtil.ShaderHasError(sr.sharedMaterial.shader), "Shader import has no errors");
        foreach (var path in new[] { "Assets/Resources/GameHallway.prefab", "Assets/Resources/Tutorials/TutorialRecordingStage.prefab" })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(root, "Consumer imports " + path);
            var fixtures = root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Panel ") && t.name.Contains("Sconce")).ToArray();
            Require(fixtures.Length == 20, "Twenty inherited fixtures " + path);
            Require(fixtures.All(t => t.GetComponentsInChildren<Light>(true).Length == 3), "Sixty inherited real bulb lights " + path);
        }
        Debug.Log("PASS authored sconce native import and hierarchy checks. Render, performance and visual approval still require camera/playback review.");
    }
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAIL hallway lighting: " + message);
        Debug.Log("PASS hallway lighting: " + message);
    }
}
#endif
