using System.Collections.Generic;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // Unity geometry adapter. The world remains the simulation/authority owner;
    // speed integration and remembered body intervals have no Unity dependency.
    internal sealed class GhostTrapPassageSweep
    {
        readonly GhostTrap _Trap;
        readonly TrapPassageMemory<HotelPlayer> _Memory = new TrapPassageMemory<HotelPlayer>();
        readonly List<TrapPassageInterval> _Intervals = new List<TrapPassageInterval>();
        readonly List<TrapPassageInterval> _Body = new List<TrapPassageInterval>();
        readonly List<HotelPlayer> _Candidates = new List<HotelPlayer>();
        RaycastHit[] _Hits = new RaycastHit[8];
        public GhostTrapPassageSweep(GhostTrap trap) { _Trap = trap; }
        public void Clear() { _Memory.Clear(); _Intervals.Clear(); _Candidates.Clear(); }

        public float Advance(float axis, float speed, float factor, float seconds, ISet<HotelPlayer> contacts)
        {
            if (!TrapImpactRules.Finite(axis) || Mathf.Abs(axis) < .00001f || !TrapImpactRules.Finite(seconds) || seconds <= 0 ||
                !TrapImpactRules.Finite(speed) || speed <= 0 || !TrapImpactRules.Finite(factor) || factor <= 0 || factor > 1) return 0;
            float sign = Mathf.Sign(axis), start = _Trap.Position.x;
            var limits = _Trap.Limits;
            float maxDistance = Mathf.Max(0, sign > 0 ? limits.y - start : start - limits.x);
            maxDistance = Mathf.Min(maxDistance, Mathf.Abs(axis) * speed * seconds);
            if (maxDistance <= 0) return 0;
            var half = _Trap.HalfExtents;
            half.x = _Trap.BodyHalfSize.x;
            int count;
            while ((count = Physics.BoxCastNonAlloc(_Trap.CollisionBounds.center, half, Vector3.right * sign, _Hits,
                _Trap.transform.rotation, maxDistance, ~0, QueryTriggerInteraction.Ignore)) == _Hits.Length)
                System.Array.Resize(ref _Hits, _Hits.Length * 2);
            for (int i = 0; i < count; i++)
            {
                var collider = _Hits[i].collider;
                if (!collider || collider.transform.IsChildOf(_Trap.transform) || collider.GetComponentInParent<HotelPlayer>()) continue;
                maxDistance = Mathf.Min(maxDistance, Mathf.Max(0, _Hits[i].distance - .01f));
            }

            _Memory.RetireExited(start, sign);
            _Intervals.Clear();
            _Candidates.Clear();
            _Memory.AppendIntervals(_Intervals, start, sign);
            foreach (var fish in HotelPlayer.ActivePlayers)
            {
                if (!Eligible(fish) || _Memory.Contains(fish)) continue;
                _Body.Clear();
                _Trap.CollectHorizontalPassages(fish.BodyController, _Body);
                bool candidate = false;
                foreach (var relative in _Body)
                {
                    var along = relative.Along(0, sign);
                    if (along.End < 0 || along.Start > maxDistance) continue;
                    _Intervals.Add(along);
                    candidate = true;
                }
                if (candidate) _Candidates.Add(fish);
            }

            float distance = TrapPassageMotion.Travel(seconds, Mathf.Abs(axis) * speed, maxDistance, factor, _Intervals);
            foreach (var fish in _Candidates)
            {
                _Body.Clear();
                _Trap.CollectHorizontalPassages(fish.BodyController, _Body);
                bool hit = false;
                foreach (var relative in _Body)
                {
                    var along = relative.Along(0, sign);
                    if (along.End >= 0 && along.Start <= distance) { hit = true; break; }
                }
                if (!hit) continue;
                contacts.Add(fish);
                for (int i = 0; i < _Body.Count; i++) _Body[i] = _Body[i].Offset(start);
                _Memory.Remember(fish, _Body);
            }
            _Memory.RetireExited(start + sign * distance, sign);
            return sign * distance;
        }

        bool Eligible(HotelPlayer fish) => fish && fish.ControlsReady && fish.RoundReleased && !fish.InBossFight && !fish.BossHallwayLocked &&
            fish.ControlMode == HotelControlMode.Fish && fish.Movement && fish.Movement.SimulationReady && fish.BodyController &&
            (!_Trap.World || fish.RoundStateKey == _Trap.World.RoundKey && fish.InventoryScope == _Trap.World.RoomScope);
    }
}
