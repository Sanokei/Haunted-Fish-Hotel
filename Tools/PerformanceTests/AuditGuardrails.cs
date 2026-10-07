using System;
using System.Reflection;
using System.Linq;
using HauntedFish.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runtime behavioral checks in a disposable copy of the actual production project.
public static class AuditGuardrails
{
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
            Check(Vector3.Distance(before,remote.GhostFlightPosition)<=6*Time.unscaledDeltaTime+.0001f,"Remote ghost authority integrates no faster than configured flight speed");
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
