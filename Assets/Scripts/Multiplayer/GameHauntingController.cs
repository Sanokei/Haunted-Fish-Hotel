using System.Collections.Generic;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // An authored non-network scene may supply the same round-scoped world/floor.
    // This only chooses the context; authority, readiness and participant checks remain below.
    public interface ITrapDamageContext
    {
        bool DamageContextActive { get; }
        GhostPlacementWorld DamageWorld { get; }
        float DamageFloorY { get; }
    }

    // Death is authoritative; the original ghost stays assigned and dead fish spectate.
    public sealed class GameHauntingController : MonoBehaviour
    {
        public const float ContactMargin = .15f;
        [SerializeField, Min(0)] float _ContactDamagePerSecond = 50;
        readonly Dictionary<HotelPlayer, int> _Seen = new Dictionary<HotelPlayer, int>();
        string _RoundKey, _RoomScope;
        int _ObservedVersion;
        ITrapDamageContext _Context;
        GhostPlacementWorld _ContextWorld;
        public void BindContext(ITrapDamageContext context) { _Context = context; _ContextWorld = context?.DamageWorld; Clear(); }
        public void UnbindContext(ITrapDamageContext context)
        {
            if (ReferenceEquals(_Context, context)) { _Context = null; _ContextWorld = null; Clear(); }
        }
        bool TryContext(out GhostPlacementWorld world, out float floorY)
        {
            world = null; floorY = 0;
            if (!isActiveAndEnabled) return false;
            if (_Context != null)
            {
                if (_Context is Object owner && !owner || _Context is Behaviour behaviour && !behaviour.isActiveAndEnabled || !_Context.DamageContextActive) return false;
                world = _Context.DamageWorld; floorY = _Context.DamageFloorY;
                if (world != _ContextWorld) return false;
            }
            else
            {
                var scene = GameSceneController.Current;
                if (!scene || !scene.isActiveAndEnabled) return false;
                world = scene.PlacementWorld; floorY = scene.HallwayBounds.max.y;
            }
            return world && world.isActiveAndEnabled && !string.IsNullOrEmpty(world.RoundKey) &&
                !string.IsNullOrEmpty(world.RoomScope) && TrapImpactRules.Finite(floorY);
        }
        void OnEnable() => HotelPlayer.PlayerDisabled += ForgetPlayer;
        void ForgetPlayer(HotelPlayer player)
        {
            // Another scoped observer may own this actor's effect.
            if (_Seen.Remove(player) && player && player.DeathEmergence) player.DeathEmergence.Stop();
        }

        public void ApplyTrapDamage(GhostTrap trap, Vector3 previousPosition, float seconds)
        {
            if (!trap || !TryContext(out var world, out _) || trap.World != world || !TrapImpactRules.Finite(seconds) || seconds <= 0 ||
                !trap.HasMotionContacts && (trap.Position - previousPosition).sqrMagnitude < .000001f && trap.Phase != TrapPhase.Active) return;
            var players = HotelPlayer.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                var fish = players[i];
                if (!CanAffect(fish) || !fish.BodyController ||
                    !(trap.HadMotionContact(fish) || !trap.PiercesBodies && trap.TouchesBody(fish.BodyController, ContactMargin))) continue;
                if (!trap.CanBeginImpact(fish)) continue;
                bool applied = trap.Impact == TrapImpact.KnockbackStun
                    ? fish.ApplyTrapStun(trap.MotionDirection, trap.KnockbackDistance, trap.KnockbackSeconds, trap.StunSeconds)
                    : ApplyDamage(fish, TrapImpactRules.Damage(trap.Impact, _ContactDamagePerSecond, seconds));
                // Settling/disabled movement may reject a stun. Only a successful
                // effect consumes this contact episode, so it can be tried later.
                if (applied) trap.RecordImpact(fish);
            }
        }

        bool CanAffect(HotelPlayer fish)
        {
            return TryContext(out var world, out _) && fish && fish.ControlsReady && fish.RoundReleased && !fish.IsDead &&
                !fish.InBossFight && !fish.BossHallwayLocked && fish.ControlMode == HotelControlMode.Fish &&
                (!fish.Networked || _Context == null && fish.IsServer) && fish.RoundStateKey == world.RoundKey &&
                fish.RoundVersion == world.RoundVersion && fish.InventoryScope == world.RoomScope;
        }

        public bool ApplyDamage(HotelPlayer fish, float amount)
        {
            if (!CanAffect(fish) || !TrapImpactRules.Finite(amount) || amount <= 0) return false;
            if (!TryContext(out _, out var floorY)) return false;
            fish.Health = Mathf.Max(0, fish.Health - amount);
            if (fish.Health > 0) return true;
            // Commit once before publishing the existing production emergence event.
            // Subsequent hits and old fish input packets are rejected by Spectator mode.
            fish.BecomeSpectator(floorY);
            fish.GhostEmergencePosition = fish.transform.position;
            fish.GhostEmergenceVersion++;
            return true;
        }

        void Update()
        {
            string key, scope; int version;
            bool offline = _Context != null;
            if (offline)
            {
                if (!TryContext(out var world, out _)) { Clear(); return; }
                key = world.RoundKey; scope = world.RoomScope; version = world.RoundVersion;
            }
            else
            {
                var local = HotelPlayer.LocalPlayer;
                if (!local || !local.ControlsReady || string.IsNullOrEmpty(local.RoundStateKey)) { Clear(); return; }
                key = local.RoundStateKey; scope = local.InventoryScope; version = local.RoundVersion;
            }
            if (_RoundKey != key || _RoomScope != scope || _ObservedVersion != version)
            { Clear(); _RoundKey = key; _RoomScope = scope; _ObservedVersion = version; }
            var players = HotelPlayer.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (!player.ControlsReady || player.RoundStateKey != key || player.InventoryScope != scope ||
                    player.RoundVersion != version || offline && player.Networked)
                { ForgetPlayer(player); continue; }
                if (!_Seen.TryGetValue(player, out var seen)) { _Seen[player] = player.GhostEmergenceVersion; continue; }
                _Seen[player] = player.GhostEmergenceVersion;
                if (player.GhostEmergenceVersion > seen && player.IsDead && player.DeathEmergence)
                    player.DeathEmergence.PlayAt(player.GhostEmergencePosition);
            }
        }
        void Clear()
        {
            foreach (var player in _Seen.Keys)
                if (player && player.DeathEmergence) player.DeathEmergence.Stop();
            _Seen.Clear(); _RoundKey = _RoomScope = null; _ObservedVersion = 0;
        }
        void OnDisable() { HotelPlayer.PlayerDisabled -= ForgetPlayer; Clear(); }
    }
}
