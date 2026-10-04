namespace HauntedFish.Multiplayer
{
    public enum HotelSessionState
    {
        Idle,
        PreparingTransport,
        RequestingRoom,
        Connecting,
        AwaitingPlayer,
        Connected,
        Leaving,
        Failed
    }

    internal static class HotelSessionProgress
    {
        public static float For(HotelSessionState state)
        {
            switch (state)
            {
                case HotelSessionState.PreparingTransport: return .15f;
                case HotelSessionState.RequestingRoom: return .4f;
                case HotelSessionState.Connecting: return .7f;
                case HotelSessionState.AwaitingPlayer: return .9f;
                case HotelSessionState.Connected: return 1f;
                default: return 0f;
            }
        }
    }
}
