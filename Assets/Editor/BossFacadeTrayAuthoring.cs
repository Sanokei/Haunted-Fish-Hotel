#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HauntedFish.BossFight;

// Rebuilds the three tray pieces each side places (bottle, tuna can, building) as miniatures of the lobby-intro
// city: the same night facade textures OpeningCityBuilder hangs on its skyline. Run with the BossFight scene open.
public static class BossFacadeTrayAuthoring
{
    const string NightFolder = "Assets/Art/LobbyIntro/CityFacades/Night/";
    const string DefenseFolder = "Assets/Resources/BossFight/Defenses/";
    static readonly string[] Facades = { "Tenement_2ndAve808_HABS", "Tenement_2ndAve873_HABS", "CastIron_PerryBlock_HABS" };

    [MenuItem("Tools/Boss Fight/Rebuild Tray Pieces As Lobby Buildings")]
    public static void Rebuild()
    {
        BossTrayArtworkAuthoring.Guard();
        var board = UnityEngine.Object.FindObjectOfType<BossDefenseBoard>();
        if (!board) throw new Exception("Open the BossFight scene: no BossDefenseBoard found.");
        for (int slot = 0; slot < 6; slot++)
        {
            var piece = board.Piece(slot);
            var so = new SerializedObject(piece);
            string facade = Facades[slot % 3];
            float radius = BossDefenseCatalog.Radius(BossDefenseCatalog.KindOf(slot));
            foreach (string field in new[] { "_Healthy", "_Broken", "_ValidPreview", "_InvalidPreview" })
            {
                var property = so.FindProperty(field);
                var old = (GameObject)property.objectReferenceValue;
                bool broken = field == "_Broken";
                var replacement = Building(piece.transform, facade, radius, broken);
                if (field.Contains("Preview"))
                {
                    var tint = AssetDatabase.LoadAssetAtPath<Material>(DefenseFolder + (field == "_ValidPreview" ? "Valid" : "Invalid") + ".mat");
                    foreach (var r in replacement.GetComponentsInChildren<Renderer>())
                        r.sharedMaterials = Enumerable.Repeat(tint, r.sharedMaterials.Length).ToArray();
                }
                replacement.SetActive(field == "_Healthy");
                property.objectReferenceValue = replacement;
                if (old) UnityEngine.Object.DestroyImmediate(old);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(piece);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Boss tray pieces rebuilt as lobby-intro facade buildings. Save the scene.");
    }

    static GameObject Building(Transform parent, string facade, float radius, bool broken)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(NightFolder + facade + ".png") ?? throw new Exception("Missing " + NightFolder + facade + ".png");
        var material = FacadeMaterial(facade, texture);
        float height = .95f;
        float width = Mathf.Min(height * texture.width / texture.height, radius * 1.9f);
        float depth = width * .8f;
        var root = new GameObject((broken ? "Collapsed " : "Miniature ") + facade);
        root.transform.SetParent(parent, false);
        if (!broken)
        {
            Slab(root.transform, "Body", new Vector3(0, height * .35f, 0), new Vector3(width * .98f, height * .7f, depth * .98f), Flat("Body", new Color(.035f, .04f, .06f)));
            Face(root.transform, material, new Vector3(0, height * .5f, -depth * .5f), 0, width, height);
            Face(root.transform, material, new Vector3(0, height * .5f, depth * .5f), 180, width, height);
            Face(root.transform, material, new Vector3(-width * .5f, height * .5f, 0), 90, depth, height);
            Face(root.transform, material, new Vector3(width * .5f, height * .5f, 0), -90, depth, height);
            return root;
        }
        // Collapsed: leaning facade shards and a rubble block, still readable as the same building.
        for (int i = 0; i < 3; i++)
        {
            var shard = Face(root.transform, material, new Vector3((i - 1) * width * .3f, height * .12f, (i - 1) * depth * .2f), i * 70, width * .45f, height * .4f);
            shard.localRotation = Quaternion.Euler(70 - i * 15, i * 70, i * 12);
        }
        Slab(root.transform, "Rubble", new Vector3(0, .06f, 0), new Vector3(width * .8f, .12f, depth * .8f), Flat("Body", new Color(.035f, .04f, .06f)));
        return root;
    }

    static Transform Face(Transform parent, Material material, Vector3 position, float yaw, float width, float height)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Facade";
        UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = position;
        quad.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        quad.transform.localScale = new Vector3(width, height, 1);
        quad.GetComponent<Renderer>().sharedMaterial = material;
        return quad.transform;
    }

    static void Slab(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = size;
        cube.GetComponent<Renderer>().sharedMaterial = material;
    }

    static Material FacadeMaterial(string facade, Texture2D texture)
    {
        string path = DefenseFolder + "TrayFacade_" + facade + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetTexture("_BaseMap", texture);
        material.SetFloat("_AlphaClip", 1);
        material.SetFloat("_Cutoff", .5f);
        material.SetFloat("_Cull", 0); // quads are seen from outside only, but keep both sides for the collapsed shards
        material.EnableKeyword("_ALPHATEST_ON");
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static Material Flat(string name, Color color)
    {
        string path = DefenseFolder + "TrayFacade_" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
#endif
