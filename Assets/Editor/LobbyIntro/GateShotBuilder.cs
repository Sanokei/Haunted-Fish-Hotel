using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HauntedFish.EditorTools
{
    // Exterior jib: pulls the gate layer back so the whole gate is in frame, extends the stone
    // wall past both pillars with repeated wall sections cut from the gate art, adds trees along
    // the wall, and moves the crane so it ends high and wide instead of low and pushed in.
    //   Unity -batchmode -executeMethod HauntedFish.EditorTools.GateShotBuilder.Build
    public static class GateShotBuilder
    {
        const string Prefab = "Assets/Resources/LobbyIntroductionUI.prefab";
        const string Rig = "Ink Lobby Introduction/Storyboard viewport/Exterior parallax/";
        const float GateScale = .68f, SegmentWidth = 440 * 1420f / 4096f;
        const int SegmentsPerSide = 5;

        [MenuItem("Haunted Fish/Lobby Intro/Build Gate Shot")]
        public static void Build()
        {
            foreach (var side in new[] { "Left", "Right" })
            {
                var path = "Assets/Art/LobbyIntro/EXT-GateWall-" + side + ".png";
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
                if (!AssetDatabase.LoadAssetAtPath<Sprite>(path)) throw new System.InvalidOperationException("Wall sprite did not import: " + path);
            }
            var root = PrefabUtility.LoadPrefabContents(Prefab);
            Transform Find(string path) => root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => AnimationUtility.CalculateTransformPath(t, root.transform) == path);

            var gateLayer = (RectTransform)Find(Rig + "Foreground gate");
            gateLayer.localScale = Vector3.one * GateScale;
            var gate = (RectTransform)Find(Rig + "Foreground gate/gate");
            var gateImage = gate.GetComponent<Image>();
            foreach (Transform child in gate.parent.Cast<Transform>().ToArray())
                if (child.name.StartsWith("Wall extension")) Object.DestroyImmediate(child.gameObject);
            for (int side = -1; side <= 1; side += 2)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/LobbyIntro/EXT-GateWall-" + (side < 0 ? "Left" : "Right") + ".png");
                for (int i = 0; i < SegmentsPerSide; i++)
                {
                    var segment = new GameObject("Wall extension " + (side < 0 ? "L" : "R") + (i + 1), typeof(RectTransform), typeof(Image));
                    var rect = (RectTransform)segment.transform;
                    rect.SetParent(gate.parent, false);
                    rect.SetSiblingIndex(gate.GetSiblingIndex());
                    rect.anchorMin = rect.anchorMax = gate.anchorMin;
                    rect.sizeDelta = new Vector2(SegmentWidth, gate.sizeDelta.y);
                    // Butt each section against the last; a pixel of overlap hides seams.
                    rect.anchoredPosition = gate.anchoredPosition + new Vector2(side * (gate.sizeDelta.x * .5f + SegmentWidth * (i + .5f) - 2 * (i + 1)), 0);
                    var image = segment.GetComponent<Image>();
                    image.sprite = sprite;
                    image.color = gateImage.color;
                    image.raycastTarget = false;
                }
            }

            // More trees along the wall: copies of the authored foliage, further out and slightly smaller.
            var foliageLayer = Find(Rig + "Middle foliage");
            foreach (Transform child in foliageLayer.Cast<Transform>().ToArray())
                if (child.name.StartsWith("Wall trees")) Object.DestroyImmediate(child.gameObject);
            var source = foliageLayer.Cast<Transform>().First(t => t.name == "foliage");
            var placements = new[] { (-980f, -40f, .82f), (1000f, -50f, .8f), (-1350f, -70f, .7f), (1380f, -60f, .72f), (-760f, -90f, .6f), (780f, -95f, .62f) };
            foreach (var (x, y, scale) in placements)
            {
                var tree = Object.Instantiate(source.gameObject, foliageLayer, false);
                tree.name = "Wall trees " + (x < 0 ? "L" : "R");
                var rect = (RectTransform)tree.transform;
                rect.anchoredPosition = new Vector2(x, y);
                rect.sizeDelta = ((RectTransform)source).sizeDelta * scale;
                tree.transform.SetAsFirstSibling();
                tree.SetActive(source.gameObject.activeSelf);
                foreach (var label in tree.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Label")) Object.DestroyImmediate(label.gameObject);
            }

            // Crane: start high and wide, finish still above the gate with only a gentle push.
            var ui = root.GetComponentInChildren<Monologue.Dialogue.StoryUI>(true);
            var serialized = new SerializedObject(ui);
            var rigs = serialized.FindProperty("_ParallaxRigs");
            for (int r = 0; r < rigs.arraySize; r++)
            {
                var rig = rigs.GetArrayElementAtIndex(r);
                if (rig.FindPropertyRelative("Tag").stringValue != "exterior") continue;
                var poses = rig.FindPropertyRelative("Poses");
                for (int p = 0; p < poses.arraySize; p++)
                {
                    var pose = poses.GetArrayElementAtIndex(p);
                    var tag = pose.FindPropertyRelative("Tag").stringValue;
                    if (tag == "wide") { pose.FindPropertyRelative("Camera").vector2Value = new Vector2(0, 170); pose.FindPropertyRelative("Push").floatValue = 0; }
                    if (tag == "gate_arrival") { pose.FindPropertyRelative("Camera").vector2Value = new Vector2(0, 95); pose.FindPropertyRelative("Push").floatValue = .03f; }
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("[BatchQA] gate shot built");
        }
    }
}
