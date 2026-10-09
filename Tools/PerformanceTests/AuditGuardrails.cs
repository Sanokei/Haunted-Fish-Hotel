using System;
using System.Reflection;
using System.Linq;
using HauntedFish.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runtime behavioral checks in a disposable copy of the actual production project.
public static class AuditGuardrails
{
    sealed class Commands : IHotelGameCommands, IHotelBossCommands
    {
        public int Calls;
        public HotelPlayer Actor;
        public string Key;
        public int Version;
        void Record(HotelPlayer actor, string key, int version) { Calls++; Actor = actor; Key = key; Version = version; }
        public void Acknowledge(HotelPlayer p, int version) => Record(p, null, version);
        public void RefreshPlacement(HotelPlayer p) => Record(p, null, 0);
        public void AcceptFlightInput(HotelPlayer p, Vector2 axis, string key, int version) => Record(p, key, version);
        public bool TakePackage(HotelPlayer p, int id, Vector3 point, string key, int version) { Record(p,key,version); return true; }
        public bool PlaceTrap(HotelPlayer p, Vector3 point, string family, string key, int version) { Record(p,key,version); return false; }
        public bool DisposeTrap(HotelPlayer p, int id, string key, int version) { Record(p,key,version); return true; }
        public void PossessTrap(HotelPlayer p, int id, Vector3 point, string key, int version) => Record(p,key,version);
        public void ActOnTrap(HotelPlayer p, int id, int kind, float axis, Vector3 point, string key, int version) => Record(p,key,version);
        public bool TryEnter(HotelPlayer p, string key, int version) { Record(p,key,version); return true; }
        public bool AcceptReady(HotelPlayer p, string key, int version, uint epoch) { Record(p,key,version); return true; }
        public bool AcceptInput(HotelPlayer p, Vector2 axis, string key, int version, uint epoch) { Record(p,key,version); return true; }
    }

