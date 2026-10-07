// Runs the production scene protocol with deterministic peer/scene doubles; no Unity native runtime required.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using HauntedFish.Multiplayer;
using Mirage;
using UnityEngine;

static class Program
{
    static int checks;
    static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        ++checks;
    }
    static void Call(object instance, string name) => instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, null);
    static void Configure(HauntedHotelMessageTravel travel, NetworkServer server, NetworkClient client,
        ServerObjectManager serverObjects, ClientObjectManager clientObjects, HotelSessionContext context)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(HauntedHotelMessageTravel).GetField("_Server", flags).SetValue(travel, server);
        typeof(HauntedHotelMessageTravel).GetField("_Client", flags).SetValue(travel, client);
        typeof(HauntedHotelMessageTravel).GetField("_ServerObjects", flags).SetValue(travel, serverObjects);
        typeof(HauntedHotelMessageTravel).GetField("_ClientObjects", flags).SetValue(travel, clientObjects);
        travel.Configure(context, new HotelTravelCallbacks(_ => { }, () => "ABC123", () => { }, () => { }, () => { }));
    }
    static void Main()
    {
        var gate = new GameRoundGate();
        gate.Begin(new uint[] { 1, 2, 3 }, 1, 10);
        Check(!gate.Finish(1, 1, 18.9f), "Round presentation cannot finish early");
        Check(!gate.Finish(1, 0, 20), "Previous-round completion cannot unlock a new round");
        Check(!gate.Finish(99, 1, 20), "Nonparticipant cannot complete the selection gate");
        Check(gate.Finish(1, 1, 20) && !gate.Complete, "One viewer cannot release everyone");
        Check(!gate.Finish(1, 1, 20), "Duplicate completion does not count twice");
        Check(gate.Finish(2, 1, 20) && !gate.Complete, "Slowest viewer retains the gameplay gate");
        gate.Remove(3);
        Check(gate.Complete, "Disconnected viewer cannot deadlock the remaining players");
        gate.Begin(new uint[] { 1, 2 }, 2, 30);
        Check(!gate.Complete && !gate.Finish(1, 1, 40), "Returning viewers must watch the new round again");
        Check(gate.Finish(1, 2, 39) && gate.Finish(2, 2, 39) && gate.Complete, "All current viewers release gameplay together");
        var focus = new HotelSessionContext();
        var focusEvents = new List<bool>();
        Action<bool> focusListener = value =>
        {
            Check(focus.InputFocused == value, "Focus is updated before notification");
            focusEvents.Add(value);
        };
        focus.InputFocusChanged += focusListener;
        focus.SetInputFocused(false);
        focus.SetInputFocused(true);
        focus.SetInputFocused(true);
        focus.SetInputFocused(false);
        Check(focusEvents.SequenceEqual(new[] { true, false }), "Focus events fire once per transition");
        focus.InputFocusChanged -= focusListener;
        focus.SetInputFocused(true);
        Check(focusEvents.Count == 2, "Unsubscribed focus listeners are released");
        var host = new NetworkServer { Active = true };
        var client = new NetworkClient();
        var objects = new ServerObjectManager();
        var peerObjects = new ClientObjectManager();
        var session = new HotelSessionContext();
        var local = new Peer(); var remote = new Peer();
        host.LocalPlayer = local;
        host.AuthenticatedPlayers.Add(local); host.AuthenticatedPlayers.Add(remote);
        var travel = new HauntedHotelMessageTravel();
        Configure(travel, host, client, objects, peerObjects, session);
        host.Started.Invoke(); client.Started.Invoke();
        var permitted = false;
        var travelCallbacks = (HotelTravelCallbacks)typeof(HauntedHotelMessageTravel).GetField("_Callbacks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(travel);
        travelCallbacks.CanStartGame = () => permitted;
        travel.GoToGame();
        Check(!travel.Loading && host.Sent.Count == 0, "Start is rejected by the lobby readiness/authority gate");
        permitted = true;
        travel.GoToGame();
        Check(travel.Loading && session.Loading && !local.SceneIsReady && !remote.SceneIsReady, "Travel gates every peer and injected input context");
        Check(host.Sent.Count == 1 && host.Sent[0] is HotelTravel { Version: 1, Scene: "Game" }, "Host sends versioned scene request");
        host.MessageHandler.Deliver(remote, new HotelTravelReady { Version = 0, Scene = "Game" });
        Check(!remote.SceneIsReady, "Stale readiness cannot release a peer");
        host.MessageHandler.Deliver(remote, new HotelTravelReady { Version = 1, Scene = "Lobby" });
        Check(!remote.SceneIsReady, "Wrong scene cannot release a peer");
        host.MessageHandler.Deliver(remote, new HotelTravelReady { Version = 1, Scene = "Game" });
        Check(remote.SceneIsReady && objects.Visible.Count == 0, "Early readiness waits for host load before spawning");
        travel.CompleteLoads();
        Check(!travel.Loading && !session.Loading && local.SceneIsReady && objects.Visible.Contains(remote), "Host completion releases local input and early ready observers");
        var late = new Peer(); travel.Admit(late);
        Check(!late.SceneIsReady && late.Sent[0] is HotelTravel { Version: 1, Scene: "Game" }, "Late joins load Game before receiving objects");
        travel.ReturnToLobby();
        Check(host.Sent.Count == 1 && travel.TargetScene == "Game", "A second travel cannot overtake a loading peer");
        host.MessageHandler.Deliver(late, new HotelTravelReady { Version = 1, Scene = "Game" });
        Check(late.SceneIsReady && objects.Visible.Contains(late), "Late readiness spawns objects after host load");
        var stalled = new Peer(); travel.Admit(stalled); Time.unscaledTime = 31; Call(travel, "Update");
        Check(stalled.Disconnected, "Unresponsive scene peer times out");
        travel.ReturnToLobby(); travel.CompleteLoads();
        Check(travel.TargetScene == "Lobby" && host.Sent.Count == 2, "Connected return to Lobby uses next travel request");
        travel.SessionEnded();
        Check(!travel.Loading && travel.TargetScene == "Lobby", "Session end resets protocol state");
        travel.Unconfigure();
        Check(host.Started.Count == 0 && client.Started.Count == 0 && host.Disconnected.Count == 0, "Unconfigure removes lifecycle listeners");

        host = new NetworkServer(); client = new NetworkClient(); session = new HotelSessionContext();
        travel = new HauntedHotelMessageTravel();
        Configure(travel, host, client, new ServerObjectManager(), new ClientObjectManager(), session);
        client.Started.Invoke();
        client.MessageHandler.Deliver(remote, new HotelTravel { Version = 1, Scene = "Game" });
        Check(session.Loading, "Client receives loading gate through injected context");
        travel.CompleteLoads();
        Check(client.Sent[0] is HotelTravelReady { Version: 1, Scene: "Game" }, "Client acknowledges loaded scene/version");
        client.MessageHandler.Deliver(remote, new HotelTravel { Version = 1, Scene = "Game" });
        Check(!travel.Loading, "Duplicate travel does not start another load");
        client.MessageHandler.Deliver(remote, new HotelTravel { Version = 2, Scene = "Unknown" });
        Check(!travel.Loading && travel.TargetScene == "Game", "Unknown scenes cannot change protocol state");
        Application.CanLoad = false;
        client.MessageHandler.Deliver(remote, new HotelTravel { Version = 2, Scene = "Lobby" });
        Check(client.WasDisconnected, "Missing client scene disconnects cleanly");
        var solo = new[] { new LobbyMember { Ready = true } };
        var duo = new[] { new LobbyMember { Ready = true }, new LobbyMember { Ready = true } };
        Check(!LobbyRules.CanStart(solo, true, false), "Solo quickplay waits for four");
        Check(!LobbyRules.CanStart(solo, false, false), "Friend lobby cannot start alone in a build");
        Check(LobbyRules.CanStart(solo, true, true), "Editor can start solo on stairs");
        Check(LobbyRules.CanStart(duo, false, false), "Two ready friends can start");
        Check(!LobbyRules.CanStart(duo, true, false), "Two quickplay players wait for four");
        Check(LobbyRules.CanStart(Enumerable.Repeat(new LobbyMember { Ready = true }, 4).ToArray(), true, false), "Four ready quickplay players can start");
        Check(!LobbyRules.AllReady(null) && !LobbyRules.AllReady(Array.Empty<LobbyMember>()), "Empty lobbies cannot start");
        Check(!LobbyRules.AllReady(new[] { new LobbyMember { Ready = true }, new LobbyMember { Ready = false } }), "Every member must be ready");
        Check(LobbyRules.AllReady(new[] { new LobbyMember { Ready = true }, new LobbyMember { Ready = true } }), "All members ready allows the start UI");
        Check(LobbyCode.TryNormalize(" abc123 ", out var normalized) && normalized == "ABC123", "Invitations normalize before own-lobby comparison");
        TestAutomaticStairsTravel();
        TestQuickplayStairsTravel();
        TestLobbyAuthority();
        TestConnectionLifecycle();
        TestAdmissionDuringTravel();
        TestKickRejoinAndGameMenuCommands();
        Console.WriteLine($"Passed {checks} multiplayer checks.");
    }
    static void TestAutomaticStairsTravel()
    {
        Application.CanLoad = true;
        Time.unscaledTime = 0;
        var host = new NetworkServer { Active = true };
        var client = new NetworkClient();
        var local = new Peer(); var guest = new Peer();
        host.LocalPlayer = local;
        host.AuthenticatedPlayers.AddRange(new[] { local, guest });
        var objects = new ServerObjectManager(); var peerObjects = new ClientObjectManager();
        var context = new HotelSessionContext { Connected = true };
        var travel = new HauntedHotelMessageTravel();
        Configure(travel, host, client, objects, peerObjects, context);
        using var network = new HotelNetworkSession(host, client, objects, peerObjects, new Mirage.SocketLayer.SocketFactory(),
            new NetworkIdentity(), new NetworkIdentity(), Array.Empty<NetworkIdentity>(), context, travel, _ => default);
        var callbacks = (HotelTravelCallbacks)typeof(HauntedHotelMessageTravel).GetField("_Callbacks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(travel);
        callbacks.CanStartGame = () => network.LobbyPlayers.CanStartGame;
        network.LobbyPlayers.BindReadyZone(player => player != null && player.Ready);
        network.StartHost(new Room { code = "ABC123", joinKey = "secret" }, 4);
        host.Started.Invoke(); client.Started.Invoke();
        network.Tick();
        Check(!travel.Loading, "Empty lobby does not automatically travel");
        foreach (var peer in new[] { local, guest })
            host.MessageHandler.Deliver(peer, new LobbyConnectionRequest { PlayerId = peer.PlayerId, Code = "ABC123", JoinKey = "secret" });
        local.Identity.Character.Ready = true;
        Time.unscaledTime = 1; network.Tick();
        Check(!travel.Loading, "Automatic travel waits for every player on the stairs");
        guest.Identity.Character.Ready = true;
        context.Loading = true;
        Time.unscaledTime = 2; network.Tick();
        Check(!travel.Loading, "Automatic travel respects the session loading gate");
        context.Loading = false;
        Time.unscaledTime = 3; network.Tick();
        Check(travel.Loading && travel.TargetScene == "Game" && host.Sent.Count == 1,
            "All players on stairs starts shared Game travel without a command");
        Time.unscaledTime = 4; network.Tick();
        Check(host.Sent.Count == 1, "Automatic start broadcasts travel exactly once");
        travel.CompleteLoads();
        Time.unscaledTime = 5; network.Tick();
        Check(host.Sent.Count == 1 && travel.TargetScene == "Game", "Game scene does not retrigger stairs travel");
        travel.Unconfigure();
    }

    static void TestQuickplayStairsTravel()
    {
        foreach (var editorSolo in new[] { false, true })
        {
            Application.CanLoad = true;
            Time.unscaledTime = 0;
            var host = new NetworkServer { Active = true };
            var client = new NetworkClient();
            var peers = Enumerable.Range(0, 4).Select(_ => new Peer()).ToArray();
            host.LocalPlayer = peers[0]; client.Player = peers[0];
            var objects = new ServerObjectManager();
            var context = new HotelSessionContext { Connected = true };
            var travel = new HauntedHotelMessageTravel();
            Configure(travel, host, client, objects, new ClientObjectManager(), context);
            using var network = new HotelNetworkSession(host, client, objects, new ClientObjectManager(), new Mirage.SocketLayer.SocketFactory(),
                new NetworkIdentity(), new NetworkIdentity(), Array.Empty<NetworkIdentity>(), context, travel, _ => default);
            var callbacks = (HotelTravelCallbacks)typeof(HauntedHotelMessageTravel).GetField("_Callbacks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(travel);
            callbacks.CanStartGame = () => network.LobbyPlayers.CanStartGame;
            network.LobbyPlayers.AllowEditorSolo = editorSolo;
            network.LobbyPlayers.BindReadyZone(player => player != null && player.Ready);
            network.StartHost(new Room { code = "ABC123", joinKey = "secret", quickplay = true }, 4);
            host.Started.Invoke(); client.Started.Invoke();
            for (var i = 0; i < (editorSolo ? 1 : 4); ++i)
            {
                var peer = peers[i];
                host.MessageHandler.Deliver(peer, new LobbyConnectionRequest { PlayerId = peer.PlayerId, Code = "ABC123", JoinKey = "secret", Quickplay = true });
                peer.Identity.Character.Ready = true;
                Time.unscaledTime = i + 1; network.Tick();
                Check(travel.Loading == (editorSolo || i == 3), "Authoritative stairs start enforces quickplay size and editor solo exception");
                Check(network.LobbyPlayers.Roster.Quickplay, "Quickplay mode replicates to roster");
            }
            travel.Unconfigure();
        }
    }

    static void TestLobbyAuthority()
    {
        Application.CanLoad = true;
        Time.unscaledTime = 0;
        var host = new NetworkServer { Active = true };
        var client = new NetworkClient();
        var local = new Peer(); var guest = new Peer(); var other = new Peer();
        host.LocalPlayer = local;
        host.AuthenticatedPlayers.AddRange(new[] { local, guest, other });
        var objects = new ServerObjectManager(); var peerObjects = new ClientObjectManager();
        var context = new HotelSessionContext { Connected = true };
        var travel = new HauntedHotelMessageTravel();
        Configure(travel, host, client, objects, peerObjects, context);
        var network = new HotelNetworkSession(host, client, objects, peerObjects, new Mirage.SocketLayer.SocketFactory(),
            new NetworkIdentity(), new NetworkIdentity(), Array.Empty<NetworkIdentity>(), context, travel, _ => default);
        var callbacks = (HotelTravelCallbacks)typeof(HauntedHotelMessageTravel).GetField("_Callbacks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(travel);
        network.LobbyPlayers.BindReadyZone(player => player != null && player.Ready);
        callbacks.CanStartGame = () => network.LobbyPlayers.CanStartGame;
        network.StartHost(new Room { code = "ABC123", joinKey = "secret" }, 4);
        host.Started.Invoke(); client.Started.Invoke();
        foreach (var peer in new[] { local, guest, other })
            host.MessageHandler.Deliver(peer, new LobbyConnectionRequest { PlayerId = peer.PlayerId, Code = "ABC123", JoinKey = "secret" });
        client.Player = local;
        Check(network.LobbyPlayers.Roster.Leader == local.Identity.NetId && network.LobbyPlayers.IsLeader, "Initial lobby crown belongs to the connection host");
        host.MessageHandler.Deliver(guest, new LobbyCommand { Action = LobbyAction.Kick, Target = other.Identity.NetId });
        Check(!other.Disconnected, "Guests cannot kick players");
        host.MessageHandler.Deliver(guest, new LobbyCommand { Action = LobbyAction.King, Target = guest.Identity.NetId });
        Check(network.LobbyPlayers.Roster.Leader == local.Identity.NetId, "Guests cannot crown themselves");
        foreach (var peer in new[] { local, guest, other }) peer.Identity.Character.Ready = true;
        other.Identity.Character.Ready = false;
        host.MessageHandler.Deliver(local, new LobbyCommand { Action = LobbyAction.Start });
        Check(!travel.Loading, "Leader cannot start while someone is outside the stairs");
        other.Identity.Character.Ready = true;
        travel.GoToGame();
        Check(!travel.Loading, "Direct scene calls cannot bypass the authenticated leader command");
        host.MessageHandler.Deliver(local, new LobbyCommand { Action = LobbyAction.King, Target = guest.Identity.NetId });
        Check(network.LobbyPlayers.Roster.Leader == local.Identity.NetId, "Current host stays authoritative until replacement transport is prepared");
        var preparing = 0; var switching = 0; string destination = null; bool becameHost = true;
        network.LobbyPlayers.HostPreparing += _ => ++preparing;
        network.LobbyPlayers.HostSwitching += (code, successor) => { ++switching; destination = code; becameHost = successor; };
        client.MessageHandler.Deliver(local, new LobbyHostPrepare { Version = 1, Target = guest.Identity.NetId });
        client.MessageHandler.Deliver(local, new LobbyHostPrepare { Version = 1, Target = guest.Identity.NetId });
        Check(preparing == 1, "Duplicate preparation does not allocate another transport");
        client.MessageHandler.Deliver(local, new LobbyHostCommit { Version = 1, Target = other.Identity.NetId, Code = "DEF456" });
        Check(!client.Sent.Exists(m => m is LobbyHostAck), "Clients reject a commit for a different successor");
        host.MessageHandler.Deliver(local, new LobbyCommand { Action = LobbyAction.Start });
        Check(!travel.Loading, "Starts are blocked throughout host migration");
        host.MessageHandler.Deliver(local, new LobbyHostPrepared { Version = 1, Code = "DEF456" });
        Check(!guest.Sent.Exists(m => m is LobbyHostCommit), "Only the selected successor can prepare the destination");
        host.MessageHandler.Deliver(guest, new LobbyHostPrepared { Version = 0, Code = "DEF456" });
        Check(!guest.Sent.Exists(m => m is LobbyHostCommit), "Stale host preparation is rejected");
        host.MessageHandler.Deliver(guest, new LobbyHostPrepared { Version = 1, Code = "DEF456" });
        Check(guest.Sent.Exists(m => m is LobbyHostCommit { Code: "DEF456" }), "Destination reaches every peer before disconnection");
        client.MessageHandler.Deliver(local, new LobbyHostCommit { Version = 1, Target = guest.Identity.NetId, Code = "DEF456" });
        Check(client.Sent.Exists(m => m is LobbyHostAck { Version: 1 }), "Client acknowledges the verified destination before disconnecting");
        host.MessageHandler.Deliver(local, new LobbyHostAck { Version = 1 });
        host.MessageHandler.Deliver(guest, new LobbyHostAck { Version = 1 });
        host.MessageHandler.Deliver(new Peer(), new LobbyHostAck { Version = 1 });
        Check(!guest.Sent.Exists(m => m is LobbyHostSwitch), "Migration waits for every admitted peer");
        host.MessageHandler.Deliver(other, new LobbyHostAck { Version = 0 });
        Check(!guest.Sent.Exists(m => m is LobbyHostSwitch), "Stale acknowledgements cannot commit migration");
        host.MessageHandler.Deliver(other, new LobbyHostAck { Version = 1 });
        Check(guest.Sent.Exists(m => m is LobbyHostSwitch), "Host switch requires every peer to know the destination");
        var switches = guest.Sent.FindAll(m => m is LobbyHostSwitch).Count;
        host.MessageHandler.Deliver(other, new LobbyHostAck { Version = 1 });
        Check(guest.Sent.FindAll(m => m is LobbyHostSwitch).Count == switches, "Duplicate acknowledgement cannot repeat the switch broadcast");
        client.MessageHandler.Deliver(local, new LobbyHostSwitch { Version = 0 });
        Check(switching == 0, "Stale switch is rejected by clients");
        client.MessageHandler.Deliver(local, new LobbyHostSwitch { Version = 1 });
        client.MessageHandler.Deliver(local, new LobbyHostSwitch { Version = 1 });
        Check(switching == 1 && destination == "DEF456" && !becameHost, "Former host reconnects as a guest exactly once");
        network.Stop();
        host.LocalPlayer = guest; client.Player = guest; host.Active = true;
        network.StartHost(new Room { code = "DEF456", joinKey = "new-secret" }, 4);
        foreach (var peer in new[] { guest, local, other })
            host.MessageHandler.Deliver(peer, new LobbyConnectionRequest { PlayerId = peer.PlayerId, Code = "DEF456", JoinKey = "new-secret" });
        Check(network.LobbyPlayers.Roster.Leader == guest.Identity.NetId && network.LobbyPlayers.Roster.TransportHost == guest.Identity.NetId,
            "Successor owns both the network connection and the crown after takeover");
        host.MessageHandler.Deliver(local, new LobbyCommand { Action = LobbyAction.Start });
        Check(!travel.Loading, "Former host loses start authority");
        host.MessageHandler.Deliver(guest, new LobbyCommand { Action = LobbyAction.Kick, Target = local.Identity.NetId });
        Check(local.Disconnected, "New host can kick the former host");
        foreach (var peer in new[] { guest, other }) peer.Identity.Character.Ready = true;
        guest.Identity.Character.Ready = false;
        host.MessageHandler.Deliver(guest, new LobbyCommand { Action = LobbyAction.Start });
        Check(!travel.Loading, "New host must also be in the stair trigger");
        guest.Identity.Character.Ready = true;
        host.MessageHandler.Deliver(new Peer(), new LobbyCommand { Action = LobbyAction.Start });
        Check(!travel.Loading, "Unadmitted sender cannot start");
        host.MessageHandler.Deliver(guest, new LobbyCommand { Action = LobbyAction.King, Target = other.Identity.NetId });
        guest.Identity.Character.Ready = false;
        Time.unscaledTime = 46;
        network.Tick();
        Check(other.Sent.Exists(m => m is LobbyHostCancel { Version: 2 }), "Timed-out replacement preserves the current lobby and notifies peers");
        Check(network.LobbyPlayers.Roster.Leader == guest.Identity.NetId, "Failed migration retains the current host");
        host.MessageHandler.Deliver(guest, new LobbyCommand { Action = LobbyAction.King, Target = other.Identity.NetId });
        host.MessageHandler.Deliver(other, new LobbyHostPrepared { Version = 3, Code = "invalid" });
        Check(other.Sent.Exists(m => m is LobbyHostCancel { Version: 3 }), "Invalid destination cancels transfer");
        host.MessageHandler.Deliver(guest, new LobbyCommand { Action = LobbyAction.King, Target = other.Identity.NetId });
        host.MessageHandler.Deliver(local, new LobbyHostCancel { Version = 4 });
        host.MessageHandler.Deliver(guest, new LobbyCommand { Action = LobbyAction.Start });
        Check(!travel.Loading, "Unselected guest cannot cancel another player's handoff");
        host.MessageHandler.Deliver(other, new LobbyHostCancel { Version = 4 });
        Check(other.Sent.Exists(m => m is LobbyHostCancel { Version: 4 }), "Successor can abort a failed transport preparation");
        guest.Identity.Character.Ready = true;
        host.MessageHandler.Deliver(guest, new LobbyCommand { Action = LobbyAction.Start });
        Check(travel.Loading, "Current host can resume gameplay after an aborted transfer");
        network.Dispose(); travel.Unconfigure();
    }

    static void TestKickRejoinAndGameMenuCommands()
    {
        Time.unscaledTime = 0;
        var server = new NetworkServer { Active = true };
        var client = new NetworkClient();
        var host = new Peer(); var guest = new Peer(); var other = new Peer();
        server.LocalPlayer = host; client.Player = host;
        server.AuthenticatedPlayers.AddRange(new[] { host, guest, other });
        var context = new HotelSessionContext { Connected = true };
        var travel = new HauntedHotelMessageTravel();
        Configure(travel, server, client, new ServerObjectManager(), new ClientObjectManager(), context);
        using var network = new HotelNetworkSession(server, client, new ServerObjectManager(), new ClientObjectManager(),
            new Mirage.SocketLayer.SocketFactory(), new NetworkIdentity(), new NetworkIdentity(),
            Array.Empty<NetworkIdentity>(), context, travel, _ => default);
        network.StartHost(new Room { code = "ABC123", joinKey = "secret" }, 4);
        server.Started.Invoke(); client.Started.Invoke();
        void Join(Peer peer) => server.MessageHandler.Deliver(peer,
            new LobbyConnectionRequest { Code = "ABC123", JoinKey = "secret", PlayerId = peer.PlayerId });
        foreach (var peer in new[] { host, guest, other }) Join(peer);
        var invalid = new Peer { PlayerId = "" }; Join(invalid);
        Check(!invalid.HasCharacter && network.PlayerCount == 3, "Missing identity cannot enter a lobby");
        var duplicate = new Peer { PlayerId = other.PlayerId }; Join(duplicate);
        Check(!duplicate.HasCharacter && network.PlayerCount == 3, "Concurrent duplicate player identity is rejected");
        server.MessageHandler.Deliver(host, new LobbyCommand { Action = LobbyAction.Kick, Target = guest.Identity.NetId });
        Check(guest.Disconnected && !guest.HasCharacter && network.PlayerCount == 2,
            "Kick immediately removes membership and despawns the avatar");
        Check(guest.Sent.OfType<LobbyConnectionResult>().Last().Message.Contains("kicked"), "Kicked player receives the reason");
        var rejoin = new Peer { PlayerId = guest.PlayerId }; Join(rejoin);
        Check(!rejoin.HasCharacter && !rejoin.Sent.OfType<LobbyConnectionResult>().Last().Accepted,
            "A new connection with the kicked player's identity cannot rejoin");
        var alternateCase = new Peer { PlayerId = guest.PlayerId.ToUpperInvariant() }; Join(alternateCase);
        Check(!alternateCase.HasCharacter, "Changing identity letter case cannot bypass a kick");
        Time.unscaledTime = .3f; network.Tick();
        Check(rejoin.Disconnected, "Rejected rejoin is disconnected after the rejection is delivered");
        server.Disconnected.Invoke(other);
        var voluntary = new Peer { PlayerId = other.PlayerId }; Join(voluntary);
        Check(voluntary.HasCharacter && network.PlayerCount == 2, "Voluntary departures can rejoin normally");
        server.MessageHandler.Deliver(host, new LobbyCommand { Action = LobbyAction.King, Target = voluntary.Identity.NetId });
        server.MessageHandler.Deliver(voluntary, new LobbyHostPrepared { Version = 1, Code = "DEF456" });
        var commit = voluntary.Sent.OfType<LobbyHostCommit>().Last();
        Check(commit.KickedPlayerIds.SequenceEqual(new[] { guest.PlayerId }), "Host transfer carries the kicked identities");
        client.MessageHandler.Deliver(host, new LobbyHostPrepare { Version = 1, Target = voluntary.Identity.NetId });
        client.MessageHandler.Deliver(host, commit);
        Check(network.LobbyPlayers.TransferKickedPlayerIds.SequenceEqual(commit.KickedPlayerIds), "Successor retains the received kick list");
        server.MessageHandler.Deliver(voluntary, new LobbyHostCancel { Version = 1 });
        typeof(HauntedHotelMessageTravel).GetField("<TargetScene>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(travel, "Game");
        server.MessageHandler.Deliver(host, new LobbyCommand { Action = LobbyAction.King, Target = voluntary.Identity.NetId });
        Check(network.LobbyPlayers.Roster.Leader == voluntary.Identity.NetId && network.LobbyPlayers.Roster.TransportHost == host.Identity.NetId,
            "In-game crowning changes the leader while keeping the running transport and scene");
        server.MessageHandler.Deliver(voluntary, new LobbyCommand { Action = LobbyAction.Kick, Target = host.Identity.NetId });
        Check(!host.Disconnected, "In-game leader cannot disconnect the transport host");
        var newcomer = new Peer(); Join(newcomer);
        server.MessageHandler.Deliver(host, new LobbyCommand { Action = LobbyAction.Kick, Target = newcomer.Identity.NetId });
        Check(!newcomer.Disconnected, "Former leader cannot kick after an in-game crown transfer");
        server.MessageHandler.Deliver(voluntary, new LobbyCommand { Action = LobbyAction.Kick, Target = newcomer.Identity.NetId });
        Check(newcomer.Disconnected && !newcomer.HasCharacter, "Leader can kick from the in-game member menu");
        var inGameRejoin = new Peer { PlayerId = newcomer.PlayerId }; Join(inGameRejoin);
        Check(!inGameRejoin.HasCharacter, "In-game kicks also block reconnection");
        network.Stop();
        network.StartHost(new Room { code = "ABC123", joinKey = "secret" }, 4, commit.KickedPlayerIds);
        var afterTransfer = new Peer { PlayerId = guest.PlayerId }; Join(afterTransfer);
        Check(!afterTransfer.HasCharacter, "A successor host enforces the transferred kick list");
        network.Stop();
        network.StartHost(new Room { code = "ABC123", joinKey = "secret" }, 4);
        var newLobby = new Peer { PlayerId = guest.PlayerId }; Join(newLobby);
        Check(newLobby.HasCharacter, "Kick restrictions end when a genuinely new lobby starts");
        travel.Unconfigure();
    }

    static void TestAdmissionDuringTravel()
    {
        var server = new NetworkServer { Active = true };
        var client = new NetworkClient();
        var guest = new Peer();
        server.AuthenticatedPlayers.Add(guest);
        var objects = new ServerObjectManager();
        var clientObjects = new ClientObjectManager();
        var context = new HotelSessionContext();
        var travel = new HauntedHotelMessageTravel();
        Configure(travel, server, client, objects, clientObjects, context);
        var placements = 0;
        using var network = new HotelNetworkSession(server, client, objects, clientObjects,
            new Mirage.SocketLayer.SocketFactory(), new NetworkIdentity(), new NetworkIdentity(),
            Array.Empty<NetworkIdentity>(), context, travel, _ =>
            {
                Check(!travel.Loading, "Admission does not consult an unavailable scene during travel");
                ++placements;
                return default;
            });
        network.StartHost(new Room { code = "ABC123", joinKey = "secret" }, 2);
        server.Started.Invoke();
        travel.GoToGame();
        Check(travel.Loading, "Admission test begins while destination is loading");
        server.MessageHandler.Deliver(guest, new LobbyConnectionRequest
        {
            PlayerId = guest.PlayerId, Code = "ABC123", JoinKey = "secret"
        });
        Check(guest.HasCharacter && !guest.SceneIsReady && placements == 0,
            "Mid-travel admission stages a gated character without a scene spawn lookup");
        travel.Unconfigure();
        network.PositionCharacters();
        Check(placements == 1, "Destination scene can place the staged character once loaded");
    }

    static void TestConnectionLifecycle()
    {
        Time.unscaledTime = 0;
        var server = new NetworkServer { Active = true };
        var client = new NetworkClient();
        var host = new Peer(); var guest = new Peer(); var overflow = new Peer();
        server.LocalPlayer = host;
        server.AuthenticatedPlayers.AddRange(new[] { host, guest, overflow });
        var objects = new ServerObjectManager(); var clientObjects = new ClientObjectManager();
        var context = new HotelSessionContext { Connected = true };
        var travel = new HauntedHotelMessageTravel();
        Configure(travel, server, client, objects, clientObjects, context);
        var network = new HotelNetworkSession(server, client, objects, clientObjects, new Mirage.SocketLayer.SocketFactory(),
            new NetworkIdentity(), new NetworkIdentity(), Array.Empty<NetworkIdentity>(), context, travel, _ => default);
        var lobby = network.LobbyPlayers;
        lobby.BindReadyZone(player => player != null && player.Ready);
        var joined = 0; var left = 0; var changed = 0; var admitted = 0;
        var rosterChanges = 0;
        lobby.RosterChanged += () => ++rosterChanges;
        HotelLobbyPlayer guestState = null;
        lobby.PlayerJoined += player => { ++joined; if (!player.IsHost) guestState = player; };
        lobby.PlayerLeft += _ => ++left;
        lobby.PlayerChanged += _ => ++changed;
        network.PlayerAdmitted += player =>
        {
            ++admitted;
            Check(player.HasCharacter && lobby.Players.Any(p => p.Id == player.Identity.NetId),
                "Connection observers receive a spawned avatar and an initialized lobby player");
        };
        network.StartHost(new Room { code = "ABC123", joinKey = "secret" }, 2);
        Check(context.RoomScope=="ABC123"&&context.ConnectionGeneration==1,"New host establishes a room inventory scope and connection generation");
        server.Started.Invoke(); client.Started.Invoke();
        server.MessageHandler.Deliver(host, new LobbyConnectionRequest { PlayerId = host.PlayerId, Code = "ABC123", JoinKey = "wrong" });
        Check(joined == 0 && !host.HasCharacter && network.PlayerCount == 0, "Rejected admission cannot spawn or create a lobby player");
        server.MessageHandler.Deliver(host, new LobbyConnectionRequest { PlayerId = host.PlayerId, Code = "ABC123", JoinKey = "secret" });
        server.MessageHandler.Deliver(host, new LobbyConnectionRequest { PlayerId = host.PlayerId, Code = "ABC123", JoinKey = "secret" });
        Check(joined == 1 && admitted == 1 && network.PlayerCount == 1, "Duplicate admission cannot duplicate join events");
        server.MessageHandler.Deliver(guest, new LobbyConnectionRequest { PlayerId = guest.PlayerId, Code = "ABC123", JoinKey = "secret" });
        Check(joined == 2 && guestState != null && !guestState.Ready, "Joining guest receives separate initial lobby state");
        server.MessageHandler.Deliver(overflow, new LobbyConnectionRequest { PlayerId = overflow.PlayerId, Code = "ABC123", JoinKey = "secret" });
        Check(joined == 2 && !overflow.HasCharacter, "Full lobby rejection leaves player state unchanged");
        var playerChanges = 0; var playerLeft = 0;
        guestState.Changed += () => ++playerChanges;
        guestState.Left += () => ++playerLeft;
        guest.Identity.Character.Ready = true;
        network.Tick();
        Check(guestState.Ready && changed == 1 && playerChanges == 1, "Readiness changes notify the player and lobby observers");
        var rosterChangesBefore = rosterChanges;
        var sentBefore = guest.Sent.OfType<LobbyRoster>().Count();
        Time.unscaledTime = .2f; network.Tick();
        Check(changed == 1 && playerChanges == 1, "Unchanged snapshots do not repeat player-change events");
        Check(rosterChanges == rosterChangesBefore && guest.Sent.OfType<LobbyRoster>().Count() == sentBefore,
            "Unchanged readiness does not broadcast a roster or refresh lobby observers");
        server.Disconnected.Invoke(guest);
        Check(left == 1 && playerLeft == 1 && !guestState.IsConnected && !guestState.Ready && network.PlayerCount == 1 && lobby.Players.Count() == 1,
            "Disconnection removes membership and emits one leave event after despawning");
        var snapshot = new LobbyRoster { Leader = 77, TransportHost = 77, Members = new[] { new LobbyMember { Id = 77, Ready = true } } };
        client.MessageHandler.Deliver(host, snapshot);
        var replica = lobby.Players.Single();
        Check(replica.Id == 77 && replica.IsHost && replica.Ready, "Clients reconcile independent player state from the authoritative roster");
        var joinedBefore = joined; var changedBefore = changed;
        rosterChangesBefore = rosterChanges;
        client.MessageHandler.Deliver(host, snapshot);
        Check(joined == joinedBefore && changed == changedBefore, "Duplicate client roster cannot repeat joins or state changes");
        Check(rosterChanges == rosterChangesBefore, "Duplicate client roster does not refresh lobby observers");
        client.MessageHandler.Deliver(host, new LobbyRoster { Leader = 88, TransportHost = 77, Members = snapshot.Members });
        Check(rosterChanges == rosterChangesBefore + 1 && lobby.Roster.Leader == 88,
            "Leadership changes notify observers even when membership and readiness are unchanged");
        var departingCharacter=host.Identity.Character;
        network.Dispose();
        Check(context.RoomScope==""&&context.ConnectionGeneration==2&&departingCharacter.InventoryCleared,"Stopping a session invalidates scope and clears all surviving character inventories");
        Check(!lobby.Players.Any(), "Session disposal clears lobby players");
        Check(server.Authenticated.Count == 0 && server.Started.Count == 1 && client.Started.Count == 1 &&
            client.Authenticated.Count == 0 && client.Disconnected.Count == 0, "Disposal removes connection and lobby subscriptions without removing scene travel listeners");
        travel.Unconfigure();
    }

}
namespace UnityEngine
{
    public sealed class SerializeField : Attribute { }
    public class Object
    {
        public static implicit operator bool(Object value) => value != null;
        public static T Instantiate<T>(T value) where T : Object => (T)(Object)new Mirage.NetworkIdentity();
        public static T Instantiate<T>(T value, Vector3 position, Quaternion rotation) where T : Object => Instantiate(value);
        public static void DontDestroyOnLoad(GameObject value) { }
        public static void Destroy(GameObject value) { }
        public static T[] FindObjectsByType<T>(FindObjectsSortMode mode) => Array.Empty<T>();
    }
    public class MonoBehaviour : UnityEngine.Object
    {
        readonly List<IEnumerator> routines = new();
        public object StartCoroutine(IEnumerator routine) { routines.Add(routine); routine.MoveNext(); return routine; }
        public void StopAllCoroutines() => routines.Clear();
        static void Drain(IEnumerator routine)
        {
            do { if (routine.Current is IEnumerator child) { child.MoveNext(); Drain(child); } } while (routine.MoveNext());
        }
        public void CompleteLoads() { foreach (var routine in routines.ToArray()) Drain(routine); routines.Clear(); }
    }
    public struct Vector3 { }
    public struct Quaternion { public static Quaternion identity => default; }
    public class Collider : Object { }
    public enum FindObjectsSortMode { None }
    public static class Time { public static float unscaledTime; }
    public static class PlayerPrefs
    {
        static readonly Dictionary<string, string> values = new();
        public static string GetString(string key, string fallback) => values.TryGetValue(key, out var value) ? value : fallback;
        public static void SetString(string key, string value) => values[key] = value;
        public static void Save() { }
    }
    public static class Application { public static bool CanLoad = true; public static bool CanStreamedLevelBeLoaded(string name) => CanLoad; }
    public class GameObject : UnityEngine.Object { public UnityEngine.SceneManagement.Scene scene; public bool activeInHierarchy = true; }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene { public string name; }
    public enum LoadSceneMode { Single }
    public static class SceneManager
    {
        public static object LoadSceneAsync(string name, LoadSceneMode mode) => null;
        public static Scene GetActiveScene() => new Scene { name = "Lobby" };
    }
}
namespace Mirage
{
    public class Signal
    {
        event Action listeners;
        public int Count => listeners?.GetInvocationList().Length ?? 0;
        public void AddListener(Action listener) => listeners += listener;
        public void RemoveListener(Action listener) => listeners -= listener;
        public void Invoke() => listeners?.Invoke();
    }
    public class Signal<T>
    {
        event Action<T> listeners;
        public int Count => listeners?.GetInvocationList().Length ?? 0;
        public void AddListener(Action<T> listener) => listeners += listener;
        public void RemoveListener(Action<T> listener) => listeners -= listener;
        public void Invoke(T value) => listeners?.Invoke(value);
    }
    public class MessageHandler
    {
        readonly Dictionary<Type, Delegate> handlers = new();
        public void RegisterHandler<T>(Action<INetworkPlayer, T> handler, bool allowUnauthenticated) => handlers[typeof(T)] = handler;
        public void Deliver<T>(INetworkPlayer peer, T value) => ((Action<INetworkPlayer, T>)handlers[typeof(T)])(peer, value);
    }
    public interface INetworkPlayer
    {
        bool SceneIsReady { get; set; }
        bool HasCharacter { get; }
        NetworkIdentity Identity { get; }
        void Send<T>(T message);
        void Disconnect();
    }
    public class Peer : INetworkPlayer
    {
        public string PlayerId = Guid.NewGuid().ToString("N");
        public bool SceneIsReady { get; set; } = true;
        public bool HasCharacter => Owned != null;
        public NetworkIdentity Owned;
        public NetworkIdentity Identity => Owned;
        public readonly List<object> Sent = new();
        public bool Disconnected;
        public void Send<T>(T message) => Sent.Add(message);
        public void Disconnect() => Disconnected = true;
    }
    public class World { public List<NetworkIdentity> SpawnedIdentities = new(); }
    public class NetworkServer : UnityEngine.Object
    {
        public bool Active;
        public ServerObjectManager ObjectManager;
        public Mirage.SocketLayer.SocketFactory SocketFactory;
        public int MaxConnections;
        public Signal Stopped = new();
        public Signal<INetworkPlayer> Authenticated = new();
        public void StartServer(NetworkClient client) { Active = true; }
        public void Stop() { Active = false; }
        public Signal Started = new();
        public Signal<INetworkPlayer> Disconnected = new();
        public MessageHandler MessageHandler = new();
        public List<INetworkPlayer> AuthenticatedPlayers = new();
        public INetworkPlayer LocalPlayer;
        public World World = new();
        public List<object> Sent = new();
        public void SendToAll<T>(T message, bool authenticatedOnly, bool excludeLocalPlayer) => Sent.Add(message);
    }
    public enum ClientStoppedReason { Disconnected }
    public class NetworkClient : UnityEngine.Object
    {
        public bool Active;
        public INetworkPlayer Player;
        public ClientObjectManager ObjectManager;
        public Mirage.SocketLayer.SocketFactory SocketFactory;
        public Signal<INetworkPlayer> Authenticated = new();
        public Signal<ClientStoppedReason> Disconnected = new();
        public void Connect(string address, ushort port) { Active = true; }
        public Signal Started = new();
        public MessageHandler MessageHandler = new();
        public List<object> Sent = new();
        public bool WasDisconnected;
        public void Send<T>(T message) => Sent.Add(message);
        public void Disconnect() => WasDisconnected = true;
    }
    public struct SpawnMessage { }
    public class NetworkIdentity : UnityEngine.Object
    {
        static uint next;
        public uint NetId = ++next;
        public int PrefabHash;
        public bool IsSpawned = true;
        public HotelPlayer Character = new();
        public GameObject gameObject = new();
        public T GetComponent<T>() => (T)(object)Character;
    }
    public class ServerObjectManager
    {
        public List<INetworkPlayer> Visible = new();
        public void Destroy(NetworkIdentity identity) { }
        public void Spawn(NetworkIdentity identity) { }
        public void AddCharacter(INetworkPlayer player, NetworkIdentity identity) => ((Peer)player).Owned = identity;
        public void DestroyCharacter(INetworkPlayer player) => ((Peer)player).Owned = null;
        public void SpawnVisibleObjects(INetworkPlayer player) => Visible.Add(player);
        public void SpawnSceneObjects() { }
    }
    public class ClientObjectManager
    {
        public void PrepareToSpawnSceneObjects() { }
        public void UnregisterSpawnHandler(int hash) { }
        public void RegisterSpawnHandler(NetworkIdentity prefab, Func<SpawnMessage, NetworkIdentity> spawn, Action<NetworkIdentity> destroy) { }
        public void RegisterPrefab(NetworkIdentity prefab) { }
    }
}
namespace HauntedFish.Multiplayer
{
    public class HotelPlayer : UnityEngine.Object
    {
        public bool Ready;
        public bool InventoryCleared;
        public void ClearRoundInventory(){InventoryCleared=true;}
        public void Configure(HotelSessionContext context) { }
        public void ResetSceneMotion() { }
        public void Teleport(Vector3 position) { }
    }
}


namespace Mirage.SocketLayer { public class SocketFactory { } }
namespace Monologue.Dialogue { public static class StoryFunctions { public static void ApplyNetworkCue(HauntedFish.Multiplayer.SharedWorldCue cue) { } } }
