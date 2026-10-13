using System;

namespace HauntedFish.Multiplayer
{
    public enum RoundHowToStatus { WaitingForPlayer, FinishingIntroduction, Reading, MenuOpen, CanReady, SendingReady, Ready }

    // Local UI policy only. Replicated readiness and the countdown remain host-owned.
    public static class RoundHowToState
    {
        public static RoundHowToStatus Status(bool hasLocal, bool cinematicComplete, bool ready, bool submitted,
            bool blocked, bool visible, float readSeconds, float minimum)
        {
            if (!hasLocal) return RoundHowToStatus.WaitingForPlayer;
            if (ready) return RoundHowToStatus.Ready;
            if (submitted) return RoundHowToStatus.SendingReady;
            if (!cinematicComplete) return RoundHowToStatus.FinishingIntroduction;
            if (blocked || !visible) return RoundHowToStatus.MenuOpen;
            minimum = GameplayPresentationPolicy.Finite(minimum) ? Math.Max(0, minimum) : 1.5f;
            return GameplayPresentationPolicy.Finite(readSeconds) && readSeconds >= minimum
                ? RoundHowToStatus.CanReady : RoundHowToStatus.Reading;
        }

        public static string Label(RoundHowToStatus status)
        {
            switch (status)
            {
                case RoundHowToStatus.Ready: return "YOU ARE READY";
                case RoundHowToStatus.SendingReady: return "READY SENT";
                case RoundHowToStatus.WaitingForPlayer: return "WAITING FOR CONNECTION";
                case RoundHowToStatus.FinishingIntroduction: return "FINISHING INTRODUCTION";
                case RoundHowToStatus.Reading: return "READ, THEN READY UP";
                case RoundHowToStatus.MenuOpen: return "CLOSE MENU TO READY";
                default: return "READY UP";
            }
        }

        public static string Progress(int ready, int participants)
        {
            participants = Math.Max(0, participants);
            ready = Math.Max(0, Math.Min(ready, participants));
            return ready + " / " + participants + " PLAYERS READY";
        }
    }
}
