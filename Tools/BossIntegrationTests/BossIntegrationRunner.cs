using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using HauntedFish.BossFight;
using HauntedFish.Multiplayer;
using Mirage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public sealed class BossIntegrationRunner : MonoBehaviour
{
    Keyboard _Keyboard;
    string _Folder;
    int _Checks;
    static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    static void Call(object owner, string method) => owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
    void Check(bool value, string text) { if (!value) throw new Exception(text); _Checks++; Debug.Log("BOSS_INTEGRATION_CHECK: " + text); }
    public static void Run() { EditorSceneManager.OpenScene("Assets/Scenes/Helper.unity"); EditorApplication.EnterPlaymode(); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Monitor()
    {
        if (Environment.GetCommandLineArgs().Contains("--boss-integration"))
        { var obj = new GameObject("Isolated Boss integration observer"); DontDestroyOnLoad(obj); obj.AddComponent<BossIntegrationRunner>(); }
    }
    IEnumerator Start()
    {
        _Folder = System.IO.Path.Combine(Application.dataPath, "../BossIntegrationEvidence"); Directory.CreateDirectory(_Folder);
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        _Keyboard = InputSystem.AddDevice<Keyboard>();
        var test = Tests();
        while (true)
        {
            object next;
            try { if (!test.MoveNext()) break; next = test.Current; }
            catch (Exception error) { Debug.LogException(error); File.WriteAllText(System.IO.Path.Combine(_Folder, "result.txt"), "FAIL " + error); EditorApplication.Exit(1); yield break; }
            yield return next;
        }
        File.WriteAllText(System.IO.Path.Combine(_Folder, "result.txt"), "PASS " + _Checks + " real host/participant/physics BossFight checks");
        Debug.Log("BOSS_INTEGRATION_PASS " + _Checks); EditorApplication.Exit(0);
    }
    void Keys(params Key[] keys) { InputSystem.QueueStateEvent(_Keyboard, new KeyboardState(keys)); InputSystem.Update(); }
    IEnumerator Tests()
    {
        float timeout = Time.unscaledTime + 90;
        HauntedHotelMultiplayer session = null; LobbyManager lobby = null;
        while (Time.unscaledTime < timeout)
        {
            session = FindFirstObjectByType<HauntedHotelMultiplayer>(); lobby = FindFirstObjectByType<LobbyManager>();
            if (session && session.ReadyToPlay && lobby && HotelPlayer.LocalPlayer) break;
            yield return null;
        }
        Check(session && session.ReadyToPlay && lobby, "Native Helper establishes real hotel host for BossFight integration");
        if (lobby.IntroductionPlaying) lobby.SkipIntroduction();
        var local = HotelPlayer.LocalPlayer;
        yield return null; yield return null;
        if (lobby.IntroductionPlaying) lobby.SkipIntroduction();
        yield return null; yield return null;
        var stairs = Get<LobbyTrigger>(lobby, "_StairZone").GetComponent<Collider>();
        local.Teleport(stairs.bounds.center - local.BodyController.center); Physics.SyncTransforms();
        var menu = FindFirstObjectByType<MemberMenu>(FindObjectsInactive.Include); var start = Get<Button>(menu, "_Start");
        while (!start.interactable && session.SceneTravel.TargetScene == "Lobby" && Time.unscaledTime < timeout) yield return null;
        Check(start.interactable || session.SceneTravel.TargetScene == "Game", "Existing Lobby ready/start flow remains usable (including automatic ready travel)");
        if (session.SceneTravel.TargetScene == "Lobby") start.onClick.Invoke();
        while ((!GameSceneController.Current || !local.RoundReleased) && Time.unscaledTime < timeout) yield return null;
        var scene = GameSceneController.Current; var coordinator = BossArenaCoordinator.Current;
        Check(scene && coordinator && local.RoundReleased, "Serialized Game entrance/coordinator initializes after unchanged wheel/attic/setup gate");
        var objects = FindFirstObjectByType<ServerObjectManager>(); var context = Get<HotelSessionContext>(local, "_Session");
        var ghost = Instantiate(Resources.Load<HotelPlayer>("HotelNetworkPlayer")); objects.Spawn(ghost.Identity); ghost.Configure(context);
        var other = Instantiate(Resources.Load<HotelPlayer>("HotelNetworkPlayer")); objects.Spawn(other.Identity); other.Configure(context);
        yield return null; yield return null;
        yield return new WaitForSecondsRealtime(GameRoundGate.MinimumDuration + .2f);
        scene.Acknowledge(ghost, local.RoundVersion); scene.Acknowledge(other, local.RoundVersion);
        ghost.Teleport(new Vector3(8, 1, 0)); other.Teleport(new Vector3(-4, 1, 0)); yield return null; yield return null;
        Check(scene.Haunting.AddSpook(ghost, scene.Haunting.SpookThreshold), "Production spook handoff assigns real local fish and remote ghost for match");
        yield return new WaitForSecondsRealtime(1.1f);
        Check(local.ControlMode == HotelControlMode.Fish && ghost.ControlMode == HotelControlMode.Ghost && other.ControlMode == HotelControlMode.Fish, "Real three-actor roles provide one match fish/ghost plus uninvolved hallway fish");
        var entrance = Get<BoxCollider>(coordinator, "_Entrance");
        local.Teleport(entrance.bounds.center - local.BodyController.center); yield return null; yield return null; yield return null;
        local.BodyController.enabled = false; Check(coordinator.EntranceContains(local), "Entrance proximity uses authored capsule dimensions even with an observer-disabled CharacterController"); local.BodyController.enabled = true; Physics.SyncTransforms();
        var original = local.transform.position; var otherPoint = other.transform.position;
        var key = local.RoundStateKey; int version = local.RoundVersion; string roster = local.RoundRoster;
        Check(!coordinator.TryEnter(other, key, version), "Authority rejects hallway participant away from authored end trigger");
        Keys(Key.E); Call(coordinator, "Update"); Keys();
        timeout = Time.unscaledTime + 30;
        while ((!coordinator.Arena || !coordinator.Arena.Active || !local.InBossFight) && Time.unscaledTime < timeout) yield return null;
        var arena = coordinator.Arena;
        Check(arena && arena.Active && local.InBossFight && ghost.InBossFight && !other.InBossFight, "Actual E input loads authored additive BossFight and admits only fish/current ghost");
        yield return null; yield return null; yield return null;
        Check(arena.ParticipantsReady && local.Movement.ActiveInputMap == "BossFight" && !local.BodyController.enabled && !ghost.BodyController.enabled, "Participant readiness gates serve and owned Boss input replaces hallway collision/movement");
        Check(HotelViewCamera.Current == arena.Camera && Mathf.Abs(other.transform.position.x - otherPoint.x) < .01f && local.RoundStateKey == key && local.RoundVersion == version && local.RoundRoster == roster, "Shared match view preserves spectator position and hallway round");
        Check(other.BossWatching && !other.InBossFight && !other.Movement.SimulationReady, "Hallway spectator watches and freezes without paddle ownership");
        var late=Instantiate(Resources.Load<HotelPlayer>("HotelNetworkPlayer"));objects.Spawn(late.Identity);late.Configure(context);late.Teleport(new Vector3(1,1,0));
        yield return null;yield return null;yield return null;
        var latePoint=late.transform.position;
        Check(late.BossWatching&&!late.InBossFight&&!late.Movement.SimulationReady, "Real late server actor joins shared view as a frozen spectator");
        var envelope = JsonUtility.FromJson<HotelBossSnapshot>(other.BossArenaJson);
        Check(envelope != null && envelope.RoundKey == key && envelope.RoomScope == local.InventoryScope && JsonUtility.FromJson<BossArenaSnapshot>(envelope.Arena).Active, "Existing SyncVars publish scoped Boss physics state to hallway observers");
        uint epoch = arena.MatchEpoch;
        Check(!coordinator.AcceptInput(other, Vector2.one, key, version, epoch) && !coordinator.AcceptInput(local, Vector2.one, "stale", version, epoch) && !coordinator.AcceptInput(local, new Vector2(float.NaN, 0), key, version, epoch), "Scoped Boss input rejects outsider, stale round and nonfinite axes");
        var before = JsonUtility.FromJson<BossArenaSnapshot>(arena.Snapshot).FishPaddlePosition;
        Keys(Key.D, Key.W); yield return new WaitForSecondsRealtime(.25f); Keys(); yield return new WaitForSecondsRealtime(.15f);
        var after = JsonUtility.FromJson<BossArenaSnapshot>(arena.Snapshot).FishPaddlePosition;
        Check(Vector3.Distance(before, after) > .2f && after.z < arena.Bounds.center.z, "Native owned WASD drives actual server Rigidbody paddle within fish half");
        var characters=arena.gameObject.scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<BossCharacterPresentation>(true)).Single();
        Check(characters.FishId==local.NetId&&characters.GhostId==ghost.NetId&&!Get<MonoBehaviour>(local,"_Visual").gameObject.activeInHierarchy,"Standing proxies bind real participants and hallway fish visual is hidden");
        Capture(arena.Camera, "01-live-authoritative-match");
        yield return new WaitForSecondsRealtime(1.1f);
        while(Get<bool>(arena,"_Serving"))yield return null;
        int resultVersion = local.BossResultVersion;
        FireGoal(arena, true); yield return new WaitForSecondsRealtime(.25f);
        Check(arena.Active&&local.BossWatching&&other.BossWatching&&characters.Playing&&local.BossResultVersion==resultVersion,"Winning Ink reaction retains spectator camera and defers return exactly once");
        Capture(arena.Camera,"02-live-win-reaction");
        float reactionDeadline=Time.unscaledTime+5;while(arena.Active&&Time.unscaledTime<reactionDeadline)yield return null;yield return null;
        Check(!arena.Active && !local.InBossFight && !ghost.InBossFight && local.BossWon && local.BossResultVersion == resultVersion + 1, "Actual cue-ball/goal trigger gives fish one-goal win and exactly one shared result");
        Check(Vector3.Distance(local.transform.position, original) < .2f && local.Movement.ActiveInputMap == "Game" && HotelViewCamera.Current != arena.Camera && !other.InBossFight && Mathf.Abs(other.transform.position.x - otherPoint.x) < .01f, "Win safely restores original hallway body/input/camera and uninvolved player");
        Check(!late.BossWatching&&Vector3.Distance(late.transform.position,latePoint)<.2f, "Late spectator returns to its saved hallway position without a match input lock");
        objects.Destroy(late.Identity,false);Destroy(late.gameObject);
        Check(!arena.RegisterGoal(true) && local.BossResultVersion == resultVersion + 1, "Late duplicate goal cannot issue another result");
        Check(coordinator.TryEnter(local, key, version), "Fish can re-enter same round after completed match"); yield return null; yield return null;
        Check(arena.MatchEpoch > epoch && arena.Active, "Re-entry starts a fresh match epoch without resetting hallway");
        yield return new WaitForSecondsRealtime(1.1f);
        while(Get<bool>(arena,"_Serving"))yield return null;
        resultVersion = local.BossResultVersion; FireGoal(arena, false); yield return new WaitForSecondsRealtime(.4f);
        Check(arena.Active && local.InBossFight && JsonUtility.FromJson<BossArenaSnapshot>(arena.Snapshot).GhostScore == 1, "First actual ghost goal keeps one-versus-one match active");
        yield return new WaitForSecondsRealtime(1.1f); while(Get<bool>(arena,"_Serving"))yield return null; FireGoal(arena, false); yield return new WaitForSecondsRealtime(.25f);
        Check(arena.Active&&local.BossWatching&&other.BossWatching&&characters.Playing,"Ghost winning reaction remains shared before hallway return");
        reactionDeadline=Time.unscaledTime+5;while(arena.Active&&Time.unscaledTime<reactionDeadline)yield return null;yield return null;
        Check(!arena.Active && !local.InBossFight && !local.BossWon && local.BossResultVersion == resultVersion + 1 && Mathf.Abs(local.transform.position.x - scene.SpawnPosition(0).x) < .01f, "Second actual ghost goal returns losing fish to authored level beginning exactly once");
        yield return null; yield return null;
        Check(local.BossReturnLocked && other.BossReturnLocked && !ghost.BossReturnLocked && local.BossReturnSetupRemaining > 2 && !local.Movement.SimulationReady, "Authoritative three-second return gate freezes fish and spectators while ghost is released");
        Check(scene.PlacementWorld.AcceptFlightInput(ghost, Vector2.right, key, version), "Ghost has real scoped movement control during return head start");
        Check(!coordinator.TryEnter(local,key,version), "Return head start cannot be bypassed by immediate match re-entry");
        yield return new WaitForSecondsRealtime(3.1f);
        Check(!local.BossReturnLocked && !other.BossReturnLocked && local.Movement.SimulationReady && Mathf.Abs(other.transform.position.x-otherPoint.x)<.01f, "Server return deadline releases movement once and preserves spectator position");
        local.Teleport(entrance.bounds.center-local.BodyController.center);yield return null;yield return null;
        Check(coordinator.TryEnter(local, key, version), "Match resets cleanly after ghost two-goal result"); yield return null; yield return null;
        coordinator.enabled=false;yield return null;yield return null;
        Check(!arena.Active&&!local.BossWatching&&!other.BossWatching&&!local.BossReturnLocked, "Interrupting coordinator cancels shared match and releases every watcher");
        coordinator.enabled=true;yield return null;local.Teleport(entrance.bounds.center-local.BodyController.center);yield return null;
        Check(coordinator.TryEnter(local,key,version), "Coordinator re-enable restores subscriptions and allows a fresh match");yield return null;yield return null;
        objects.Destroy(ghost.Identity, false); Destroy(ghost.gameObject); yield return null; yield return null;
        Check(!arena.Active && !local.InBossFight && !other.InBossFight && HotelViewCamera.Current != arena.Camera, "Real Mirage ghost departure cancels match and restores surviving participant's hallway view");
        Check(!local.BossWatching && !other.BossWatching && !local.BossReturnLocked, "Departure cancellation clears spectator and return locks");
        objects.Destroy(other.Identity,false);Destroy(other.gameObject);
        float devDeadline=Time.unscaledTime+45;
        while(!local.RoundReleased&&Time.unscaledTime<devDeadline)yield return null;
        Check(local.RoundReleased, "Existing ghost departure re-selection completes its new solo intro/setup normally");
        if(local.ControlMode==HotelControlMode.Ghost)local.SwitchEditorRole();yield return null;yield return null;
        var dev=scene.GetComponent<GameEditorRoleSwitch>();var devRoot=Get<GameObject>(dev,"_Root");var bossButton=Get<Button>(dev,"_BossButton");
        Check(devRoot.CompareTag("EditorOnly") && bossButton && bossButton.interactable, "Authored visible role and boss dev controls are build-stripped EditorOnly");
        bossButton.onClick.Invoke();yield return null;yield return null;
        Check(local.BossWatching && !local.InBossFight && HotelViewCamera.Current==arena.Camera, "Solo Editor boss button previews authored arena without fabricating a scored opponent");
        bossButton.onClick.Invoke();yield return null;yield return null;
        yield return null;yield return null;yield return null;
        Debug.Log("DEV_RETURN_STATE watching="+local.BossWatching+" locked="+local.BossReturnLocked+" round="+local.RoundReleased+" role="+local.ControlMode+" movement="+local.Movement.SimulationReady+" camera="+(HotelViewCamera.Current?HotelViewCamera.Current.name:"null"));
        Check(!local.BossWatching && !local.BossHallwayLocked && local.Movement.ControlsReady && HotelViewCamera.Current!=arena.Camera, "Editor preview return clears all local input/camera locks");
        float afterPreview=local.transform.position.x;Keys(Key.A);yield return new WaitForSecondsRealtime(.25f);Keys();yield return null;
        Check(local.Movement.SimulationReady&&local.transform.position.x<afterPreview-.1f, "Native fish movement resumes after dev-preview collision settling");

    }
    void FireGoal(BossArenaManager arena, bool fish)
    {
        var goal = arena.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<BossGoal>(true)).First(value => Get<bool>(value, "_FishScores") == fish);
        var point = goal.GetComponent<BoxCollider>().bounds.center;
        float sign = Mathf.Sign(point.z - arena.Bounds.center.z);
        arena.Puck.position = new Vector3(point.x, arena.Puck.position.y, point.z - sign * .8f);
        arena.Puck.linearVelocity = Vector3.forward * (sign * 10);
        Physics.SyncTransforms();
    }
    void Capture(Camera camera, string name)
    {
        var target = new RenderTexture(1280, 720, 24); var previous = camera.targetTexture; camera.targetTexture = target; camera.Render();
        var active = RenderTexture.active; RenderTexture.active = target; var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply(); File.WriteAllBytes(System.IO.Path.Combine(_Folder, name + ".png"), pixels.EncodeToPNG());
        RenderTexture.active = active; camera.targetTexture = previous; target.Release(); Destroy(target); Destroy(pixels);
    }
}
