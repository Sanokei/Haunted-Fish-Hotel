using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HauntedFish.Multiplayer
{
    [RequireComponent(typeof(Collider))]
    public sealed class LobbyTrigger : MonoBehaviour
    {
        public event UnityAction<Collider, HotelPlayer, bool> ZonePresenceChanged;
        readonly Dictionary<HotelPlayer, HashSet<Collider>> _Players = new Dictionary<HotelPlayer, HashSet<Collider>>();
        [SerializeField] Collider _Zone;
        public bool Contains(HotelPlayer player) => isActiveAndEnabled && player && _Players.ContainsKey(player);
        public bool ContainsPosition(HotelPlayer player)
        {
            if (!isActiveAndEnabled || !player || !player.gameObject.activeInHierarchy) return false;
            var zone = _Zone ? _Zone : GetComponent<Collider>();
            if (!zone || !zone.enabled || !zone.gameObject.activeInHierarchy) return false;
            // Readiness must reflect current occupancy even after spawning, teleporting or
            // a domain reload without an enter callback. Use the controller's center even
            // on clients where movement disables the CharacterController.
            var controller = player.GetComponent<CharacterController>();
            var point = controller ? controller.transform.TransformPoint(controller.center) : player.transform.position;
            return (zone.ClosestPoint(point) - point).sqrMagnitude < .000001f;
        }
        void Awake()
        {
            if (!_Zone) _Zone = GetComponent<Collider>();
        }
        void OnTriggerStay(Collider other) => OnTriggerEnter(other);
        void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<HotelPlayer>();
            if (!player || !player.CompareTag("Player")) return;
            if (!_Players.TryGetValue(player, out var colliders))
            {
                colliders = new HashSet<Collider>();
                _Players.Add(player, colliders);
            }
            if (colliders.Add(other) && colliders.Count == 1)
                ZonePresenceChanged?.Invoke(_Zone, player, true);
        }
        void OnTriggerExit(Collider other)
        {
            var player = other.GetComponentInParent<HotelPlayer>();
            if (!player || !_Players.TryGetValue(player, out var colliders) || !colliders.Remove(other)) return;
            if (colliders.Count != 0) return;
            _Players.Remove(player);
            ZonePresenceChanged?.Invoke(_Zone, player, false);
        }
        public void Replay(UnityAction<Collider, HotelPlayer, bool> listener)
        {
            foreach (var player in _Players.Keys)
                if (player) listener(_Zone, player, true);
        }
        void OnDisable()
        {
            foreach (var player in _Players.Keys)
                if (player) ZonePresenceChanged?.Invoke(_Zone, player, false);
            _Players.Clear();
        }
    }
}
