using System;
using System.Collections.Generic;
using System.Linq;
using Mirage;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // A successful admission emits one connection event. Rejected or duplicate requests never create a lobby player.
    internal sealed class HotelConnection : IDisposable
    {
        readonly NetworkServer _Server;
        readonly NetworkClient _Client;
        readonly HashSet<INetworkPlayer> _Players = new HashSet<INetworkPlayer>();
        readonly Dictionary<INetworkPlayer, float> _Pending = new Dictionary<INetworkPlayer, float>();
        readonly List<INetworkPlayer> _Expired = new List<INetworkPlayer>();
        Room _Room;
        int _Capacity;
        public IReadOnlyCollection<INetworkPlayer> Players => _Players;
        public bool IsAdmitted { get; private set; }
        public event Action<INetworkPlayer> Connected;
        public event Action<INetworkPlayer> Disconnected;
        public event Action<string> ConnectionLost;
        public event Action Resetting;

        public HotelConnection(NetworkServer server, NetworkClient client)
        {
            _Server = server;
            _Client = client;
            server.Started.AddListener(RegisterServerMessages);
            server.Authenticated.AddListener(AwaitConnection);
            server.Disconnected.AddListener(OnDisconnection);
            server.Stopped.AddListener(Reset);
            client.Started.AddListener(RegisterClientMessages);
            client.Authenticated.AddListener(RequestConnection);
            client.Disconnected.AddListener(OnConnectionLost);
        }

        public void Begin(Room room, int capacity)
        {
            _Room = room;
            _Capacity = capacity;
            IsAdmitted = false;
        }

        void RegisterServerMessages() => _Server.MessageHandler.RegisterHandler<LobbyConnectionRequest>(OnConnection, allowUnauthenticated: false);
        void RegisterClientMessages() => _Client.MessageHandler.RegisterHandler<LobbyConnectionResult>(HandleConnectionResult, allowUnauthenticated: false);

        void AwaitConnection(INetworkPlayer player)
        {
            if (!_Players.Contains(player)) _Pending[player] = Time.unscaledTime + 8f;
        }

        void RequestConnection(INetworkPlayer player)
        {
            if (_Room == null) return;
            _Client.Send(new LobbyConnectionRequest { Code = _Room.code, JoinKey = _Room.joinKey, Reservation = _Room.reservation });
        }

        void OnConnection(INetworkPlayer player, LobbyConnectionRequest request)
        {
            if (_Players.Contains(player)) return;
            string error = null;
            if (_Room == null || request.Code != _Room.code || request.JoinKey != _Room.joinKey)
                error = "This lobby is unavailable or the invitation has expired.";
            else if (_Players.Count >= _Capacity)
                error = "Lobby is full.";
            if (error != null)
            {
                player.Send(new LobbyConnectionResult { Accepted = false, Message = error });
                _Pending[player] = Time.unscaledTime + .25f;
                return;
            }
            _Players.Add(player);
            _Pending.Remove(player);
            player.Send(new LobbyConnectionResult { Accepted = true, Message = "Connected" });
            Connected?.Invoke(player);
        }

        void HandleConnectionResult(INetworkPlayer player, LobbyConnectionResult result)
        {
            IsAdmitted = result.Accepted;
            if (!result.Accepted) ConnectionLost?.Invoke(result.Message);
        }

        void OnDisconnection(INetworkPlayer player)
        {
            _Pending.Remove(player);
            if (_Players.Remove(player)) Disconnected?.Invoke(player);
        }

        void OnConnectionLost(ClientStoppedReason reason)
        {
            if (!_Server.Active) Reset();
            IsAdmitted = false;
            ConnectionLost?.Invoke("Host unavailable or connection lost.");
        }

        public void Tick()
        {
            _Expired.Clear();
            foreach (var entry in _Pending)
            {
                if (Time.unscaledTime <= entry.Value) continue;
                _Expired.Add(entry.Key);
            }
            foreach (var player in _Expired)
                if (_Pending.Remove(player)) player.Disconnect();
        }

        public void Reset()
        {
            var hadConnection = _Room != null || _Players.Count > 0 || IsAdmitted;
            _Room = null;
            IsAdmitted = false;
            _Players.Clear();
            _Pending.Clear();
            if (hadConnection) Resetting?.Invoke();
        }

        public void Dispose()
        {
            _Server.Started.RemoveListener(RegisterServerMessages);
            _Server.Authenticated.RemoveListener(AwaitConnection);
            _Server.Disconnected.RemoveListener(OnDisconnection);
            _Server.Stopped.RemoveListener(Reset);
            _Client.Started.RemoveListener(RegisterClientMessages);
            _Client.Authenticated.RemoveListener(RequestConnection);
            _Client.Disconnected.RemoveListener(OnConnectionLost);
            Reset();
        }
    }
}
