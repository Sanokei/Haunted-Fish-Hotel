using System;
using System.Text.RegularExpressions;
namespace HauntedFish.Multiplayer
{
    [Serializable] public class Room
    {
        public string code, address, joinKey, ownerKey, error;
        public int port, capacity, players;
    }
    [Serializable] public class CreateRoom { public string address; public int port, capacity; }
    [Serializable] public class RoomHeartbeat { public string ownerKey; public int players; }
    public static class LobbyCode
    {
        public static bool TryNormalize(string input, out string code)
        {
            code = (input ?? "").Trim().ToUpperInvariant();
            return Regex.IsMatch(code, "^[A-Z0-9]{6}$");
        }
    }
    public struct LobbyHello { public string Code, JoinKey; }
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

