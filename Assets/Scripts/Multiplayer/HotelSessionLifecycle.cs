using System;
using System.Collections;
using System.Collections.Generic;

namespace HauntedFish.Multiplayer
{
    // Owns connection operations and the current room lease. Unity schedules the routines;
    // adapters own transport, directory I/O and scene presentation.
    internal sealed class HotelSessionLifecycle : IDisposable
    {
        readonly IHotelSessionNetwork _Network;
        readonly IHotelSessionTransport _Transport;
        readonly IHotelSessionDirectory _Directory;
        readonly IHotelSessionRuntime _Runtime;
        readonly IHotelSessionScene _Scene;
        readonly int _Port;
        readonly float _RetryInterval, _AdmissionTimeout;
        int _Capacity;
        object _Operation, _Heartbeat;
        string _ConnectionFailure;
        float _NextAttempt;
        bool _Disposed;

        public HotelSessionState State { get; private set; } = HotelSessionState.Idle;
        public string Status { get; private set; } = "Connecting...";
        public Room Room { get; private set; }
        public string Code => Room?.code ?? string.Empty;
        public int Version { get; private set; }
        public bool Busy => State != HotelSessionState.Idle && State != HotelSessionState.Connected && State != HotelSessionState.Failed;
        public bool Transitioning => Busy || _Scene.Loading;
        public bool ReadyToPlay => State == HotelSessionState.Connected && !_Scene.Loading;
        public bool RetryDue => !_Disposed && (State == HotelSessionState.Idle || State == HotelSessionState.Failed) &&
            !_Network.IsRunning && !_Scene.Loading && _Runtime.Time >= _NextAttempt;
        public event Action Changed;

        public HotelSessionLifecycle(IHotelSessionNetwork network, IHotelSessionTransport transport,
            IHotelSessionDirectory directory, IHotelSessionRuntime runtime, IHotelSessionScene scene,
            int port, int capacity, float retryInterval, float admissionTimeout)
        {
            _Network = network ?? throw new ArgumentNullException(nameof(network));
            _Transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _Directory = directory ?? throw new ArgumentNullException(nameof(directory));
            _Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _Scene = scene ?? throw new ArgumentNullException(nameof(scene));
            _Port = port;
            _Capacity = capacity;
            _RetryInterval = retryInterval;
            _AdmissionTimeout = admissionTimeout;
        }

        public void CreateLobby()
        {
            if (!_Disposed && !Transitioning && !_Network.IsRunning) BeginSession(null);
        }

        public void JoinLobby(string invitation)
        {
            if (_Disposed || Transitioning) return;
            if (!LobbyCode.TryNormalize(invitation, out var code))
            {
                SetStatus("Enter exactly six letters or digits.");
                return;
            }
            if (string.Equals(code, Code, StringComparison.OrdinalIgnoreCase))
            {
                SetStatus("You are already in this lobby.");
                return;
            }
            BeginSession(code);
        }

        public void StartPrivateRoom()
        {
            if (!_Disposed && !Transitioning && !_Network.IsHost) BeginSession(null);
        }

        void BeginSession(string invitation, bool quickplay = false)
        {
            CancelOperation();
            _ConnectionFailure = null;
            _Operation = _Runtime.Start(OpenSession(invitation, Version, quickplay));
        }

        public IEnumerator OpenSession(string invitation, int version, bool quickplay)
        {
            if (!Current(version)) yield break;
            _ConnectionFailure = null;
            _Scene.PrepareIntroduction();
            if (!Current(version)) yield break;
            if (Room != null || _Network.IsRunning) yield return CloseSession();
            while (_Scene.Loading && Current(version)) yield return null;
            if (!Current(version)) yield break;
            ScheduleRetry();
            if (!_Transport.Supported)
            {
                SetState(HotelSessionState.Failed, "Assign a supported socket with a configurable port.");
                yield break;
            }
            SetState(HotelSessionState.PreparingTransport, "Connecting...");
            if (!Current(version)) yield break;
            yield return _Transport.Prepare(_Port);
            if (!Current(version)) yield break;
            if (!_Transport.Ready)
            {
                ScheduleRetry();
                SetState(HotelSessionState.Failed, _Transport.Failure ?? "Unable to connect to relay.");
                yield break;
            }

            SetState(HotelSessionState.RequestingRoom, invitation == null ? "Creating lobby..." : "Finding lobby...");
            if (!Current(version)) yield break;
            Room result = null;
            string error = null;
            Action<Room, string> completed = (value, failure) =>
            {
                if (!Current(version)) return;
                result = value;
                error = failure;
            };
            if (invitation == null)
                yield return _Directory.Create(new CreateRoom { address = _Transport.Address, port = _Transport.Port, capacity = _Capacity }, completed);
            else
                yield return _Directory.Join(invitation, _Transport.UsesRelay, completed);
            if (!Current(version)) yield break;
            if (result == null)
            {
                yield return FailSession(error ?? "The room request failed.");
                yield break;
            }

            result.quickplay = invitation == null || quickplay;
            Room = result;
            SetState(HotelSessionState.Connecting, "Connecting to lobby...");
            if (!Current(version)) yield break;
            try
            {
                if (invitation == null) _Network.StartHost(Room, _Capacity);
                else _Network.StartClient(Room, _Capacity);
            }
            catch (Exception exception)
            {
                _ConnectionFailure = "Unable to start the connection: " + exception.Message;
            }
            yield return AwaitAdmission(version);
            if (!Current(version)) yield break;
            if (!AdmissionComplete)
            {
                yield return FailSession(_ConnectionFailure ?? "Host unavailable or lobby admission timed out.");
                yield break;
            }
            Connected(version, "Connected");
            _Operation = null;
        }

