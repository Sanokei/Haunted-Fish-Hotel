using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HauntedFish.Multiplayer;
using Mirage;
using Monologue.Dialogue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public sealed class GameplayUpdateRunner : MonoBehaviour
{
    Keyboard _Keyboard;
    Mouse _Mouse;
    string _Folder;
    int _Checks;
    static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    static void Set(object owner, string name, object value) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    static void Call(object owner, string name) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
    void Check(bool value, string text) { if (!value) throw new Exception(text); _Checks++; Debug.Log("GAMEPLAY_CHECK: " + text); }
    public static void Run() { EditorSceneManager.OpenScene("Assets/Scenes/Helper.unity"); EditorApplication.EnterPlaymode(); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Monitor()
    {
        if (Environment.GetCommandLineArgs().Contains("--gameplay-update"))
        { var obj = new GameObject("Isolated gameplay update observer"); DontDestroyOnLoad(obj); obj.AddComponent<GameplayUpdateRunner>(); }
    }
    IEnumerator Start()
    {
        _Folder = System.IO.Path.Combine(Application.dataPath, "../GameplayUpdateEvidence"); Directory.CreateDirectory(_Folder);
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        _Keyboard = InputSystem.AddDevice<Keyboard>(); _Mouse = InputSystem.AddDevice<Mouse>();
        var test = Tests();
        while (true)
        {
            object next;
            try { if (!test.MoveNext()) break; next = test.Current; }
            catch (Exception error) { Debug.LogException(error); File.WriteAllText(System.IO.Path.Combine(_Folder, "result.txt"), "FAIL " + error); EditorApplication.Exit(1); yield break; }
            yield return next;
        }
        File.WriteAllText(System.IO.Path.Combine(_Folder, "result.txt"), "PASS " + _Checks + " real gameplay and Lobby checks");
        Debug.Log("GAMEPLAY_UPDATE_PASS " + _Checks); EditorApplication.Exit(0);
    }
    void Keys(params Key[] keys) { InputSystem.QueueStateEvent(_Keyboard, new KeyboardState(keys)); InputSystem.Update(); }
    IEnumerator Tests()
    {
        float timeout = Time.unscaledTime + 60;
        HauntedHotelMultiplayer session = null; LobbyManager lobby = null;
        while (Time.unscaledTime < timeout)
        {
            session = FindFirstObjectByType<HauntedHotelMultiplayer>(); lobby = FindFirstObjectByType<LobbyManager>();
            if (session && session.ReadyToPlay && lobby && HotelPlayer.LocalPlayer) break;
            yield return null;
        }
        Check(session && session.ReadyToPlay && lobby, "Real Helper establishes native solo host in Lobby");
        if (lobby.IntroductionPlaying) lobby.SkipIntroduction();
        var local = HotelPlayer.LocalPlayer; var movement = local.Movement;
        yield return null; yield return null;
        Check(movement.ActiveInputMap == "Lobby" && movement.LobbyWalkingSpeed == 6 && movement.RunningSpeed == 4.5f && movement.WalkingSpeed == 1.8f, "Authored Lobby speed 6 is separate from Game walk 1.8/run 4.5");
        Keys(Key.LeftShift); Check(!movement.RunHeld && !movement.Running, "Shift cannot enable Lobby sprint"); Keys();
        var cursor = lobby.GetComponent<LobbyCursorManager>();
        var origin = Get<Transform>(cursor, "_InteractionOrigin"); var facing = Get<Transform>(cursor, "_Facing");
        var front = cursor.FrontDirection;
        movement.Teleport(origin.position + front * 1.5f); Physics.SyncTransforms(); yield return null; yield return null;
        Check(cursor.InFront(local), "Authored Grandmafish interaction accepts owned local fish in front");
        movement.Teleport(origin.position - front * 1.5f); Check(!cursor.InFront(local), "Grandmafish inspection rejects rear approach");
        movement.Teleport(origin.position + front * 5); Check(!cursor.InFront(local), "Grandmafish inspection rejects distant approach");
        movement.Teleport(origin.position + front * 1.5f + Vector3.up * 4); Check(!cursor.InFront(local), "Grandmafish inspection rejects another floor");
        movement.Teleport(origin.position + front * 1.5f); Physics.SyncTransforms(); yield return null; yield return null;
        var target = Get<Collider>(cursor, "_Target");
        var targetRay = new Ray(target.bounds.center + front * .8f, -front);
        Debug.Log("CURSOR_TARGET bounds="+target.bounds+" direction="+targetRay.direction+" direct="+target.Raycast(targetRay,out var direct,100)+" distance="+direct.distance);
        foreach(var hit in Physics.RaycastAll(targetRay,1,~0,QueryTriggerInteraction.Ignore)) Debug.Log("CURSOR_BLOCKER "+hit.collider.name+" distance="+hit.distance);
        Check(cursor.HitTarget(new Ray(target.bounds.center + front * .8f, -front), local), "Actual authored Grandmafish collider supports unblocked target ray");
        var camera = HotelViewCamera.Current;
        var screen = camera.WorldToScreenPoint(target.bounds.center);
        InputSystem.QueueStateEvent(_Mouse, new MouseState { position = new Vector2(screen.x, screen.y) }); InputSystem.Update(); Call(cursor, "Update");
        Check(cursor.Hovering, "Actual camera/mouse ray shows inspect cursor on Grandmafish");
        InputSystem.QueueStateEvent(_Mouse, new MouseState { position = new Vector2(screen.x, screen.y), buttons = 1 }); InputSystem.Update(); Call(cursor, "Update");
        InputSystem.QueueStateEvent(_Mouse, new MouseState { position = new Vector2(screen.x, screen.y) }); InputSystem.Update(); Call(cursor, "Update");
        Check(DialogueManager.Instance && DialogueManager.Instance.ActiveDialoguePanel && DialogueManager.Instance.CurrentAsset == Get<EnterDialogueMode>(cursor, "_Dialogue").InkAsset && !DialogueManager.Instance.IsSharedDialogue, "Mouse press/release enters authored item Ink for relevant local player");
        DialogueManager.Instance.ExitDialogMode();
        cursor.enabled = false; Check(!cursor.Hovering, "Cursor disable clears hover and restores cursor"); cursor.enabled = true;
        var stairs = Get<LobbyTrigger>(lobby, "_StairZone").GetComponent<Collider>();
        local.Teleport(stairs.bounds.center - local.BodyController.center); Physics.SyncTransforms(); yield return null; yield return null;
        var menu = FindFirstObjectByType<MemberMenu>(FindObjectsInactive.Include); var start = Get<Button>(menu, "_Start");
        float readyTimeout = Time.unscaledTime + 5;
        while (!start.interactable && session.SceneTravel.TargetScene == "Lobby" && Time.unscaledTime < readyTimeout) yield return null;
        Check(start.interactable || session.SceneTravel.TargetScene == "Game", "Existing ready-zone start/travel remains usable after cursor interaction"); if (session.SceneTravel.TargetScene == "Lobby") start.onClick.Invoke();
        timeout = Time.unscaledTime + 60;
        while (!GameSceneController.Current && Time.unscaledTime < timeout) yield return null;
        var scene = GameSceneController.Current; Check(scene, "Native Lobby travel enters Game without replacing network classes");
        local = HotelPlayer.LocalPlayer; movement = local.Movement;
        while (!local.RoundIntroComplete && Time.unscaledTime < timeout) yield return null;
        Check(local.RoundIntroComplete && !local.RoundReleased && local.GhostSetupRemaining > 8.5f, "Mandatory wheel/attic finishes into the ten-second setup phase");
        Check(local.ControlMode == HotelControlMode.Ghost && scene.GhostFlightSpeed > movement.RunningSpeed, "Ghost acts during setup and shared editable flight speed exceeds fish run");
        float deadline = Get<float>(scene, "_SetupDeadline"); int version = local.RoundVersion; string key = local.RoundStateKey;
        var objects = FindFirstObjectByType<ServerObjectManager>();
        var remote = Instantiate(Resources.Load<HotelPlayer>("HotelNetworkPlayer")); objects.Spawn(remote.Identity); remote.Configure(Get<HotelSessionContext>(local, "_Session"));
        yield return null; yield return null;
        Check(remote.IsServer && !remote.IsRelevantPlayer && remote.RoundStateKey == key && remote.RoundVersion == version && Get<float>(scene, "_SetupDeadline") == deadline, "Real Mirage late participant inherits existing round without resetting setup deadline");
        Check(!remote.RoundReleased && remote.ControlMode == HotelControlMode.Selection && !remote.Movement.SimulationReady, "Late fish remains blocked until its own presentation acknowledgement");
        remote.RoundIntroComplete = true;
        Call(remote, "ConfigureMovement");
        Check(remote.ControlMode == HotelControlMode.Fish && !remote.Movement.ControlsReady, "Fish body/input stays blocked throughout ghost setup");
        remote.RoundIntroComplete = false;
        var manager = scene.Traps;
        Check(manager.Running && manager.PackageCount > 0, "Conveyor supplies already run during setup");
        var conveyor = JsonUtility.FromJson<ConveyorSnapshot>(manager.Snapshot);
        Check(Mathf.Abs(conveyor.PathStart.x - scene.HallwayBounds.min.x) < .001f && Mathf.Abs(conveyor.PathEnd.x - scene.HallwayBounds.max.x) < .001f, "Conveyor spans both ends of authored hallway bounds");
        while (!local.RoundReleased && Time.unscaledTime < timeout) yield return null;
        Check(local.RoundReleased && local.RoundVersion == version && local.RoundStateKey == key && Time.unscaledTime >= deadline, "Setup releases the original round once at its original deadline");
        scene.Acknowledge(remote, version); yield return null; yield return null; yield return null;
        Check(remote.RoundReleased && remote.RoundIntroComplete, "Late participant acknowledges into running round without a second countdown");
        scene.Acknowledge(remote, version); Check(Get<float>(scene, "_SetupDeadline") == deadline, "Duplicate intro completion cannot restart setup");
        local.SwitchEditorRole(); yield return null; yield return null; yield return null;
        Check(local.ControlMode == HotelControlMode.Fish && movement.ActiveInputMap == "Game", "Existing local role switch enables Game input for native speed test");
        movement.Teleport(new Vector3(-8, 1, 0)); yield return null; yield return null; yield return null;
        float before = local.transform.position.x; Keys(Key.D); yield return new WaitForSecondsRealtime(.25f); float walked = local.transform.position.x - before; Keys(); yield return null;
        before = local.transform.position.x; Keys(Key.D, Key.LeftShift); yield return new WaitForSecondsRealtime(.25f); float ran = local.transform.position.x - before;
        Check(movement.RunHeld && movement.Running && ran > walked * 1.5f && walked > .1f, "Native held Shift drives faster authoritative Game motion and running animation state");
        Keys(); yield return new WaitForSecondsRealtime(.15f); Check(!movement.RunHeld && !movement.Running, "Releasing Shift clears Game run state");
        float x = local.transform.position.x; Keys(Key.W, Key.S); yield return new WaitForSecondsRealtime(.15f); Keys();
        Check(Mathf.Abs(local.transform.position.x - x) < .01f && Mathf.Abs(local.transform.position.z) < .001f, "Game W/S produces no horizontal or depth motion");
        movement.Teleport(new Vector3(100, 100, 100)); var bounds = local.BodyController.bounds;
        Check(bounds.max.x <= scene.HallwayBounds.max.x + .01f && bounds.min.y >= scene.HallwayBounds.max.y - .01f && bounds.max.y <= Get<BoxCollider>(scene, "_HallwayCeiling").bounds.min.y + .01f && Mathf.Abs(local.transform.position.z) < .001f, "Teleport constrains full fish collision extents to hallway right/ceiling/floor and plane");
        movement.Teleport(new Vector3(-100, -100, -100)); bounds = local.BodyController.bounds;
        Check(bounds.min.x >= scene.HallwayBounds.min.x - .01f && bounds.min.y >= scene.HallwayBounds.max.y - .01f, "Teleport constrains left and floor body extents");
        local.ResetEditorRole(); yield return null; yield return null;
        remote.Movement.Teleport(new Vector3(8, 1, 0)); yield return null; yield return null;
        var world = scene.PlacementWorld; var controls = world.Controls;
        Set(controls, "_Position", new Vector3(0, 1, 0));
        Check(world.Hold(local, "cart", new Vector3(0, 1, 0)) && world.TryPlace(local, new Vector3(0, 1, 0), "cart"), "Real ghost author places cart for disposal and swap fixtures");
        CaptureWorld("01-cart-grounded-current-camera");
        var record = JsonUtility.FromJson<GhostCubeSnapshot>(world.Snapshot).Cubes[0]; var trap = world.Trap(record.Id);
        world.Possess(local, trap.Index, trap.Position); yield return null; yield return null;
        var view = HotelViewCamera.Current;
        var previousRotation = view.transform.rotation; var previousSize = view.orthographicSize;
        view.transform.rotation = Quaternion.Euler(0, 0, 8); view.orthographicSize = previousSize * 1.15f;
        CaptureWorld("02-cart-grounded-camera-roll-and-zoom");
        view.transform.rotation = previousRotation; view.orthographicSize = previousSize;
        var presentation = trap.GetComponentInChildren<GhostTrapAreaPresentation>(true);
        Call(presentation, "LateUpdate");
        var cartRect = Get<RectTransform>(presentation, "_Cart");
        var projectedFoot = view.WorldToScreenPoint(trap.Position - Vector3.up * trap.BodyHalfSize.y);
        Check(Vector2.Distance(cartRect.position, projectedFoot) < .01f, "Cart artwork's measured opaque foot anchors to actual projected collider floor");
        int puffVersion = local.PossessionEffectVersion; var puffPoint = trap.Position;
        Check(world.Dispose(local, trap.Index) && local.PossessionEffectVersion == puffVersion + 1 && local.PossessionEffectPosition == puffPoint && !world.Trap(trap.Index), "Possessed Q disposal emits one shared puff event at removed object's exact position");
        yield return null; CaptureWorld("03-disposal-poof-at-cart"); Check(FindObjectsByType<PossessionSmoke>(FindObjectsSortMode.None).Length > 0, "Existing Ink smoke materializes after synchronized disposal event"); yield return new WaitForSecondsRealtime(.7f);
        Check(FindObjectsByType<PossessionSmoke>(FindObjectsSortMode.None).Length == 0, "Disposal Ink clears runtime effect completely");
        Set(controls, "_Position", new Vector3(0, 1, 0)); world.Hold(local, "cart", new Vector3(0, 1, 0)); Check(world.TryPlace(local, new Vector3(0, 1, 0), "cart"), "Cart fixture survives repeated placement after disposal");
        trap = world.Trap(JsonUtility.FromJson<GhostCubeSnapshot>(world.Snapshot).Cubes[0].Id);
        remote.Movement.Teleport(new Vector3(1.85f, 1, 0)); yield return null; yield return null; yield return null;
        Set(controls, "_Position", trap.Position); world.Possess(local, trap.Index, trap.Position);
        Check(world.AcceptTrapAction(local, trap.Index, new TrapInput(TrapInputKind.Move, 1)), "Actual possessed cart accepts authoritative motion toward fish");
        Keys(Key.D); yield return new WaitForSecondsRealtime(.2f); Keys();
        Debug.Log("SPOOK_CONTACT trap="+trap.CollisionBounds+" fish="+remote.BodyController.bounds+" amount="+remote.Spook);
        world.AcceptTrapInput(local, trap.Index, 0); world.Possess(local, -1, trap.Position);
        Check(remote.Spook > 0, "Actual moving cart contact accumulates shared fish spook through production physics flow");
        remote.Movement.Teleport(new Vector3(8, 1, 0)); yield return null; yield return null; yield return null;
        uint localId = local.NetId, remoteId = remote.NetId; var bodyPoint = remote.transform.position;
        Check(scene.Haunting.AddSpook(remote, scene.Haunting.SpookThreshold - 1 - remote.Spook) && remote.Spook > 0 && remote.ControlMode == HotelControlMode.Fish, "Subthreshold authoritative spook retains actual fish role");
        Check(scene.Haunting.AddSpook(remote, 1), "Threshold spook triggers authority handoff"); yield return null; yield return null;
        Check(local.ControlMode == HotelControlMode.Fish && remote.ControlMode == HotelControlMode.Ghost && local.GhostId == remoteId && remote.GhostId == remoteId && local.NetId == localId && remote.NetId == remoteId, "Actual former ghost becomes fish and spooked fish becomes ghost while network identities remain owned by same peers");
        Check(Vector3.Distance(local.transform.position, bodyPoint) < .2f && local.BodyController.enabled && !remote.BodyController.enabled && remote.GhostFlightReady, "Former ghost occupies fish body location and authoritative collision/input role switches");
        Check(local.ControlledCube < 0 && remote.ControlledCube < 0 && local.HeldTrapFamily == "" && remote.HeldTrapFamily == "" && trap.CaptureState().OwnerId == remoteId, "Role swap safely releases controlled/held inventory and transfers existing traps");
        int effectVersion = remote.GhostEmergenceVersion; Check(effectVersion == 1 && !scene.Haunting.AddSpook(remote, 100) && remote.GhostEmergenceVersion == effectVersion, "Role/authority guards prevent duplicate ghost emergence and repeated swap");
        CaptureWorld("04-actual-player-ghost-emergence");
        Check(FindObjectsByType<PossessionSmoke>(FindObjectsSortMode.None).Any(effect => effect.name.StartsWith("GhostEmergence")), "Reusable actual player_ghost prefab emerges through the existing Ink animation integration");
        yield return new WaitForSecondsRealtime(.9f); Check(!FindObjectsByType<PossessionSmoke>(FindObjectsSortMode.None).Any(effect => effect.name.StartsWith("GhostEmergence")), "Emergence Ink disposes its instance after rise/clear");
        scene.PlacementWorld.ResetRound(); objects.Destroy(remote.Identity, false); Destroy(remote.gameObject); yield return null;
        Check(!HotelPlayer.ActivePlayers.Contains(remote), "Real Mirage unspawn removes temporary peer after role swap");
        Keys(Key.LeftShift);
        var travel = FindFirstObjectByType<HauntedHotelMessageTravel>();
        Check(travel.CanTravel, "Existing message-travel authority remains ready after role handoff"); travel.ReturnToLobby();
        timeout = Time.unscaledTime + 30;
        while ((!FindFirstObjectByType<LobbyManager>() || !session.ReadyToPlay) && Time.unscaledTime < timeout) yield return null;
        var returnedLobby = FindFirstObjectByType<LobbyManager>();
        if (returnedLobby && returnedLobby.IntroductionPlaying) returnedLobby.SkipIntroduction();
        local = HotelPlayer.LocalPlayer; yield return null; yield return null; yield return null;
        Check(local && local.ControlMode == HotelControlMode.Lobby && local.Movement.ActiveInputMap == "Lobby" && !local.Movement.RunHeld && !local.Movement.Running && local.Movement.LobbyWalkingSpeed == 6, "Actual Game-to-Lobby message travel restores faster Lobby walk with no leaked held-Shift sprint");
        Keys();
    }
 void CaptureWorld(string name)
 {
  var camera=HotelViewCamera.Current;var target=new RenderTexture(1280,720,24);var priorTarget=camera.targetTexture;camera.targetTexture=target;
  var canvases=new List<Canvas>();var modes=new List<RenderMode>();var cameras=new List<Camera>();var planes=new List<float>();
  // Project authored world art at the render target's size, rather than reusing the
  // previous GameView frame's pixel coordinates (which cropped the review image).
  foreach(var presentation in FindObjectsByType<GhostTrapAreaPresentation>(FindObjectsSortMode.None))presentation.GetType().GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(presentation,null);
  foreach(var foreground in FindObjectsByType<GhostTentaclePresentation>(FindObjectsSortMode.None))foreground.GetType().GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(foreground,null);
  var projected=new List<RectTransform>();
  foreach(var presentation in FindObjectsByType<GhostTrapAreaPresentation>(FindObjectsSortMode.None)){projected.Add(Get<RectTransform>(presentation,"_Area"));projected.Add(Get<RectTransform>(presentation,"_Cart"));}
  foreach(var foreground in FindObjectsByType<GhostTentaclePresentation>(FindObjectsSortMode.None))projected.Add(Get<Image>(foreground,"_Image").rectTransform);
  foreach(var puff in FindObjectsByType<PossessionSmoke>(FindObjectsSortMode.None))projected.Add(Get<RectTransform>(puff,"_Anchor"));
  var points=new List<Vector3>();foreach(var rect in projected)points.Add(rect.position);
  foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))if(canvas.isRootCanvas&&canvas.isActiveAndEnabled&&canvas.renderMode==RenderMode.ScreenSpaceOverlay){canvases.Add(canvas);modes.Add(canvas.renderMode);cameras.Add(canvas.worldCamera);planes.Add(canvas.planeDistance);canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}
  Canvas.ForceUpdateCanvases();
  for(int i=0;i<projected.Count;i++){var canvas=projected[i].GetComponentInParent<Canvas>();if(RectTransformUtility.ScreenPointToWorldPointInRectangle(canvas.transform as RectTransform,points[i],camera,out var point))projected[i].position=point;}
  camera.Render();var prior=RenderTexture.active;RenderTexture.active=target;var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(System.IO.Path.Combine(_Folder,name+".png"),pixels.EncodeToPNG());RenderTexture.active=prior;
  for(int i=0;i<canvases.Count;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=planes[i];}
  for(int i=0;i<projected.Count;i++)projected[i].position=points[i];
  camera.targetTexture=priorTarget;target.Release();Destroy(target);Destroy(pixels);
 }

}
