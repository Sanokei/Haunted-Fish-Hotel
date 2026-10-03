using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Mirage;
using Mirage.SocketLayer;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace HauntedFish.Multiplayer
{
    public sealed class HotelLobby : MonoBehaviour
    {
        public static HotelLobby Instance { get; private set; }
        public static bool InputFocused { get; private set; }
        public string DirectoryUrl = "http://127.0.0.1:8787";
        public string AdvertisedAddress = "127.0.0.1";
        public int Port = 7777, Capacity = 4;
        public SocketFactory Socket;
        public NetworkIdentity PlayerPrefab;
        public List<NetworkIdentity> AdditionalPrefabs = new List<NetworkIdentity>();
        public string Status { get; private set; } = "Create a lobby or enter a six-character code.";
        public string Code => room?.code ?? "";
        public bool Busy { get; private set; }
        const int PlayerHash = 0x48464801, DialogueHash = 0x48464802;
        NetworkServer server;
        NetworkClient client;
        ServerObjectManager serverObjects;
        ClientObjectManager clientObjects;
        Room room;
        string enteredCode = "";
        bool accepted, leaving;
        float deadline;
        int generation;
        readonly HashSet<INetworkPlayer> admitted = new HashSet<INetworkPlayer>();
        readonly Dictionary<INetworkPlayer, float> pending = new Dictionary<INetworkPlayer, float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= Bootstrap;
            SceneManager.sceneLoaded += Bootstrap;
        }
        static void Bootstrap(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Lobby" || FindFirstObjectByType<HotelLobby>()) return;
            new GameObject("Hotel Multiplayer").AddComponent<HotelLobby>();
            if (!FindFirstObjectByType<Collider>())
            {
                var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name="Multiplayer Test Ground";
                floor.transform.localScale=Vector3.one*4;
            }
        }
        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            AdvertisedAddress = LanAddress();
            Application.runInBackground = true;
            server = gameObject.AddComponent<NetworkServer>();
            client = gameObject.AddComponent<NetworkClient>();
            serverObjects = gameObject.AddComponent<ServerObjectManager>();
            clientObjects = gameObject.AddComponent<ClientObjectManager>();
            server.ObjectManager = serverObjects;
            client.ObjectManager = clientObjects;
            if (!Socket) Socket = GetComponent<SocketFactory>();
            if (!Socket)
            {
                Socket = gameObject.AddComponent<Mirage.Sockets.Udp.UdpSocketFactory>();
            }
            server.SocketFactory = client.SocketFactory = Socket;
            server.Started.AddListener(() => server.MessageHandler.RegisterHandler<LobbyHello>(OnHello, allowUnauthenticated: false));
            server.Authenticated.AddListener(player => { if (!admitted.Contains(player)) pending[player] = Time.unscaledTime + 8; });
            server.Disconnected.AddListener(player => {
                if (player.HasCharacter) serverObjects.DestroyCharacter(player);
                admitted.Remove(player); pending.Remove(player);
            });
            server.Stopped.AddListener(() => { admitted.Clear(); pending.Clear(); });
            client.Started.AddListener(RegisterClient);
            client.Authenticated.AddListener(_ => client.Send(new LobbyHello { Code = room.code, JoinKey = room.joinKey }));
            client.Disconnected.AddListener(_ => {
                accepted = false;
                if (!leaving) { Status = "Host unavailable or connection lost. Check the host address and UDP port."; StartCoroutine(Leave()); }
            });
            if (!Socket) Status = "Mirage UDP SocketFactory is missing. Assign a supported UDP socket in the Inspector.";
        }
        void RegisterClient()
        {
            client.MessageHandler.RegisterHandler<SharedWorldCue>((_, cue) => Monologue.Dialogue.StoryFunctions.ApplyNetworkCue(cue), allowUnauthenticated: false);
            client.MessageHandler.RegisterHandler<LobbyWelcome>((_, message) => {
                if (!message.Accepted)
                {
                    Status = message.Message;
                    StartCoroutine(Leave());
                    return;
                }
                accepted = true;
                Status = "Connected to " + Code + ". WASD or arrow keys to move.";
            }, allowUnauthenticated: false);
            clientObjects.UnregisterSpawnHandler(PlayerHash);
            clientObjects.UnregisterSpawnHandler(DialogueHash);
            if (PlayerPrefab) clientObjects.RegisterPrefab(PlayerPrefab);
            else clientObjects.RegisterSpawnHandler(PlayerHash, message => CreatePlayer(Vector3.zero),
                identity => Destroy(identity.gameObject));
            clientObjects.RegisterSpawnHandler(DialogueHash, _ => CreateDialogue(), identity => Destroy(identity.gameObject));
            foreach (var prefab in AdditionalPrefabs) if (prefab) clientObjects.RegisterPrefab(prefab);
        }
        void OnHello(INetworkPlayer player, LobbyHello hello)
        {
            if (admitted.Contains(player)) return;
            string error = "";
            if (room == null || hello.Code != room.code || hello.JoinKey != room.joinKey)
                error = "This lobby is unavailable or the invitation has expired.";
            else if (admitted.Count >= Capacity) error = "Lobby is full.";
            if (error != "")
            {
                player.Send(new LobbyWelcome { Accepted = false, Message = error });
                StartCoroutine(Reject(player));
                return;
            }
            admitted.Add(player);
            pending.Remove(player);
            player.Send(new LobbyWelcome { Accepted = true, Message = "Connected" });
            var position = new Vector3((admitted.Count-1)*2.2f, 1.1f, 0);
            var identity = PlayerPrefab ? Instantiate(PlayerPrefab, position, Quaternion.identity) : CreatePlayer(position);
            if (PlayerPrefab) serverObjects.AddCharacter(player, identity);
            else serverObjects.AddCharacter(player, identity, PlayerHash);
        }
        IEnumerator Reject(INetworkPlayer player) { yield return new WaitForSecondsRealtime(.25f); player.Disconnect(); }
        NetworkIdentity CreatePlayer(Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Network Player";
            go.tag = "Player";
            go.transform.position = position;
            var collider = go.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var identity = go.AddComponent<NetworkIdentity>();
            var controller = go.AddComponent<CharacterController>();
            controller.center = Vector3.zero;
            controller.height = 2; controller.radius = .45f;
            var spin = new GameObject("Facing");
            spin.transform.SetParent(go.transform, false);
            go.AddComponent<HotelPlayer>().Spin = spin.transform;
            return identity;
        }
        NetworkIdentity CreateDialogue()
        {
            var go = new GameObject("Shared Dialogue");
            var identity = go.AddComponent<NetworkIdentity>();
            go.AddComponent<SharedDialogue>();
            return identity;
        }
        public void CreateLobby() { if (!Busy && !client.Active && !server.Active) StartCoroutine(Create()); }
        public void JoinLobby(string code)
        {
            if (Busy || client.Active || server.Active) return;
            if (!LobbyCode.TryNormalize(code, out var normalized)) { Status = "Enter exactly six letters or digits."; return; }
            StartCoroutine(Join(normalized));
        }
        bool ConfigureSocket()
        {
            if (!Socket || !Socket.IsSupported || !(Socket is IHasPort port))
            { Status = "A supported UDP SocketFactory with a configurable port is required."; return false; }
            port.Port = Port;
            server.MaxConnections = Capacity; // host is excluded; admission enforces total capacity.
            return true;
        }
        IEnumerator Create()
        {
            if (!ConfigureSocket()) yield break;
            Busy = true; accepted = false;
            Status = "Creating lobby...";
            var ticket = ++generation;
            Room result = null;
            yield return Request("/rooms", "POST", JsonUtility.ToJson(new CreateRoom {
                address = AdvertisedAddress, port = Port, capacity = Capacity }), value => result = value);
            if (ticket != generation) yield break;
            if (result == null) { Busy = false; yield break; }
            room = result;
            bool started = false;
            try
            {
                server.StartServer(client);
                serverObjects.Spawn(CreateDialogue(), DialogueHash);
                started = true;
            }
            catch (Exception exception) { Status = "Unable to start host: " + exception.Message; }
            Busy = false;
            if (!started) { yield return Leave(); yield break; }
            deadline = Time.unscaledTime + 10;
            StartCoroutine(Heartbeat(ticket));
        }
        IEnumerator Join(string code)
        {
            if (!ConfigureSocket()) yield break;
            Busy = true; accepted = false;
            Status = "Finding lobby...";
            var ticket = ++generation;
            Room result = null;
            yield return Request("/rooms/" + code, "GET", null, value => result = value);
            if (ticket != generation) yield break;
            Busy = false;
            if (result == null) yield break;
            room = result;
            try { client.Connect(room.address, (ushort)room.port); deadline = Time.unscaledTime + 10; Status = "Connecting to " + room.code + "..."; }
            catch (Exception exception) { Status = "Unable to connect: " + exception.Message; StartCoroutine(Leave()); }
        }
        IEnumerator Heartbeat(int ticket)
        {
            while (ticket == generation && server.Active && room != null)
            {
                yield return new WaitForSecondsRealtime(5);
                if (ticket != generation || !server.Active || room == null) yield break;
                Room result = null;
                yield return Request("/rooms/" + Code + "/heartbeat", "POST",
                    JsonUtility.ToJson(new RoomHeartbeat { ownerKey = room.ownerKey, players = Mathf.Max(1, admitted.Count) }),
                    value => result = value);
                if (ticket != generation) yield break;
                if (result == null)
                { Status = "Lobby directory unavailable; lobby closed. " + Status; yield return Leave(); yield break; }
            }
        }
        public void LeaveLobby() { Status = "Disconnected."; StartCoroutine(Leave()); }
        IEnumerator Leave()
        {
            if (leaving) yield break;
            leaving = true; ++generation;
            // Let Mirage finish its disconnect callbacks before stopping either peer.
            // Re-entering Disconnect here invalidates ClientObjectManager during cleanup.
            yield return null;
            var oldRoom = room;
            room = null; accepted = false; Busy = false;
            if (server.Active) server.Stop();
            else if (client.Active) client.Disconnect();
            if (oldRoom != null && !string.IsNullOrEmpty(oldRoom.ownerKey))
                yield return Request("/rooms/" + oldRoom.code, "DELETE",
                    JsonUtility.ToJson(new RoomHeartbeat { ownerKey = oldRoom.ownerKey }), _ => {}, false);
            leaving = false;
        }
        IEnumerator Request(string path, string method, string json, Action<Room> callback, bool showErrors = true)
        {
            using (var request = new UnityWebRequest(DirectoryUrl.TrimEnd('/') + path, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                if (json != null) request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 8;
                yield return request.SendWebRequest();
                Room result = null;
                try { result = JsonUtility.FromJson<Room>(request.downloadHandler.text); } catch (ArgumentException) {}
                if (request.result != UnityWebRequest.Result.Success)
                {
                    if (showErrors) Status = !string.IsNullOrEmpty(result?.error) ? result.error :
                        "Lobby directory unavailable. Start the local directory and check its URL.";
                    callback(null);
                }
                else callback(result);
            }
        }
        void Update()
        {
            if (client.Active && !accepted && !Busy && Time.unscaledTime > deadline)
            { Status = "Host unavailable or lobby admission timed out."; StartCoroutine(Leave()); }
            foreach (var item in pending.ToArray())
                if (Time.unscaledTime > item.Value) { pending.Remove(item.Key); item.Key.Disconnect(); }
        }
        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(15, 15, 340, 280), GUI.skin.box);
            GUILayout.Label("Haunted Fish Hotel Multiplayer");
            GUILayout.Label(Status);
            GUI.enabled = !Busy && !client.Active && !server.Active && !leaving;
            GUILayout.Label("Lobby directory (same URL on every player)");
            DirectoryUrl = GUILayout.TextField(DirectoryUrl);
            GUILayout.Label("Host address reachable from other players");
            AdvertisedAddress = GUILayout.TextField(AdvertisedAddress);
            if (GUILayout.Button("Create lobby")) CreateLobby();
            enteredCode = GUILayout.TextField(enteredCode, 6);
            if (GUILayout.Button("Join lobby")) JoinLobby(enteredCode);
            GUI.enabled = true;
            if (Code != "") GUILayout.Label("Lobby code: " + Code);
            if ((client.Active || server.Active || Busy) && GUILayout.Button("Leave")) LeaveLobby();
            InputFocused = !client.Active && !server.Active && GUIUtility.keyboardControl != 0;
            GUILayout.EndArea();
        }
        static string LanAddress()
        {
            try { return Dns.GetHostEntry(Dns.GetHostName()).AddressList.FirstOrDefault(a =>
                a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))?.ToString() ?? "127.0.0.1"; }
            catch (SocketException) { return "127.0.0.1"; }
        }
        void OnDestroy()
        {
            if (server && server.Active) server.Stop();
            else if (client && client.Active) client.Disconnect();
            if (Instance == this) { Instance = null; InputFocused = false; }
            // Directory lease expires automatically after an abrupt exit.
        }
    }
}


