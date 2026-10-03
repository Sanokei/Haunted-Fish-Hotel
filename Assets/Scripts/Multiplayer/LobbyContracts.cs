using System;
using System.Text.RegularExpressions;
namespace HauntedFish.Multiplayer
{
    // These small serialized messages separate room-directory admission, shared story state and server-issued world effects.
    [Serializable] public class Room
    {
        // The directory-issued invitation secret prevents an unrelated connection from claiming this room by code alone.
        public string code, address, joinKey, ownerKey, error;
        // These small serialized messages separate room-directory admission, shared story state and server-issued world effects.
        public int port, capacity, players;
    }
    // These small serialized messages separate room-directory admission, shared story state and server-issued world effects.
    [Serializable] public class CreateRoom { public string address; public int port, capacity; }
    // Refresh the directory lease and player count; abrupt host exits are handled by lease expiration.
    [Serializable] public class RoomHeartbeat { public string ownerKey; public int players; }
    // Use a normalized six-character room code to resolve the directory entry for this particular host.
    public static class LobbyCode
    {
        // These small serialized messages separate room-directory admission, shared story state and server-issued world effects.
        public static bool TryNormalize(string input, out string code)
        {
            // These small serialized messages separate room-directory admission, shared story state and server-issued world effects.
            code = (input ?? "").Trim().ToUpperInvariant();
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            return Regex.IsMatch(code, "^[A-Z0-9]{6}$");
        }
    }
    // Verify the directory-issued invitation secret as well as the public room code before admitting a peer.
    public struct LobbyHello { public string Code, JoinKey; }
    // These small serialized messages separate room-directory admission, shared story state and server-issued world effects.
    public struct LobbyWelcome { public bool Accepted; public string Message; }
    // These small serialized messages separate room-directory admission, shared story state and server-issued world effects.
    public struct SharedWorldCue
    {
        // These small serialized messages separate room-directory admission, shared story state and server-issued world effects.
        public int Type, X, Y;
        // These small serialized messages separate room-directory admission, shared story state and server-issued world effects.
        public string First, Second;
        // These small serialized messages separate room-directory admission, shared story state and server-issued world effects.
        public float Delay;
        // These small serialized messages separate room-directory admission, shared story state and server-issued world effects.
        public bool Flag;
    }
    // These small serialized messages separate room-directory admission, shared story state and server-issued world effects.
    public enum DialogueAudience { RelevantPlayer, Everyone }
}

