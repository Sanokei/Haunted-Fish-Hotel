// Runs the production scene protocol with deterministic peer/scene doubles; no Unity native runtime required.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
    static void Main()
    {
        var host = new NetworkServer { Active = true };
        var client = new NetworkClient();
        var objects = new ServerObjectManager();
        var peerObjects = new ClientObjectManager();
        var session = new HotelSessionContext();
        var local = new Peer(); var remote = new Peer();
        host.LocalPlayer = local;
        host.AuthenticatedPlayers.Add(local); host.AuthenticatedPlayers.Add(remote);
        var travel = new HauntedHotelMessageTravel();
        travel.Configure(host, client, objects, peerObjects, session, _ => { }, () => "ABC123", () => { }, _ => new Vector3());
        host.Started.Invoke(); client.Started.Invoke();
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
        travel.Configure(host, client, new ServerObjectManager(), new ClientObjectManager(), session, _ => { }, () => "ABC123", () => { }, _ => new Vector3());
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
        Check(client.Disconnected, "Missing client scene disconnects cleanly");
        Console.WriteLine($"Passed {checks} scene protocol checks.");
    }
}
namespace UnityEngine
{
    public class Object
    {
        public static implicit operator bool(Object value) => value != null;
        public static T[] FindObjectsByType<T>(FindObjectsSortMode mode) => Array.Empty<T>();
    }
    public class MonoBehaviour : UnityEngine.Object
    {
        readonly List<IEnumerator> routines = new();
        public object StartCoroutine(IEnumerator routine) { routines.Add(routine); routine.MoveNext(); return routine; }
        public void StopAllCoroutines() => routines.Clear();
        public void CompleteLoads() { foreach (var routine in routines.ToArray()) while (routine.MoveNext()) { } routines.Clear(); }
    }
    public struct Vector3 { }
    public enum FindObjectsSortMode { None }
    public static class Time { public static float unscaledTime; }
    public static class Application { public static bool CanLoad = true; public static bool CanStreamedLevelBeLoaded(string name) => CanLoad; }
    public class GameObject : UnityEngine.Object { public UnityEngine.SceneManagement.Scene scene; }
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
        public bool HasCharacter => false;
        public NetworkIdentity Identity => null;
        public readonly List<object> Sent = new();
        public bool Disconnected;
        public void Send<T>(T message) => Sent.Add(message);
        public void Disconnect() => Disconnected = true;
    }
    public class World { public List<NetworkIdentity> SpawnedIdentities = new(); }
    public class NetworkServer : UnityEngine.Object
    {
        public bool Active;
        public Signal Started = new();
        public Signal<INetworkPlayer> Disconnected = new();
        public MessageHandler MessageHandler = new();
        public List<INetworkPlayer> AuthenticatedPlayers = new();
        public INetworkPlayer LocalPlayer;
        public World World = new();
        public List<object> Sent = new();
        public void SendToAll<T>(T message, bool authenticatedOnly, bool excludeLocalPlayer) => Sent.Add(message);
    }
    public class NetworkClient : UnityEngine.Object
    {
        public Signal Started = new();
        public MessageHandler MessageHandler = new();
        public List<object> Sent = new();
        public bool Disconnected;
        public void Send<T>(T message) => Sent.Add(message);
        public void Disconnect() => Disconnected = true;
    }
    public class NetworkIdentity : UnityEngine.Object
    {
        public GameObject gameObject = new();
        public T GetComponent<T>() => default;
    }
    public class ServerObjectManager
    {
        public List<INetworkPlayer> Visible = new();
        public void Destroy(NetworkIdentity identity) { }
        public void SpawnVisibleObjects(INetworkPlayer player) => Visible.Add(player);
        public void SpawnSceneObjects() { }
    }
    public class ClientObjectManager { public void PrepareToSpawnSceneObjects() { } }
}
namespace HauntedFish.Multiplayer
{
    public class HotelPlayer : UnityEngine.Object
    {
        public void ResetSceneMotion() { }
        public void Teleport(Vector3 position) { }
    }
}

