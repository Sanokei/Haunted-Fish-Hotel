using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace HauntedFish.Multiplayer
{
    // Injected into both host and client avatars; no global session lookup is required.
    public sealed class HotelSessionContext
    {
        public bool Connected { get; internal set; }
        public bool Loading { get; internal set; }
        public bool InputFocused { get; private set; }
        public event Action<bool> InputFocusChanged;
        internal void SetInputFocused(bool focused)
        {
            if (InputFocused == focused) return;
            InputFocused = focused;
            InputFocusChanged?.Invoke(focused);
        }
    }
    [Serializable] public class Room
    {
        // The directory-issued invitation secret prevents an unrelated connection from claiming this room by code alone.
        public string code, address, joinKey, ownerKey, error, reservation, status;
        public int port, capacity, players;
    }
    [Serializable] public class CreateRoom { public string address; public int port, capacity; }
    // Refresh the directory lease and player count; abrupt host exits are handled by lease expiration.
    [Serializable] public class RoomHeartbeat { public string ownerKey; public int players; }
    // Use a normalized six-character room code to resolve the directory entry for this particular host.
    public static class LobbyCode
    {
        public static bool TryNormalize(string input, out string code)
        {
            code = (input ?? "").Trim().ToUpperInvariant();
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            return Regex.IsMatch(code, "^[A-Z0-9]{6}$");
        }
    }
    // Verify the directory-issued invitation secret as well as the public room code before admitting a peer.
    public struct LobbyConnectionRequest { public string Code, JoinKey, Reservation; }
    public struct LobbyConnectionResult { public bool Accepted; public string Message; }
    public struct LobbyMember { public uint Id; public bool Ready; }
    public struct LobbyRoster { public uint Leader, TransportHost; public LobbyMember[] Members; }
    public enum LobbyAction { Start, Kick, King }
    public struct LobbyCommand { public LobbyAction Action; public uint Target; }
    public struct LobbyHostPrepare { public int Version; public uint Target; }
    public struct LobbyHostPrepared { public int Version; public string Code; }
    public struct LobbyHostCommit { public int Version; public uint Target; public string Code; }
    public struct LobbyHostAck { public int Version; }
    public struct LobbyHostSwitch { public int Version; }
    public struct LobbyHostCancel { public int Version; public string Message; }

    public static class LobbyRules
    {
        public static bool RosterMatches(LobbyRoster roster, uint leader, uint host, IReadOnlyList<LobbyMember> members)
        {
            if (roster.Leader != leader || roster.TransportHost != host) return false;
            var previous = roster.Members ?? Array.Empty<LobbyMember>();
            if (previous.Length != members.Count) return false;
            for (var i = 0; i < previous.Length; ++i)
                if (previous[i].Id != members[i].Id || previous[i].Ready != members[i].Ready) return false;
            return true;
        }
        public static bool AllReady(LobbyMember[] members)
        {
            if (members == null || members.Length == 0) return false;
            foreach (var member in members) if (!member.Ready) return false;
            return true;
        }
    }
    public struct SharedWorldCue
    {
        public int Type, X, Y;
        public string First, Second;
        public float Delay;
        public bool Flag;
    }
    public enum DialogueAudience { RelevantPlayer, Everyone }
}

