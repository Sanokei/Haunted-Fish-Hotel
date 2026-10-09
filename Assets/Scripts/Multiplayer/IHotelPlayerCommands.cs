using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // Scene owners bind these endpoints for the lifetime of a player's scene membership.
    // RPC identity/ownership stays on HotelPlayer; round and gameplay checks stay here.
    public interface IHotelGameCommands
    {
        void Acknowledge(HotelPlayer player, int version);
        void RefreshPlacement(HotelPlayer player);
        void AcceptFlightInput(HotelPlayer player, Vector2 axis, string key, int version);
        bool TakePackage(HotelPlayer player, int id, Vector3 position, string key, int version);
        bool PlaceTrap(HotelPlayer player, Vector3 position, string family, string key, int version);
        bool DisposeTrap(HotelPlayer player, int id, string key, int version);
        void PossessTrap(HotelPlayer player, int id, Vector3 position, string key, int version);
        void ActOnTrap(HotelPlayer player, int id, int kind, float axis, Vector3 point, string key, int version);
    }

    public interface IHotelBossCommands
    {
        bool TryEnter(HotelPlayer player, string key, int version);
        bool AcceptReady(HotelPlayer player, string key, int version, uint epoch);
        bool AcceptInput(HotelPlayer player, Vector2 axis, string key, int version, uint epoch);
    }
}
