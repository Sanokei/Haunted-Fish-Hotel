using Monologue.Dialogue;
using Monologue.StoryInput;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace HauntedFish.Multiplayer
{
    // Helper owns injected dependencies; Lobby owns multiplayer and gameplay.
    public static class HotelDialogueBootstrap
    {
        static bool loadingHelper;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            loadingHelper=false;
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
        }
        static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Lobby")
            {
                // Also support opening Lobby directly in the editor.
                if (!DialogueManager.Instance && !loadingHelper && !SceneManager.GetSceneByName("Helper").isLoaded)
                {
                    if (Application.CanStreamedLevelBeLoaded("Helper"))
                    { loadingHelper=true; SceneManager.LoadSceneAsync("Helper",LoadSceneMode.Additive); }
                    else Debug.LogError("Add Assets/Scenes/Helper.unity to the build scene list. Helper owns dialogue dependencies.");
                }
                return;
            }
            if (scene.name != "Helper") return;
            loadingHelper=false;
            var manager=DialogueManager.Instance;
            var inputManager=StoryInputTextFieldManager.Instance;
            var needsDialogue=!manager || !manager._DialoguePanel;
            var needsInput=!inputManager || !inputManager.InputPanel;
            if (!needsDialogue && !needsInput) return;
            var dialoguePrefab=Resources.Load<Panel>("MultiplayerDialoguePanel");
            var inputPrefab=Resources.Load<TextFieldPanel>("MultiplayerInputPanel");
            var globals=Resources.Load<TextAsset>("MultiplayerGlobals");
            if (!dialoguePrefab || !inputPrefab || !globals)
            { Debug.LogError("Hotel dialogue resource references are missing. Run Tools/Haunted Fish Hotel/Refresh Multiplayer Assets."); return; }
            var root=new GameObject("Helper Dialogue Dependencies");
            SceneManager.MoveGameObjectToScene(root,scene);
            root.SetActive(false);
            if (needsDialogue)
            {
                var dialogue=Object.Instantiate(dialoguePrefab,root.transform);
                dialogue.gameObject.SetActive(false);
                WorldDialogueCanvas.Place(dialogue,Vector3.zero);
                if (!manager) manager=root.AddComponent<DialogueManager>();
                manager.ConfigureMissing(globals,dialogue);
            }
            if (needsInput)
            {
                var input=Object.Instantiate(inputPrefab,root.transform);
                input.gameObject.SetActive(false);
                WorldDialogueCanvas.Place(input,Vector3.zero);
                if (!inputManager) inputManager=root.AddComponent<StoryInputTextFieldManager>();
                inputManager.Configure(input);
            }
            root.SetActive(true);
            Object.DontDestroyOnLoad(root);
            if (manager.transform.parent==null) Object.DontDestroyOnLoad(manager.gameObject);
            if (inputManager.transform.parent==null) Object.DontDestroyOnLoad(inputManager.gameObject);
        }
    }
}
