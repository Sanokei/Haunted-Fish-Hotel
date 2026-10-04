using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Mirage;
using Mirage.SocketLayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HauntedFish.Multiplayer
{
    public class HauntedHotelMultiplayer : MonoBehaviour
    {
        [SerializeField] NetworkServer server;
        [SerializeField] NetworkClient client;
        [SerializeField] ServerObjectManager serverObjects;
        [SerializeField] ClientObjectManager clientObjects;
        [SerializeField] HauntedHotelMessageTravel sceneTravel;
        public HotelSessionContext Session { get; } = new HotelSessionContext();
        public event Action StateChanged;
        public event Action<INetworkPlayer> PlayerAdmitted;
        public event Action<INetworkPlayer> PlayerLeft;
        public bool IsHost => server && server.Active;
        public bool Busy { get; private set; }
        public bool Transitioning => Busy || leaving || switching ||
            (client && client.Active && !accepted) || (SceneTravel && SceneTravel.Loading);
        bool accepted, leaving, switching, initialized;
        string AdvertisedAddress;
        [SerializeField] string DirectoryUrl = "http://127.0.0.1:8787";
        [SerializeField] int Port = 7777, Capacity = 4;

        [SerializeField] bool AutoCreateOnStart = true;

        float nextAutoAttempt;

        public SocketFactory Socket;
        public NetworkIdentity PlayerPrefab;
        public NetworkIdentity DialoguePrefab;
        public List<NetworkIdentity> AdditionalPrefabs = new();

        /* Lobby Code */
        string status = "Connecting...";
        public string Status
        {
            get => status;
            private set { status = value; StateChanged?.Invoke(); }
        }
        public string Code => room?.code ?? "";

        public HauntedHotelMessageTravel SceneTravel { get; private set; }
        public void SetStatus(string message) {Status=message;}

        Room room;

        float deadline;
        int generation;

        readonly HashSet<INetworkPlayer> admitted = new();
        readonly Dictionary<INetworkPlayer, float> pending = new();

        void Awake()
        {
            // A persistent session owns its transport; a newly loaded Lobby must not start another.
            foreach (var existing in FindObjectsByType<HauntedHotelMultiplayer>(FindObjectsSortMode.None))
                if (existing != this && existing.initialized)
                {
                    if (server && server != existing.server) Destroy(server.gameObject);
                    Destroy(gameObject);
                    return;
                }
            if (!server || !client || !serverObjects || !clientObjects || !sceneTravel || !PlayerPrefab || !DialoguePrefab || !Socket)
            {
                Status = "Assign the authored Mirage peers, object managers, scene travel and network prefabs.";
                Debug.LogError(Status, this);
                enabled = false;
                return;
            }
            initialized = true;
            DontDestroyOnLoad(gameObject);
            DontDestroyOnLoad(server.transform.root.gameObject);
            DontDestroyOnLoad(client.transform.root.gameObject);
            nextAutoAttempt=Time.unscaledTime+1;

            AdvertisedAddress = LanAddress();
            Application.runInBackground = true;

            server.ObjectManager = serverObjects;
            client.ObjectManager = clientObjects;
            SceneTravel = sceneTravel;
            SceneTravel.Configure(server, client, serverObjects, clientObjects, Session,
                SetStatus, () => Code, BeforeTravel, SpawnPosition);
            SceneTravel.StateChanged += NotifyStateChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            server.SocketFactory = client.SocketFactory = Socket;
            server.Started.AddListener(RegisterServer);
            server.Authenticated.AddListener(OnServerAuthenticated);
            server.Disconnected.AddListener(OnServerDisconnected);
            server.Stopped.AddListener(OnServerStopped);
            client.Started.AddListener(RegisterClient);
            client.Authenticated.AddListener(OnClientAuthenticated);
            client.Disconnected.AddListener(OnClientDisconnected);
            if (!Socket) Status = "Mirage UDP SocketFactory is missing. Assign a supported UDP socket in the Inspector.";
        }
        HotelTurnSocketFactory Relay => Socket as HotelTurnSocketFactory;
        void NotifyStateChanged() => StateChanged?.Invoke();
        void RegisterServer() => server.MessageHandler.RegisterHandler<LobbyHello>(AdmitHello, allowUnauthenticated: false);
        void OnServerAuthenticated(INetworkPlayer player)
        {
            if (!admitted.Contains(player)) pending[player] = Time.unscaledTime + 8;
        }
        void OnServerDisconnected(INetworkPlayer player)
        {
            if (player.HasCharacter) serverObjects.DestroyCharacter(player);
            pending.Remove(player);
            if (admitted.Remove(player)) PlayerLeft?.Invoke(player);
        }
        void OnServerStopped() { admitted.Clear(); pending.Clear(); }
        void OnClientAuthenticated(INetworkPlayer player)
        {
            if (room == null || leaving) return;
            client.Send(new LobbyHello { Code = room.code, JoinKey = room.joinKey, Reservation = room.reservation });
        }
        void OnClientDisconnected(ClientStoppedReason reason)
        {
            accepted = false;
            if (leaving) return;
            Status = "Host unavailable or connection lost. Check the host address and UDP port.";
            StartCoroutine(Leave());
        }
        static string LanAddress()
        {
            try { return Dns.GetHostEntry(Dns.GetHostName()).AddressList.FirstOrDefault(address =>
                address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))?.ToString() ?? "127.0.0.1"; }
            catch (SocketException) { return "127.0.0.1"; }
        }
        static Vector3 SpawnPosition(int index)
        {
            var definition = FindFirstObjectByType<GameSceneDefinition>();
            return definition ? definition.Spawn(index) : new Vector3(index * 2.2f, 1.1f, 2);
        }
        static void BeforeTravel()
        {
            var dialogue = FindFirstObjectByType<Monologue.Dialogue.DialogueManager>();
            if (dialogue && dialogue.IsSharedDialogue) dialogue.ExitDialogMode();
        }
        NetworkIdentity SpawnClientPlayer(SpawnMessage message)
        {
            var identity = Instantiate(PlayerPrefab);
            BindPlayer(identity);
            return identity;
        }
        void BindPlayer(NetworkIdentity identity)
        {
            var player = identity.GetComponent<HotelPlayer>();
            if (player) player.Configure(Session);
        }
        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => BindScene();
        void BindScene()
        {
            foreach (var panel in FindObjectsByType<HotelLobbyPanel>(FindObjectsSortMode.None)) panel.Bind(this);
        }
        void RegisterClient()
        {
            client.MessageHandler.RegisterHandler<SharedWorldCue>((_, cue) => Monologue.Dialogue.StoryFunctions.ApplyNetworkCue(cue), allowUnauthenticated: false);
            client.MessageHandler.RegisterHandler<LobbyWelcome>(OnWelcome, allowUnauthenticated: false);
            clientObjects.UnregisterSpawnHandler(PlayerPrefab.PrefabHash);
            clientObjects.UnregisterSpawnHandler(DialoguePrefab.PrefabHash);
            clientObjects.RegisterSpawnHandler(PlayerPrefab, SpawnClientPlayer, identity => Destroy(identity.gameObject));
            clientObjects.RegisterPrefab(DialoguePrefab);
            foreach (var prefab in AdditionalPrefabs) if (prefab) clientObjects.RegisterPrefab(prefab);
        }
        void OnWelcome(INetworkPlayer player, LobbyWelcome message)
        {
            if (!message.Accepted)
            {
                Status = message.Message;
                StartCoroutine(Leave());
                return;
            }
            accepted = true;
            Status = "Connected to " + Code + ". WASD or arrow keys to move.";
        }
        void AdmitHello(INetworkPlayer player, LobbyHello hello)
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
            var position = SpawnPosition(admitted.Count - 1);
            var identity = Instantiate(PlayerPrefab, position, Quaternion.identity);
            BindPlayer(identity);
            SceneTravel.Admit(player);
            serverObjects.AddCharacter(player, identity);
            PlayerAdmitted?.Invoke(player);
        }
        IEnumerator Reject(INetworkPlayer player) { yield return new WaitForSecondsRealtime(.25f); player.Disconnect(); }
        public void CreateLobby() { if (initialized && !Transitioning && !client.Active && !server.Active) StartCoroutine(Create()); }
        public void JoinLobby(string code)
        {
            if (!initialized || Transitioning) return;
            if (!LobbyCode.TryNormalize(code, out var normalized)) { Status = "Enter exactly six letters or digits."; return; }
            if (normalized == Code) return;
            StartCoroutine(SwitchRoom(normalized));
        }
        IEnumerator Start()
        {
            yield return null;
            if (!initialized) yield break;
            BindScene();
            if (AutoCreateOnStart && !Transitioning && !client.Active && !server.Active && room==null) yield return Create();
        }
        public void StartPrivateRoom()
        {
            if (!initialized || Transitioning || IsHost) return;
            StartCoroutine(SwitchRoom(null));
        }
        IEnumerator SwitchRoom(string invitation)
        {
            switching=true; NotifyStateChanged();
            var ticket = generation;
            if (invitation!=null)
            {
                Room target=null; Busy=true;
                yield return Request("/rooms/"+invitation,"GET",null,value=>target=value);
                if (ticket != generation) { switching=false; NotifyStateChanged(); yield break; }
                Busy=false; NotifyStateChanged();
                if (target==null) { switching=false; NotifyStateChanged(); yield break; }
            }
            if (room!=null || client.Active || server.Active) yield return Leave();
            ticket = generation;
            while (SceneTravel.Loading) yield return null;
            if (ticket != generation) { switching=false; NotifyStateChanged(); yield break; }
            if (invitation==null) yield return Create(); else yield return Join(invitation);
            if (invitation!=null && room==null && !client.Active && !server.Active) yield return Create();
            switching=false; NotifyStateChanged();
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
            nextAutoAttempt=Time.unscaledTime+15;
            if (!ConfigureSocket()) yield break;
            Busy = true; accepted = false;
            Status = "Creating lobby...";
            var ticket = ++generation;
            if (Relay)
            {
                yield return Relay.Prepare(DirectoryUrl);
                if (ticket != generation) yield break;
                if (!Relay.Ready) { Busy=false; Status=Relay.Failure ?? "Unable to connect to relay."; yield break; }
                AdvertisedAddress=Relay.RelayEndPoint.Address.ToString(); Port=Relay.RelayEndPoint.Port;
            }
            Room result = null;
            yield return Request("/rooms", "POST", JsonUtility.ToJson(new CreateRoom {
                address = AdvertisedAddress, port = Port, capacity = Capacity }), value => result = value);
            if (ticket != generation) yield break;
            if (result == null)
            {
                if (Relay) yield return Relay.EndSession();
                if (ticket != generation) yield break;
                Busy = false; NotifyStateChanged();
                yield break;
            }
            room = result;
            bool started = false;
            try
            {
                server.StartServer(client);
                var dialogue=Instantiate(DialoguePrefab);
                DontDestroyOnLoad(dialogue.gameObject);
                serverObjects.Spawn(dialogue);
                started = true;
            }
            catch (Exception exception) { Status = "Unable to start host: " + exception.Message; }
            Busy = false; NotifyStateChanged();
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
            if (Relay)
            {
                yield return Relay.Prepare(DirectoryUrl);
                if (ticket != generation) yield break;
                if (!Relay.Ready) { Busy=false; Status=Relay.Failure ?? "Unable to connect to relay."; yield break; }
                AdvertisedAddress=Relay.RelayEndPoint.Address.ToString(); Port=Relay.RelayEndPoint.Port;
            }
            Room result = null;
            yield return Request("/rooms/" + code + (Relay ? "/join" : ""), Relay ? "POST" : "GET", Relay ? "{}" : null, value => result = value);
            if (ticket != generation) yield break;
            if (result == null)
            {
                if (Relay) yield return Relay.EndSession();
                if (ticket != generation) yield break;
                Busy = false; NotifyStateChanged();
                yield break;
            }
            room = result;
            try { client.Connect(room.address, (ushort)room.port); deadline = Time.unscaledTime + 10; Status = "Connecting to " + room.code + "..."; }
            catch (Exception exception) { Status = "Unable to connect: " + exception.Message; StartCoroutine(Leave()); }
            Busy = false; NotifyStateChanged();
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
        public void LeaveLobby() { if (!initialized) return; Status = "Disconnected."; StartCoroutine(Leave()); }
        IEnumerator Leave()
        {
            if (leaving) yield break;
            leaving = true; ++generation; NotifyStateChanged();
            yield return null;
            var oldRoom = room;
            room = null; accepted = false; Busy = false; Session.InputFocused = false; NotifyStateChanged();
            if (server.Active) server.Stop();
            else if (client.Active) client.Disconnect();
            if (oldRoom != null && !string.IsNullOrEmpty(oldRoom.ownerKey))
                yield return Request("/rooms/" + oldRoom.code, "DELETE",
                    JsonUtility.ToJson(new RoomHeartbeat { ownerKey = oldRoom.ownerKey }), _ => {}, false);
            if (Relay) yield return Relay.EndSession();
            SceneTravel.SessionEnded();
            leaving = false; NotifyStateChanged();
        }
        IEnumerator Request(string path, string method, string json, Action<Room> callback, bool showErrors = true)
        {
            var ticket = generation;
            yield return HotelRoomDirectory.Request(DirectoryUrl, Relay ? Relay.AuthorizationToken : null,
                path, method, json, (result, error) =>
                {
                    if (ticket != generation) return;
                    if (showErrors && error != null)
                    {
                        Debug.LogWarning("Room directory " + method + " request failed: " + error, this);
                        Status = error;
                    }
                    callback(result);
                });
        }
        void Update()
        {
            if (AutoCreateOnStart && !Transitioning && room==null && !client.Active && !server.Active &&
                Time.unscaledTime>=nextAutoAttempt && SceneManager.GetActiveScene().name=="Lobby")
                CreateLobby();
            if (client.Active && !accepted && !Busy && !leaving && Time.unscaledTime > deadline)
            { Status = "Host unavailable or lobby admission timed out."; StartCoroutine(Leave()); }
            foreach (var item in pending.ToArray())
                if (Time.unscaledTime > item.Value) { pending.Remove(item.Key); item.Key.Disconnect(); }
        }
        public void SetInputFocused(bool focused) { Session.InputFocused = focused; }
        void OnDestroy()
        {
            if (!initialized) return;
            leaving = true;
            ++generation;
            if (server && server.Active) server.Stop();
            else if (client && client.Active) client.Disconnect();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneTravel.StateChanged -= NotifyStateChanged;
            SceneTravel.Unconfigure();
            server.Started.RemoveListener(RegisterServer);
            server.Authenticated.RemoveListener(OnServerAuthenticated);
            server.Disconnected.RemoveListener(OnServerDisconnected);
            server.Stopped.RemoveListener(OnServerStopped);
            client.Started.RemoveListener(RegisterClient);
            client.Authenticated.RemoveListener(OnClientAuthenticated);
            client.Disconnected.RemoveListener(OnClientDisconnected);
            Session.InputFocused = false;
        }
    }
}
