using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HauntedFish.EditorTools
{
    // The walls above the lobby's 3D wainscot rise into the intro's frame shaft. They are dark so
    // the lobby reads as a lit room under an endless black well, not a cream box.
    //   Unity -batchmode -executeMethod HauntedFish.EditorTools.LobbyShaftBuilder.Build
    public static class LobbyShaftBuilder
    {
        const string Material = "Assets/Art/Materials/LobbyRevision/Lobby_Upper_Shaft_Dark.mat";

        [MenuItem("Haunted Fish/Lobby Intro/Darken Upper Lobby Walls")]
        public static void Build()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Material);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", new Color(.035f, .03f, .028f));
                material.SetFloat("_Smoothness", .05f);
                AssetDatabase.CreateAsset(material, Material);
            }
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity", OpenSceneMode.Single);
            var walls = scene.GetRootGameObjects().First(g => g.name == "Walls").transform;
            int changed = 0;
            foreach (Transform wall in walls)
            {
                if (!wall.name.StartsWith("WallOne")) continue;
                var renderer = wall.GetComponent<Renderer>();
                if (!renderer) continue;
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                changed++;
            }
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[BatchQA] darkened " + changed + " upper lobby walls");
        }
    }
}
