using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirage;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HauntedFish.Multiplayer
{
    public struct HotelTravel { public int Version; public string Scene; }
    public struct HotelTravelReady { public int Version; public string Scene; }

    // Only the reliable scene protocol and readiness barrier live here.
    public sealed class HauntedHotelMessageTravel : MonoBehaviour
    {
        public bool Loading { get; private set; }
        public string TargetScene { get; private set; } = "Lobby";
        public event Action StateChanged;
        NetworkServer server;
        NetworkClient client;
        ServerObjectManager serverObjects;
        ClientObjectManager clientObjects;
        HotelSessionContext session;
        Action<string> reportStatus;
        Func<string> roomCode;
        Action beforeTravel;
        Func<int, Vector3> spawnPosition;
        int version;
        bool serverLoaded;
        readonly Dictionary<INetworkPlayer, float> waiting = new Dictionary<INetworkPlayer, float>();

        public void Configure(NetworkServer host, NetworkClient peer, ServerObjectManager hostObjects,
            ClientObjectManager peerObjects, HotelSessionContext context, Action<string> status,
            Func<string> code, Action prepareTravel, Func<int, Vector3> spawn)
        {
            Unconfigure();
            server = host; client = peer; serverObjects = hostObjects; clientObjects = peerObjects;
            session = context; reportStatus = status; roomCode = code; beforeTravel = prepareTravel; spawnPosition = spawn;
            server.Started.AddListener(RegisterServer);
            client.Started.AddListener(RegisterClient);
            server.Disconnected.AddListener(OnDisconnected);
        }
        public void Unconfigure()
        {
            StopAllCoroutines();
            if (server)
            {
                server.Started.RemoveListener(RegisterServer);
                server.Disconnected.RemoveListener(OnDisconnected);
            }
            if (client) client.Started.RemoveListener(RegisterClient);
            waiting.Clear();
            SetLoading(false);
            server = null; client = null;
            reportStatus = null; roomCode = null; beforeTravel = null; spawnPosition = null;
            session = null;
        }
        void OnDestroy() => Unconfigure();
        void RegisterServer() => server.MessageHandler.RegisterHandler<HotelTravelReady>(Ready, allowUnauthenticated: false);
        void RegisterClient() => client.MessageHandler.RegisterHandler<HotelTravel>(Receive, allowUnauthenticated: false);
        void OnDisconnected(INetworkPlayer player) => waiting.Remove(player);
        void SetLoading(bool value)
        {
            Loading = value;
            if (session != null) session.Loading = value;
            StateChanged?.Invoke();
        }
        // A second transition cannot overtake peers still loading the first scene.
        public bool CanTravel => server && server.Active && !Loading && waiting.Count == 0;
        public void GoToGame() { if (CanTravel && TargetScene == "Lobby") Begin("Game"); }
        public void ReturnToLobby() { if (CanTravel && TargetScene == "Game") Begin("Lobby"); }
        void Begin(string scene)
        {
            if (!Application.CanStreamedLevelBeLoaded(scene)) { reportStatus?.Invoke("Add " + scene + " to the build scene list."); return; }
            beforeTravel?.Invoke();
            ++version; TargetScene = scene; serverLoaded = false; SetLoading(true); waiting.Clear();
            foreach (var player in server.AuthenticatedPlayers) WaitFor(player);
            foreach (var identity in server.World.SpawnedIdentities.ToArray())
                if (identity && identity.gameObject.scene.name != "DontDestroyOnLoad") serverObjects.Destroy(identity);
            server.SendToAll(new HotelTravel { Version = version, Scene = scene }, authenticatedOnly: true, excludeLocalPlayer: true);
            StartCoroutine(Load(scene, true));
        }
        void WaitFor(INetworkPlayer player)
        {
            player.SceneIsReady = false;
            waiting[player] = Time.unscaledTime + 30;
        }
        public void Admit(INetworkPlayer player)
        {
            if (TargetScene == "Lobby" && !Loading) { player.SceneIsReady = true; return; }
            WaitFor(player);
            if (player != server.LocalPlayer) player.Send(new HotelTravel { Version = version, Scene = TargetScene });
        }
        void Receive(INetworkPlayer sender, HotelTravel message)
        {
            if (server.Active || (message.Scene != "Game" && message.Scene != "Lobby") || message.Version <= version) return;
            if (!Application.CanStreamedLevelBeLoaded(message.Scene))
            {
                reportStatus?.Invoke("Scene missing from this build: " + message.Scene);
                client.Disconnect();
                return;
            }
            version = message.Version; TargetScene = message.Scene; SetLoading(true);
            StartCoroutine(Load(message.Scene, false));
        }
        IEnumerator Load(string scene, bool host)
        {
            reportStatus?.Invoke("Loading " + scene + " with the connected room...");
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            clientObjects.PrepareToSpawnSceneObjects();
            foreach (var player in FindObjectsByType<HotelPlayer>(FindObjectsSortMode.None)) player.ResetSceneMotion();
            if (host)
            {
                serverLoaded = true;
                var index = 0;
                foreach (var player in server.AuthenticatedPlayers)
                    if (player.HasCharacter)
                    {
                        var character = player.Identity.GetComponent<HotelPlayer>();
                        if (character) character.Teleport(spawnPosition(index));
                        ++index;
                    }
                if (server.LocalPlayer != null) { server.LocalPlayer.SceneIsReady = true; waiting.Remove(server.LocalPlayer); }
                serverObjects.SpawnSceneObjects();
                foreach (var player in server.AuthenticatedPlayers)
                    if (player.SceneIsReady) serverObjects.SpawnVisibleObjects(player);
            }
            else client.Send(new HotelTravelReady { Version = version, Scene = scene });
            SetLoading(false);
            reportStatus?.Invoke("Connected to " + roomCode?.Invoke() + " - " + scene + ".");
        }
        void Ready(INetworkPlayer player, HotelTravelReady message)
        {
            if (!waiting.ContainsKey(player) || message.Version != version || message.Scene != TargetScene) return;
            waiting.Remove(player); player.SceneIsReady = true;
            if (serverLoaded) serverObjects.SpawnVisibleObjects(player);
        }
        void Update()
        {
            if (!server || !server.Active) return;
            foreach (var entry in waiting.ToArray())
                if (Time.unscaledTime > entry.Value) { waiting.Remove(entry.Key); entry.Key.Disconnect(); }
        }
        public void SessionEnded()
        {
            StopAllCoroutines(); waiting.Clear(); SetLoading(false); version = 0; TargetScene = "Lobby"; serverLoaded = false;
            if (SceneManager.GetActiveScene().name == "Game" && Application.CanStreamedLevelBeLoaded("Lobby")) StartCoroutine(ReturnOffline());
        }
        IEnumerator ReturnOffline()
        {
            SetLoading(true);
            yield return SceneManager.LoadSceneAsync("Lobby", LoadSceneMode.Single);
            SetLoading(false);
        }
    }
}
