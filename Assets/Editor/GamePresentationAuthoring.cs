#if UNITY_EDITOR
using HauntedFish.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
public static class GamePresentationAuthoring
{
    // Explicit native authoring only. No InitializeOnLoad or runtime fallback.
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);
        var controller = Object.FindFirstObjectByType<GhostPlacementController>(FindObjectsInactive.Include);
        if (!controller) throw new System.InvalidOperationException("Game ghost controls missing");
        var serialized = new SerializedObject(controller);
        var hud = serialized.FindProperty("_Hud").objectReferenceValue as GameObject;
        var source = serialized.FindProperty("_PreviewSprite").objectReferenceValue as SpriteRenderer;
        if (!hud || !source) throw new System.InvalidOperationException("Authored HUD/preview source missing");
        if (!serialized.FindProperty("_ForbiddenArea").objectReferenceValue)
        {
            var node = new GameObject("Authored starting hallway forbidden overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            node.transform.SetParent(hud.transform, false); node.transform.SetAsFirstSibling();
            var image = node.GetComponent<Image>(); image.color = new Color(1,.06f,.08f,.32f); image.raycastTarget = false;
            var outline = node.GetComponent<Outline>(); outline.effectColor = new Color(1,.04f,.06f,.9f); outline.effectDistance = new Vector2(3,-3);
            node.SetActive(false); serialized.FindProperty("_ForbiddenArea").objectReferenceValue = image;
        }
        if (!serialized.FindProperty("_PlacementPreview").objectReferenceValue)
        {
            var node = new GameObject("Authored snapped trap destination"); node.transform.SetParent(controller.transform, false);
            var artwork = new GameObject("Authored destination sprite", typeof(SpriteRenderer)); artwork.transform.SetParent(node.transform, false);
            var renderer = artwork.GetComponent<SpriteRenderer>(); renderer.sharedMaterial = source.sharedMaterial;
            renderer.sortingLayerID = source.sortingLayerID; renderer.sortingOrder = source.sortingOrder;
            node.SetActive(false); serialized.FindProperty("_PlacementPreview").objectReferenceValue = node;
            serialized.FindProperty("_PlacementPreviewSprite").objectReferenceValue = renderer;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("Authored Game overlay and snapped preview saved; no runtime assembly needed.");
    }
}
#endif