    static void CheckCommandRouting(HotelPlayer player)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var game = (IHotelGameCommands)typeof(HotelPlayer).GetField("_GameCommands", flags).GetValue(player);
        var boss = (IHotelBossCommands)typeof(HotelPlayer).GetField("_BossCommands", flags).GetValue(player);
        int reply = player.GhostPlacementReply;
        bool accepted = player.GhostPlacementAccepted;
        var commands = new Commands();
        try
        {
            player.BindGameCommands(commands);
            player.BindBossCommands(commands);
            player.RequestConveyorPackage(123, Vector3.one, "routing", 42);
            Check(commands.Calls == 1 && commands.Actor == player && commands.Key == "routing" && commands.Version == 42 && player.GhostPlacementAccepted && player.GhostPlacementReply == reply + 1, "Woven host pickup RPC uses bound endpoint and publishes one accepted reply");
            player.RequestPlaceHeldTrap(Vector3.one, "family", "routing", 42);
            Check(!player.GhostPlacementAccepted && player.GhostPlacementReply == reply + 2, "Rejected placement publishes exactly one negative reply");
            player.RequestDisposeTrap(123, "routing", 42);
            player.SendGhostFlightInput(Vector2.one, "routing", 42);
            player.RequestScopedTrapPossession(123, Vector3.one, "routing", 42);
            player.SendScopedTrapAction(123, 0, 1, Vector3.one, "routing", 42);
            player.FinishGhostSelection(42);
            Check(commands.Calls == 7, "All gameplay RPCs route once through their bound endpoint");
            player.RequestBossFight("routing", 42);
            player.ReadyBossScene("routing", 42, 5);
            player.SendBossInput(Vector2.one, "routing", 42, 5);
            Check(commands.Calls == 10, "All boss RPCs route once through their bound endpoint");
            var replacement = new Commands();
            player.BindBossCommands(replacement);
            player.UnbindBossCommands(commands);
            player.RequestBossFight("replacement", 43);
            Check(replacement.Calls == 1 && commands.Calls == 10, "Old boss owner cannot detach a replacement");
            player.UnbindBossCommands(replacement);
            player.UnbindGameCommands(commands);
            player.RequestConveyorPackage(123, Vector3.one, "routing", 42);
            player.RequestBossFight("routing", 42);
            Check(commands.Calls == 10 && replacement.Calls == 1 && !player.GhostPlacementAccepted && player.GhostPlacementReply == reply + 4, "Unbound RPCs reject pickup and do not call stale owners");
        }
        finally
        {
            player.BindGameCommands(game);
            player.BindBossCommands(boss);
            player.GhostPlacementReply = reply;
            player.GhostPlacementAccepted = accepted;
        }
    }

    static void Check(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
        Debug.Log("AUDIT_CHECK: " + message);
    }

    static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    static void Call(object owner, string method) => owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
    public static void Run(HotelPlayer player, GhostPlacementWorld world, TrapManager manager)
    {
        if (player.Networked && player.IsServer) CheckCommandRouting(player);
        // Camera replacement and additive scene ownership do not depend on tags.
        var binding = UnityEngine.Object.FindFirstObjectByType<HotelViewCamera>();
        var original = binding.Output;
        var replacement = new GameObject("Audit output replacement").AddComponent<Camera>();
        replacement.tag = "Untagged";
        binding.BindOutput(replacement);
        Check(HotelViewCamera.Current == replacement, "Explicit untagged output replaces the cached scene camera");
        replacement.enabled = false;
        Call(binding, "Update");
        Check(!HotelViewCamera.Current, "Disabled output is not returned as an active view");
        replacement.enabled = true;
        Call(binding, "Update");
        Check(HotelViewCamera.Current == replacement, "Re-enabled output becomes available without global discovery");
        var prior = SceneManager.GetActiveScene();
        var other = SceneManager.CreateScene("Audit additive camera");
        var otherObject = new GameObject("Audit additive binding");
        SceneManager.MoveGameObjectToScene(otherObject, other);
        var otherCamera = otherObject.AddComponent<Camera>();
        otherObject.AddComponent<HotelViewCamera>();
        SceneManager.SetActiveScene(other);
        Check(HotelViewCamera.Current == otherCamera, "Active additive scene owns camera selection");
        otherObject.SetActive(false);
        Check(HotelViewCamera.Current == replacement, "Disabling additive binding restores surviving output");
        SceneManager.SetActiveScene(prior);
        SceneManager.UnloadSceneAsync(other);
        binding.BindOutput(original);
        UnityEngine.Object.Destroy(replacement.gameObject);
        Check(HotelViewCamera.Current == original, "Camera replacement restores authored output");
        var ownership = UnityEngine.Object.FindFirstObjectByType<GameUIEventOwnership>();
        var authoredEvents = (UnityEngine.EventSystems.EventSystem)ownership.GetType().GetField("_Events",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ownership);
        bool priorAuthoredEnabled=authoredEvents.enabled;
        var persistentObject = new GameObject("Audit persistent UI provider");
        UnityEngine.Object.DontDestroyOnLoad(persistentObject);
        var persistentEvents = persistentObject.AddComponent<UnityEngine.EventSystems.EventSystem>();
        var persistentInput = persistentObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        persistentInput.AssignDefaultActions();
        UnityEngine.EventSystems.EventSystem.current = persistentEvents;
        Call(ownership,"Update");
        Check(!authoredEvents.enabled && persistentEvents.enabled,"Persistent UI provider suppresses the authored scene provider after transition");
        persistentObject.SetActive(false);Call(ownership,"Update");
        Check(authoredEvents.enabled==priorAuthoredEnabled,"Removing temporary persistent provider restores prior scene input ownership");
        UnityEngine.Object.Destroy(persistentObject);
        var refresh = (Action)Delegate.CreateDelegate(typeof(Action),ownership,ownership.GetType().GetMethod("Refresh",BindingFlags.Instance|BindingFlags.NonPublic));

        // Warm managed allocation measurements compare concrete old and new calls.
        var players = HotelPlayer.ActivePlayers;
        var family = world.DefaultFamily;
        world.Definition(family);
        manager.SelectWeighted(.5);
        for (int i = 0; i < 10; i++)
            UnityEngine.Object.FindObjectsByType<HotelPlayer>(FindObjectsSortMode.None);
        var positiveControl = new byte[1000][];
        long begin = GC.GetAllocatedBytesForCurrentThread();
        for (int i=0;i<positiveControl.Length;i++) positiveControl[i]=new byte[1024];
        long control=GC.GetAllocatedBytesForCurrentThread()-begin;
        GC.KeepAlive(positiveControl);
        var retainedResults = new HotelPlayer[1000][];
        GC.Collect();
        long heapBefore=UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
        int total=0;
        begin=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<retainedResults.Length;i++) {retainedResults[i]=UnityEngine.Object.FindObjectsByType<HotelPlayer>(FindObjectsSortMode.None);total+=retainedResults[i].Length;}
        long scan=GC.GetAllocatedBytesForCurrentThread()-begin;
        GC.Collect();
        long retainedBytes=UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong()-heapBefore;
        GC.KeepAlive(retainedResults);
        begin=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<1000;i++)
        {
            for(int j=0;j<players.Count;j++)if(players[j])total++;
            world.Definition(family);manager.SelectWeighted(.5);var view=HotelViewCamera.Current;refresh();
        }
        long cached=GC.GetAllocatedBytesForCurrentThread()-begin;
        Debug.Log("AUDIT_ALLOC: Editor thread counter positive control="+control+" bytes; 1000 global actor array queries="+scan+
            "; cached actors/definition/weighted/camera/UI="+cached+"; retained global result arrays grow Mono used heap="+retainedBytes+" bytes; observable="+total);
        if(control>0 && scan>0) Check(cached==0,"Validated thread allocation counter sees no cached-loop allocation");
        else Debug.Log("AUDIT_ALLOC_LIMIT: Thread counter did not observe known allocations; zero is not evidence of allocation-free behavior. Retained heap delta is a separate limited measurement.");

        player.SwitchEditorRole();
        Check(player.ControlMode == HotelControlMode.Ghost, "Audit returns actual owned player to ghost role");
        // A paused conveyor must reconcile a shortened path before publishing.
        manager.PauseConveyor();
        var state = JsonUtility.FromJson<ConveyorSnapshot>(manager.Snapshot);
        Set(manager, "_PathEnd", state.PathStart + Vector3.right * 5);
        manager.Simulate(player, 0);
        var shorter = JsonUtility.FromJson<ConveyorSnapshot>(manager.Snapshot);
        Check(shorter.Packages.TrueForAll(p => p.Distance <= 5), "Paused shortened conveyor removes out-of-range package distances");
        Check(manager.ApplySnapshot(manager.Snapshot), "Shortened paused path publishes a valid replica snapshot");
        int conveyorRevision=JsonUtility.FromJson<ConveyorSnapshot>(manager.Snapshot).Revision;
        manager.enabled=false;
        var stoppedConveyor=JsonUtility.FromJson<ConveyorSnapshot>(player.ConveyorJson);
        Check(stoppedConveyor.Revision>conveyorRevision&&!stoppedConveyor.Running&&stoppedConveyor.Packages.Count==0,
            "Actual authority manager disable publishes higher empty shared conveyor state");
        Check(!manager.StartConveyor(),"Disabled manager continues rejecting fresh authoring after final publication");
        manager.enabled=true;manager.Simulate(player,0);

        // Invalid editable grid spacing is finite and bounded, including tiny values.
        var gridObject = new GameObject("Audit invalid spacing");
        gridObject.SetActive(false);
        var grid = gridObject.AddComponent<GhostPlacementGrid>();
        grid.target = player.transform;
        foreach (float step in new[]
        {
            0f,
            -1f,
            float.NaN,
            float.PositiveInfinity,
            .000000001f
        }

        )
        {
            grid.gridStep = step;
            Call(grid, gridObject.GetComponent<MeshFilter>() ? "GenerateGridMesh" : "Start");
            Check(gridObject.GetComponent<MeshFilter>().sharedMesh.vertexCount <= 6144, "Grid generates bounded geometry for spacing " + step);
        }

        UnityEngine.Object.Destroy(gridObject);
        // Reset preserves same-round identity and releases possession exactly once.
        manager.Simulate(player, 0);
        var cart = Resources.Load<GhostTrap>("Traps/ShoppingCartTrap");
        var replacementPrefab = UnityEngine.Object.Instantiate(cart);
        replacementPrefab.gameObject.SetActive(false);
        Set(replacementPrefab, "_MoveSpeed", 1f);
        Check(world.Hold(player, cart.FamilyTag, Vector3.zero), "Audit acquires held family before catalog replacement");
        manager.ConfigureDefinitions(new[] { new TrapDefinition { Prefab = replacementPrefab, Weight = 1 } });
        Check(world.Definition(cart.FamilyTag) == cart && manager.Definition(cart.FamilyTag).Prefab == cart, "Same-family replacement preserves held prefab identity and supply definition through round");
        world.Dispose(player, -1);
        Vector3 point = default;
        bool clear = false;
        for (int x = -12; x <= 12; x += 4)
            if (world.PlacementPoint(new Vector3(x, cart.BodyHalfSize.y, 0), cart.FamilyTag, out point))
            {
                clear = true;
                break;
            }

        Check(clear && world.Hold(player, cart.FamilyTag, point) && world.TryPlace(player, point, cart.FamilyTag), "Audit places retained family using authored prefab");
        var replicaObject=new GameObject("Audit inactive observer reconstruction");replicaObject.SetActive(false);
        var replica=replicaObject.AddComponent<GhostPlacementWorld>();replica.InjectDefinitions(new[]{cart});
        replica.BeginRound(world.RoomScope,world.RoundKey,world.RoundVersion);
        Check(replica.ApplySnapshot(world.Snapshot),"Observer reconstructs placed instance from round-scoped authored prefab snapshot");
        replica.InjectDefinitions(new[]{replacementPrefab});
        Check(replica.Definition(cart.FamilyTag)==cart,"Placed observer instances preserve family prefab identity on replacement");
        UnityEngine.Object.Destroy(replicaObject);
        var placed = JsonUtility.FromJson<GhostCubeSnapshot>(world.Snapshot);
        int oldId = placed.Cubes[0].Id;
        Set(world.Controls, "_Position", world.Trap(oldId).Position);
        world.Possess(player, oldId, world.Trap(oldId).Position);
        Check(player.ControlledCube == oldId, "Audit controls placed item before reset");
        Check(world.AcceptTrapAction(player,oldId,new TrapInput(TrapInputKind.Move,1)),"Owned trap accepts motion before controller cancellation");
        world.Controls.enabled=false;
        var stoppedAxis=(float)typeof(GhostTrap).GetField("_Axis",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(world.Trap(oldId));
        Check(stoppedAxis==0,"Disabling actual owned ghost controller immediately stops authoritative trap input");
        world.Controls.enabled=true;

        string oldJson = world.Snapshot;
        int oldRevision = placed.Revision;
        world.enabled = false;
        Check(!world.RequestMatches(player,world.RoundKey,world.RoundVersion) && !world.Hold(player,cart.FamilyTag,point),
            "Disabled world rejects fresh requests and cannot reacquire held inventory");
        var empty = JsonUtility.FromJson<GhostCubeSnapshot>(player.PlacedObjectsJson);
        Check(player.ControlledCube == -1 && world.Count == 0 && empty.Cubes.Count == 0 && empty.Revision > oldRevision, "World disable releases owner and publishes higher empty revision");
        world.enabled = true;
        Check(!world.ApplySnapshot(oldJson) && !world.AcceptTrapAction(player, oldId, new TrapInput(TrapInputKind.Move, 1)), "Delayed old snapshot and input cannot resurrect deleted same-round item");
        world.BeginRound(empty.RoomScope, empty.RoundKey, empty.RoundVersion);
        Check(world.Hold(player, cart.FamilyTag, point) && world.TryPlace(player, point, cart.FamilyTag), "Same round re-enabled world accepts fresh inventory");
        var next = JsonUtility.FromJson<GhostCubeSnapshot>(world.Snapshot);
        Check(next.Cubes[0].Id > oldId && next.Revision > empty.Revision, "Same-round IDs and revisions remain monotonic after disable");
        Check(!world.InteractionPosition(player, new Vector3(10000, 10000, 0), out _), "Interaction rejects forged coordinates distant from authority's actual flight");
        Check(!world.AcceptFlightInput(player, new Vector2(float.NaN, 1), player.RoundStateKey, player.RoundVersion), "Authority rejects nonfinite ghost flight input");
        world.ResetRound();
        if (player.Networked)
        {
            var objects = UnityEngine.Object.FindFirstObjectByType<Mirage.ServerObjectManager>();
            var remote = UnityEngine.Object.Instantiate(Resources.Load<HotelPlayer>("HotelNetworkPlayer"));
            objects.Spawn(remote.Identity);
            var context = (HotelSessionContext)typeof(HotelPlayer).GetField("_Session",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(player);
            remote.Configure(context);
            remote.RoundStateKey=world.RoundKey;remote.RoundVersion=world.RoundVersion;remote.RoundRoster=player.RoundRoster;
            remote.GhostId=remote.NetId;remote.RoundReleased=true;
            Check(remote.IsServer && !remote.IsRelevantPlayer && remote.ControlsReady && remote.ControlMode==HotelControlMode.Ghost,
                "Installed Mirage spawns a real nonlocal server ghost for authority validation");
            Check(world.AcceptFlightInput(remote,new Vector2(10,10),world.RoundKey,world.RoundVersion),"Server accepts finite scoped flight input");
            var motions = (System.Collections.IDictionary)world.GetType().GetField("_Flights",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(world);
            var motion = motions[remote];
            var axis=(Vector2)motion.GetType().GetField("Axis").GetValue(motion);
            Check(axis.magnitude<=1.00001f,"Server clamps oversized flight axes before movement");
            var before=(Vector3)motion.GetType().GetField("Position").GetValue(motion);
            world.GetType().GetMethod("SimulateFlight",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(world,new object[]{remote});
            Check(Vector3.Distance(before,remote.GhostFlightPosition)<=(GameSceneController.Current ? GameSceneController.Current.GhostFlightSpeed : 8)*Time.unscaledDeltaTime+.0001f,"Remote ghost authority integrates no faster than configured flight speed");
            Check(!world.AcceptFlightInput(remote,Vector2.one,"stale",world.RoundVersion)&&
                !world.InteractionPosition(remote,new Vector3(100,6.5f,0),out _),"Remote authority rejects stale movement scope and forged proximity coordinates");
            var package=manager.ClosestPackage(remote.GhostFlightPosition);
            if(package && Vector3.Distance(package.transform.position,remote.GhostFlightPosition)>2)
                Check(!manager.TryTake(remote,package.PackageId,package.transform.position,world.RoundKey,world.RoundVersion),"Remote cannot pick up a distant package by claiming its coordinate");
            objects.Destroy(remote.Identity,false);
            Check(!HotelPlayer.ActivePlayers.Contains(remote),"Mirage unspawn removes an active actor from the registry without requiring GameObject disable");
            UnityEngine.Object.Destroy(remote.gameObject);
        }
        world.BeginRound(world.RoomScope,"audit-next-round",world.RoundVersion+1);
        Check(world.Definition(cart.FamilyTag)==replacementPrefab,"Deferred same-family replacement becomes available in the next round");
        UnityEngine.Object.Destroy(replacementPrefab.gameObject);
    }
}
