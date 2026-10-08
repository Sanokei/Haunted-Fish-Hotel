// Executes the production session policy with controlled time and queued directory callbacks.
// No Unity, Mirage, real network endpoint or Editor instance participates in this suite.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HauntedFish.Multiplayer;

static class Program
{
    static int checks;
    static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        checks++;
    }

    static int Main()
    {
        try
        {
            TestCreateAndAdmission();
            TestJoinValidation();
            TestReplacingSession();
            TestUnsupportedAndRelayFailure();
            TestDirectoryFailure();
            TestAdmissionTimeoutAndFailure();
            TestCancelledDirectoryCallback();
            TestCancelledPreparationAndAdmission();
            TestHeartbeatFailure();
            TestQuickplayMatch();
            TestQuickplayGates();
            TestHeartbeatCancellation();
            TestDispose();
            TestTransferredHost();
            TestReentrantCancellation();
            Console.WriteLine($"PASS {checks} session lifecycle checks");
            return 0;
        }
        catch (Exception exception)
        {
            // Report a failed assertion as a normal exit, avoiding a Windows crash
            // handler that can retain this fixture's apphost and block its next build.
            Console.Error.WriteLine("FAIL session lifecycle checks: " + exception.Message);
            return 1;
        }
    }

    static void TestCreateAndAdmission()
    {
        using var f = new Fixture();
        f.Network.AutoAdmit = f.Network.AutoCharacter = false;
        var states = new List<HotelSessionState>();
        f.Session.Changed += () => states.Add(f.Session.State);
        f.Session.CreateLobby();
        Check(f.Scene.Preparations == 1, "Create prepares the introduction once");
        Check(f.Session.State == HotelSessionState.Connecting && f.Session.Busy, "Connection remains pending before admission");
        Check(f.Transport.Preparations == 1 && f.Transport.PreparedPort == 7777, "Transport preparation receives the configured port");
        var request = f.Directory.Requests.Single(r => r.Kind == RequestKind.Create);
        Check(request.Create.address == "198.51.100.9" && request.Create.port == 9000 && request.Create.capacity == 4, "Directory creation uses the prepared endpoint and capacity");
        Check(f.Network.HostStarts == 1 && f.Network.ClientStarts == 0 && f.Session.Room.quickplay, "Created room starts one quickplay host");
        f.Network.IsAdmitted = true;
        f.Runtime.Frame(.1f);
        Check(f.Session.State == HotelSessionState.AwaitingPlayer && !f.Session.ReadyToPlay, "Admission waits for the local character");
        f.Network.HasLocalCharacter = true;
        f.Runtime.Frame(.1f);
        Check(f.Session.State == HotelSessionState.Connected && f.Session.ReadyToPlay && !f.Session.Busy, "Admission and character complete the connection");
        Check(states.SequenceEqual(new[] { HotelSessionState.PreparingTransport, HotelSessionState.RequestingRoom, HotelSessionState.Connecting, HotelSessionState.AwaitingPlayer, HotelSessionState.Connected }), "Successful connection publishes ordered state transitions");
        f.Scene.Loading = true;
        Check(!f.Session.ReadyToPlay && f.Session.Transitioning, "Scene loading gates readiness independently of connection state");
        f.Scene.Loading = false;
        f.Runtime.Frame(5);
        var heartbeat = f.Directory.Requests.Single(r => r.Kind == RequestKind.Heartbeat);
        Check(heartbeat.Code == f.Session.Code && heartbeat.Heartbeat.ownerKey == "owner" && heartbeat.Heartbeat.players == 1, "Host heartbeat carries the current lease and bounded player count");
    }

    static void TestJoinValidation()
    {
        using var f = new Fixture();
        f.Session.JoinLobby("bad!");
        Check(f.Directory.Requests.Count == 0 && f.Network.ClientStarts == 0 && f.Session.State == HotelSessionState.Idle, "Invalid invitations have no external effects");
        f.Session.JoinLobby(" ab12cd ");
        var join = f.Directory.Requests.Single();
        Check(join.Kind == RequestKind.Join && join.Code == "AB12CD" && !join.Relay, "Join normalizes invitation codes and selects direct directory mode");
        Check(f.Network.ClientStarts == 1 && !f.Session.Room.quickplay && f.Session.State == HotelSessionState.Connected, "Invitation joins start a friend-room client");
        int version = f.Session.Version;
        f.Session.JoinLobby("ab12cd");
        Check(f.Session.Version == version && f.Directory.Requests.Count == 1, "Joining the current room does not restart it");
        f.Session.CreateLobby();
        Check(f.Network.HostStarts == 0, "Create cannot replace a running peer");
    }

    static void TestReplacingSession()
    {
        using (var f = new Fixture())
        {
            f.Session.CreateLobby();
            f.Session.JoinLobby("JOIN02");
            Check(f.Session.State == HotelSessionState.Leaving && f.Network.Stops == 0, "Session replacement yields before stopping a peer");
            f.Runtime.Frame();
            Check(f.Directory.Requests.Count(r => r.Kind == RequestKind.Delete) == 1 && f.Network.Stops == 1 && f.Transport.Ends == 1, "Replacing owned room deletes its lease and ends the old transport");
            Check(f.Session.State == HotelSessionState.Connected && f.Network.ClientStarts == 1 && f.Session.Code == "JOIN02", "Replacement joins after cleanup completes");
        }
        using (var f = new Fixture())
        {
            f.Session.JoinLobby("JOIN02");
            f.Session.LeaveLobby();
            f.Runtime.Frame();
            Check(!f.Directory.Requests.Any(r => r.Kind == RequestKind.Delete), "Clients cannot delete a room they do not own");
            Check(f.Session.State == HotelSessionState.Idle && f.Session.Room == null && f.Scene.FocusResets == 1 && f.Scene.Ends == 1, "Leave clears room, focus and scene session state");
            Check(!f.Session.RetryDue, "Leave observes configured retry delay");
            f.Runtime.Frame(15);
            Check(f.Session.RetryDue, "Idle room creation resumes after the retry delay");
        }
    }

    static void TestUnsupportedAndRelayFailure()
    {
        using (var f = new Fixture())
        {
            f.Transport.Supported = false;
            f.Session.CreateLobby();
            Check(f.Session.State == HotelSessionState.Failed && f.Transport.Preparations == 0 && f.Directory.Requests.Count == 0, "Unsupported transport fails before preparation or directory I/O");
        }
        using (var f = new Fixture())
        {
            f.Transport.UsesRelay = true;
            f.Transport.OnPrepare = () => { f.Transport.Ready = false; f.Transport.Failure = "Relay unavailable"; f.Transport.RetryDelay = 45; };
            f.Session.CreateLobby();
            Check(f.Session.State == HotelSessionState.Failed && f.Session.Status == "Relay unavailable" && f.Directory.Requests.Count == 0, "Relay preparation failure prevents directory creation");
            f.Runtime.Frame(15);
            Check(!f.Session.RetryDue, "Relay-provided backoff survives the failure");
            f.Runtime.Frame(30);
            Check(f.Session.RetryDue, "Retry becomes due at the relay backoff boundary");
        }
        using (var f = new Fixture())
        {
            f.Transport.UsesRelay = true;
            f.Session.JoinLobby("RELAY2");
            Check(f.Directory.Requests.Single().Relay, "Relay join selects the reservation-aware directory path");
        }
    }

    static void TestAdmissionTimeoutAndFailure()
    {
        using (var f = new Fixture())
        {
            f.Network.AutoAdmit = false;
            f.Session.CreateLobby();
            f.Runtime.Frame();
            Check(f.Session.State == HotelSessionState.Failed && !f.Session.ReadyToPlay, "A visible character alone cannot complete an unadmitted connection");
        }
        using (var f = new Fixture())
        {
            f.Network.AutoAdmit = f.Network.AutoCharacter = false;
            f.Session.CreateLobby();
            f.Runtime.Frame(10);
            Check(f.Session.State == HotelSessionState.Leaving, "Admission timeout first enters cleanup");
            f.Runtime.Frame();
            Check(f.Session.State == HotelSessionState.Failed && f.Session.Room == null && f.Network.Stops == 1, "Admission timeout ends the peer and clears the room");
            Check(f.Directory.Requests.Count(r => r.Kind == RequestKind.Delete) == 1 && f.Session.Status.Contains("timed out"), "Timed-out owned room is released with useful failure status");
        }
        using (var f = new Fixture())
        {
            f.Network.StartException = new InvalidOperationException("socket occupied");
            f.Session.CreateLobby();
            f.Runtime.Frame();
            Check(f.Session.State == HotelSessionState.Failed && f.Session.Status.Contains("socket occupied"), "Network startup exceptions become session failure after cleanup");
        }
        using (var f = new Fixture())
        {
            f.Network.AutoAdmit = f.Network.AutoCharacter = false;
            f.Session.JoinLobby("REJECT");
            f.Session.ConnectionLost("Invitation expired");
            f.Runtime.Frame();
            f.Runtime.Frame();
            Check(f.Session.State == HotelSessionState.Failed && f.Session.Status == "Invitation expired", "Admission rejection retains the transport failure reason");
        }
    }

    static void TestDirectoryFailure()
    {
        using (var f = new Fixture())
        {
            f.Directory.CreateError = "Directory unavailable";
            f.Session.CreateLobby();
            f.Runtime.Frame();
            Check(f.Session.State == HotelSessionState.Failed && f.Session.Status == "Directory unavailable" && f.Network.HostStarts == 0, "Failed creation cannot start a host and preserves the directory failure");
            Check(f.Transport.Ends == 1 && f.Scene.Ends == 1 && !f.Directory.Requests.Any(r => r.Kind == RequestKind.Delete), "Creation failure releases transport without inventing an owned-room deletion");
        }
        using (var f = new Fixture())
        {
            f.Directory.JoinError = "Room missing";
            f.Session.JoinLobby("MISS01");
            f.Runtime.Frame();
            Check(f.Session.State == HotelSessionState.Failed && f.Session.Status == "Room missing" && f.Network.ClientStarts == 0, "Failed invitation lookup cannot start a client");
            f.Runtime.Frame(14.9f);
            Check(!f.Session.RetryDue, "Directory failure retry does not start before its deadline");
            f.Runtime.Frame(.1f);
            Check(f.Session.RetryDue, "Directory failure retry is due at its deadline");
        }
    }

    static void TestCancelledDirectoryCallback()
    {
        using var f = new Fixture();
        f.Directory.Automatic = false;
        f.Session.CreateLobby();
        var abandoned = f.Directory.Requests.Single();
        int version = f.Session.Version;
        f.Session.LeaveLobby();
        f.Runtime.Frame();
        Check(f.Session.Version > version && f.Session.State == HotelSessionState.Idle, "Leave invalidates an in-flight directory operation");
        abandoned.Complete(NewRoom("OLD001"), null);
        f.Runtime.Frame();
        Check(f.Session.Room == null && f.Network.HostStarts == 0 && f.Session.State == HotelSessionState.Idle, "A queued completion from a cancelled create cannot adopt or start its room");
        f.Session.JoinLobby("NEW002");
        var current = f.Directory.Requests.Last();
        current.Complete(NewRoom("NEW002", false), null);
        f.Runtime.Frame();
        Check(f.Session.Code == "NEW002" && f.Network.ClientStarts == 1, "A fresh operation can connect after an abandoned callback");
    }

    static void TestCancelledPreparationAndAdmission()
    {
        using (var f = new Fixture())
        {
            f.Transport.PreparationPending = true;
            f.Session.CreateLobby();
            Check(f.Session.State == HotelSessionState.PreparingTransport, "Preparation can remain in flight");
            f.Session.CancelOperation();
            f.Transport.PreparationPending = false;
            f.Runtime.Frame();
            Check(f.Directory.Requests.Count == 0 && f.Network.HostStarts == 0, "Cancelled preparation cannot continue to directory creation");
        }
        using (var f = new Fixture())
        {
            f.Network.AutoCharacter = false;
            f.Session.CreateLobby();
            f.Session.CancelOperation();
            f.Network.HasLocalCharacter = true;
            f.Runtime.Frame();
            Check(f.Session.State != HotelSessionState.Connected && !f.Directory.Requests.Any(r => r.Kind == RequestKind.Heartbeat), "Cancelled admission cannot publish connected or start a heartbeat");
        }
    }

    static void TestHeartbeatFailure()
    {
        using var f = new Fixture();
        f.Session.CreateLobby();
        f.Directory.HeartbeatError = "Lease expired";
        f.Runtime.Frame(5);
        Check(f.Session.State == HotelSessionState.Leaving, "Heartbeat failure starts session cleanup");
        f.Runtime.Frame();
        Check(f.Session.State == HotelSessionState.Failed && f.Session.Status == "Lease expired" && f.Session.Room == null, "Heartbeat failure clears the session and preserves the directory error");
        f.Runtime.Frame(20);
        Check(f.Directory.Requests.Count(r => r.Kind == RequestKind.Heartbeat) == 1 && f.Session.RetryDue, "Failed lease cannot keep heartbeating and respects retry timing");
    }

    static void TestQuickplayMatch()
    {
        using var f = new Fixture();
        f.Scene.IsEditor = false;
        f.Network.AllReady = true;
        f.Network.Quickplay = true;
        f.Session.CreateLobby();
        f.Directory.HeartbeatResult = NewRoom("MATCH2", false);
        f.Directory.HeartbeatResult.status = "matched";
        f.Runtime.Frame(5);
        var heartbeat = f.Directory.Requests.Single(r => r.Kind == RequestKind.Heartbeat);
        Check(heartbeat.Heartbeat.quickplay, "Ready single-host quickplay lobby enters the matchmaking queue");
        Check(f.Session.State == HotelSessionState.Leaving, "Quickplay match closes the old room before reconnecting");
        f.Runtime.Frame();
        Check(f.Network.ClientStarts == 1 && f.Session.Code == "MATCH2" && f.Session.Room.quickplay && f.Session.State == HotelSessionState.Connected, "Match reconnect retains quickplay intent on the destination room");
        Check(f.Directory.Requests.Count(r => r.Kind == RequestKind.Delete) == 1, "Match transition relinquishes the old owned lobby");
    }

    static void TestQuickplayGates()
    {
        Action<Fixture>[] gates =
        {
            f => f.Scene.IsEditor = true,
            f => f.Scene.IntroductionPlaying = true,
            f => f.Scene.TargetScene = "Game",
            f => f.Scene.Loading = true,
            f => f.Network.AllReady = false,
            f => f.Network.Quickplay = false
        };
        foreach (var gate in gates)
        {
            using var f = new Fixture();
            f.Scene.IsEditor = false;
            f.Network.Quickplay = f.Network.AllReady = true;
            f.Session.CreateLobby();
            gate(f);
            f.Directory.HeartbeatResult = NewRoom("MATCH2", false);
            f.Directory.HeartbeatResult.status = "matched";
            f.Runtime.Frame(5);
            Check(!f.Directory.Requests.Single(r => r.Kind == RequestKind.Heartbeat).Heartbeat.quickplay && f.Network.ClientStarts == 0, "Each readiness/presentation/editor gate suppresses quickplay matchmaking");
        }
        using (var f = new Fixture())
        {
            f.Scene.IsEditor = false;
            f.Network.Quickplay = f.Network.AllReady = true;
            f.Network.PlayerCount = 2;
            f.Session.CreateLobby();
            f.Directory.HeartbeatResult = NewRoom("MATCH2", false);
            f.Directory.HeartbeatResult.status = "matched";
            f.Runtime.Frame(5);
            Check(f.Directory.Requests.Single(r => r.Kind == RequestKind.Heartbeat).Heartbeat.players == 2 && f.Network.ClientStarts == 0, "A multiplayer host is not moved by a single-host quickplay match response");
        }
    }

    static void TestHeartbeatCancellation()
    {
        using var f = new Fixture();
        f.Session.CreateLobby();
        f.Directory.Automatic = false;
        f.Runtime.Frame(5);
        var heartbeat = f.Directory.Requests.Last();
        f.Session.LeaveLobby();
        f.Runtime.Frame();
        var deletion = f.Directory.Requests.Last();
        deletion.Complete(null, null);
        f.Runtime.Frame();
        heartbeat.Complete(null, "Old heartbeat failed");
        f.Runtime.Frame();
        Check(f.Session.State == HotelSessionState.Idle && f.Session.Status == "Disconnected.", "Late cancelled heartbeat errors cannot overwrite a completed leave");
    }

    static void TestDispose()
    {
        using (var f = new Fixture())
        {
            f.Session.CreateLobby();
            int published = 0;
            f.Session.Changed += () => published++;
            f.Session.Dispose();
            int version = f.Session.Version;
            f.Session.Dispose();
            f.Session.CreateLobby();
            f.Session.JoinLobby("AFTER1");
            f.Session.LeaveLobby();
            f.Session.ConnectionLost("Late disconnect");
            f.Session.Fail("Late failure");
            f.Runtime.Frame(30);
            Check(f.Session.Version == version && f.Directory.Requests.Count == 1 && f.Network.HostStarts == 1, "Disposal is idempotent and blocks new session operations and heartbeats");
            Check(!f.Session.RetryDue && published == 0 && f.Runtime.ActiveCount == 0, "Disposal cancels scheduled policy work and releases subscribers");
        }
        using (var f = new Fixture())
        {
            f.Directory.Automatic = false;
            f.Session.JoinLobby("PEND01");
            var pending = f.Directory.Requests.Single();
            f.Session.Dispose();
            pending.Complete(NewRoom("PEND01", false), null);
            f.Runtime.Frame();
            Check(f.Session.Room == null && f.Network.ClientStarts == 0, "Disposed queued directory callbacks cannot start a peer");
        }
    }

    static void TestTransferredHost()
    {
        using (var f = new Fixture())
        {
            var room = NewRoom("MOVE01");
            room.capacity = 7;
            f.Runtime.Start(f.Session.StartTransferredHost(room, new[] { "kicked-player" }));
            Check(f.Network.HostStarts == 1 && f.Network.LastCapacity == 7 && f.Network.Kicked.SequenceEqual(new[] { "kicked-player" }), "Transferred host starts with destination capacity and carried admission bans");
            Check(f.Session.Room == room && f.Session.State == HotelSessionState.Connected && f.Session.Status == "Host transferred", "Transferred host adopts the supplied lease after admission");
            Check(f.Transport.Preparations == 0 && f.Directory.Requests.Count == 0, "Transferred startup uses the already prepared transport and room");
            f.Runtime.Frame(5);
            Check(f.Directory.Requests.Single().Kind == RequestKind.Heartbeat && f.Directory.Requests.Single().Code == "MOVE01", "Transferred host keeps its new lease alive");
        }
        using (var f = new Fixture())
        {
            f.Network.AutoAdmit = f.Network.AutoCharacter = false;
            f.Runtime.Start(f.Session.StartTransferredHost(NewRoom("MOVE01"), Array.Empty<string>()));
            f.Runtime.Frame(10);
            f.Runtime.Frame();
            Check(f.Session.State == HotelSessionState.Failed && f.Session.Status == "The new host could not start." && f.Session.Room == null, "Transferred host admission timeout cleans up and reports its own failure");
        }
        using (var f = new Fixture())
        {
            f.Network.AutoCharacter = false;
            f.Runtime.Start(f.Session.StartTransferredHost(NewRoom("MOVE01"), Array.Empty<string>()));
            f.Session.CancelOperation();
            f.Network.HasLocalCharacter = true;
            f.Runtime.Frame();
            Check(f.Session.State != HotelSessionState.Connected && f.Directory.Requests.Count == 0, "Cancelling a transferred-host admission prevents connection publication and heartbeat");
            f.Session.LeaveLobby();
            f.Runtime.Frame();
            Check(f.Session.State == HotelSessionState.Idle && f.Session.Room == null && f.Network.Stops == 1, "Cancelled transferred startup remains cleanly leaveable");
        }
    }

    static Room NewRoom(string code, bool owner = true) => new Room { code = code, address = "203.0.113.5", port = 7777, capacity = 4, ownerKey = owner ? "owner" : null, joinKey = "join" };

    static void TestReentrantCancellation()
    {
        var failures = new List<string>();
        var stages = new[] { HotelSessionState.PreparingTransport, HotelSessionState.RequestingRoom, HotelSessionState.Connecting };
        foreach (bool joining in new[] { false, true })
        foreach (bool disposing in new[] { false, true })
        foreach (var stage in stages)
        {
            using var f = new Fixture();
            bool interrupted = false;
            f.Session.Changed += () =>
            {
                if (interrupted || f.Session.State != stage) return;
                interrupted = true;
                if (disposing) f.Session.Dispose();
                else f.Session.LeaveLobby();
            };
            if (joining) f.Session.JoinLobby("CANCEL");
            else f.Session.CreateLobby();
            f.Runtime.Frame();
            f.Runtime.Frame(30);
            string scenario = $"{(joining ? "join" : "create")}/{(disposing ? "dispose" : "leave")}/{stage}";
            Verify(interrupted, scenario + " publishes its interruption point");
            int expectedPreparations = stage == HotelSessionState.PreparingTransport ? 0 : 1;
            int expectedRequests = stage == HotelSessionState.Connecting ? 1 : 0;
            Verify(f.Transport.Preparations == expectedPreparations,
                scenario + " starts no transport preparation after the cancellation notification");
            Verify(f.Directory.Requests.Count(r => r.Kind == RequestKind.Create || r.Kind == RequestKind.Join) == expectedRequests,
                scenario + " starts no directory request after the cancellation notification");
            Verify(f.Network.HostStarts == 0 && f.Network.ClientStarts == 0,
                scenario + " starts no peer after the cancellation notification");
            Verify(!f.Directory.Requests.Any(r => r.Kind == RequestKind.Heartbeat) && f.Session.State != HotelSessionState.Connected,
                scenario + " cannot reconnect or keep a lease alive after interruption");
        }
        foreach (bool disposing in new[] { false, true })
        {
            using var f = new Fixture();
            bool interrupted = false;
            f.Session.Changed += () =>
            {
                if (interrupted || f.Session.State != HotelSessionState.Connecting) return;
                interrupted = true;
                if (disposing) f.Session.Dispose();
                else f.Session.LeaveLobby();
            };
            f.Runtime.Start(f.Session.StartTransferredHost(NewRoom("MOVE01"), new[] { "blocked" }));
            f.Runtime.Frame();
            f.Runtime.Frame(30);
            Verify(interrupted && f.Network.HostStarts == 0 && f.Network.ClientStarts == 0,
                $"transferred/{(disposing ? "dispose" : "leave")} starts no host after its Connecting notification cancels it");
        }
        if (failures.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, failures));

        void Verify(bool condition, string description)
        {
            if (!condition) failures.Add(description);
            checks++;
        }
    }

    sealed class Fixture : IDisposable
    {
        public readonly FakeNetwork Network = new FakeNetwork();
        public readonly FakeTransport Transport = new FakeTransport();
        public readonly FakeDirectory Directory = new FakeDirectory();
        public readonly FakeRuntime Runtime = new FakeRuntime();
        public readonly FakeScene Scene = new FakeScene();
        public readonly HotelSessionLifecycle Session;
        public Fixture() => Session = new HotelSessionLifecycle(Network, Transport, Directory, Runtime, Scene, 7777, 4, 15, 10);
        public void Dispose() => Session.Dispose();
    }

    sealed class FakeRuntime : IHotelSessionRuntime
    {
        sealed class Routine
        {
            public readonly Stack<IEnumerator> Stack = new Stack<IEnumerator>();
            public bool Stopped;
            public float Due;
            public Routine(IEnumerator root) => Stack.Push(root);
        }
        sealed class DelayValue { public float Seconds; }
        readonly List<Routine> routines = new List<Routine>();
        public float Time { get; private set; }
        public int ActiveCount => routines.Count(r => !r.Stopped && r.Stack.Count > 0);
        public object Start(IEnumerator routine)
        {
            var handle = new Routine(routine);
            routines.Add(handle);
            Advance(handle);
            return handle;
        }
        public void Stop(object handle) => ((Routine)handle).Stopped = true;
        public object Delay(float seconds) => new DelayValue { Seconds = seconds };
        public void Frame(float seconds = 0)
        {
            Time += seconds;
            foreach (var routine in routines.ToArray())
                if (!routine.Stopped && routine.Stack.Count > 0 && routine.Due <= Time)
                    Advance(routine);
            routines.RemoveAll(r => r.Stopped || r.Stack.Count == 0);
        }
        void Advance(Routine routine)
        {
            while (!routine.Stopped && routine.Stack.Count > 0)
            {
                var current = routine.Stack.Peek();
                if (!current.MoveNext()) { routine.Stack.Pop(); continue; }
                if (current.Current is IEnumerator nested) { routine.Stack.Push(nested); continue; }
                routine.Due = current.Current is DelayValue delay ? Time + delay.Seconds : Time;
                return;
            }
        }
    }

    sealed class FakeNetwork : IHotelSessionNetwork
    {
        public bool IsHost { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsAdmitted { get; set; }
        public bool HasLocalCharacter { get; set; }
        public int PlayerCount { get; set; } = 1;
        public bool AllReady { get; set; }
        public bool Quickplay { get; set; }
        public bool AutoAdmit = true, AutoCharacter = true;
        public int HostStarts, ClientStarts, Stops, LastCapacity;
        public string[] Kicked = Array.Empty<string>();
        public Exception StartException;
        public void StartHost(Room room, int capacity, IEnumerable<string> kickedPlayerIds = null)
        {
            HostStarts++;
            LastCapacity = capacity;
            Kicked = (kickedPlayerIds ?? Array.Empty<string>()).ToArray();
            Start(true);
        }
        public void StartClient(Room room, int capacity) { ClientStarts++; LastCapacity = capacity; Start(false); }
        void Start(bool host)
        {
            if (StartException != null) throw StartException;
            IsHost = host;
            IsRunning = true;
            IsAdmitted = AutoAdmit;
            HasLocalCharacter = AutoCharacter;
        }
        public void Stop() { Stops++; IsHost = IsRunning = IsAdmitted = HasLocalCharacter = false; }
    }

    sealed class FakeTransport : IHotelSessionTransport
    {
        public bool Supported { get; set; } = true;
        public bool UsesRelay { get; set; }
        public bool Ready { get; set; } = true;
        public string Failure { get; set; }
        public string Address => "198.51.100.9";
        public int Port => 9000;
        public float RetryDelay { get; set; }
        public int Preparations, Ends, PreparedPort;
        public bool PreparationPending;
        public Action OnPrepare;
        public IEnumerator Prepare(int port)
        {
            Preparations++;
            PreparedPort = port;
            OnPrepare?.Invoke();
            while (PreparationPending) yield return null;
        }
        public IEnumerator EndSession() { Ends++; yield break; }
    }

    enum RequestKind { Create, Join, Delete, Heartbeat }
    sealed class Request
    {
        public RequestKind Kind;
        public string Code;
        public bool Relay;
        public CreateRoom Create;
        public RoomHeartbeat Heartbeat;
        public Action<Room, string> Callback;
        public bool Finished;
        public void Complete(Room room, string error) { Finished = true; Callback(room, error); }
    }
    sealed class FakeDirectory : IHotelSessionDirectory
    {
        public readonly List<Request> Requests = new List<Request>();
        public bool Automatic = true;
        public Room HeartbeatResult;
        public string HeartbeatError, CreateError, JoinError;
        IEnumerator Run(Request request, Room response = null, string error = null)
        {
            Requests.Add(request);
            if (Automatic) request.Complete(response, error);
            while (!request.Finished) yield return null;
        }
        public IEnumerator Create(CreateRoom request, Action<Room, string> completed) => Run(new Request { Kind = RequestKind.Create, Create = request, Callback = completed }, CreateError == null ? NewRoom("HOST01") : null, CreateError);
        public IEnumerator Join(string code, bool relay, Action<Room, string> completed) => Run(new Request { Kind = RequestKind.Join, Code = code, Relay = relay, Callback = completed }, JoinError == null ? NewRoom(code, false) : null, JoinError);
        public IEnumerator Delete(Room room, Action<Room, string> completed) => Run(new Request { Kind = RequestKind.Delete, Code = room.code, Callback = completed });
        public IEnumerator Heartbeat(string code, RoomHeartbeat request, Action<Room, string> completed) => Run(new Request { Kind = RequestKind.Heartbeat, Code = code, Heartbeat = request, Callback = completed }, HeartbeatResult, HeartbeatError);
    }

    sealed class FakeScene : IHotelSessionScene
    {
        public bool Loading { get; set; }
        public bool IntroductionPlaying { get; set; }
        public bool IsEditor { get; set; } = true;
        public string TargetScene { get; set; } = "Lobby";
        public int Preparations, FocusResets, Ends;
        public void PrepareIntroduction() => Preparations++;
        public void ResetInputFocus() => FocusResets++;
        public void SessionEnded() => Ends++;
    }
}
