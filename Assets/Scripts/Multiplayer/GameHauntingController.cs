using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HauntedFish.Multiplayer
{
    // The authority changes roles while preserving each peer's network identity and input owner.
    public sealed class GameHauntingController : MonoBehaviour
    {
        [SerializeField, Min(1)] float _SpookThreshold = 100;
        [SerializeField, Min(0)] float _ContactSpookPerSecond = 50;
        [SerializeField, Min(0)] float _SwapCooldown = 1;
        [SerializeField] PossessionSmoke _EmergencePrefab;
        [SerializeField] Text _Meter;
        readonly Dictionary<HotelPlayer, int> _Seen = new Dictionary<HotelPlayer, int>();
        readonly List<PossessionSmoke> _Active = new List<PossessionSmoke>();
        string _RoundKey;
        float _NextSwap;
        int _DisplayedSpook = -1;
        public float SpookThreshold => float.IsNaN(_SpookThreshold) || float.IsInfinity(_SpookThreshold) ? 100 : Mathf.Max(1, _SpookThreshold);
        void OnEnable() => HotelPlayer.PlayerDisabled += ForgetPlayer;
        void ForgetPlayer(HotelPlayer player) => _Seen.Remove(player);

        public void ApplyTrapSpook(GhostTrap trap, Vector3 previousPosition, float seconds)
        {
            if (!trap || seconds <= 0 || (trap.Position - previousPosition).sqrMagnitude < .000001f && trap.Phase != TrapPhase.Active) return;
            var contact = trap.CollisionBounds;
            contact.Expand(.3f);
            var players = HotelPlayer.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                var fish = players[i];
                if (fish.BodyController && contact.Intersects(fish.BodyController.bounds))
                    AddSpook(fish, _ContactSpookPerSecond * seconds);
            }
        }

        public bool AddSpook(HotelPlayer fish, float amount)
        {
            var scene = GameSceneController.Current;
            if (!scene || !scene.PlacementWorld || !fish || !fish.ControlsReady || !fish.RoundReleased || fish.InBossFight || fish.BossHallwayLocked ||
                fish.ControlMode != HotelControlMode.Fish || fish.Networked && !fish.IsServer ||
                fish.RoundStateKey != scene.PlacementWorld.RoundKey || fish.InventoryScope != scene.PlacementWorld.RoomScope ||
                float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0 || Time.unscaledTime < _NextSwap) return false;
            fish.Spook = Mathf.Min(SpookThreshold, fish.Spook + amount);
            if (fish.Spook < SpookThreshold) return true;
            HotelPlayer previous = null;
            var players = HotelPlayer.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
                if (players[i].NetId == fish.GhostId && players[i].RoundStateKey == fish.RoundStateKey && players[i].InventoryScope == fish.InventoryScope)
                    previous = players[i];
            if (!previous || previous == fish || previous.ControlMode != HotelControlMode.Ghost) return true;
            var bodyPosition = fish.transform.position;
            _NextSwap = Time.unscaledTime + Mathf.Max(0, _SwapCooldown);
            foreach (var member in players)
                if (member.RoundStateKey == fish.RoundStateKey && member.InventoryScope == fish.InventoryScope)
                {
                    member.GhostId = fish.NetId;
                    member.ResetEditorRole();
                    member.ResetSceneMotion();
                }
            previous.Spook = fish.Spook = 0;
            previous.TakeFishBody(fish);
            fish.GhostEmergencePosition = bodyPosition;
            fish.GhostEmergenceVersion++;
            scene.PlacementWorld.TransferGhost(previous, fish, bodyPosition + Vector3.up);
            return true;
        }

        void Update()
        {
            var local = HotelPlayer.LocalPlayer;
            if (!local || !local.ControlsReady || string.IsNullOrEmpty(local.RoundStateKey)) { Clear(); return; }
            if (_RoundKey != local.RoundStateKey) { Clear(); _RoundKey = local.RoundStateKey; }
            if (_Meter)
            {
                bool visible = local.RoundReleased && local.ControlMode == HotelControlMode.Fish && !local.InBossFight && !local.BossHallwayLocked;
                _Meter.gameObject.SetActive(visible);
                int value = Mathf.CeilToInt(local.Spook);
                if (value != _DisplayedSpook) { _DisplayedSpook = value; _Meter.text = "SPOOK  " + value + " / " + Mathf.CeilToInt(SpookThreshold); }
            }
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
            _Active.Clear(); _Seen.Clear(); _RoundKey = null; _DisplayedSpook = -1;
            if (_Meter) _Meter.gameObject.SetActive(false);
        }
        void OnDisable() { HotelPlayer.PlayerDisabled -= ForgetPlayer; Clear(); }
    }
}
