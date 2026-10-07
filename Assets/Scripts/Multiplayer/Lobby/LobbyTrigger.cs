using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HauntedFish.Multiplayer
{
    [RequireComponent(typeof(Collider))]
    public sealed class LobbyTrigger : MonoBehaviour
    {
        public event UnityAction<Collider, HotelPlayer, bool> ZonePresenceChanged;
        readonly List<HotelPlayer> _Departed = new List<HotelPlayer>();
        readonly Dictionary<HotelPlayer, HashSet<Collider>> _Players = new Dictionary<HotelPlayer, HashSet<Collider>>();
        [SerializeField] Collider _Zone;
        public bool Contains(HotelPlayer player) => isActiveAndEnabled && player && _Players.ContainsKey(player) && ContainsPosition(player);
        public bool ContainsPosition(HotelPlayer player)
        {
            if (!isActiveAndEnabled || !player || !player.isActiveAndEnabled || !player.gameObject.activeInHierarchy) return false;
            var zone = _Zone;
            if (!zone || !zone.enabled || !zone.gameObject.activeInHierarchy) return false;
            // Readiness must reflect current occupancy even after spawning, teleporting or
            // a domain reload without an enter callback. Use the controller's center even
            // on clients where movement disables the CharacterController.
            var controller = player.BodyController;
            var point = controller ? controller.transform.TransformPoint(controller.center) : player.transform.position;
            return (zone.ClosestPoint(point) - point).sqrMagnitude < .000001f;
        }
        void Awake()
        {
            if (!_Zone) _Zone = GetComponent<Collider>();
        }
        void OnEnable() => HotelPlayer.PlayerDisabled += RemovePlayer;
        void RemovePlayer(HotelPlayer player)
        {
            if (!_Players.Remove(player)) return;
            if (player) ZonePresenceChanged?.Invoke(_Zone, player, false);
        }
        void LateUpdate()
        {
            _Departed.Clear();
            foreach (var player in _Players.Keys)
                if (!ContainsPosition(player)) _Departed.Add(player);
            for (int i=0;i<_Departed.Count;i++) RemovePlayer(_Departed[i]);
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
                if (ContainsPosition(player)) listener(_Zone, player, true);
        }
        void OnDisable()
        {
            HotelPlayer.PlayerDisabled -= RemovePlayer;
            foreach (var player in _Players.Keys)
                if (player) ZonePresenceChanged?.Invoke(_Zone, player, false);
            _Players.Clear();
        }
    }
}
