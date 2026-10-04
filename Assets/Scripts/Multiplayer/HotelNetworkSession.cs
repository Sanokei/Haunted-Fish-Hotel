using System;
using System.Collections.Generic;
using System.Linq;
using Mirage;
using Mirage.SocketLayer;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // Owns Mirage subscriptions, room admission and network prefab lifetime.
    // The composition root supplies every dependency before a peer is started.
    internal sealed class HotelNetworkSession : IDisposable
    {
        readonly NetworkServer _Server;
        readonly NetworkClient _Client;
        readonly ServerObjectManager _ServerObjects;
        readonly ClientObjectManager _ClientObjects;
        readonly NetworkIdentity _PlayerPrefab;
        readonly NetworkIdentity _DialoguePrefab;
        readonly IEnumerable<NetworkIdentity> _AdditionalPrefabs;
        readonly HotelSessionContext _Context;
        readonly HauntedHotelMessageTravel _Travel;
        readonly Func<int, Vector3> _SpawnPosition;
        readonly HashSet<INetworkPlayer> _Admitted = new HashSet<INetworkPlayer>();
        readonly Dictionary<INetworkPlayer, float> _Pending = new Dictionary<INetworkPlayer, float>();
        readonly Dictionary<NetworkIdentity, HotelPlayer> _Characters = new Dictionary<NetworkIdentity, HotelPlayer>();
        Room _Room;
        int _Capacity;

        public event Action<INetworkPlayer> PlayerAdmitted;
        public event Action<INetworkPlayer> PlayerLeft;
        public event Action<string> ConnectionLost;
        public bool IsHost => _Server.Active;
        public bool IsRunning => _Server.Active || _Client.Active;
        public bool IsAdmitted { get; private set; }
        public int PlayerCount => _Admitted.Count;
        public bool HasLocalCharacter => _Client.Player != null && _Client.Player.HasCharacter &&
            _Client.Player.Identity.IsSpawned && _Client.Player.Identity.gameObject.activeInHierarchy;

        public HotelNetworkSession(NetworkServer server, NetworkClient client,
            ServerObjectManager serverObjects, ClientObjectManager clientObjects,
            SocketFactory socket, NetworkIdentity playerPrefab, NetworkIdentity dialoguePrefab,
            IEnumerable<NetworkIdentity> additionalPrefabs, HotelSessionContext context,
            HauntedHotelMessageTravel travel, Func<int, Vector3> spawnPosition)
        {
            this._Server = server;
            this._Client = client;
            this._ServerObjects = serverObjects;
            this._ClientObjects = clientObjects;
            this._PlayerPrefab = playerPrefab;
            this._DialoguePrefab = dialoguePrefab;
            this._AdditionalPrefabs = additionalPrefabs;
            this._Context = context;
            this._Travel = travel;
            this._SpawnPosition = spawnPosition;

            server.ObjectManager = serverObjects;
            client.ObjectManager = clientObjects;
            server.SocketFactory = socket;
            client.SocketFactory = socket;
            server.Started.AddListener(RegisterServerMessages);
            server.Authenticated.AddListener(OnServerAuthenticated);
            server.Disconnected.AddListener(OnServerDisconnected);
            server.Stopped.AddListener(OnServerStopped);
            client.Started.AddListener(RegisterClientMessages);
            client.Authenticated.AddListener(OnClientAuthenticated);
            client.Disconnected.AddListener(OnClientDisconnected);
        }

        public void StartHost(Room room, int capacity)
        {
            SetRoom(room, capacity);
            _Server.MaxConnections = capacity;
            _Server.StartServer(_Client);
            var dialogue = UnityEngine.Object.Instantiate(_DialoguePrefab);
            UnityEngine.Object.DontDestroyOnLoad(dialogue.gameObject);
            _ServerObjects.Spawn(dialogue);
        }

        public void StartClient(Room room, int capacity)
        {
            SetRoom(room, capacity);
            _Client.Connect(room.address, (ushort)room.port);
        }

        void SetRoom(Room value, int limit)
        {
            _Room = value;
            _Capacity = limit;
            IsAdmitted = false;
        }

        public void Stop()
        {
            _Room = null;
            IsAdmitted = false;
            if (_Server.Active)
                _Server.Stop();
            else if (_Client.Active)
                _Client.Disconnect();
            _Admitted.Clear();
            _Pending.Clear();
            _Characters.Clear();
        }

        public void Tick()
        {
            foreach (var entry in _Pending.ToArray())
            {
                if (Time.unscaledTime <= entry.Value)
                    continue;
                _Pending.Remove(entry.Key);
                entry.Key.Disconnect();
            }
        }

        void RegisterServerMessages()
        {
            _Server.MessageHandler.RegisterHandler<LobbyHello>(OnHello, allowUnauthenticated: false);
        }

        void RegisterClientMessages()
        {
            _Client.MessageHandler.RegisterHandler<LobbyWelcome>(OnWelcome, allowUnauthenticated: false);
            _Client.MessageHandler.RegisterHandler<SharedWorldCue>(OnWorldCue, allowUnauthenticated: false);
            _ClientObjects.UnregisterSpawnHandler(_PlayerPrefab.PrefabHash);
            _ClientObjects.UnregisterSpawnHandler(_DialoguePrefab.PrefabHash);
            _ClientObjects.RegisterSpawnHandler(_PlayerPrefab, SpawnClientPlayer, DestroyClientPlayer);
            _ClientObjects.RegisterPrefab(_DialoguePrefab);
            foreach (var prefab in _AdditionalPrefabs)
            {
                if (prefab)
                    _ClientObjects.RegisterPrefab(prefab);
            }
        }

        void OnServerAuthenticated(INetworkPlayer player)
        {
            if (!_Admitted.Contains(player))
                _Pending[player] = Time.unscaledTime + 8f;
        }

        void OnClientAuthenticated(INetworkPlayer player)
        {
            if (_Room == null)
                return;
            _Client.Send(new LobbyHello
            {
                Code = _Room.code,
                JoinKey = _Room.joinKey,
                Reservation = _Room.reservation
            });
        }

        void OnHello(INetworkPlayer player, LobbyHello hello)
        {
            if (_Admitted.Contains(player))
                return;
            var error = ValidateInvitation(hello);
            if (error != null)
            {
                player.Send(new LobbyWelcome { Accepted = false, Message = error });
                _Pending[player] = Time.unscaledTime + .25f;
                return;
            }

            _Admitted.Add(player);
            _Pending.Remove(player);
            player.Send(new LobbyWelcome { Accepted = true, Message = "Connected" });
            var identity = UnityEngine.Object.Instantiate(_PlayerPrefab, _SpawnPosition(_Admitted.Count - 1), Quaternion.identity);
            BindCharacter(identity);
            _Travel.Admit(player);
            _ServerObjects.AddCharacter(player, identity);
            PlayerAdmitted?.Invoke(player);
        }

        string ValidateInvitation(LobbyHello hello)
        {
            if (_Room == null || hello.Code != _Room.code || hello.JoinKey != _Room.joinKey)
                return "This lobby is unavailable or the invitation has expired.";
            if (_Admitted.Count >= _Capacity)
                return "Lobby is full.";
            return null;
        }

        void OnWelcome(INetworkPlayer player, LobbyWelcome message)
        {
            if (message.Accepted)
                IsAdmitted = true;
            else
                ConnectionLost?.Invoke(message.Message);
        }

        static void OnWorldCue(INetworkPlayer player, SharedWorldCue cue)
        {
            Monologue.Dialogue.StoryFunctions.ApplyNetworkCue(cue);
        }

        void OnClientDisconnected(ClientStoppedReason reason)
        {
            IsAdmitted = false;
            _Characters.Clear();
            ConnectionLost?.Invoke("Host unavailable or connection lost.");
        }

        void OnServerDisconnected(INetworkPlayer player)
        {
            if (player.HasCharacter)
            {
                _Characters.Remove(player.Identity);
                _ServerObjects.DestroyCharacter(player);
            }
            _Pending.Remove(player);
            if (_Admitted.Remove(player))
                PlayerLeft?.Invoke(player);
        }

        void OnServerStopped()
        {
            _Admitted.Clear();
            _Pending.Clear();
            _Characters.Clear();
        }

        NetworkIdentity SpawnClientPlayer(SpawnMessage message)
        {
            var identity = UnityEngine.Object.Instantiate(_PlayerPrefab);
            BindCharacter(identity);
            return identity;
        }

        void BindCharacter(NetworkIdentity identity)
        {
            var character = identity.GetComponent<HotelPlayer>();
            character.Configure(_Context);
            _Characters[identity] = character;
        }

        void DestroyClientPlayer(NetworkIdentity identity)
        {
            _Characters.Remove(identity);
            UnityEngine.Object.Destroy(identity.gameObject);
        }

        public void ResetCharacterMotion()
        {
            foreach (var character in _Characters.Values)
                character.ResetSceneMotion();
        }

        public void PositionCharacters()
        {
            var index = 0;
            foreach (var player in _Server.AuthenticatedPlayers)
            {
                if (player.HasCharacter && _Characters.TryGetValue(player.Identity, out var character))
                    character.Teleport(_SpawnPosition(index++));
            }
        }

        public void Dispose()
        {
            _Server.Started.RemoveListener(RegisterServerMessages);
            _Server.Authenticated.RemoveListener(OnServerAuthenticated);
            _Server.Disconnected.RemoveListener(OnServerDisconnected);
            _Server.Stopped.RemoveListener(OnServerStopped);
            _Client.Started.RemoveListener(RegisterClientMessages);
            _Client.Authenticated.RemoveListener(OnClientAuthenticated);
            _Client.Disconnected.RemoveListener(OnClientDisconnected);
            Stop();
            _Characters.Clear();
        }
    }
}
