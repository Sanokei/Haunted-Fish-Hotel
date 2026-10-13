using System.Collections.Generic;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // Death is authoritative; the original ghost stays assigned and dead fish spectate.
    public sealed class GameHauntingController : MonoBehaviour
    {
        [SerializeField, Min(0)] float _ContactDamagePerSecond = 50;
        [SerializeField] PossessionSmoke _EmergencePrefab;
        readonly Dictionary<HotelPlayer, int> _Seen = new Dictionary<HotelPlayer, int>();
        readonly List<PossessionSmoke> _Active = new List<PossessionSmoke>();
        string _RoundKey;
        void OnEnable() => HotelPlayer.PlayerDisabled += ForgetPlayer;
        void ForgetPlayer(HotelPlayer player) => _Seen.Remove(player);

        public void ApplyTrapDamage(GhostTrap trap, Vector3 previousPosition, float seconds)
        {
            if (!trap || seconds <= 0 || (trap.Position - previousPosition).sqrMagnitude < .000001f && trap.Phase != TrapPhase.Active) return;
            var players = HotelPlayer.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                var fish = players[i];
                if (fish.BodyController && (trap.HadMotionContact(fish) || trap.TouchesBody(fish.BodyController, .15f)))
                    ApplyDamage(fish, _ContactDamagePerSecond * seconds);
            }
        }

        public bool ApplyDamage(HotelPlayer fish, float amount)
        {
            var scene = GameSceneController.Current;
            if (!scene || !scene.PlacementWorld || !fish || !fish.ControlsReady || !fish.RoundReleased || fish.InBossFight || fish.BossHallwayLocked ||
                fish.ControlMode != HotelControlMode.Fish || fish.Networked && !fish.IsServer ||
                fish.RoundStateKey != scene.PlacementWorld.RoundKey || fish.InventoryScope != scene.PlacementWorld.RoomScope ||
                float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0) return false;
            fish.Health = Mathf.Max(0, fish.Health - amount);
            if (fish.Health > 0) return true;
            // Commit once before publishing the existing production emergence event.
            // Subsequent hits and old fish input packets are rejected by Spectator mode.
            fish.BecomeSpectator(scene.HallwayBounds.max.y);
            fish.GhostEmergencePosition = fish.transform.position;
            fish.GhostEmergenceVersion++;
            return true;
        }

        void Update()
        {
            var local = HotelPlayer.LocalPlayer;
            if (!local || !local.ControlsReady || string.IsNullOrEmpty(local.RoundStateKey)) { Clear(); return; }
            if (_RoundKey != local.RoundStateKey) { Clear(); _RoundKey = local.RoundStateKey; }
            var players = HotelPlayer.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player.RoundStateKey != _RoundKey || player.InventoryScope != local.InventoryScope) continue;
                if (!_Seen.TryGetValue(player, out var seen)) { _Seen[player] = player.GhostEmergenceVersion; continue; }
                _Seen[player] = player.GhostEmergenceVersion;
                if (player.GhostEmergenceVersion > seen && _EmergencePrefab)
                {
                    var effect = Instantiate(_EmergencePrefab);
                    effect.PlayAt(player.GhostEmergencePosition);
                    _Active.Add(effect);
                }
            }
            for (int i = _Active.Count - 1; i >= 0; i--) if (!_Active[i]) _Active.RemoveAt(i);
        }
        void Clear()
        {
            foreach (var effect in _Active) if (effect) Destroy(effect.gameObject);
            _Active.Clear(); _Seen.Clear(); _RoundKey = null;
        }
        void OnDisable() { HotelPlayer.PlayerDisabled -= ForgetPlayer; Clear(); }
    }
}
