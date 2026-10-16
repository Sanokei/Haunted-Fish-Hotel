using HauntedFish.QA;
using Monologue.Dialogue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HauntedFish.EditorTools
{
    public static class AtticIntroQAScene
    {
        const string ScenePath = "Assets/Scenes/QA/AtticIntroQA.unity";

        [MenuItem("Haunted Fish/QA/Build Attic Intro QA Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("QA camera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.magenta; // anything uncovered by the cutscene shows as a hole
            var runner = new GameObject("Attic intro QA runner").AddComponent<AtticIntroQARunner>();
            var so = new SerializedObject(runner);
            so.FindProperty("_Prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StoryUI>("Assets/Resources/AtticRoundCutscene.prefab");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        [MenuItem("Haunted Fish/QA/Play Attic Intro QA")]
        public static void Play()
        {
            if (!System.IO.File.Exists(ScenePath)) Build();
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        }
    }
}
