using System;
using System.Collections;
using System.Collections.Generic;
using Mirage;
using Mirage.SocketLayer;
using Monologue.Dialogue;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HauntedFish.Multiplayer
{
    // Authored composition root: binds scene/UI adapters to the session and host handoff services.
    public sealed class HauntedHotelMultiplayer : MonoBehaviour, IHotelSessionRuntime, IHotelSessionScene
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

        MemberMenu _MemberMenu;
        bool _DeskInputFocused;
        public bool MemberMenuOpen { get; private set; }
        public bool IntroductionPlaying { get; private set; }
        HotelNetworkSession _Network;
        IHotelScene _Scene;
        DialogueManager _Dialogue;
        HotelSessionLifecycle _Lifecycle;
        bool _OwnsSession;
        HotelHostMigration _HostMigration;

        public HotelSessionContext Session { get; } = new HotelSessionContext();
        public HotelSessionState State => _Lifecycle?.State ?? HotelSessionState.Idle;
        public string Status => _Lifecycle?.Status ?? "Connecting...";
        public string Code => _Lifecycle?.Code ?? string.Empty;
        public HauntedHotelMessageTravel SceneTravel => _SceneTravel;
        public int PlayerCount => _Network?.PlayerCount ?? 0;
        public bool IsHost => _Network != null && _Network.IsHost;
        public HotelLobbyPlayers LobbyPlayers => _Network?.LobbyPlayers;
        public bool IsLobbyLeader => _Network != null && _Network.LobbyPlayers.IsLeader;
        public LobbyRoster Roster => _Network != null ? _Network.LobbyPlayers.Roster : default;
        public uint TransportHostId => Roster.TransportHost;
        Func<HotelPlayer, bool> _ReadyZone;
        public void BindReadyZone(Func<HotelPlayer, bool> isInReadyZone)
        {
            _ReadyZone = isInReadyZone;
            _Network?.LobbyPlayers.BindReadyZone(_ReadyZone);
        }
        public void LobbyCommand(LobbyAction action, uint target = 0) => _Network?.LobbyPlayers.Command(action, target);
        public bool ReadyToPlay => _Lifecycle != null && _Lifecycle.ReadyToPlay;
        public bool Busy => _Lifecycle != null && _Lifecycle.Busy;
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
                Debug.LogError("Assign the authored hotel network, travel and player dependencies.", this);
                enabled = false;
                return;
            }
            _Network = new HotelNetworkSession(_Server, _Client, _ServerObjects, _ClientObjects, _Socket,
                _PlayerPrefab, _DialoguePrefab, _AdditionalPrefabs, Session, _SceneTravel, SpawnPosition);
            _Lifecycle = new HotelSessionLifecycle(_Network,
                new UnityHotelSessionTransport(() => _Socket, () => _DirectoryUrl),
                new UnityHotelSessionDirectory(() => _DirectoryUrl, () => _Relay ? _Relay.AuthorizationToken : null, this),
                this, this, _Port, _Capacity, _RetryInterval, _AdmissionTimeout);
            _Lifecycle.Changed += PublishState;
            _Network.LobbyPlayers.AllowEditorSolo = Application.isEditor;
            _Network.LobbyPlayers.BindReadyZone(_ReadyZone);
            _Network.PlayerAdmitted += OnPlayerAdmitted;
            _Network.PlayerLeft += OnPlayerLeft;
            _Network.ConnectionLost += OnConnectionLost;
            _Network.LobbyPlayers.RosterChanged += PublishState;
            _HostMigration = new HotelHostMigration(this, _Network, _Lifecycle,
                () => _Socket, () => _DirectoryUrl, AdoptHostSocket, _Port, _Capacity);
            _SceneTravel.Configure(Session, new HotelTravelCallbacks(
                reportStatus: SetStatus,
                roomCode: GetRoomCode,
                beforeLoad: PrepareTravel,
                resetMotion: _Network.ResetCharacterMotion,
                positionCharacters: _Network.PositionCharacters) { CanStartGame = () => _Network.LobbyPlayers.CanStartGame });
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
            _MemberMenu = Instantiate(Resources.Load<MemberMenu>("MemberMenu"));
            DontDestroyOnLoad(_MemberMenu.gameObject);
            _MemberMenu.Bind(this);
            Application.runInBackground = true;
            for (var index = 0; index < SceneManager.sceneCount; ++index)
                BindScene(SceneManager.GetSceneAt(index));
            if (_AutoCreateOnStart)
                CreateLobby();
        }

        public void CreateLobby() => _Lifecycle?.CreateLobby();
        public void JoinLobby(string invitation) => _Lifecycle?.JoinLobby(invitation);
        public void StartPrivateRoom() => _Lifecycle?.StartPrivateRoom();

        public void LeaveLobby()
        {
            if (_Lifecycle == null) return;
            _HostMigration.Cancel();
            _Lifecycle.LeaveLobby();
        }

        void Update()
        {
            if (_Network == null || !_OwnsSession)
                return;
            _Network.Tick();
            if (_AutoCreateOnStart && !_HostMigration.OperationActive && _Lifecycle.RetryDue && SceneManager.GetActiveScene().name == "Lobby")
                CreateLobby();
        }

        void OnConnectionLost(string message)
        {
            if (!_HostMigration.ConnectionLost(message)) _Lifecycle.ConnectionLost(message);
        }

        void AdoptHostSocket(SocketFactory socket, int capacity)
        {
            _Socket = socket;
            _Capacity = capacity;
            _Server.SocketFactory = socket;
            _Client.SocketFactory = socket;
        }

        public void SetStatus(string message) => _Lifecycle?.SetStatus(message);

        void PublishState()
        {
            Session.Connected = State == HotelSessionState.Connected;
            StateChanged?.Invoke();
        }

        float IHotelSessionRuntime.Time => Time.unscaledTime;
        object IHotelSessionRuntime.Start(IEnumerator routine) => StartCoroutine(routine);
        void IHotelSessionRuntime.Stop(object handle) => StopCoroutine((Coroutine)handle);
        object IHotelSessionRuntime.Delay(float seconds) => new WaitForSecondsRealtime(seconds);
        bool IHotelSessionScene.Loading => _SceneTravel.Loading;
        bool IHotelSessionScene.IsEditor => Application.isEditor;
        string IHotelSessionScene.TargetScene => _SceneTravel.TargetScene;
        void IHotelSessionScene.ResetInputFocus() => Session.SetInputFocused(false);
        void IHotelSessionScene.SessionEnded() => _SceneTravel.SessionEnded();
        void IHotelSessionScene.PrepareIntroduction()
        {
            foreach (var manager in FindObjectsByType<LobbyManager>(FindObjectsSortMode.None))
                manager.PrepareIntroduction();
        }

        public void SetInputFocused(bool focused)
        {
            _DeskInputFocused = focused;
            Session.SetInputFocused(focused || MemberMenuOpen || IntroductionPlaying);
        }
        public void SetMemberMenuOpen(bool open)
        {
            if (MemberMenuOpen == open) return;
            MemberMenuOpen = open;
            Session.SetInputFocused(open || _DeskInputFocused || IntroductionPlaying);
            StateChanged?.Invoke();
        }
        public void SetIntroductionPlaying(bool playing)
        {
            if (IntroductionPlaying == playing) return;
            IntroductionPlaying = playing;
            Session.SetInputFocused(playing || MemberMenuOpen || _DeskInputFocused);
        }

        string GetRoomCode() => Code;
        void OnPlayerAdmitted(INetworkPlayer player) => PlayerAdmitted?.Invoke(player);
        void OnPlayerLeft(INetworkPlayer player) => PlayerLeft?.Invoke(player);
        Vector3 SpawnPosition(int index)
        {
            if (_Scene == null || (_Scene is UnityEngine.Object owner && !owner))
                throw new InvalidOperationException("The loaded scene must provide an IHotelScene before spawning players.");
            return _Scene.SpawnPosition(index);
        }
        internal void BindDialogue(DialogueManager service) => _Dialogue = service;

        void PrepareTravel()
        {
            if (_Dialogue && _Dialogue.IsSharedDialogue)
                _Dialogue.ExitDialogMode();
            ExitScene();
        }

        void ExitScene()
        {
            if (_Scene != null && !(_Scene is UnityEngine.Object owner && !owner))
                _Scene.Exit();
            _Scene = null;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_OwnsSession)
                return;
            if (mode == LoadSceneMode.Single)
            {
                ExitScene();
                _Dialogue = null;
            }
            BindScene(scene);
        }

        void BindScene(Scene scene)
        {
            // Discover scene services once at scene entry and bind them directly to the session.
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent<HauntedHotelMultiplayer>(out var duplicate) && duplicate != this)
                    duplicate.RetireAuthoredSession();
                foreach (var dialogue in root.GetComponentsInChildren<DialogueManager>(true))
                    BindDialogue(dialogue);
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (!(component is IHotelScene behavior))
                        continue;
                    if (_Scene != null && !ReferenceEquals(_Scene, behavior))
                        throw new InvalidOperationException("Only one hotel scene behavior can own the session at a time.");
                    _Scene = behavior;
                    behavior.Enter(this);
                }
                foreach (var screen in root.GetComponentsInChildren<HotelConnectionScreen>(true))
                    screen.Bind(this);
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
            ExitScene();
            if (_MemberMenu) Destroy(_MemberMenu.gameObject);
            if (_Lifecycle != null)
            {
                _Lifecycle.Changed -= PublishState;
                _Lifecycle.Dispose();
            }
            _HostMigration?.Dispose();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_Network == null)
                return;
            _Network.PlayerAdmitted -= OnPlayerAdmitted;
            _Network.PlayerLeft -= OnPlayerLeft;
            _Network.ConnectionLost -= OnConnectionLost;
            _Network.LobbyPlayers.RosterChanged -= PublishState;
            _Network.Dispose();
            _SceneTravel.StateChanged -= PublishState;
            _SceneTravel.Unconfigure();
            Session.Connected = false;
            Session.SetInputFocused(false);
        }
    }
}
