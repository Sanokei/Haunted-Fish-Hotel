using System;
using Mirage;

namespace HauntedFish.Multiplayer
{
    // Lobby state belongs to each player, separate from movement, admission and UI.
    public sealed class HotelLobbyPlayer
    {
        public uint Id { get; }
        public bool IsConnected { get; private set; } = true;
        public bool Ready { get; private set; }
        public bool IsHost { get; private set; }
        public event Action Changed;
        public event Action Left;

        internal HotelLobbyPlayer(uint id) => Id = id;
        internal HotelLobbyPlayer(INetworkPlayer connection)
        {
            Id = connection.Identity.NetId;
        }

        internal bool Apply(bool ready, bool host)
        {
            if (Ready == ready && IsHost == host) return false;
            Ready = ready;
            IsHost = host;
            Changed?.Invoke();
            return true;
        }

        internal void OnDisconnection()
        {
            if (!IsConnected) return;
            IsConnected = false;
            Ready = IsHost = false;
            Left?.Invoke();
        }
    }
}
