using System;
using System.Text.RegularExpressions;
namespace HauntedFish.Multiplayer
{
    // Injected into both host and client avatars; no global session lookup is required.
    public sealed class HotelSessionContext
    {
        public bool Connected { get; internal set; }
        public bool Loading { get; internal set; }
        public bool InputFocused { get; internal set; }
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
    public struct LobbyHello { public string Code, JoinKey, Reservation; }
    public struct LobbyWelcome { public bool Accepted; public string Message; }
    public struct SharedWorldCue
    {
        public int Type, X, Y;
        public string First, Second;
        public float Delay;
        public bool Flag;
    }
    public enum DialogueAudience { RelevantPlayer, Everyone }
}

