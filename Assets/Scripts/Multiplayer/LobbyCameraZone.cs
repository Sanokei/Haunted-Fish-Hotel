using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
namespace HauntedFish.Multiplayer
{
    // Optional authored Lobby zones only. The existing Brain, lens, body and blends are untouched.
    public sealed class LobbyCameraZone : MonoBehaviour
    {
        public Collider Volume;
        public CinemachineCamera Camera;
        public int Priority=20;
        [Tooltip("Only set a Follow target when this existing zone camera is authored to follow.")]
        public bool FollowOwnedPlayer;
        static readonly HashSet<LobbyCameraZone> zones=new HashSet<LobbyCameraZone>();
        static LobbyCameraZone active;
        static int evaluatedFrame=-1;
        PrioritySettings previousPriority;
        Transform previousFollow;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetStatics() { zones.Clear(); active=null; evaluatedFrame=-1; }
        void Awake() { if (!Volume) Volume=GetComponent<Collider>(); }
        void OnEnable() { zones.Add(this); evaluatedFrame=-1; }
        void OnDisable()
        {
            zones.Remove(this); evaluatedFrame=-1;
            if (active==this) { Restore(); active=null; }
        }
        bool Contains(HotelPlayer player)
        {
            if (!Volume || !Volume.enabled || !Volume.isTrigger || !Camera || gameObject.scene.name!="Lobby") return false;
            var controller=player.GetComponent<CharacterController>();
            var point=player.transform.TransformPoint(controller.center);
            return Vector3.Distance(Volume.ClosestPoint(point),point)<=controller.radius;
        }
        void LateUpdate()
        {
            if (evaluatedFrame==Time.frameCount) return;
            evaluatedFrame=Time.frameCount;
            HotelPlayer owner=null;
            foreach (var player in FindObjectsByType<HotelPlayer>(FindObjectsSortMode.None))
                if (player.IsRelevantPlayer && (!player.Networked || player.Identity.IsSpawned)) { owner=player; break; }
            LobbyCameraZone chosen=null;
            if (owner)
                foreach (var zone in zones)
                    if (zone && zone.isActiveAndEnabled && zone.Contains(owner) &&
                        (!chosen || zone.Priority>chosen.Priority || (zone.Priority==chosen.Priority && zone.GetInstanceID()<chosen.GetInstanceID()))) chosen=zone;
            if (active!=chosen)
            {
                if (active) active.Restore();
                active=chosen;
                if (active)
                {
                    active.previousPriority=active.Camera.Priority;
                    active.previousFollow=active.Camera.Follow;
                    active.Camera.Priority=active.Priority;
                }
            }
            if (active && active.FollowOwnedPlayer) active.Camera.Follow=owner.transform;
        }
        void Restore()
        {
            if (!Camera) return;
            Camera.Priority=previousPriority;
            if (FollowOwnedPlayer) Camera.Follow=previousFollow;
        }
    }
}
