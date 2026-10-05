using UnityEngine;

namespace HauntedFish.Multiplayer
{
    public static class LobbyTrigger
    {
        // Use the player center, including on clients whose CharacterController is disabled.
        // This avoids relying on trigger callbacks from remote replicas or multiple colliders.
        public static bool Contains(Collider volume, HotelPlayer player)
        {
            if (!volume || !volume.enabled || !volume.isTrigger || !volume.gameObject.activeInHierarchy ||
                volume.gameObject.scene.name != "Lobby" || !player || !player.gameObject.activeInHierarchy) return false;
            var controller = player.GetComponent<CharacterController>();
            var point = player.transform.TransformPoint(controller ? controller.center : Vector3.zero);
            return (volume.ClosestPoint(point) - point).sqrMagnitude < .0001f;
        }
    }
}
