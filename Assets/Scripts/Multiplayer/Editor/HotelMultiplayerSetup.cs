#if UNITY_EDITOR
using System.Linq;
using HauntedFish.Multiplayer;
using Monologue.Dialogue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace HauntedFish.Editor
{
    public static class HotelMultiplayerSetup
    {
        [MenuItem("Tools/Haunted Fish Hotel/Refresh Multiplayer Assets")]
        public static void Refresh()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets","Resources");
            CopyIfMissing("Assets/DialoguePrefabs/Dialogue/Depreceated/Dialogue Panel.prefab","Assets/Resources/MultiplayerDialoguePanel.prefab");
            CopyIfMissing("Assets/DialoguePrefabs/StoryInput/Input Panel.prefab","Assets/Resources/MultiplayerInputPanel.prefab");
            CopyIfMissing("Assets/Dialogue/importGlobals.json","Assets/Resources/MultiplayerGlobals.json");
            var path="Assets/Resources/MultiplayerDialogueCatalog.asset";
            var catalog=AssetDatabase.LoadAssetAtPath<DialogueCatalog>(path);
            if (!catalog) { catalog=ScriptableObject.CreateInstance<DialogueCatalog>(); AssetDatabase.CreateAsset(catalog,path); }
            catalog.Stories=AssetDatabase.FindAssets("t:TextAsset",new[]{"Assets/Dialogue"})
                .Select(guid=>AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset=>asset && asset.text.Contains("\"inkVersion\"")).ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }
        static void CopyIfMissing(string source,string target)
        { if (!AssetDatabase.LoadAssetAtPath<Object>(target)) AssetDatabase.CopyAsset(source,target); }
        [MenuItem("Tools/Haunted Fish Hotel/Configure World Dialogue")]
        public static void ConfigureWorldDialogue()
        {
            foreach (var manager in Object.FindObjectsByType<DialogueManager>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if (!manager._DialoguePanel) continue;
                Undo.RegisterFullObjectHierarchyUndo(manager._DialoguePanel.gameObject,"Configure World Dialogue");
                WorldDialogueCanvas.Place(manager._DialoguePanel,manager.transform.position);
                EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            }
            foreach (var input in Object.FindObjectsByType<Monologue.StoryInput.TextFieldPanel>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                Undo.RegisterFullObjectHierarchyUndo(input.gameObject,"Configure World Input");
                WorldDialogueCanvas.Place(input,input.transform.position);
                EditorSceneManager.MarkSceneDirty(input.gameObject.scene);
            }
        }
    }
}
#endif

