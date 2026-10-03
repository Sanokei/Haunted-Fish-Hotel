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
    // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
    public sealed class HotelLobby : MonoBehaviour
    {
        // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
        public static HotelLobby Instance { get; private set; }
        // Keep text-entry focus from becoming a movement command while the player edits the lobby controls.
        public static bool InputFocused { get; private set; }
        // All peers must query the same room directory; it advertises endpoints and invitations rather than relaying game traffic.
        public string DirectoryUrl = "http://127.0.0.1:8787";
        // Advertise an address reachable by other players; a local-only address cannot serve a remote client.
        public string AdvertisedAddress = "127.0.0.1";
        // Entering Lobby creates a private room automatically; tests can isolate this lifecycle without changing scene structure.
        public bool AutoCreateOnStart = true;
        // Serialize user-requested switches with transport cleanup so an old peer cannot receive a new room's callbacks.
        bool switching;
        // Retry a missing service at most once every fifteen seconds, never continuously each frame.
        float nextAutoAttempt;
        // The player menu waits for admission as well as HTTP/transport/scene transitions before offering another action.
        public bool Transitioning => Busy || leaving || switching || (client && client.Active && !accepted) || (SceneTravel && SceneTravel.Loading);

        // Limit room admission including the host; raw transport connections do not bypass this total-player bound.
        public int Port = 7777, Capacity = 4;
        // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
        public SocketFactory Socket;
        // Store an explicit authored prefab reference so host and clients agree on network object structure.
        public NetworkIdentity PlayerPrefab;
        // Store an explicit authored prefab reference so host and clients agree on network object structure.
        public NetworkIdentity DialoguePrefab;
        // Store an explicit authored prefab reference so host and clients agree on network object structure.
        public List<NetworkIdentity> AdditionalPrefabs = new List<NetworkIdentity>();
        // Expose the session result to the authored UI so connection failures and current room state remain visible.
        public string Status { get; private set; } = "Connecting...";
        // Use a normalized six-character room code to resolve the directory entry for this particular host.
        public string Code => room?.code ?? "";
        // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
        // Host-only scene buttons must reflect actual server authority rather than merely a joined room code.
        public bool IsHost => server && server.Active;
        public bool Busy { get; private set; }
        // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
        public HotelSceneTravel SceneTravel { get; private set; }
        // Expose the session result to the authored UI so connection failures and current room state remain visible.
        public void SetStatus(string message) {Status=message;}
        // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
        const int PlayerHash = 0x48464801, DialogueHash = 0x48464802;
        NetworkServer server;
        NetworkClient client;
        ServerObjectManager serverObjects;
        ClientObjectManager clientObjects;
        Room room;
        // Use a normalized six-character room code to resolve the directory entry for this particular host.
        bool accepted, leaving;
        // Use a bounded timeout so a failed host or admission handshake cannot leave the UI connecting forever.
        float deadline;
        // This session ticket invalidates old asynchronous responses after leaving or starting another session.
        int generation;
        // Track admitted connections separately from transport connections to enforce total room capacity and reject duplicates.
        readonly HashSet<INetworkPlayer> admitted = new HashSet<INetworkPlayer>();
        // Limit the time an authenticated connection may wait for valid room admission.
        readonly Dictionary<INetworkPlayer, float> pending = new Dictionary<INetworkPlayer, float>();

        // Resolve the authored dependencies early; the scene and prefab data determine what exists.
        void Awake()
        {
            // Clean up this existing object or duplicate so obsolete presentation/session state does not survive into the next session.
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
            Instance = this;
            // Wait through one frame of scene initialization before Update can request automatic recovery.
            nextAutoAttempt=Time.unscaledTime+1;
            // Keep the established session or spawned network identity alive across connected Lobby/Game travel.
            DontDestroyOnLoad(gameObject);
            // Advertise an address reachable by other players; a local-only address cannot serve a remote client.
            AdvertisedAddress = LanAddress();
            // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
            Application.runInBackground = true;
            // Read a component already serialized on this object; do not create a runtime replacement for missing authoring.
            server = GetComponent<NetworkServer>();
            // Read a component already serialized on this object; do not create a runtime replacement for missing authoring.
            client = GetComponent<NetworkClient>();
            // Read a component already serialized on this object; do not create a runtime replacement for missing authoring.
            serverObjects = GetComponent<ServerObjectManager>();
            // Read a component already serialized on this object; do not create a runtime replacement for missing authoring.
            clientObjects = GetComponent<ClientObjectManager>();
            // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
            server.ObjectManager = serverObjects;
            // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
            client.ObjectManager = clientObjects;
            // Read a component already serialized on this object; do not create a runtime replacement for missing authoring.
            SceneTravel=GetComponent<HotelSceneTravel>();
            // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
            SceneTravel.Configure(this,server,client,serverObjects,clientObjects);
            // Read a component already serialized on this object; do not create a runtime replacement for missing authoring.
            if (!Socket) Socket = GetComponent<SocketFactory>();
            // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
            server.SocketFactory = client.SocketFactory = Socket;
            // Register the exact message type on this peer; the handler applies only the session protocol it understands.
            server.Started.AddListener(() => server.MessageHandler.RegisterHandler<LobbyHello>(OnHello, allowUnauthenticated: false));
            // Transport authentication alone is not room admission; the room code and join key are checked separately.
            server.Authenticated.AddListener(player => { if (!admitted.Contains(player)) pending[player] = Time.unscaledTime + 8; });
            // End this peer connection through Mirage so its normal identity and observer cleanup runs.
            server.Disconnected.AddListener(player => {
                // Remove the disconnected connection's owned avatar so rejoining cannot leave stale players behind.
                if (player.HasCharacter) serverObjects.DestroyCharacter(player);
                // Track admitted connections separately from transport connections to enforce total room capacity and reject duplicates.
                admitted.Remove(player); pending.Remove(player);
            });
            // Track admitted connections separately from transport connections to enforce total room capacity and reject duplicates.
            server.Stopped.AddListener(() => { admitted.Clear(); pending.Clear(); });
            // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
            client.Started.AddListener(RegisterClient);
            // Transport authentication alone is not room admission; the room code and join key are checked separately.
            client.Authenticated.AddListener(_ => client.Send(new LobbyHello { Code = room.code, JoinKey = room.joinKey }));
            // End this peer connection through Mirage so its normal identity and observer cleanup runs.
            client.Disconnected.AddListener(_ => {
                // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
                accepted = false;
                // Expose the session result to the authored UI so connection failures and current room state remain visible.
                if (!leaving) { Status = "Host unavailable or connection lost. Check the host address and UDP port."; StartCoroutine(Leave()); }
            });
            // Expose the session result to the authored UI so connection failures and current room state remain visible.
            if (!Socket) Status = "Mirage UDP SocketFactory is missing. Assign a supported UDP socket in the Inspector.";
        }
        // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
        void RegisterClient()
        {
            // Register the exact message type on this peer; the handler applies only the session protocol it understands.
            client.MessageHandler.RegisterHandler<SharedWorldCue>((_, cue) => Monologue.Dialogue.StoryFunctions.ApplyNetworkCue(cue), allowUnauthenticated: false);
            // Register the exact message type on this peer; the handler applies only the session protocol it understands.
            client.MessageHandler.RegisterHandler<LobbyWelcome>((_, message) => {
                // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
                if (!message.Accepted)
                {
                    // Expose the session result to the authored UI so connection failures and current room state remain visible.
                    Status = message.Message;
                    // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
                    StartCoroutine(Leave());
                    // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
                    return;
                }
                // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
                accepted = true;
                // Expose the session result to the authored UI so connection failures and current room state remain visible.
                Status = "Connected to " + Code + ". WASD or arrow keys to move.";
            }, allowUnauthenticated: false);
            // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
            clientObjects.UnregisterSpawnHandler(PlayerHash);
            // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
            clientObjects.UnregisterSpawnHandler(DialogueHash);
            // Register an authored prefab with Mirage so observers can reproduce server-spawned objects with matching identity hashes.
            clientObjects.RegisterPrefab(PlayerPrefab);
            // Register an authored prefab with Mirage so observers can reproduce server-spawned objects with matching identity hashes.
            clientObjects.RegisterPrefab(DialoguePrefab);
            // Register an authored prefab with Mirage so observers can reproduce server-spawned objects with matching identity hashes.
            foreach (var prefab in AdditionalPrefabs) if (prefab) clientObjects.RegisterPrefab(prefab);
        }
        // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
        void OnHello(INetworkPlayer player, LobbyHello hello)
        {
            // Track admitted connections separately from transport connections to enforce total room capacity and reject duplicates.
            if (admitted.Contains(player)) return;
            // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
            string error = "";
            // Verify the directory-issued invitation secret as well as the public room code before admitting a peer.
            if (room == null || hello.Code != room.code || hello.JoinKey != room.joinKey)
                // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
                error = "This lobby is unavailable or the invitation has expired.";
            // Track admitted connections separately from transport connections to enforce total room capacity and reject duplicates.
            else if (admitted.Count >= Capacity) error = "Lobby is full.";
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (error != "")
            {
                // Send this protocol payload through the established Mirage connection rather than creating a separate gameplay session.
                player.Send(new LobbyWelcome { Accepted = false, Message = error });
                // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
                StartCoroutine(Reject(player));
                // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
                return;
            }
            // Track admitted connections separately from transport connections to enforce total room capacity and reject duplicates.
            admitted.Add(player);
            // Limit the time an authenticated connection may wait for valid room admission.
            pending.Remove(player);
            // Send this protocol payload through the established Mirage connection rather than creating a separate gameplay session.
            player.Send(new LobbyWelcome { Accepted = true, Message = "Connected" });
            // Ask Mirage to replicate this authoritative object and its initial state to connected observers.
            var position = GameSceneDefinition.Current?GameSceneDefinition.Current.Spawn(admitted.Count-1):new Vector3((admitted.Count-1)*2.2f, 1.1f, 2);
            // Instantiate the assigned authored network prefab for a real network spawn; no scene structure is assembled by code.
            var identity = Instantiate(PlayerPrefab, position, Quaternion.identity);
            // Assign this spawned identity to its connection, which establishes ownership for local input and owner-only RPCs.
            serverObjects.AddCharacter(player, identity);
            // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
            SceneTravel.Admit(player);
        }
        // End this peer connection through Mirage so its normal identity and observer cleanup runs.
        IEnumerator Reject(INetworkPlayer player) { yield return new WaitForSecondsRealtime(.25f); player.Disconnect(); }
        // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
        public void CreateLobby() { if (!Busy && !client.Active && !server.Active) StartCoroutine(Create()); }
        // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
        public void JoinLobby(string code)
        {
            // Reject overlap rather than letting HTTP responses race a scene load or another room switch.
            if (Transitioning) return;
            // Normalize the invitation before touching the current room; a typo must not disconnect its players.
            if (!LobbyCode.TryNormalize(code, out var normalized)) { Status = "Enter exactly six letters or digits."; return; }
            // Joining the current invitation is already satisfied and must not tear down its session.
            if (normalized == Code) return;
            // The coroutine preflights the code, then waits for old-room cleanup before starting the replacement connection.
            StartCoroutine(SwitchRoom(normalized));
        }
        // Automatic startup uses the real directory and Mirage host path after the authored scene has initialized.
        IEnumerator Start()
        {
            // Let persistent Helper services and the local test fixture initialize before opening a socket.
            yield return null;
            // Do not compete with an invitation, loading scene or an existing connection.
            if (AutoCreateOnStart && !Transitioning && !client.Active && !server.Active && room==null) yield return Create();
        }
        // Starting a private room is the normal "Play alone" menu action, not a developer Host button.
        public void StartPrivateRoom()
        {
            // Keep one transition in flight and preserve an already private authoritative room.
            if (Transitioning || IsHost) return;
            // Leaving another room must finish before the player can become a new host.
            StartCoroutine(SwitchRoom(null));
        }
        // A valid invitation can replace an automatically created room without requiring a separate Leave action.
        IEnumerator SwitchRoom(string invitation)
        {
            // Lock the compact UI while checking the invitation and shutting down its preceding session.
            switching=true;
            // Verify an invitation exists before closing the current private room or interrupting its other players.
            if (invitation!=null)
            {
                // Preserve the internal HTTP error in Status; the player UI presents a concise connection state.
                Room target=null; Busy=true;
                // Directory lookup alone changes no game connection or scene.
                yield return Request("/rooms/"+invitation,"GET",null,value=>target=value);
                // The lookup is complete before deciding whether old-room teardown is necessary.
                Busy=false;
                // Invalid, expired or full invitations leave the current room intact.
                if (target==null) { switching=false; yield break; }
            }
            // Tear down host/client identities, unregister the owner lease, and return to Lobby when necessary.
            if (room!=null || client.Active || server.Active) yield return Leave();
            // SessionEnded may asynchronously load Lobby; do not spawn the replacement into the departing Game scene.
            while (SceneTravel.Loading) yield return null;
            // A null invitation requests a new private room; a code requests the same real admission path used before.
            if (invitation==null) yield return Create(); else yield return Join(invitation);
            // If a room expired between preflight and join, restore private play automatically after the failed lookup.
            if (invitation!=null && room==null && !client.Active && !server.Active) yield return Create();
            // Normal player actions become available once this transition and any remaining admission complete.
            switching=false;
        }
        // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
        bool ConfigureSocket()
        {
            // Use the configured UDP port for Mirage traffic; the HTTP room directory uses a separate endpoint.
            if (!Socket || !Socket.IsSupported || !(Socket is IHasPort port))
            // Expose the session result to the authored UI so connection failures and current room state remain visible.
            { Status = "A supported UDP SocketFactory with a configurable port is required."; return false; }
            // Use the configured UDP port for Mirage traffic; the HTTP room directory uses a separate endpoint.
            port.Port = Port;
            // Limit room admission including the host; raw transport connections do not bypass this total-player bound.
            server.MaxConnections = Capacity; // host is excluded; admission enforces total capacity.
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            return true;
        }
        // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
        IEnumerator Create()
        {
            // A failed endpoint is recoverable when the service comes online; space retries without hiding the failure.
            nextAutoAttempt=Time.unscaledTime+15;
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (!ConfigureSocket()) yield break;
            // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
            Busy = true; accepted = false;
            // Expose the session result to the authored UI so connection failures and current room state remain visible.
            Status = "Creating lobby...";
            // This session ticket invalidates old asynchronous responses after leaving or starting another session.
            var ticket = ++generation;
            // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
            Room result = null;
            // Yield during this asynchronous handshake or load so Unity can keep servicing connections and the scene.
            yield return Request("/rooms", "POST", JsonUtility.ToJson(new CreateRoom {
                // Limit room admission including the host; raw transport connections do not bypass this total-player bound.
                address = AdvertisedAddress, port = Port, capacity = Capacity }), value => result = value);
            // This session ticket invalidates old asynchronous responses after leaving or starting another session.
            if (ticket != generation) yield break;
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (result == null) { Busy = false; yield break; }
            // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
            room = result;
            // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
            bool started = false;
            try
            {
                // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
                server.StartServer(client);
                // Instantiate the assigned authored network prefab for a real network spawn; no scene structure is assembled by code.
                var dialogue=Instantiate(DialoguePrefab);
                // Keep the established session or spawned network identity alive across connected Lobby/Game travel.
                DontDestroyOnLoad(dialogue.gameObject);
                // Ask Mirage to replicate this authoritative object and its initial state to connected observers.
                serverObjects.Spawn(dialogue);
                // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
                started = true;
            }
            // Expose the session result to the authored UI so connection failures and current room state remain visible.
            catch (Exception exception) { Status = "Unable to start host: " + exception.Message; }
            // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
            Busy = false;
            // Yield during this asynchronous handshake or load so Unity can keep servicing connections and the scene.
            if (!started) { yield return Leave(); yield break; }
            // Use a bounded timeout so a failed host or admission handshake cannot leave the UI connecting forever.
            deadline = Time.unscaledTime + 10;
            // Compare the captured session ticket so an old HTTP result cannot overwrite a newer room session.
            StartCoroutine(Heartbeat(ticket));
        }
        // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
        IEnumerator Join(string code)
        {
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (!ConfigureSocket()) yield break;
            // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
            Busy = true; accepted = false;
            // Expose the session result to the authored UI so connection failures and current room state remain visible.
            Status = "Finding lobby...";
            // This session ticket invalidates old asynchronous responses after leaving or starting another session.
            var ticket = ++generation;
            // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
            Room result = null;
            // Yield during this asynchronous handshake or load so Unity can keep servicing connections and the scene.
            yield return Request("/rooms/" + code, "GET", null, value => result = value);
            // This session ticket invalidates old asynchronous responses after leaving or starting another session.
            if (ticket != generation) yield break;
            // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
            Busy = false;
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (result == null) yield break;
            // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
            room = result;
            // Use a bounded timeout so a failed host or admission handshake cannot leave the UI connecting forever.
            try { client.Connect(room.address, (ushort)room.port); deadline = Time.unscaledTime + 10; Status = "Connecting to " + room.code + "..."; }
            // Expose the session result to the authored UI so connection failures and current room state remain visible.
            catch (Exception exception) { Status = "Unable to connect: " + exception.Message; StartCoroutine(Leave()); }
        }
        // Compare the captured session ticket so an old HTTP result cannot overwrite a newer room session.
        IEnumerator Heartbeat(int ticket)
        {
            // This session ticket invalidates old asynchronous responses after leaving or starting another session.
            while (ticket == generation && server.Active && room != null)
            {
                // Yield during this asynchronous handshake or load so Unity can keep servicing connections and the scene.
                yield return new WaitForSecondsRealtime(5);
                // This session ticket invalidates old asynchronous responses after leaving or starting another session.
                if (ticket != generation || !server.Active || room == null) yield break;
                // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
                Room result = null;
                // Yield during this asynchronous handshake or load so Unity can keep servicing connections and the scene.
                yield return Request("/rooms/" + Code + "/heartbeat", "POST",
                    // Track admitted connections separately from transport connections to enforce total room capacity and reject duplicates.
                    JsonUtility.ToJson(new RoomHeartbeat { ownerKey = room.ownerKey, players = Mathf.Max(1, admitted.Count) }),
                    // Keep this small operation on the existing component so callers share one state transition.
                    value => result = value);
                // This session ticket invalidates old asynchronous responses after leaving or starting another session.
                if (ticket != generation) yield break;
                // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
                if (result == null)
                // Yield during this asynchronous handshake or load so Unity can keep servicing connections and the scene.
                { Status = "Lobby directory unavailable; lobby closed. " + Status; yield return Leave(); yield break; }
            }
        }
        // End this peer connection through Mirage so its normal identity and observer cleanup runs.
        public void LeaveLobby() { Status = "Disconnected."; StartCoroutine(Leave()); }
        // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
        IEnumerator Leave()
        {
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (leaving) yield break;
            // This session ticket invalidates old asynchronous responses after leaving or starting another session.
            leaving = true; ++generation;
            // Let Mirage finish its disconnect callbacks before stopping either peer.
            // Re-entering Disconnect here invalidates ClientObjectManager during cleanup.
            // Yield during this asynchronous handshake or load so Unity can keep servicing connections and the scene.
            yield return null;
            // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
            var oldRoom = room;
            // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
            room = null; accepted = false; Busy = false;
            // Stop the authoritative host and let Mirage release connections and spawned state.
            if (server.Active) server.Stop();
            // End this peer connection through Mirage so its normal identity and observer cleanup runs.
            else if (client.Active) client.Disconnect();
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (oldRoom != null && !string.IsNullOrEmpty(oldRoom.ownerKey))
                // Yield during this asynchronous handshake or load so Unity can keep servicing connections and the scene.
                yield return Request("/rooms/" + oldRoom.code, "DELETE",
                    // Refresh the directory lease and player count; abrupt host exits are handled by lease expiration.
                    JsonUtility.ToJson(new RoomHeartbeat { ownerKey = oldRoom.ownerKey }), _ => {}, false);
            // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
            leaving = false;
            // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
            SceneTravel.SessionEnded();
        }
        // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
        IEnumerator Request(string path, string method, string json, Action<Room> callback, bool showErrors = true)
        {
            using (var request = new UnityWebRequest(DirectoryUrl.TrimEnd('/') + path, method))
            {
                // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
                request.downloadHandler = new DownloadHandlerBuffer();
                // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
                if (json != null) request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
                request.SetRequestHeader("Content-Type", "application/json");
                // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
                request.timeout = 8;
                // Yield during this asynchronous handshake or load so Unity can keep servicing connections and the scene.
                yield return request.SendWebRequest();
                // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
                Room result = null;
                // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
                try { result = JsonUtility.FromJson<Room>(request.downloadHandler.text); } catch (ArgumentException) {}
                // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
                if (request.result != UnityWebRequest.Result.Success)
                {
                    // Expose the session result to the authored UI so connection failures and current room state remain visible.
                    // Keep a concise diagnostic for developer logs without printing HTTP credentials or response bodies.
                    if (showErrors) Debug.LogWarning("Room directory request failed: "+request.result+" (HTTP "+request.responseCode+").",this);
                    if (showErrors) Status = !string.IsNullOrEmpty(result?.error) ? result.error :
                        "Lobby directory unavailable. Start the local directory and check its URL.";
                    // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
                    callback(null);
                }
                // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
                else callback(result);
            }
        }
        // Service local presentation and authoritative simulation each frame, with ownership/readiness checks inside the path.
        void Update()
        {
            // Recover private play after a late directory startup or completed disconnect, using actual registration.
            if (AutoCreateOnStart && !Transitioning && room==null && !client.Active && !server.Active &&
                // The retry deadline also prevents an unavailable transport from producing a busy request loop.
                Time.unscaledTime>=nextAutoAttempt && SceneManager.GetActiveScene().name=="Lobby")
                // Creating succeeds only after the directory and Mirage host setup succeed; no fake code or offline success is shown.
                CreateLobby();
            // Use a bounded timeout so a failed host or admission handshake cannot leave the UI connecting forever.
            if (client.Active && !accepted && !Busy && Time.unscaledTime > deadline)
            // Expose the session result to the authored UI so connection failures and current room state remain visible.
            { Status = "Host unavailable or lobby admission timed out."; StartCoroutine(Leave()); }
            // Limit the time an authenticated connection may wait for valid room admission.
            foreach (var item in pending.ToArray())
                // End this peer connection through Mirage so its normal identity and observer cleanup runs.
                if (Time.unscaledTime > item.Value) { pending.Remove(item.Key); item.Key.Disconnect(); }
        }
        // The owner sends bounded movement intent rather than a position, leaving collision and movement authority on the server.
        public static void SetInputFocused(bool focused) { InputFocused=focused; }
        // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
        static string LanAddress()
        {
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            try { return Dns.GetHostEntry(Dns.GetHostName()).AddressList.FirstOrDefault(a =>
                // This component coordinates the persistent host/client session; scene objects and network prefabs are assigned in authored assets.
                a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))?.ToString() ?? "127.0.0.1"; }
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            catch (SocketException) { return "127.0.0.1"; }
        }
        // Clean up this existing object or duplicate so obsolete presentation/session state does not survive into the next session.
        void OnDestroy()
        {
            // Stop the authoritative host and let Mirage release connections and spawned state.
            if (server && server.Active) server.Stop();
            // End this peer connection through Mirage so its normal identity and observer cleanup runs.
            else if (client && client.Active) client.Disconnect();
            // Keep text-entry focus from becoming a movement command while the player edits the lobby controls.
            if (Instance == this) { Instance = null; InputFocused = false; }
            // Directory lease expires automatically after an abrupt exit.
        }
    }
}