        bool AdmissionComplete => _ConnectionFailure == null && _Network.IsAdmitted && _Network.HasLocalCharacter;
        bool Current(int version) => !_Disposed && version == Version;

        IEnumerator AwaitAdmission(int version)
        {
            var deadline = _Runtime.Time + _AdmissionTimeout;
            while (Current(version) && _ConnectionFailure == null && !_Network.HasLocalCharacter && _Runtime.Time < deadline)
            {
                if (_Network.IsAdmitted && State != HotelSessionState.AwaitingPlayer)
                    SetState(HotelSessionState.AwaitingPlayer, "Preparing your player...");
                yield return null;
            }
        }

        public void LeaveLobby()
        {
            if (_Disposed) return;
            CancelOperation();
            _Operation = _Runtime.Start(LeaveSession());
        }

        IEnumerator LeaveSession()
        {
            var version = Version;
            yield return CloseSession();
            if (!Current(version)) yield break;
            SetState(HotelSessionState.Idle, "Disconnected.");
            ScheduleRetry();
            _Operation = null;
        }

        public IEnumerator CloseSession()
        {
            var version = Version;
            SetState(HotelSessionState.Leaving, "Disconnecting...");
            if (!Current(version)) yield break;
            StopHeartbeat();
            // Let the transport's current disconnect callback finish before issuing another stop.
            yield return null;
            if (!Current(version)) yield break;
            var previousRoom = Room;
            Room = null;
            _Scene.ResetInputFocus();
            _Network.Stop();
            if (previousRoom != null && !string.IsNullOrEmpty(previousRoom.ownerKey))
                yield return _Directory.Delete(previousRoom, IgnoreResponse);
            if (!Current(version)) yield break;
            yield return _Transport.EndSession();
            if (!Current(version)) yield break;
            _Scene.SessionEnded();
        }

        public IEnumerator FailSession(string message)
        {
            var version = Version;
            yield return CloseSession();
            if (!Current(version)) yield break;
            SetState(HotelSessionState.Failed, message);
            ScheduleRetry();
            _Operation = null;
        }

        public void ConnectionLost(string message)
        {
            if (_Disposed || State == HotelSessionState.Leaving) return;
            _ConnectionFailure = message;
            if (State != HotelSessionState.Connected) return;
            Fail(message);
        }

        public void Fail(string message)
        {
            if (_Disposed) return;
            CancelOperation();
            _Operation = _Runtime.Start(FailSession(message));
        }

        public IEnumerator StartTransferredHost(Room room, IEnumerable<string> kickedPlayerIds)
        {
            var version = Version;
            if (!Current(version)) yield break;
            Room = room;
            _Capacity = room.capacity;
            _ConnectionFailure = null;
            SetState(HotelSessionState.Connecting, "Starting the transferred lobby...");
            if (!Current(version)) yield break;
            try { _Network.StartHost(Room, _Capacity, kickedPlayerIds); }
            catch (Exception exception) { _ConnectionFailure = "Unable to start the new host: " + exception.Message; }
            yield return AwaitAdmission(version);
            if (!Current(version)) yield break;
            if (!AdmissionComplete) yield return FailSession(_ConnectionFailure ?? "The new host could not start.");
            else Connected(version, "Host transferred");
        }

        void Connected(int version, string message)
        {
            SetState(HotelSessionState.Connected, message);
            if (Current(version) && State == HotelSessionState.Connected && _Network.IsHost)
                _Heartbeat = _Runtime.Start(KeepRoomAlive(version));
        }

        IEnumerator KeepRoomAlive(int version)
        {
            while (Current(version) && _Network.IsHost && Room != null)
            {
                yield return _Runtime.Delay(5f);
                if (!Current(version) || !_Network.IsHost || Room == null) yield break;
                var room = Room;
                var queued = !_Scene.IsEditor && ReadyToPlay && !_Scene.IntroductionPlaying &&
                    _Scene.TargetScene == "Lobby" && _Network.Quickplay && _Network.AllReady;
                Room response = null;
                string failure = null;
                yield return _Directory.Heartbeat(room.code, new RoomHeartbeat
                {
                    ownerKey = room.ownerKey, players = Math.Max(1, _Network.PlayerCount), quickplay = queued
                }, (value, error) =>
                {
                    if (!Current(version)) return;
                    response = value;
                    failure = error;
                });
                if (!Current(version)) yield break;
                if (response != null && response.status == "matched" && queued && _Network.PlayerCount == 1 &&
                    _Network.AllReady && room.quickplay)
                {
                    _Heartbeat = null;
                    BeginSession(response.code, true);
                    yield break;
                }
                if (failure != null)
                {
                    _Heartbeat = null;
                    _Operation = _Runtime.Start(FailSession(failure));
                    yield break;
                }
            }
        }

        public void CancelOperation()
        {
            ++Version;
            if (_Operation == null) return;
            _Runtime.Stop(_Operation);
            _Operation = null;
        }

        void StopHeartbeat()
        {
            if (_Heartbeat == null) return;
            _Runtime.Stop(_Heartbeat);
            _Heartbeat = null;
        }

        void ScheduleRetry() => _NextAttempt = _Runtime.Time + Math.Max(_RetryInterval, _Transport.RetryDelay);
        static void IgnoreResponse(Room room, string error) { }

        public void SetState(HotelSessionState state, string message)
        {
            State = state;
            SetStatus(message);
        }

        public void SetStatus(string message)
        {
            Status = message;
            Changed?.Invoke();
        }

        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            CancelOperation();
            StopHeartbeat();
            Changed = null;
        }
    }
}
