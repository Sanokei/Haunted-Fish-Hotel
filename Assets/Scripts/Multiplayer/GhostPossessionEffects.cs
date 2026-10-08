using System.Collections.Generic;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    public sealed class GhostPossessionEffects : MonoBehaviour
    {
        [SerializeField]
        PossessionSmoke _Prefab;
        readonly Dictionary<HotelPlayer, int> _Seen = new Dictionary<HotelPlayer, int>();
        readonly List<PossessionSmoke> _Active = new List<PossessionSmoke>();
        string _RoundKey;
        void OnEnable() => HotelPlayer.PlayerDisabled += ForgetPlayer;
        void ForgetPlayer(HotelPlayer player) => _Seen.Remove(player);
        void Update()
        {
            var local = HotelPlayer.LocalPlayer;
            if (!local || !local.ControlsReady)
            {
                if (_RoundKey != null)
                {
                    Clear();
                    _RoundKey = null;
                }

                return;
            }

            if (_RoundKey != local.RoundStateKey)
            {
                Clear();
                _RoundKey = local.RoundStateKey;
            }

            if (string.IsNullOrEmpty(_RoundKey))
                return;
            var players = HotelPlayer.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (!player.ControlsReady)
                {
                    _Seen.Remove(player);
                    continue;
                }

                if (player.RoundStateKey != _RoundKey || player.InventoryScope != local.InventoryScope)
                    continue;
                if (!_Seen.TryGetValue(player, out var seen))
                {
                    _Seen[player] = player.PossessionEffectVersion;
                    continue;
                }

                _Seen[player] = player.PossessionEffectVersion;
                if (local.GhostSetupReady && player.PossessionEffectVersion > seen && _Prefab)
                {
                    var puff = Instantiate(_Prefab);
                    puff.PlayAt(player.PossessionEffectPosition);
                    _Active.Add(puff);
                }
            }

            for (int i = _Active.Count - 1; i >= 0; i--)
                if (!_Active[i])
                    _Active.RemoveAt(i);
        }

        void Clear()
        {
            foreach (var puff in _Active)
                if (puff)
                {
                    puff.gameObject.SetActive(false);
                    Destroy(puff.gameObject);
                }

            _Active.Clear();
            _Seen.Clear();
        }

        void OnDisable()
        {
            HotelPlayer.PlayerDisabled -= ForgetPlayer;
            Clear();
            _RoundKey = null;
        }
    }
}
