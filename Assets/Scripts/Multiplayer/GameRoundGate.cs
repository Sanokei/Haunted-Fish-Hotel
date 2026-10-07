using System.Collections.Generic;

namespace HauntedFish.Multiplayer
{
    // Every participant must complete this round's full presentation before controls are released.
    public sealed class GameRoundGate
    {
        public const float MinimumDuration = 9;
        readonly Dictionary<uint, float> _Waiting = new Dictionary<uint, float>();
        int _Version;
        public bool Complete => _Waiting.Count == 0;
        public void Begin(IEnumerable<uint> players, int version, float now)
        {
            _Waiting.Clear();
            foreach (uint id in players) _Waiting[id] = now + MinimumDuration;
            _Version = version;

        }
        public void Join(uint player, int version, float now) { if (version == _Version && !_Waiting.ContainsKey(player)) _Waiting[player] = now + MinimumDuration; }
        public bool Finish(uint player, int version, float now)
        {
            if (version != _Version || !_Waiting.TryGetValue(player, out var earliest) || now < earliest) return false;
            return _Waiting.Remove(player);
        }
        public void Remove(uint player) => _Waiting.Remove(player);
    }
}
