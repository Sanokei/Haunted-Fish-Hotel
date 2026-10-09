using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using HauntedFish.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class AuditLifecycleRunner : MonoBehaviour
{
    sealed class Environment : IGameMovementEnvironment
    {
        public Vector3 ClampMouse(Vector3 point) => point;
    }

    static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    static void Check(bool value, string text)
    {
        if (!value)
            throw new Exception(text);
        Debug.Log("AUDIT_CHECK: " + text);
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Monitor()
    {
        if (System.Environment.GetCommandLineArgs().Contains("--audit-lifecycle"))
        {
            var obj = new GameObject("Audit lifecycle observer");
            DontDestroyOnLoad(obj);
            obj.AddComponent<AuditLifecycleRunner>();
        }
    }

    IEnumerator Start()
    {
        var test = Tests();
        while (true)
        {
            object next;
            try
            {
                if (!test.MoveNext())
                    break;
                next = test.Current;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../audit-lifecycle-result.txt"), "FAIL " + error);
                EditorApplication.Exit(1);
                yield break;
            }

            yield return next;
        }

        File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../audit-lifecycle-result.txt"), "PASS actual preview unload/reload and real CharacterController cart collision/reset checks");
        Debug.Log("AUDIT_LIFECYCLE_PASS");
        EditorApplication.Exit(0);
    }

    IEnumerator Tests()
    {
        yield return null;
        yield return null;
        var preview = FindObjectsByType<HotelPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(p => !p.Networked);
        Check(preview.gameObject.scene.name == "Game", "Authored offline preview stays scene-local instead of DontDestroyOnLoad");
        var commandsField = typeof(HotelPlayer).GetField("_GameCommands", BindingFlags.NonPublic | BindingFlags.Instance);
        var bossField = typeof(HotelPlayer).GetField("_BossCommands", BindingFlags.NonPublic | BindingFlags.Instance);
        var sceneOwner = GameSceneController.Current;
        var bossOwner = BossArenaCoordinator.Current;
        Check(commandsField.GetValue(preview) != null, "Scene entry binds gameplay commands to the existing preview");
        Check(ReferenceEquals(bossField.GetValue(preview), bossOwner), "Boss owner binds commands to existing players");
        var originalCommands = (IHotelGameCommands)commandsField.GetValue(preview);
        sceneOwner.Exit();
        Check(commandsField.GetValue(preview) == null, "Scene exit releases commands on a surviving player");
        sceneOwner.Enter(null);
        var replacementCommands = commandsField.GetValue(preview);
        preview.UnbindGameCommands(originalCommands);
        Check(replacementCommands != null && ReferenceEquals(commandsField.GetValue(preview), replacementCommands), "Old scene binding cannot clear its replacement");
        bossOwner.enabled = false;
        Check(bossField.GetValue(preview) == null, "Disabled boss owner releases player commands");
        bossOwner.enabled = true;
        Check(ReferenceEquals(bossField.GetValue(preview), bossOwner), "Re-enabled boss owner rebinds surviving players");
        preview.gameObject.SetActive(false);
        Check(commandsField.GetValue(preview) == null && bossField.GetValue(preview) == null, "Disabled player retains no scene command owners");
        preview.gameObject.SetActive(true);
        Check(commandsField.GetValue(preview) != null && ReferenceEquals(bossField.GetValue(preview), bossOwner), "Re-enabled player receives both command owners");
        var actions = (InputActionAsset)typeof(HotelPlayerMovement).GetField("_OwnedActions", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(preview.Movement);
        var gameScene = preview.gameObject.scene;
        var empty = SceneManager.CreateScene("Audit empty physics");
        SceneManager.SetActiveScene(empty);
        yield return SceneManager.UnloadSceneAsync(gameScene);
        yield return null;
        Check(!preview && !actions, "Scene unload destroys offline preview and its owned cloned input maps");
        Check(FindObjectsByType<HotelPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0 && HotelPlayer.ActivePlayers.Count == 0, "Travel leaves no inactive persistent preview or stale registry entry");
        yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Additive);
        yield return null;
        yield return null;
        var previews = FindObjectsByType<HotelPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Check(previews.Count(p => !p.Networked) == 1, "Repeated Game load creates exactly one scene-local preview");
        yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName("Game"));
        yield return null;
        Check(HotelPlayer.ActivePlayers.Count == 0, "Repeated Game unload releases registry membership again");
        float priorScale = Time.timeScale;
        Time.timeScale = 0;
        var floor = new GameObject("Audit floor");
        floor.transform.position = new Vector3(0, 9.5f, 0);
        floor.AddComponent<BoxCollider>().size = new Vector3(30, 1, 5);
        var fishObject = new GameObject("Audit real fish controller");
        fishObject.SetActive(false);
        var controller = fishObject.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = .35f;
        controller.center = new Vector3(0, .9f, 0);
        controller.skinWidth = .04f;
        var movement = fishObject.AddComponent<HotelPlayerMovement>();
        movement.InputActions = Resources.Load<InputActionAsset>("HotelMultiplayerActions");
        var fish = fishObject.AddComponent<HotelPlayer>();
        fish.Networked = false;
        fish.RoundReleased = true;
        Set(fish, "_EditorRole", 0);
        fishObject.transform.position = new Vector3(1.6f, 10, 0);
        fishObject.SetActive(true);
        fishObject.GetComponent<GameSideScrollMotor>().Bind(new Environment());
        yield return null;
        yield return null;
        yield return null;
        Check(movement.SimulationReady && fish.ControlsReady && fish.ControlMode == HotelControlMode.Fish, "Physics fixture uses actual ready authority fish and CharacterController");
        var cart = Instantiate(Resources.Load<GhostTrap>("Traps/ShoppingCartTrap"));
        cart.Initialize(null, new GhostCubePlacement { Id = 1, FamilyTag = cart.FamilyTag, Position = new Vector3(0, 11, 0), Origin = new Vector3(0, 11, 0) }, 1);
        float cartFront = cart.Position.x + cart.BodyHalfSize.x;
        fishObject.transform.position = new Vector3(cartFront + controller.radius + .05f, 10, 0);
        Physics.SyncTransforms();
        var wall = new GameObject("Audit pinning wall");
        wall.transform.position = new Vector3(fishObject.transform.position.x + controller.radius + .08f, 11, 0);
        wall.AddComponent<BoxCollider>().size = new Vector3(.1f, 2, 2);
        Physics.SyncTransforms();
        cart.AcceptInput(new TrapInput(TrapInputKind.Move, 1), Time.unscaledTime);
        cart.Simulate(.5f, Time.unscaledTime);
        Check(cart.Position.x + cart.BodyHalfSize.x <= controller.bounds.min.x + .08f, "Large cart step cannot penetrate a fish pinned against a wall");
        wall.SetActive(false);
        Destroy(wall);
        yield return null;
        var fish2Object = new GameObject("Audit second real fish");
        fish2Object.SetActive(false);
        var controller2 = fish2Object.AddComponent<CharacterController>();
        controller2.height = 1.8f;
        controller2.radius = .35f;
        controller2.center = new Vector3(0, .9f, 0);
        controller2.skinWidth = .04f;
        var movement2 = fish2Object.AddComponent<HotelPlayerMovement>();
        movement2.InputActions = Resources.Load<InputActionAsset>("HotelMultiplayerActions");
        var fish2 = fish2Object.AddComponent<HotelPlayer>();
        fish2.Networked = false;
        fish2.RoundReleased = true;
        Set(fish2, "_EditorRole", 0);
        fish2Object.transform.position = fishObject.transform.position + Vector3.right * .85f;
        fish2Object.SetActive(true);
        fish2Object.GetComponent<GameSideScrollMotor>().Bind(new Environment());
        yield return null;
        yield return null;
        yield return null;
        Physics.SyncTransforms();
        float before = cart.Position.x;
        cart.AcceptInput(new TrapInput(TrapInputKind.Move, 1), Time.unscaledTime);
        cart.Simulate(.5f, Time.unscaledTime);
        Check(cart.Position.x > before && cart.Position.x + cart.BodyHalfSize.x <= controller.bounds.min.x + .08f, "Swept large cart move pushes a real two-fish chain without penetrating the trailing body");
        Check(controller.bounds.max.x <= controller2.bounds.min.x + .12f, "Leading-first fish push preserves separation between controllers");
        var resetState = cart.CaptureState();
        resetState.Origin = new Vector3(fishObject.transform.position.x, 11, 0);
        cart.ApplyState(resetState);
        Vector3 beforeReset = cart.Position;
        Physics.SyncTransforms();
        Debug.Log("AUDIT_RESET origin=" + resetState.Origin + " fish=" + controller.bounds + " half=" + cart.HalfExtents + " armed=" + cart.ArmedOffset);
        foreach (var overlap in Physics.OverlapBox(resetState.Origin + cart.ArmedOffset, cart.HalfExtents, cart.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
            Debug.Log("AUDIT_RESET_OVERLAP " + overlap.name);
        Check(!cart.AcceptInput(new TrapInput(TrapInputKind.Reset), Time.unscaledTime) && cart.Position == beforeReset, "Reset rejects an occupied origin without teleporting into a fish");
        fishObject.SetActive(false);
        fish2Object.SetActive(false);
        var occupiedOrigin=new GameObject("Audit occupied trap reset origin");occupiedOrigin.transform.position=resetState.Origin;occupiedOrigin.AddComponent<BoxCollider>().size=Vector3.one;
        Physics.SyncTransforms();
        Check(!cart.AcceptInput(new TrapInput(TrapInputKind.Reset),Time.unscaledTime),"Reset rejects an origin occupied by another physical object");
        Destroy(occupiedOrigin);
        Check(!HotelPlayer.ActivePlayers.Contains(fish) && !HotelPlayer.ActivePlayers.Contains(fish2), "Actor registry removes disabled bodies synchronously");
        fishObject.SetActive(true);
        Check(HotelPlayer.ActivePlayers.Contains(fish), "Re-enabled actor registers exactly once");
        fishObject.SetActive(false);
        Destroy(fishObject);
        Destroy(fish2Object);
        Destroy(cart.gameObject);
        Destroy(floor);
        Time.timeScale = priorScale;
        yield return null;
        Check(HotelPlayer.ActivePlayers.Count == 0, "Physics teardown leaves no stale actor or owned input lifecycle");
    }
}
