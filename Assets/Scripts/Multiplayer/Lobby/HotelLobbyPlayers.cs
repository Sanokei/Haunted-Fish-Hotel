using System;
using System.Collections.Generic;
using System.Linq;
using Mirage;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // Lobby rules consume admitted connection events; they do not validate invitations or spawn avatars.
    public sealed class HotelLobbyPlayers : IDisposable
    {
        readonly HotelConnection _Connection;
        readonly NetworkServer _Server;
        readonly NetworkClient _Client;
        readonly HotelSessionContext _Context;
        readonly HauntedHotelMessageTravel _Travel;
        readonly Dictionary<INetworkPlayer, HotelLobbyPlayer> _Members = new Dictionary<INetworkPlayer, HotelLobbyPlayer>();
        readonly Dictionary<uint, HotelLobbyPlayer> _Players = new Dictionary<uint, HotelLobbyPlayer>();
        readonly List<LobbyMember> _RosterMembers = new List<LobbyMember>();
        readonly HashSet<uint> _Present = new HashSet<uint>();
        readonly List<uint> _Departed = new List<uint>();
        static readonly Comparison<LobbyMember> _CompareMembers = (first, second) => first.Id.CompareTo(second.Id);
        IReadOnlyCollection<INetworkPlayer> _Admitted => _Connection.Players;
        public IEnumerable<HotelLobbyPlayer> Players => _Players.Values;
        public event Action<HotelLobbyPlayer> PlayerJoined;
        public event Action<HotelLobbyPlayer> PlayerLeft;
        public event Action<HotelLobbyPlayer> PlayerChanged;
        INetworkPlayer _Leader;
        Func<HotelPlayer, bool> _IsInReadyZone;
        float _NextRoster;
        bool _Starting;
        INetworkPlayer _NextHost;
        int _MigrationVersion, _ClientMigrationVersion;
        float _MigrationDeadline;
        LobbyHostCommit _Commit, _ClientCommit;
        readonly HashSet<INetworkPlayer> _MigrationAcks = new HashSet<INetworkPlayer>();
        public string[] TransferKickedPlayerIds { get; private set; } = Array.Empty<string>();
        public event Action<bool> HostPreparing;
        public event Action<string, bool> HostSwitching;
        public event Action<string> HostCancelled;
        public LobbyRoster Roster { get; private set; }
        public event Action RosterChanged;
        public bool IsLeader => _Client.Player != null && _Client.Player.HasCharacter &&
            _Client.Player.Identity.NetId == Roster.Leader;

        public void BindReadyZone(Func<HotelPlayer, bool> isInReadyZone) => _IsInReadyZone = isInReadyZone;
        public void Command(LobbyAction action, uint target = 0)
        {
            if (IsLeader && _Context.Connected && !_Context.Loading)
                _Client.Send(new LobbyCommand { Action = action, Target = target });
        }

        bool IsReady(INetworkPlayer player) => player.HasCharacter && player.SceneIsReady &&
            _IsInReadyZone != null && _IsInReadyZone(player.Identity.GetComponent<HotelPlayer>());
        public bool AllReady => _Members.Count > 0 && _Members.Keys.All(IsReady);
        public bool AllowEditorSolo { get; set; }
        public bool CanStartGame => _Starting && CanStart;
        bool CanStart => AllReady && _Members.Count >= (AllowEditorSolo ? 1 : _Connection.Quickplay ? 4 : 2);

        void StartWhenReady()
        {
            if (!_Server.Active || !_Context.Connected || _Context.Loading || _Starting ||
                _NextHost != null || _Travel.TargetScene != "Lobby" || !_Travel.CanTravel || !CanStart) return;
            _Starting = true;
            try { _Travel.GoToGame(); }
            finally { _Starting = false; }
        }

        void OnCommand(INetworkPlayer sender, LobbyCommand command)
        {
            if (!_Admitted.Contains(sender) || sender != _Leader || !_Context.Connected ||
                _Context.Loading || _NextHost != null) return;
            if (command.Action == LobbyAction.Start)
            {
                StartWhenReady();
                return;
            }
            var target = _Admitted.FirstOrDefault(p => p.HasCharacter && p.Identity.NetId == command.Target);
            if (target == null || target == sender) return;
            if (command.Action == LobbyAction.King && _Travel.TargetScene != "Lobby")
            {
                _Leader = target;
            }
            else if (command.Action == LobbyAction.King)
            {
                _NextHost = target;
                ++_MigrationVersion;
                _Commit = default;
                _MigrationAcks.Clear();
                _MigrationDeadline = Time.unscaledTime + 45f;
                foreach (var member in _Admitted)
                    member.Send(new LobbyHostPrepare { Version = _MigrationVersion, Target = target.Identity.NetId });
            }
            else if (command.Action == LobbyAction.Kick && target != _Server.LocalPlayer) _Connection.Kick(target);
            PublishRoster();
        }

        public void HostPrepared(string code) => _Client.Send(new LobbyHostPrepared { Version = _ClientMigrationVersion, Code = code });
        public void HostFailed(string message) => _Client.Send(new LobbyHostCancel { Version = _ClientMigrationVersion, Message = message });
        void OnHostFailed(INetworkPlayer sender, LobbyHostCancel message)
        {
            if (sender == _NextHost && message.Version == _MigrationVersion && !float.IsPositiveInfinity(_MigrationDeadline))
                CancelHostTransfer("The new host connection failed. The current lobby is still available.");
        }
        void OnHostPrepare(INetworkPlayer sender, LobbyHostPrepare message)
        {
            if (message.Version <= _ClientMigrationVersion) return;
            _ClientMigrationVersion = message.Version;
            _ClientCommit = new LobbyHostCommit { Version = message.Version, Target = message.Target };
            HostPreparing?.Invoke(_Client.Player != null && _Client.Player.HasCharacter && _Client.Player.Identity.NetId == message.Target);
        }
        void OnHostPrepared(INetworkPlayer sender, LobbyHostPrepared message)
        {
            if (sender != _NextHost || message.Version != _MigrationVersion || !string.IsNullOrEmpty(_Commit.Code)) return;
            if (!LobbyCode.TryNormalize(message.Code, out var code))
            {
                CancelHostTransfer("The new host could not prepare its connection.");
                return;
            }
            _Commit = new LobbyHostCommit { Version = message.Version, Target = sender.Identity.NetId, Code = code, KickedPlayerIds = _Connection.KickedPlayerIds };
            _MigrationDeadline = Time.unscaledTime + 8f;
            foreach (var member in _Admitted) member.Send(_Commit);
        }
        void OnHostCommit(INetworkPlayer sender, LobbyHostCommit message)
        {
            if (message.Version != _ClientMigrationVersion || message.Target != _ClientCommit.Target || !LobbyCode.TryNormalize(message.Code, out _)) return;
            _ClientCommit = message;
            TransferKickedPlayerIds = message.KickedPlayerIds ?? Array.Empty<string>();
            _Client.Send(new LobbyHostAck { Version = message.Version });
        }
        void OnHostAck(INetworkPlayer sender, LobbyHostAck message)
        {
            if (_NextHost == null || float.IsPositiveInfinity(_MigrationDeadline) || !_Admitted.Contains(sender) || message.Version != _MigrationVersion || string.IsNullOrEmpty(_Commit.Code)) return;
            _MigrationAcks.Add(sender);
            if (_Admitted.All(p => _MigrationAcks.Contains(p)))
            {
                // Each peer confirms it has the destination before the old host disconnects.
                _MigrationDeadline = float.PositiveInfinity;
                foreach (var member in _Admitted) member.Send(new LobbyHostSwitch { Version = _MigrationVersion });
            }
        }
        void OnHostSwitch(INetworkPlayer sender, LobbyHostSwitch message)
        {
            if (message.Version != _ClientMigrationVersion || string.IsNullOrEmpty(_ClientCommit.Code)) return;
            var host = _Client.Player != null && _Client.Player.HasCharacter && _Client.Player.Identity.NetId == _ClientCommit.Target;
            var code = _ClientCommit.Code;
            _ClientCommit = default;
            HostSwitching?.Invoke(code, host);
        }
        void OnHostCancel(INetworkPlayer sender, LobbyHostCancel message)
        {
            if (message.Version != _ClientMigrationVersion) return;
            _ClientCommit = default;
            HostCancelled?.Invoke(message.Message);
        }
        void CancelHostTransfer(string message)
        {
            if (_NextHost == null) return;
            var cancellation = new LobbyHostCancel { Version = _MigrationVersion, Message = message };
            _NextHost = null;
            _MigrationAcks.Clear();
            _Commit = default;
            foreach (var member in _Admitted) member.Send(cancellation);
        }

        void PublishRoster(bool force = false)
        {
            uint leader = _Leader != null && _Leader.HasCharacter ? _Leader.Identity.NetId : 0;
            uint host = _Server.LocalPlayer != null && _Server.LocalPlayer.HasCharacter ? _Server.LocalPlayer.Identity.NetId : 0;
            _RosterMembers.Clear();
            foreach (var player in _Admitted)
                if (player.HasCharacter)
                    _RosterMembers.Add(new LobbyMember
                    {
                        Id = player.Identity.NetId,
                        Ready = IsReady(player)
                    });
            _RosterMembers.Sort(_CompareMembers);
            if (!force && Roster.Quickplay == _Connection.Quickplay && LobbyRules.RosterMatches(Roster, leader, host, _RosterMembers)) return;
            var roster = new LobbyRoster
            {
                Quickplay = _Connection.Quickplay,
                Leader = leader,
                TransportHost = host,
                Members = _RosterMembers.ToArray()
            };
            OnRoster(null, roster);
            foreach (var player in _Admitted) player.Send(roster);
        }

        void OnRoster(INetworkPlayer sender, LobbyRoster roster)
        {
            var members = roster.Members ?? Array.Empty<LobbyMember>();
            if (Roster.Quickplay == roster.Quickplay && LobbyRules.RosterMatches(Roster, roster.Leader, roster.TransportHost, members)) return;
            Roster = roster;
            _Present.Clear();
            foreach (var member in members)
            {
                _Present.Add(member.Id);
                if (!_Players.TryGetValue(member.Id, out var player))
                {
                    player = _Members.Values.FirstOrDefault(p => p.Id == member.Id) ?? new HotelLobbyPlayer(member.Id);
                    _Players.Add(member.Id, player);
                    player.Apply(member.Ready, member.Id == roster.TransportHost);
                    PlayerJoined?.Invoke(player);
                }
                else if (player.Apply(member.Ready, member.Id == roster.TransportHost))
                    PlayerChanged?.Invoke(player);
            }
            _Departed.Clear();
            foreach (var id in _Players.Keys)
                if (!_Present.Contains(id)) _Departed.Add(id);
            foreach (var id in _Departed)
            {
                var player = _Players[id];
                _Players.Remove(id);
                player.OnDisconnection();
                PlayerLeft?.Invoke(player);
            }
            RosterChanged?.Invoke();
        }

        internal HotelLobbyPlayers(HotelConnection connection, NetworkServer server, NetworkClient client,
            HotelSessionContext context, HauntedHotelMessageTravel travel)
        {
            _Connection = connection;
            _Server = server;
            _Client = client;
            _Context = context;
            _Travel = travel;
            connection.Connected += OnConnection;
            connection.Disconnected += OnDisconnection;
            connection.Resetting += Reset;
            server.Started.AddListener(RegisterServerMessages);
            client.Started.AddListener(RegisterClientMessages);
        }

        void OnConnection(INetworkPlayer connection)
        {
            _Members.Add(connection, new HotelLobbyPlayer(connection));
            if (_Leader == null || connection == _Server.LocalPlayer) _Leader = connection;
            PublishRoster(force: true);
        }

        void OnDisconnection(INetworkPlayer connection)
        {
            _Members.Remove(connection);
            _MigrationAcks.Remove(connection);
            if (_NextHost != null && !float.IsPositiveInfinity(_MigrationDeadline))
                CancelHostTransfer("A player left during host transfer. Try crowning again.");
            if (_Leader == connection)
                _Leader = _Admitted.Contains(_Server.LocalPlayer) ? _Server.LocalPlayer : _Admitted.FirstOrDefault();
            PublishRoster();
        }

        public void Tick()
        {
            if (!_Server.Active) return;
            if (_NextHost != null && Time.unscaledTime > _MigrationDeadline)
                CancelHostTransfer("Host transfer timed out. The current lobby is still available.");
            if (Time.unscaledTime < _NextRoster) return;
            _NextRoster = Time.unscaledTime + .1f;
            PublishRoster();
            StartWhenReady();
        }

        void RegisterServerMessages()
        {
            _Server.MessageHandler.RegisterHandler<LobbyCommand>(OnCommand, allowUnauthenticated: false);
            _Server.MessageHandler.RegisterHandler<LobbyHostPrepared>(OnHostPrepared, allowUnauthenticated: false);
            _Server.MessageHandler.RegisterHandler<LobbyHostAck>(OnHostAck, allowUnauthenticated: false);
            _Server.MessageHandler.RegisterHandler<LobbyHostCancel>(OnHostFailed, allowUnauthenticated: false);
        }

        void RegisterClientMessages()
        {
            _Client.MessageHandler.RegisterHandler<LobbyRoster>(OnRoster, allowUnauthenticated: false);
            _Client.MessageHandler.RegisterHandler<LobbyHostPrepare>(OnHostPrepare, allowUnauthenticated: false);
            _Client.MessageHandler.RegisterHandler<LobbyHostCommit>(OnHostCommit, allowUnauthenticated: false);
            _Client.MessageHandler.RegisterHandler<LobbyHostSwitch>(OnHostSwitch, allowUnauthenticated: false);
            _Client.MessageHandler.RegisterHandler<LobbyHostCancel>(OnHostCancel, allowUnauthenticated: false);
        }

        public void Reset()
        {
            var leaving = _Players.Values.ToArray();
            _Members.Clear();
            _Players.Clear();
            _Leader = _NextHost = null;
            _Starting = false;
            _NextRoster = 0;
            _ClientMigrationVersion = 0;
            _ClientCommit = _Commit = default;
            TransferKickedPlayerIds = Array.Empty<string>();
            _MigrationAcks.Clear();
            Roster = default;
            foreach (var player in leaving)
            {
                player.OnDisconnection();
                PlayerLeft?.Invoke(player);
            }
            RosterChanged?.Invoke();
        }

        public void Dispose()
        {
            _Connection.Connected -= OnConnection;
            _Connection.Disconnected -= OnDisconnection;
            _Connection.Resetting -= Reset;
            _Server.Started.RemoveListener(RegisterServerMessages);
            _Client.Started.RemoveListener(RegisterClientMessages);
            Reset();
        }
    }
}
