using System;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // Scene-scoped adapter: no global scene lookup and no RPC or replicated reply ownership.
    internal sealed class GamePlayerCommands : IHotelGameCommands
    {
        readonly GhostPlacementWorld _World;
        readonly TrapManager _Traps;
        readonly Action<HotelPlayer, int> _Acknowledge;

        public GamePlayerCommands(GhostPlacementWorld world, TrapManager traps,
            Action<HotelPlayer, int> acknowledge)
        {
            _World = world;
            _Traps = traps;
            _Acknowledge = acknowledge;
        }

        public void Acknowledge(HotelPlayer player, int version) => _Acknowledge(player, version);

        public void RefreshPlacement(HotelPlayer player)
        {
            if (_World) player.PlacedObjectsJson = _World.Snapshot;
        }

        public void AcceptFlightInput(HotelPlayer player, Vector2 axis, string key, int version)
        {
            if (_World) _World.AcceptFlightInput(player, axis, key, version);
        }

        public bool TakePackage(HotelPlayer player, int id, Vector3 position, string key, int version)
            => _Traps && _Traps.TryTake(player, id, position, key, version);

        public bool PlaceTrap(HotelPlayer player, Vector3 position, string family, string key, int version)
            => Matches(player, key, version) && _World.PlacementNearFlight(player, position, family) &&
                _World.TryPlace(player, position, family);

        public bool DisposeTrap(HotelPlayer player, int id, string key, int version)
            => Matches(player, key, version) && _World.Dispose(player, id);

        public void PossessTrap(HotelPlayer player, int id, Vector3 position, string key, int version)
        {
            if (Matches(player, key, version)) _World.Possess(player, id, position);
        }

        public void ActOnTrap(HotelPlayer player, int id, int kind, float axis, Vector3 point, string key, int version)
        {
            if (kind < 0 || kind > (int)TrapInputKind.Reset || !Matches(player, key, version)) return;
            _World.AcceptTrapAction(player, id, new TrapInput((TrapInputKind)kind, axis, point));
        }

        bool Matches(HotelPlayer player, string key, int version)
            => _World && _World.RequestMatches(player, key, version);
    }
}
