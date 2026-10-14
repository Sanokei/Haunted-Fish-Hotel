#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Editor-only check of imported shader passes and authored renderer flags. No runtime construction.</summary>
public static class WorldArtworkLightingValidation
{
    [MenuItem("Tools/Haunted Fish/Validate world artwork shadows")]
    public static void Validate()
    {
        string[] shaderPaths = {
            "Assets/Art/Hallway/HallwaySpriteLit.shader",
            "Assets/Art/Materials/LobbyRevision/LobbySpriteFog.shader",
            "Assets/Art/WorldLighting/WorldSpriteLit.shader",
            "Assets/Art/BossFight/BossArmVoidFade.shader",
            "Assets/Prefabs/WatchingFrames/Materials/WatchingEye.shader",
            "Assets/Art/WorldLighting/LobbyHeightAtmosphere.shader"
        };
        foreach (string path in shaderPaths)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Require(shader, "Missing imported shader: " + path);
            Require(!ShaderUtil.ShaderHasError(shader), "Shader import/compiler error: " + path);
        }
        string[] prefabs = {
            "Assets/Resources/HotelNetworkPlayer.prefab",
            "Assets/Resources/GameHallway.prefab",
            "Assets/Resources/HallwaySconce.prefab",
            "Assets/Resources/HallwayDoor.prefab",
            "Assets/Resources/Traps/ShoppingCartTrap.prefab",
            "Assets/Resources/Traps/FallingChandelierTrap.prefab",
            "Assets/Resources/Traps/AnglerFishTrap.prefab",
            "Assets/Resources/BossFight/BossSegmentedOpponent.prefab",
            "Assets/Prefabs/WatchingFrames/LobbyWatchingFrames.prefab",
            "Assets/Prefabs/WatchingFrames/HallwayWatchingFrames.prefab"
        };
        int renderers = 0;
        foreach (string path in prefabs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(prefab, "Missing prefab: " + path);
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                var material = renderer.sharedMaterial;
                if (!material || !material.HasProperty("_ShadowCutoff")) continue;
                Require(material.FindPass("ShadowCaster") >= 0, "Missing alpha shadow pass: " + path + "/" + renderer.name);
                Require(renderer.shadowCastingMode == ShadowCastingMode.TwoSided, "Two-sided shadow mode missing: " + path + "/" + renderer.name);
                Require(renderer.receiveShadows, "Shadow reception disabled: " + path + "/" + renderer.name);
                Require(material.GetFloat("_ShadowCutoff") > 0, "Invalid alpha cutoff: " + path);
                renderers++;
            }
        }
        Require(renderers > 400, "Expected imported physical artwork/gallery coverage.");
        Debug.Log("PASS imported world-art shadow contracts: " + renderers + " renderers. Still inspect native cameras, moving shadows, pupils, and performance on target devices.");
    }
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
#endif
