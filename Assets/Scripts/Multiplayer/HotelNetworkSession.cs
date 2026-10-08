using System;
using System.Collections.Generic;
using Mirage;
using Mirage.SocketLayer;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // Composes connection and lobby services, and owns network prefab lifetime.
    // The composition root supplies every dependency before a peer is started.
    internal sealed class HotelNetworkSession : IDisposable, IHotelSessionNetwork
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
        readonly HotelConnection _Connection;
        readonly HotelLobbyPlayers _LobbyPlayers;
        readonly Dictionary<NetworkIdentity, HotelPlayer> _Characters = new Dictionary<NetworkIdentity, HotelPlayer>();
        public HotelLobbyPlayers LobbyPlayers => _LobbyPlayers;
        public event Action<INetworkPlayer> PlayerAdmitted { add => _Connection.Connected += value; remove => _Connection.Connected -= value; }
        public event Action<INetworkPlayer> PlayerLeft { add => _Connection.Disconnected += value; remove => _Connection.Disconnected -= value; }
        public event Action<string> ConnectionLost { add => _Connection.ConnectionLost += value; remove => _Connection.ConnectionLost -= value; }
        public bool IsHost => _Server.Active;
        public bool IsRunning => _Server.Active || _Client.Active;
        public bool IsAdmitted => _Connection.IsAdmitted;
        public int PlayerCount => _Connection.Players.Count;
        public bool AllReady => _LobbyPlayers.AllReady;
        public bool Quickplay => _LobbyPlayers.Roster.Quickplay;
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
            _Connection = new HotelConnection(server, client);
            _Connection.Connected += OnConnection;
            _Connection.Disconnected += OnDisconnection;
            _LobbyPlayers = new HotelLobbyPlayers(_Connection, server, client, context, travel);
            server.Stopped.AddListener(OnServerStopped);
            client.Started.AddListener(RegisterClientMessages);
            client.Disconnected.AddListener(OnClientDisconnected);
        }

        public void StartHost(Room room, int capacity, IEnumerable<string> kickedPlayerIds = null)
        {
            _Context.RoomScope=(room.code ?? "").ToUpperInvariant();_Context.ConnectionGeneration++;
            _Connection.Begin(room, capacity, kickedPlayerIds);
            _Server.MaxConnections = capacity;
            _Server.StartServer(_Client);
            var dialogue = UnityEngine.Object.Instantiate(_DialoguePrefab);
            UnityEngine.Object.DontDestroyOnLoad(dialogue.gameObject);
            _ServerObjects.Spawn(dialogue);
        }

        public void StartClient(Room room, int capacity)
        {
            _Context.RoomScope=(room.code ?? "").ToUpperInvariant();_Context.ConnectionGeneration++;
            _Connection.Begin(room, capacity);
            _Client.Connect(room.address, (ushort)room.port);
        }

        public void Stop()
        {
            foreach(var character in _Characters.Values) if(character) character.ClearRoundInventory();
            _Context.RoomScope="";_Context.ConnectionGeneration++;
            if (_Server.Active) _Server.Stop();
            else if (_Client.Active) _Client.Disconnect();
            _Connection.Reset();
            _Characters.Clear();
        }

        public void Tick()
        {
            _Connection.Tick();
            _LobbyPlayers.Tick();
        }

        void RegisterClientMessages()
        {
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

        void OnConnection(INetworkPlayer player)
        {
            // During travel the scene behavior is retired. CompleteHostLoad places these players
            // using the destination scene before making their characters visible to peers.
            var position = _Travel.Loading ? default : _SpawnPosition(_Connection.Players.Count - 1);
            var identity = UnityEngine.Object.Instantiate(_PlayerPrefab, position, Quaternion.identity);
            BindCharacter(identity);
            _Travel.Admit(player);
            _ServerObjects.AddCharacter(player, identity);
        }

        void OnDisconnection(INetworkPlayer player)
        {
            if (!player.HasCharacter) return;
            _Characters.Remove(player.Identity);
            _ServerObjects.DestroyCharacter(player);
        }

        static void OnWorldCue(INetworkPlayer player, SharedWorldCue cue)
        {
            Monologue.Dialogue.StoryFunctions.ApplyNetworkCue(cue);
        }

        void OnClientDisconnected(ClientStoppedReason reason) => _Characters.Clear();
        void OnServerStopped() => _Characters.Clear();

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
            _Server.Stopped.RemoveListener(OnServerStopped);
            _Client.Started.RemoveListener(RegisterClientMessages);
            _Client.Disconnected.RemoveListener(OnClientDisconnected);
            Stop();
            _Connection.Connected -= OnConnection;
            _Connection.Disconnected -= OnDisconnection;
            _LobbyPlayers.Dispose();
            _Connection.Dispose();
            _Characters.Clear();
        }
    }
}
