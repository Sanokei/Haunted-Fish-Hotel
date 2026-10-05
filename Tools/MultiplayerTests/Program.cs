// Runs the production scene protocol with deterministic peer/scene doubles; no Unity native runtime required.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using HauntedFish.Multiplayer;
using Mirage;
using UnityEngine;

static class Program
{
    static int checks;
    static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        ++checks;
    }
    static void Call(object instance, string name) => instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, null);
    static void Configure(HauntedHotelMessageTravel travel, NetworkServer server, NetworkClient client,
        ServerObjectManager serverObjects, ClientObjectManager clientObjects, HotelSessionContext context)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(HauntedHotelMessageTravel).GetField("_Server", flags).SetValue(travel, server);
        typeof(HauntedHotelMessageTravel).GetField("_Client", flags).SetValue(travel, client);
        typeof(HauntedHotelMessageTravel).GetField("_ServerObjects", flags).SetValue(travel, serverObjects);
        typeof(HauntedHotelMessageTravel).GetField("_ClientObjects", flags).SetValue(travel, clientObjects);
        travel.Configure(context, new HotelTravelCallbacks(_ => { }, () => "ABC123", () => { }, () => { }, () => { }));
    }
    static void Main()
    {
        var focus = new HotelSessionContext();
        var focusEvents = new List<bool>();
        Action<bool> focusListener = value =>
        {
            Check(focus.InputFocused == value, "Focus is updated before notification");
            focusEvents.Add(value);
        };
        focus.InputFocusChanged += focusListener;
        focus.SetInputFocused(false);
        focus.SetInputFocused(true);
        focus.SetInputFocused(true);
        focus.SetInputFocused(false);
        Check(focusEvents.SequenceEqual(new[] { true, false }), "Focus events fire once per transition");
        focus.InputFocusChanged -= focusListener;
        focus.SetInputFocused(true);
        Check(focusEvents.Count == 2, "Unsubscribed focus listeners are released");
        var host = new NetworkServer { Active = true };
        var client = new NetworkClient();
        var objects = new ServerObjectManager();
        var peerObjects = new ClientObjectManager();
        var session = new HotelSessionContext();
        var local = new Peer(); var remote = new Peer();
        host.LocalPlayer = local;
        host.AuthenticatedPlayers.Add(local); host.AuthenticatedPlayers.Add(remote);
        var travel = new HauntedHotelMessageTravel();
        Configure(travel, host, client, objects, peerObjects, session);
        host.Started.Invoke(); client.Started.Invoke();
        var permitted = false;
        var travelCallbacks = (HotelTravelCallbacks)typeof(HauntedHotelMessageTravel).GetField("_Callbacks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(travel);
        travelCallbacks.CanStartGame = () => permitted;
        travel.GoToGame();
        Check(!travel.Loading && host.Sent.Count == 0, "Start is rejected by the lobby readiness/authority gate");
        permitted = true;
        travel.GoToGame();
        Check(travel.Loading && session.Loading && !local.SceneIsReady && !remote.SceneIsReady, "Travel gates every peer and injected input context");
        Check(host.Sent.Count == 1 && host.Sent[0] is HotelTravel { Version: 1, Scene: "Game" }, "Host sends versioned scene request");
        host.MessageHandler.Deliver(remote, new HotelTravelReady { Version = 0, Scene = "Game" });
        Check(!remote.SceneIsReady, "Stale readiness cannot release a peer");
        host.MessageHandler.Deliver(remote, new HotelTravelReady { Version = 1, Scene = "Lobby" });
        Check(!remote.SceneIsReady, "Wrong scene cannot release a peer");
        host.MessageHandler.Deliver(remote, new HotelTravelReady { Version = 1, Scene = "Game" });
        Check(remote.SceneIsReady && objects.Visible.Count == 0, "Early readiness waits for host load before spawning");
        travel.CompleteLoads();
        Check(!travel.Loading && !session.Loading && local.SceneIsReady && objects.Visible.Contains(remote), "Host completion releases local input and early ready observers");
        var late = new Peer(); travel.Admit(late);
        Check(!late.SceneIsReady && late.Sent[0] is HotelTravel { Version: 1, Scene: "Game" }, "Late joins load Game before receiving objects");
        travel.ReturnToLobby();
        Check(host.Sent.Count == 1 && travel.TargetScene == "Game", "A second travel cannot overtake a loading peer");
        host.MessageHandler.Deliver(late, new HotelTravelReady { Version = 1, Scene = "Game" });
        Check(late.SceneIsReady && objects.Visible.Contains(late), "Late readiness spawns objects after host load");
        var stalled = new Peer(); travel.Admit(stalled); Time.unscaledTime = 31; Call(travel, "Update");
        Check(stalled.Disconnected, "Unresponsive scene peer times out");
        travel.ReturnToLobby(); travel.CompleteLoads();
        Check(travel.TargetScene == "Lobby" && host.Sent.Count == 2, "Connected return to Lobby uses next travel request");
        travel.SessionEnded();
        Check(!travel.Loading && travel.TargetScene == "Lobby", "Session end resets protocol state");
        travel.Unconfigure();
        Check(host.Started.Count == 0 && client.Started.Count == 0 && host.Disconnected.Count == 0, "Unconfigure removes lifecycle listeners");

        host = new NetworkServer(); client = new NetworkClient(); session = new HotelSessionContext();
        travel = new HauntedHotelMessageTravel();
        Configure(travel, host, client, new ServerObjectManager(), new ClientObjectManager(), session);
        client.Started.Invoke();
        client.MessageHandler.Deliver(remote, new HotelTravel { Version = 1, Scene = "Game" });
        Check(session.Loading, "Client receives loading gate through injected context");
        travel.CompleteLoads();
        Check(client.Sent[0] is HotelTravelReady { Version: 1, Scene: "Game" }, "Client acknowledges loaded scene/version");
        client.MessageHandler.Deliver(remote, new HotelTravel { Version = 1, Scene = "Game" });
        Check(!travel.Loading, "Duplicate travel does not start another load");
        client.MessageHandler.Deliver(remote, new HotelTravel { Version = 2, Scene = "Unknown" });
        Check(!travel.Loading && travel.TargetScene == "Game", "Unknown scenes cannot change protocol state");
        Application.CanLoad = false;
        client.MessageHandler.Deliver(remote, new HotelTravel { Version = 2, Scene = "Lobby" });
        Check(client.WasDisconnected, "Missing client scene disconnects cleanly");
        Check(!LobbyRules.AllReady(null) && !LobbyRules.AllReady(Array.Empty<LobbyMember>()), "Empty lobbies cannot start");
        Check(!LobbyRules.AllReady(new[] { new LobbyMember { Ready = true }, new LobbyMember { Ready = false } }), "Every member must be ready");
        Check(LobbyRules.AllReady(new[] { new LobbyMember { Ready = true }, new LobbyMember { Ready = true } }), "All members ready allows the start UI");
        Check(LobbyCode.TryNormalize(" abc123 ", out var normalized) && normalized == "ABC123", "Invitations normalize before own-lobby comparison");
        TestLobbyAuthority();
        TestConnectionLifecycle();
        Console.WriteLine($"Passed {checks} multiplayer checks.");
    }
    static void TestLobbyAuthority()
    {
        Application.CanLoad = true;
        Time.unscaledTime = 0;
        var host = new NetworkServer { Active = true };
        var client = new NetworkClient();
        var local = new Peer(); var guest = new Peer(); var other = new Peer();
        host.LocalPlayer = local;
        host.AuthenticatedPlayers.AddRange(new[] { local, guest, other });
        var objects = new ServerObjectManager(); var peerObjects = new ClientObjectManager();
        var context = new HotelSessionContext { Connected = true };
        var travel = new HauntedHotelMessageTravel();
        Configure(travel, host, client, objects, peerObjects, context);
        var network = new HotelNetworkSession(host, client, objects, peerObjects, new Mirage.SocketLayer.SocketFactory(),
            new NetworkIdentity(), new NetworkIdentity(), Array.Empty<NetworkIdentity>(), context, travel, _ => default);
        var callbacks = (HotelTravelCallbacks)typeof(HauntedHotelMessageTravel).GetField("_Callbacks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(travel);
        callbacks.CanStartGame = () => network.LobbyPlayers.CanStartGame;
        network.StartHost(new Room { code = "ABC123", joinKey = "secret" }, 4);
        host.Started.Invoke(); client.Started.Invoke();
        foreach (var peer in new[] { local, guest, other })
            host.MessageHandler.Deliver(peer, new LobbyConnectionRequest { Code = "ABC123", JoinKey = "secret" });
        client.Player = local;
        Check(network.LobbyPlayers.Roster.Leader == local.Identity.NetId && network.LobbyPlayers.IsLeader, "Initial lobby crown belongs to the connection host");
        host.MessageHandler.Deliver(guest, new LobbyCommand { Action = LobbyAction.Kick, Target = other.Identity.NetId });
        Check(!other.Disconnected, "Guests cannot kick players");
        host.MessageHandler.Deliver(guest, new LobbyCommand { Action = LobbyAction.King, Target = guest.Identity.NetId });
        Check(network.LobbyPlayers.Roster.Leader == local.Identity.NetId, "Guests cannot crown themselves");
        foreach (var peer in new[] { local, guest, other }) peer.Identity.Character.Ready = true;
        other.Identity.Character.Ready = false;
        host.MessageHandler.Deliver(local, new LobbyCommand { Action = LobbyAction.Start });
        Check(!travel.Loading, "Leader cannot start while someone is outside the stairs");
        other.Identity.Character.Ready = true;
        travel.GoToGame();
        Check(!travel.Loading, "Direct scene calls cannot bypass the authenticated leader command");
        host.MessageHandler.Deliver(local, new LobbyCommand { Action = LobbyAction.King, Target = guest.Identity.NetId });
        Check(network.LobbyPlayers.Roster.Leader == local.Identity.NetId, "Current host stays authoritative until replacement transport is prepared");
        var preparing = 0; var switching = 0; string destination = null; bool becameHost = true;
        network.LobbyPlayers.HostPreparing += _ => ++preparing;
        network.LobbyPlayers.HostSwitching += (code, successor) => { ++switching; destination = code; becameHost = successor; };
        client.MessageHandler.Deliver(local, new LobbyHostPrepare { Version = 1, Target = guest.Identity.NetId });
        client.MessageHandler.Deliver(local, new LobbyHostPrepare { Version = 1, Target = guest.Identity.NetId });
        Check(preparing == 1, "Duplicate preparation does not allocate another transport");
        client.MessageHandler.Deliver(local, new LobbyHostCommit { Version = 1, Target = other.Identity.NetId, Code = "DEF456" });
        Check(!client.Sent.Exists(m => m is LobbyHostAck), "Clients reject a commit for a different successor");
        host.MessageHandler.Deliver(local, new LobbyCommand { Action = LobbyAction.Start });
        Check(!travel.Loading, "Starts are blocked throughout host migration");
        host.MessageHandler.Deliver(local, new LobbyHostPrepared { Version = 1, Code = "DEF456" });
        Check(!guest.Sent.Exists(m => m is LobbyHostCommit), "Only the selected successor can prepare the destination");
        host.MessageHandler.Deliver(guest, new LobbyHostPrepared { Version = 0, Code = "DEF456" });
        Check(!guest.Sent.Exists(m => m is LobbyHostCommit), "Stale host preparation is rejected");
        host.MessageHandler.Deliver(guest, new LobbyHostPrepared { Version = 1, Code = "DEF456" });
        Check(guest.Sent.Exists(m => m is LobbyHostCommit { Code: "DEF456" }), "Destination reaches every peer before disconnection");
        client.MessageHandler.Deliver(local, new LobbyHostCommit { Version = 1, Target = guest.Identity.NetId, Code = "DEF456" });
        Check(client.Sent.Exists(m => m is LobbyHostAck { Version: 1 }), "Client acknowledges the verified destination before disconnecting");
        host.MessageHandler.Deliver(local, new LobbyHostAck { Version = 1 });
        host.MessageHandler.Deliver(guest, new LobbyHostAck { Version = 1 });
        host.MessageHandler.Deliver(new Peer(), new LobbyHostAck { Version = 1 });
        Check(!guest.Sent.Exists(m => m is LobbyHostSwitch), "Migration waits for every admitted peer");
        host.MessageHandler.Deliver(other, new LobbyHostAck { Version = 0 });
        Check(!guest.Sent.Exists(m => m is LobbyHostSwitch), "Stale acknowledgements cannot commit migration");
        host.MessageHandler.Deliver(other, new LobbyHostAck { Version = 1 });
        Check(guest.Sent.Exists(m => m is LobbyHostSwitch), "Host switch requires every peer to know the destination");
        var switches = guest.Sent.FindAll(m => m is LobbyHostSwitch).Count;
        host.MessageHandler.Deliver(other, new LobbyHostAck { Version = 1 });
        Check(guest.Sent.FindAll(m => m is LobbyHostSwitch).Count == switches, "Duplicate acknowledgement cannot repeat the switch broadcast");
        client.MessageHandler.Deliver(local, new LobbyHostSwitch { Version = 0 });
        Check(switching == 0, "Stale switch is rejected by clients");
        client.MessageHandler.Deliver(local, new LobbyHostSwitch { Version = 1 });
        client.MessageHandler.Deliver(local, new LobbyHostSwitch { Version = 1 });
        Check(switching == 1 && destination == "DEF456" && !becameHost, "Former host reconnects as a guest exactly once");
        network.Stop();
        host.LocalPlayer = guest; client.Player = guest; host.Active = true;
        network.StartHost(new Room { code = "DEF456", joinKey = "new-secret" }, 4);
        foreach (var peer in new[] { guest, local, other })
            host.MessageHandler.Deliver(peer, new LobbyConnectionRequest { Code = "DEF456", JoinKey = "new-secret" });
        Check(network.LobbyPlayers.Roster.Leader == guest.Identity.NetId && network.LobbyPlayers.Roster.TransportHost == guest.Identity.NetId,
            "Successor owns both the network connection and the crown after takeover");
        host.MessageHandler.Deliver(local, new LobbyCommand { Action = LobbyAction.Start });
        Check(!travel.Loading, "Former host loses start authority");
        host.MessageHandler.Deliver(guest, new LobbyCommand { Action = LobbyAction.Kick, Target = local.Identity.NetId });
        Check(local.Disconnected, "New host can kick the former host");
        foreach (var peer in new[] { guest, local, other }) peer.Identity.Character.Ready = true;
        guest.Identity.Character.Ready = false;
        host.MessageHandler.Deliver(guest, new LobbyCommand { Action = LobbyAction.Start });
        Check(!travel.Loading, "New host must also be in the stair trigger");
        guest.Identity.Character.Ready = true;
        host.MessageHandler.Deliver(new Peer(), new LobbyCommand { Action = LobbyAction.Start });
        Check(!travel.Loading, "Unadmitted sender cannot start");
        host.MessageHandler.Deliver(guest, new LobbyCommand { Action = LobbyAction.King, Target = other.Identity.NetId });
        Time.unscaledTime = 46;
        network.Tick();
        Check(other.Sent.Exists(m => m is LobbyHostCancel { Version: 2 }), "Timed-out replacement preserves the current lobby and notifies peers");
        Check(network.LobbyPlayers.Roster.Leader == guest.Identity.NetId, "Failed migration retains the current host");
        host.MessageHandler.Deliver(guest, new LobbyCommand { Action = LobbyAction.King, Target = other.Identity.NetId });
        host.MessageHandler.Deliver(other, new LobbyHostPrepared { Version = 3, Code = "invalid" });
        Check(other.Sent.Exists(m => m is LobbyHostCancel { Version: 3 }), "Invalid destination cancels transfer");
        host.MessageHandler.Deliver(guest, new LobbyCommand { Action = LobbyAction.King, Target = other.Identity.NetId });
        host.MessageHandler.Deliver(local, new LobbyHostCancel { Version = 4 });
        host.MessageHandler.Deliver(guest, new LobbyCommand { Action = LobbyAction.Start });
        Check(!travel.Loading, "Unselected guest cannot cancel another player's handoff");
        host.MessageHandler.Deliver(other, new LobbyHostCancel { Version = 4 });
        Check(other.Sent.Exists(m => m is LobbyHostCancel { Version: 4 }), "Successor can abort a failed transport preparation");
        host.MessageHandler.Deliver(guest, new LobbyCommand { Action = LobbyAction.Start });
        Check(travel.Loading, "Current host can resume gameplay after an aborted transfer");
        network.Dispose(); travel.Unconfigure();
    }

    static void TestConnectionLifecycle()
    {
        Time.unscaledTime = 0;
        var server = new NetworkServer { Active = true };
        var client = new NetworkClient();
        var host = new Peer(); var guest = new Peer(); var overflow = new Peer();
        server.LocalPlayer = host;
        server.AuthenticatedPlayers.AddRange(new[] { host, guest, overflow });
        var objects = new ServerObjectManager(); var clientObjects = new ClientObjectManager();
        var context = new HotelSessionContext { Connected = true };
        var travel = new HauntedHotelMessageTravel();
        Configure(travel, server, client, objects, clientObjects, context);
        var network = new HotelNetworkSession(server, client, objects, clientObjects, new Mirage.SocketLayer.SocketFactory(),
            new NetworkIdentity(), new NetworkIdentity(), Array.Empty<NetworkIdentity>(), context, travel, _ => default);
        var lobby = network.LobbyPlayers;
        var joined = 0; var left = 0; var changed = 0; var admitted = 0;
        var rosterChanges = 0;
        lobby.RosterChanged += () => ++rosterChanges;
        HotelLobbyPlayer guestState = null;
        lobby.PlayerJoined += player => { ++joined; if (!player.IsHost) guestState = player; };
        lobby.PlayerLeft += _ => ++left;
        lobby.PlayerChanged += _ => ++changed;
        network.PlayerAdmitted += player =>
        {
            ++admitted;
            Check(player.HasCharacter && lobby.Players.Any(p => p.Id == player.Identity.NetId),
                "Connection observers receive a spawned avatar and an initialized lobby player");
        };
        network.StartHost(new Room { code = "ABC123", joinKey = "secret" }, 2);
        server.Started.Invoke(); client.Started.Invoke();
        server.MessageHandler.Deliver(host, new LobbyConnectionRequest { Code = "ABC123", JoinKey = "wrong" });
        Check(joined == 0 && !host.HasCharacter && network.PlayerCount == 0, "Rejected admission cannot spawn or create a lobby player");
        server.MessageHandler.Deliver(host, new LobbyConnectionRequest { Code = "ABC123", JoinKey = "secret" });
        server.MessageHandler.Deliver(host, new LobbyConnectionRequest { Code = "ABC123", JoinKey = "secret" });
        Check(joined == 1 && admitted == 1 && network.PlayerCount == 1, "Duplicate admission cannot duplicate join events");
        server.MessageHandler.Deliver(guest, new LobbyConnectionRequest { Code = "ABC123", JoinKey = "secret" });
        Check(joined == 2 && guestState != null && !guestState.Ready, "Joining guest receives separate initial lobby state");
        server.MessageHandler.Deliver(overflow, new LobbyConnectionRequest { Code = "ABC123", JoinKey = "secret" });
        Check(joined == 2 && !overflow.HasCharacter, "Full lobby rejection leaves player state unchanged");
        var playerChanges = 0; var playerLeft = 0;
        guestState.Changed += () => ++playerChanges;
        guestState.Left += () => ++playerLeft;
        guest.Identity.Character.Ready = true;
        network.Tick();
        Check(guestState.Ready && changed == 1 && playerChanges == 1, "Readiness changes notify the player and lobby observers");
        var rosterChangesBefore = rosterChanges;
        var sentBefore = guest.Sent.OfType<LobbyRoster>().Count();
        Time.unscaledTime = .2f; network.Tick();
        Check(changed == 1 && playerChanges == 1, "Unchanged snapshots do not repeat player-change events");
        Check(rosterChanges == rosterChangesBefore && guest.Sent.OfType<LobbyRoster>().Count() == sentBefore,
            "Unchanged readiness does not broadcast a roster or refresh lobby observers");
        server.Disconnected.Invoke(guest);
        Check(left == 1 && playerLeft == 1 && !guestState.IsConnected && !guestState.Ready && network.PlayerCount == 1 && lobby.Players.Count() == 1,
            "Disconnection removes membership and emits one leave event after despawning");
        var snapshot = new LobbyRoster { Leader = 77, TransportHost = 77, Members = new[] { new LobbyMember { Id = 77, Ready = true } } };
        client.MessageHandler.Deliver(host, snapshot);
        var replica = lobby.Players.Single();
        Check(replica.Id == 77 && replica.IsHost && replica.Ready, "Clients reconcile independent player state from the authoritative roster");
        var joinedBefore = joined; var changedBefore = changed;
        rosterChangesBefore = rosterChanges;
        client.MessageHandler.Deliver(host, snapshot);
        Check(joined == joinedBefore && changed == changedBefore, "Duplicate client roster cannot repeat joins or state changes");
        Check(rosterChanges == rosterChangesBefore, "Duplicate client roster does not refresh lobby observers");
        client.MessageHandler.Deliver(host, new LobbyRoster { Leader = 88, TransportHost = 77, Members = snapshot.Members });
        Check(rosterChanges == rosterChangesBefore + 1 && lobby.Roster.Leader == 88,
            "Leadership changes notify observers even when membership and readiness are unchanged");
        network.Dispose();
        Check(!lobby.Players.Any(), "Session disposal clears lobby players");
        Check(server.Authenticated.Count == 0 && server.Started.Count == 1 && client.Started.Count == 1 &&
            client.Authenticated.Count == 0 && client.Disconnected.Count == 0, "Disposal removes connection and lobby subscriptions without removing scene travel listeners");
        travel.Unconfigure();
    }

}
namespace UnityEngine
{
    public sealed class SerializeField : Attribute { }
    public class Object
    {
        public static implicit operator bool(Object value) => value != null;
        public static T Instantiate<T>(T value) where T : Object => (T)(Object)new Mirage.NetworkIdentity();
        public static T Instantiate<T>(T value, Vector3 position, Quaternion rotation) where T : Object => Instantiate(value);
        public static void DontDestroyOnLoad(GameObject value) { }
        public static void Destroy(GameObject value) { }
        public static T[] FindObjectsByType<T>(FindObjectsSortMode mode) => Array.Empty<T>();
    }
    public class MonoBehaviour : UnityEngine.Object
    {
        readonly List<IEnumerator> routines = new();
        public object StartCoroutine(IEnumerator routine) { routines.Add(routine); routine.MoveNext(); return routine; }
        public void StopAllCoroutines() => routines.Clear();
        static void Drain(IEnumerator routine)
        {
            do { if (routine.Current is IEnumerator child) { child.MoveNext(); Drain(child); } } while (routine.MoveNext());
        }
        public void CompleteLoads() { foreach (var routine in routines.ToArray()) Drain(routine); routines.Clear(); }
    }
    public struct Vector3 { }
    public struct Quaternion { public static Quaternion identity => default; }
    public class Collider : Object { }
    public enum FindObjectsSortMode { None }
    public static class Time { public static float unscaledTime; }
    public static class Application { public static bool CanLoad = true; public static bool CanStreamedLevelBeLoaded(string name) => CanLoad; }
    public class GameObject : UnityEngine.Object { public UnityEngine.SceneManagement.Scene scene; public bool activeInHierarchy = true; }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene { public string name; }
    public enum LoadSceneMode { Single }
    public static class SceneManager
    {
        public static object LoadSceneAsync(string name, LoadSceneMode mode) => null;
        public static Scene GetActiveScene() => new Scene { name = "Lobby" };
    }
}
namespace Mirage
{
    public class Signal
    {
        event Action listeners;
        public int Count => listeners?.GetInvocationList().Length ?? 0;
        public void AddListener(Action listener) => listeners += listener;
        public void RemoveListener(Action listener) => listeners -= listener;
        public void Invoke() => listeners?.Invoke();
    }
    public class Signal<T>
    {
        event Action<T> listeners;
        public int Count => listeners?.GetInvocationList().Length ?? 0;
        public void AddListener(Action<T> listener) => listeners += listener;
        public void RemoveListener(Action<T> listener) => listeners -= listener;
        public void Invoke(T value) => listeners?.Invoke(value);
    }
    public class MessageHandler
    {
        readonly Dictionary<Type, Delegate> handlers = new();
        public void RegisterHandler<T>(Action<INetworkPlayer, T> handler, bool allowUnauthenticated) => handlers[typeof(T)] = handler;
        public void Deliver<T>(INetworkPlayer peer, T value) => ((Action<INetworkPlayer, T>)handlers[typeof(T)])(peer, value);
    }
    public interface INetworkPlayer
    {
        bool SceneIsReady { get; set; }
        bool HasCharacter { get; }
        NetworkIdentity Identity { get; }
        void Send<T>(T message);
        void Disconnect();
    }
    public class Peer : INetworkPlayer
    {
        public bool SceneIsReady { get; set; } = true;
        public bool HasCharacter => Owned != null;
        public NetworkIdentity Owned;
        public NetworkIdentity Identity => Owned;
        public readonly List<object> Sent = new();
        public bool Disconnected;
        public void Send<T>(T message) => Sent.Add(message);
        public void Disconnect() => Disconnected = true;
    }
    public class World { public List<NetworkIdentity> SpawnedIdentities = new(); }
    public class NetworkServer : UnityEngine.Object
    {
        public bool Active;
        public ServerObjectManager ObjectManager;
        public Mirage.SocketLayer.SocketFactory SocketFactory;
        public int MaxConnections;
        public Signal Stopped = new();
        public Signal<INetworkPlayer> Authenticated = new();
        public void StartServer(NetworkClient client) { Active = true; }
        public void Stop() { Active = false; }
        public Signal Started = new();
        public Signal<INetworkPlayer> Disconnected = new();
        public MessageHandler MessageHandler = new();
        public List<INetworkPlayer> AuthenticatedPlayers = new();
        public INetworkPlayer LocalPlayer;
        public World World = new();
        public List<object> Sent = new();
        public void SendToAll<T>(T message, bool authenticatedOnly, bool excludeLocalPlayer) => Sent.Add(message);
    }
    public enum ClientStoppedReason { Disconnected }
    public class NetworkClient : UnityEngine.Object
    {
        public bool Active;
        public INetworkPlayer Player;
        public ClientObjectManager ObjectManager;
        public Mirage.SocketLayer.SocketFactory SocketFactory;
        public Signal<INetworkPlayer> Authenticated = new();
        public Signal<ClientStoppedReason> Disconnected = new();
        public void Connect(string address, ushort port) { Active = true; }
        public Signal Started = new();
        public MessageHandler MessageHandler = new();
        public List<object> Sent = new();
        public bool WasDisconnected;
        public void Send<T>(T message) => Sent.Add(message);
        public void Disconnect() => WasDisconnected = true;
    }
    public struct SpawnMessage { }
    public class NetworkIdentity : UnityEngine.Object
    {
        static uint next;
        public uint NetId = ++next;
        public int PrefabHash;
        public bool IsSpawned = true;
        public HotelPlayer Character = new();
        public GameObject gameObject = new();
        public T GetComponent<T>() => (T)(object)Character;
    }
    public class ServerObjectManager
    {
        public List<INetworkPlayer> Visible = new();
        public void Destroy(NetworkIdentity identity) { }
        public void Spawn(NetworkIdentity identity) { }
        public void AddCharacter(INetworkPlayer player, NetworkIdentity identity) => ((Peer)player).Owned = identity;
        public void DestroyCharacter(INetworkPlayer player) => ((Peer)player).Owned = null;
        public void SpawnVisibleObjects(INetworkPlayer player) => Visible.Add(player);
        public void SpawnSceneObjects() { }
    }
    public class ClientObjectManager
    {
        public void PrepareToSpawnSceneObjects() { }
        public void UnregisterSpawnHandler(int hash) { }
        public void RegisterSpawnHandler(NetworkIdentity prefab, Func<SpawnMessage, NetworkIdentity> spawn, Action<NetworkIdentity> destroy) { }
        public void RegisterPrefab(NetworkIdentity prefab) { }
    }
}
namespace HauntedFish.Multiplayer
{
    public class HotelPlayer : UnityEngine.Object
    {
        public bool Ready;
        public void Configure(HotelSessionContext context) { }
        public void ResetSceneMotion() { }
        public void Teleport(Vector3 position) { }
    }
}


namespace Mirage.SocketLayer { public class SocketFactory { } }
namespace HauntedFish.Multiplayer
{
    public static class LobbyTrigger { public static bool Contains(UnityEngine.Collider volume, HotelPlayer player) => player != null && player.Ready; }
}
namespace Monologue.Dialogue { public static class StoryFunctions { public static void ApplyNetworkCue(HauntedFish.Multiplayer.SharedWorldCue cue) { } } }
public static class BubbleSceneTransition
{
    public static IEnumerator Travel(string scene, Action covered = null, Action loaded = null)
    {
        yield return null;
        covered?.Invoke(); loaded?.Invoke();
    }
}
