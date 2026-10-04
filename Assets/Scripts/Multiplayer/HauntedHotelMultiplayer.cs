using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Mirage;
using Mirage.SocketLayer;
using Monologue.Dialogue;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HauntedFish.Multiplayer
{
    // Composition root and session lifecycle. Mirage protocol details live in HotelNetworkSession.
    public sealed class HauntedHotelMultiplayer : MonoBehaviour
    {
        [Header("Authored network dependencies")]
        [SerializeField] NetworkServer _Server;
        [SerializeField] NetworkClient _Client;
        [SerializeField] ServerObjectManager _ServerObjects;
        [SerializeField] ClientObjectManager _ClientObjects;
        [SerializeField] HauntedHotelMessageTravel _SceneTravel;
        [SerializeField] SocketFactory _Socket;
        [SerializeField] NetworkIdentity _PlayerPrefab;
        [SerializeField] NetworkIdentity _DialoguePrefab;
        [SerializeField] List<NetworkIdentity> _AdditionalPrefabs = new List<NetworkIdentity>();

        [Header("Room directory")]
        [SerializeField] string _DirectoryUrl = "http://127.0.0.1:8787";
        [SerializeField] int _Port = 7777;
        [SerializeField] int _Capacity = 4;
        [SerializeField] bool _AutoCreateOnStart = true;
        [SerializeField] float _RetryInterval = 15f;
        [SerializeField] float _AdmissionTimeout = 10f;

        HotelNetworkSession _Network;
        HotelSceneBindings _SceneBindings;
        DialogueManager _Dialogue;
        Room _Room;
        Coroutine _Operation;
        Coroutine _Heartbeat;
        int _OperationVersion;
        float _NextAutoAttempt;
        bool _OwnsSession;
        string _ConnectionFailure;

        public HotelSessionContext Session { get; } = new HotelSessionContext();
        public HotelSessionState State { get; private set; } = HotelSessionState.Idle;
        public string Status { get; private set; } = "Connecting...";
        public string Code => _Room?.code ?? string.Empty;
        public HauntedHotelMessageTravel SceneTravel => _SceneTravel;
        public bool IsHost => _Network != null && _Network.IsHost;
        public bool ReadyToPlay => State == HotelSessionState.Connected && !_SceneTravel.Loading;
        public bool Busy => State != HotelSessionState.Idle && State != HotelSessionState.Connected && State != HotelSessionState.Failed;
        public bool Transitioning => Busy || (_SceneTravel && _SceneTravel.Loading);
        public float ConnectionProgress => HotelSessionProgress.For(State);
        HotelTurnSocketFactory _Relay => _Socket as HotelTurnSocketFactory;

        public event Action StateChanged;
        public event Action<INetworkPlayer> PlayerAdmitted;
        public event Action<INetworkPlayer> PlayerLeft;

        void Awake()
        {
            if (!HasAuthoredDependencies())
            {
                SetState(HotelSessionState.Failed, "Assign the network dependencies and player prefabs in the Inspector.");
                Debug.LogError(Status, this);
                enabled = false;
                return;
            }

            _Network = new HotelNetworkSession(_Server, _Client, _ServerObjects, _ClientObjects, _Socket,
                _PlayerPrefab, _DialoguePrefab, _AdditionalPrefabs, Session, _SceneTravel, SpawnPosition);
            _Network.PlayerAdmitted += OnPlayerAdmitted;
            _Network.PlayerLeft += OnPlayerLeft;
            _Network.ConnectionLost += OnConnectionLost;
            _SceneTravel.Configure(Session, new HotelTravelCallbacks(
                reportStatus: SetStatus,
                roomCode: GetRoomCode,
                beforeLoad: PrepareTravel,
                resetMotion: _Network.ResetCharacterMotion,
                positionCharacters: _Network.PositionCharacters));
            _SceneTravel.StateChanged += PublishState;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        bool HasAuthoredDependencies()
        {
            return _Server && _Client && _ServerObjects && _ClientObjects && _SceneTravel && _Socket &&
                _PlayerPrefab && _DialoguePrefab && _PlayerPrefab.GetComponent<HotelPlayer>();
        }

        void Start()
        {
            if (_Network == null)
                return;
            _OwnsSession = true;
            DontDestroyOnLoad(gameObject);
            DontDestroyOnLoad(_Server.transform.root.gameObject);
            DontDestroyOnLoad(_Client.transform.root.gameObject);
            Application.runInBackground = true;
            for (var index = 0; index < SceneManager.sceneCount; ++index)
                BindScene(SceneManager.GetSceneAt(index));
            _NextAutoAttempt = Time.unscaledTime;
            if (_AutoCreateOnStart)
                CreateLobby();
        }

        public void CreateLobby()
        {
            if (_Network == null || Transitioning || _Network.IsRunning)
                return;
            BeginSession(null);
        }

        public void JoinLobby(string invitation)
        {
            if (_Network == null || Transitioning)
                return;
            if (!LobbyCode.TryNormalize(invitation, out var code))
            {
                SetStatus("Enter exactly six letters or digits.");
                return;
            }
            if (code != Code)
                BeginSession(code);
        }

        public void StartPrivateRoom()
        {
            if (_Network != null && !Transitioning && !IsHost)
                BeginSession(null);
        }

        void BeginSession(string invitation)
        {
            CancelOperation();
            _ConnectionFailure = null;
            _Operation = StartCoroutine(OpenSession(invitation, _OperationVersion));
        }

        IEnumerator OpenSession(string invitation, int version)
        {
            if (_Room != null || _Network.IsRunning)
                yield return CloseSession();
            while (_SceneTravel.Loading)
                yield return null;
            if (version != _OperationVersion)
                yield break;
            _NextAutoAttempt = Time.unscaledTime + _RetryInterval;

            if (!_Socket.IsSupported || !(_Socket is IHasPort configurablePort))
            {
                SetState(HotelSessionState.Failed, "Assign a supported socket with a configurable port.");
                yield break;
            }
            configurablePort.Port = _Port;
            var address = LocalAddress();
            var endpointPort = _Port;
            SetState(HotelSessionState.PreparingTransport, "Connecting...");

            if (_Relay)
            {
                yield return _Relay.Prepare(_DirectoryUrl);
                if (version != _OperationVersion)
                    yield break;
                if (!_Relay.Ready)
                {
                    SetState(HotelSessionState.Failed, _Relay.Failure ?? "Unable to connect to relay.");
                    yield break;
                }
                address = _Relay.RelayEndPoint.Address.ToString();
                endpointPort = _Relay.RelayEndPoint.Port;
            }

            SetState(HotelSessionState.RequestingRoom, invitation == null ? "Creating lobby..." : "Finding lobby...");
            Room result = null;
            string error = null;
            if (invitation == null)
            {
                var request = new CreateRoom { address = address, port = endpointPort, capacity = _Capacity };
                yield return Request("/rooms", "POST", JsonUtility.ToJson(request), (value, failure) => { result = value; error = failure; });
            }
            else
            {
                var path = "/rooms/" + invitation + (_Relay ? "/join" : string.Empty);
                yield return Request(path, _Relay ? "POST" : "GET", _Relay ? "{}" : null, (value, failure) => { result = value; error = failure; });
            }
            if (version != _OperationVersion)
                yield break;
            if (result == null)
            {
                yield return FailSession(error ?? "The room request failed.");
                yield break;
            }

            _Room = result;
            SetState(HotelSessionState.Connecting, "Connecting to " + Code + "...");
            try
            {
                if (invitation == null)
                    _Network.StartHost(_Room, _Capacity);
                else
                    _Network.StartClient(_Room, _Capacity);
            }
            catch (Exception exception)
            {
                _ConnectionFailure = "Unable to start the connection: " + exception.Message;
            }

            var deadline = Time.unscaledTime + _AdmissionTimeout;
            while (_ConnectionFailure == null && !_Network.HasLocalCharacter && Time.unscaledTime < deadline)
            {
                if (_Network.IsAdmitted && State != HotelSessionState.AwaitingPlayer)
                    SetState(HotelSessionState.AwaitingPlayer, "Preparing your player...");
                yield return null;
            }
            if (version != _OperationVersion)
                yield break;
            if (_ConnectionFailure != null || !_Network.IsAdmitted || !_Network.HasLocalCharacter)
            {
                yield return FailSession(_ConnectionFailure ?? "Host unavailable or lobby admission timed out.");
                yield break;
            }

            SetState(HotelSessionState.Connected, "Connected to " + Code);
            if (_Network.IsHost)
                _Heartbeat = StartCoroutine(KeepRoomAlive(version));
            _Operation = null;
        }

        public void LeaveLobby()
        {
            if (_Network == null)
                return;
            CancelOperation();
            _Operation = StartCoroutine(LeaveSession());
        }

        IEnumerator LeaveSession()
        {
            yield return CloseSession();
            SetState(HotelSessionState.Idle, "Disconnected.");
            _NextAutoAttempt = Time.unscaledTime + _RetryInterval;
            _Operation = null;
        }

        IEnumerator CloseSession()
        {
            SetState(HotelSessionState.Leaving, "Disconnecting...");
            if (_Heartbeat != null)
            {
                StopCoroutine(_Heartbeat);
                _Heartbeat = null;
            }
            // Mirage must finish its current disconnect callback before another stop is issued.
            yield return null;
            var previousRoom = _Room;
            _Room = null;
            Session.InputFocused = false;
            _Network.Stop();
            if (previousRoom != null && !string.IsNullOrEmpty(previousRoom.ownerKey))
            {
                var payload = JsonUtility.ToJson(new RoomHeartbeat { ownerKey = previousRoom.ownerKey });
                yield return Request("/rooms/" + previousRoom.code, "DELETE", payload, IgnoreRoomResponse);
            }
            if (_Relay)
                yield return _Relay.EndSession();
            _SceneTravel.SessionEnded();
        }

        IEnumerator FailSession(string message)
        {
            yield return CloseSession();
            SetState(HotelSessionState.Failed, message);
            _NextAutoAttempt = Time.unscaledTime + _RetryInterval;
            _Operation = null;
        }

        IEnumerator KeepRoomAlive(int version)
        {
            while (version == _OperationVersion && IsHost && _Room != null)
            {
                yield return new WaitForSecondsRealtime(5f);
                if (version != _OperationVersion || !IsHost || _Room == null)
                    yield break;
                string failure = null;
                var payload = new RoomHeartbeat { ownerKey = _Room.ownerKey, players = Mathf.Max(1, _Network.PlayerCount) };
                yield return Request("/rooms/" + Code + "/heartbeat", "POST", JsonUtility.ToJson(payload),
                    (value, error) => failure = error);
                if (version == _OperationVersion && failure != null)
                {
                    _Heartbeat = null;
                    _Operation = StartCoroutine(FailSession(failure));
                    yield break;
                }
            }
        }

        IEnumerator Request(string path, string method, string payload, Action<Room, string> completed)
        {
            var version = _OperationVersion;
            var token = _Relay ? _Relay.AuthorizationToken : null;
            yield return HotelRoomDirectory.Request(_DirectoryUrl, token, path, method, payload, (result, error) =>
            {
                if (version != _OperationVersion)
                    return;
                if (error != null)
                    Debug.LogWarning("Room directory " + method + " request failed: " + error, this);
                completed(result, error);
            });
        }

        void CancelOperation()
        {
            ++_OperationVersion;
            if (_Operation != null)
            {
                StopCoroutine(_Operation);
                _Operation = null;
            }
        }

        void Update()
        {
            if (_Network == null || !_OwnsSession)
                return;
            _Network.Tick();
            var waitingForRoom = State == HotelSessionState.Idle || State == HotelSessionState.Failed;
            if (_AutoCreateOnStart && waitingForRoom && !_Network.IsRunning && !_SceneTravel.Loading &&
                Time.unscaledTime >= _NextAutoAttempt && SceneManager.GetActiveScene().name == "Lobby")
                CreateLobby();
        }

        void OnConnectionLost(string message)
        {
            if (State == HotelSessionState.Leaving)
                return;
            _ConnectionFailure = message;
            if (State == HotelSessionState.Connected)
            {
                CancelOperation();
                _Operation = StartCoroutine(FailSession(message));
            }
        }

        void SetState(HotelSessionState state, string message)
        {
            State = state;
            Status = message;
            PublishState();
        }

        public void SetStatus(string message)
        {
            Status = message;
            PublishState();
        }

        void PublishState()
        {
            Session.Connected = State == HotelSessionState.Connected;
            StateChanged?.Invoke();
        }

        public void SetInputFocused(bool focused) => Session.InputFocused = focused;
        string GetRoomCode() => Code;
        void OnPlayerAdmitted(INetworkPlayer player) => PlayerAdmitted?.Invoke(player);
        void OnPlayerLeft(INetworkPlayer player) => PlayerLeft?.Invoke(player);
        Vector3 SpawnPosition(int index) => _SceneBindings ? _SceneBindings.SpawnPosition(index) : new Vector3(index * 2.2f, 1.1f, 2f);
        internal void BindDialogue(DialogueManager service) => _Dialogue = service;

        void PrepareTravel()
        {
            if (_Dialogue && _Dialogue.IsSharedDialogue)
                _Dialogue.ExitDialogMode();
        }
        static void IgnoreRoomResponse(Room result, string error) { }

        static string LocalAddress()
        {
            try
            {
                foreach (var address in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                        return address.ToString();
                }
            }
            catch (SocketException) { }
            return "127.0.0.1";
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_OwnsSession)
                return;
            if (mode == LoadSceneMode.Single)
                _SceneBindings = null;
            BindScene(scene);
        }

        void BindScene(Scene scene)
        {
            // Scene entry is the only discovery boundary. Each root supplies serialized dependencies.
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent<HauntedHotelMultiplayer>(out var duplicate) && duplicate != this)
                    duplicate.RetireAuthoredSession();
                if (root.TryGetComponent<HotelSceneBindings>(out var bindings))
                {
                    if (bindings.HasSpawnDefinition)
                        _SceneBindings = bindings;
                    bindings.Bind(this);
                }
            }
        }

        void RetireAuthoredSession()
        {
            if (_Server)
                Destroy(_Server.transform.root.gameObject);
            if (_Client && (!_Server || _Client.transform.root != _Server.transform.root))
                Destroy(_Client.transform.root.gameObject);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            CancelOperation();
            if (_Heartbeat != null)
                StopCoroutine(_Heartbeat);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_Network == null)
                return;
            _Network.PlayerAdmitted -= OnPlayerAdmitted;
            _Network.PlayerLeft -= OnPlayerLeft;
            _Network.ConnectionLost -= OnConnectionLost;
            _Network.Dispose();
            _SceneTravel.StateChanged -= PublishState;
            _SceneTravel.Unconfigure();
            Session.Connected = false;
            Session.InputFocused = false;
        }
    }
}
