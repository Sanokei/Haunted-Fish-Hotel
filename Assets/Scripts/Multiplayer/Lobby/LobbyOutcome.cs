namespace HauntedFish.Multiplayer
{
    // Which lobby outro this client plays, seen from the local player's side of the finished match.
    public enum LobbyOutroKind { None, GhostWin, GhostDefeat, FishWin, FishDefeat }

    // Survives the Game -> Lobby scene travel; the lobby takes it once on arrival.
    public static class LobbyOutcome
    {
        public static LobbyOutroKind Pending { get; private set; }
        // Call before returning to the lobby with the local player's role in the match that just ended.
        public static void Record(bool localWasGhost, bool fishWon) =>
            Pending = localWasGhost ? (fishWon ? LobbyOutroKind.GhostDefeat : LobbyOutroKind.GhostWin)
                                    : (fishWon ? LobbyOutroKind.FishWin : LobbyOutroKind.FishDefeat);
        public static LobbyOutroKind Take()
        {
            var kind = Pending;
            Pending = LobbyOutroKind.None;
            return kind;
        }
    }
}
