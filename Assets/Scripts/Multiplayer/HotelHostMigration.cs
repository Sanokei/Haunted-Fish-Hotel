using System;
using System.Collections;
using Mirage.SocketLayer;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // Owns the prepared host transport and lease, and coordinates reliable handoff.
    // Regular create/join/leave policy stays in HotelSessionLifecycle.
    internal sealed class HotelHostMigration : IDisposable
    {
        readonly MonoBehaviour _Owner;
        readonly HotelNetworkSession _Network;
        readonly HotelSessionLifecycle _Lifecycle;
        readonly Func<SocketFactory> _GetSocket;
        readonly Func<string> _GetDirectoryUrl;
        readonly Action<SocketFactory, int> _AdoptSocket;
        readonly int _Port, _Capacity;
        SocketFactory _PreparedHostSocket;
        Room _PreparedHostRoom;
        Coroutine _HostTransfer, _PreparedLease;
        bool _TransferringHost, _SwitchingHost;
        SocketFactory _Socket => _GetSocket();
        string _DirectoryUrl => _GetDirectoryUrl();
        LobbyRoster Roster => _Network.LobbyPlayers.Roster;
        HotelSessionState State => _Lifecycle.State;
        public bool OperationActive => _HostTransfer != null;

        public HotelHostMigration(MonoBehaviour owner, HotelNetworkSession network, HotelSessionLifecycle lifecycle,
            Func<SocketFactory> socket, Func<string> directoryUrl, Action<SocketFactory, int> adoptSocket, int port, int capacity)
        {
            _Owner = owner;
            _Network = network;
            _Lifecycle = lifecycle;
            _GetSocket = socket;
            _GetDirectoryUrl = directoryUrl;
            _AdoptSocket = adoptSocket;
            _Port = port;
            _Capacity = capacity;
            _Network.LobbyPlayers.HostPreparing += OnHostPreparing;
            _Network.LobbyPlayers.HostSwitching += OnHostSwitching;
            _Network.LobbyPlayers.HostCancelled += OnHostCancelled;
        }

        public bool ConnectionLost(string message)
        {
            if (_SwitchingHost) return true;
            if (!_TransferringHost) return false;
            Cancel();
            _Lifecycle.Fail(message);
            return true;
        }

        public void Cancel()
        {
            if (_HostTransfer != null) { _Owner.StopCoroutine(_HostTransfer); _HostTransfer = null; }
            _TransferringHost = _SwitchingHost = false;
            _Owner.StartCoroutine(ClearPreparedHost());
        }

        void OnHostPreparing(bool becomingHost)
        {
            if (_TransferringHost || State != HotelSessionState.Connected) return;
            _TransferringHost = true;
            _Lifecycle.SetState(HotelSessionState.PreparingTransport, "Transferring host...");
            if (becomingHost) _HostTransfer = _Owner.StartCoroutine(PrepareNewHost());
        }

        IEnumerator PrepareNewHost()
        {
            // Prepare a distinct socket while the old connection remains available for the handoff.
            var root = new GameObject("Prepared Lobby Host Transport");
            root.transform.SetParent(_Owner.transform, false);
            _PreparedHostSocket = root.AddComponent(_Socket.GetType()) as SocketFactory;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(_Socket), _PreparedHostSocket);
            if (_PreparedHostSocket is IHasPort port) port.Port = _Port;
            var relay = _PreparedHostSocket as HotelTurnSocketFactory;
            var address = UnityHotelSessionTransport.LocalAddress();
            var endpointPort = _Port;
            if (relay)
            {
                yield return relay.Prepare(_DirectoryUrl);
                if (!relay.Ready)
                {
                    _Network.LobbyPlayers.HostPrepared(string.Empty);
                    yield break;
                }
                address = relay.RelayEndPoint.Address.ToString();
                endpointPort = relay.RelayEndPoint.Port;
            }
            string failure = null;
            yield return HotelRoomDirectory.Request(_DirectoryUrl, relay ? relay.AuthorizationToken : null,
                "/rooms", "POST", JsonUtility.ToJson(new CreateRoom { address = address, port = endpointPort, capacity = _Lifecycle.Room != null ? _Lifecycle.Room.capacity : _Capacity }),
                (room, error) => { _PreparedHostRoom = room; failure = error; });
            if (failure != null || _PreparedHostRoom == null)
                _Network.LobbyPlayers.HostPrepared(string.Empty);
            else
            {
                _PreparedLease = _Owner.StartCoroutine(KeepPreparedHostAlive());
                _Network.LobbyPlayers.HostPrepared(_PreparedHostRoom.code);
            }
            _HostTransfer = null;
        }

        IEnumerator KeepPreparedHostAlive()
        {
            while (_PreparedHostRoom != null && _PreparedHostSocket)
            {
                yield return new WaitForSecondsRealtime(5f);
                var room = _PreparedHostRoom;
                var relay = _PreparedHostSocket as HotelTurnSocketFactory;
                if (room == null || !_PreparedHostSocket) yield break;
                string failure = null;
                yield return HotelRoomDirectory.Request(_DirectoryUrl, relay ? relay.AuthorizationToken : null,
                    "/rooms/" + room.code + "/heartbeat", "POST",
                    JsonUtility.ToJson(new RoomHeartbeat { ownerKey = room.ownerKey, players = 1 }),
                    (value, error) => failure = error);
                if (failure != null && !_SwitchingHost)
                {
                    _Network.LobbyPlayers.HostFailed("The new host could not keep its lobby connection alive.");
                    yield break;
                }
            }
        }

        void OnHostCancelled(string message)
        {
            Cancel();
            _Lifecycle.SetState(HotelSessionState.Connected, message);
        }

        IEnumerator ClearPreparedHost()
        {
            if (_PreparedLease != null) { _Owner.StopCoroutine(_PreparedLease); _PreparedLease = null; }
            var room = _PreparedHostRoom;
            var socket = _PreparedHostSocket;
            _PreparedHostRoom = null;
            _PreparedHostSocket = null;
            var relay = socket as HotelTurnSocketFactory;
            if (room != null)
                yield return HotelRoomDirectory.Request(_DirectoryUrl, relay ? relay.AuthorizationToken : null,
                    "/rooms/" + room.code, "DELETE", JsonUtility.ToJson(new RoomHeartbeat { ownerKey = room.ownerKey }), IgnoreRoomResponse);
            if (relay) yield return relay.EndSession();
            if (socket) UnityEngine.Object.Destroy(socket.gameObject);
        }

        void OnHostSwitching(string code, bool becomingHost)
        {
            _SwitchingHost = true;
            _Lifecycle.CancelOperation();
            _HostTransfer = _Owner.StartCoroutine(SwitchHost(code, becomingHost));
        }

        IEnumerator SwitchHost(string code, bool becomingHost)
        {
            var quickplay = Roster.Quickplay;
            var kickedPlayerIds = _Network.LobbyPlayers.TransferKickedPlayerIds;
            // Give the old server time to deliver the reliable switch to every acknowledged peer.
            yield return new WaitForSecondsRealtime(.75f);
            if (!becomingHost)
            {
                yield return _Lifecycle.CloseSession();
                yield return new WaitForSecondsRealtime(1f);
                _TransferringHost = _SwitchingHost = false;
                var version = _Lifecycle.Version;
                var reconnectDeadline = Time.unscaledTime + 60f;
                do
                {
                    yield return _Lifecycle.OpenSession(code, version, quickplay);
                    if (version != _Lifecycle.Version || State == HotelSessionState.Connected) break;
                    _Lifecycle.SetState(HotelSessionState.PreparingTransport, "Waiting for the new host...");
                    yield return new WaitForSecondsRealtime(1f);
                } while (Time.unscaledTime < reconnectDeadline);
                if (version == _Lifecycle.Version && State != HotelSessionState.Connected)
                    _Lifecycle.SetState(HotelSessionState.Failed, "Unable to reconnect to the transferred lobby.");
                _HostTransfer = null;
                yield break;
            }
            if (!_PreparedHostSocket || _PreparedHostRoom == null || _PreparedHostRoom.code != code)
            {
                _TransferringHost = _SwitchingHost = false;
                yield return _Lifecycle.FailSession("The new host transport was unavailable.");
                yield break;
            }
            yield return _Lifecycle.CloseSession();
            var oldSocket = _Socket;
            _AdoptSocket(_PreparedHostSocket, _PreparedHostRoom.capacity);
            var room = _PreparedHostRoom;
            room.quickplay = quickplay;
            _PreparedHostSocket = null;
            _PreparedHostRoom = null;
            if (_PreparedLease != null) { _Owner.StopCoroutine(_PreparedLease); _PreparedLease = null; }
            if (oldSocket) UnityEngine.Object.Destroy(oldSocket);
            yield return _Lifecycle.StartTransferredHost(room, kickedPlayerIds);
            _TransferringHost = _SwitchingHost = false;
            _HostTransfer = null;
        }
        static void IgnoreRoomResponse(Room result, string error) { }

        public void Dispose()
        {
            _Network.LobbyPlayers.HostPreparing -= OnHostPreparing;
            _Network.LobbyPlayers.HostSwitching -= OnHostSwitching;
            _Network.LobbyPlayers.HostCancelled -= OnHostCancelled;
            if (_HostTransfer != null) _Owner.StopCoroutine(_HostTransfer);
            if (_PreparedLease != null) _Owner.StopCoroutine(_PreparedLease);
            if (_PreparedHostSocket) UnityEngine.Object.Destroy(_PreparedHostSocket.gameObject);
            _HostTransfer = _PreparedLease = null;
            _PreparedHostSocket = null;
            _PreparedHostRoom = null;
            _TransferringHost = _SwitchingHost = false;
        }
    }
}
