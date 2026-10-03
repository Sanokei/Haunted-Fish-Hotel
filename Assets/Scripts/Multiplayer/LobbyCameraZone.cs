using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
namespace HauntedFish.Multiplayer
{
    // Optional authored Lobby zones only. The existing Brain, lens, body and blends are untouched.
    // Camera zones are local presentation. Only the owned player can activate a zone; priority and follow targets are restored when it leaves.
    public sealed class LobbyCameraZone : MonoBehaviour
    {
        // Camera zones are local presentation. Only the owned player can activate a zone; priority and follow targets are restored when it leaves.
        public Collider Volume;
        // Camera zones are local presentation. Only the owned player can activate a zone; priority and follow targets are restored when it leaves.
        public CinemachineCamera Camera;
        // Use Cinemachine priority to enter a local zone, retaining the original value so leaving restores the authored composition.
        public int Priority=20;
        // Camera zones are local presentation. Only the owned player can activate a zone; priority and follow targets are restored when it leaves.
        [Tooltip("Only set a Follow target when this existing zone camera is authored to follow.")]
        // Camera zones are local presentation. Only the owned player can activate a zone; priority and follow targets are restored when it leaves.
        public bool FollowOwnedPlayer;
        // Camera zones are local presentation. Only the owned player can activate a zone; priority and follow targets are restored when it leaves.
        static readonly HashSet<LobbyCameraZone> zones=new HashSet<LobbyCameraZone>();
        // Camera zones are local presentation. Only the owned player can activate a zone; priority and follow targets are restored when it leaves.
        static LobbyCameraZone active;
        // Camera zones are local presentation. Only the owned player can activate a zone; priority and follow targets are restored when it leaves.
        static int evaluatedFrame=-1;
        // Use Cinemachine priority to enter a local zone, retaining the original value so leaving restores the authored composition.
        PrioritySettings previousPriority;
        Transform previousFollow;
        // Camera zones are local presentation. Only the owned player can activate a zone; priority and follow targets are restored when it leaves.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        // Camera zones are local presentation. Only the owned player can activate a zone; priority and follow targets are restored when it leaves.
        static void ResetStatics() { zones.Clear(); active=null; evaluatedFrame=-1; }
        // Read a component already serialized on this object; do not create a runtime replacement for missing authoring.
        void Awake() { if (!Volume) Volume=GetComponent<Collider>(); }
        // Subscribe while this existing component is active so presentation reacts to the current story or scene.
        void OnEnable() { zones.Add(this); evaluatedFrame=-1; }
        // Unsubscribe on deactivation to prevent stale listeners from acting on a later scene/session.
        void OnDisable()
        {
            // Camera zones are local presentation. Only the owned player can activate a zone; priority and follow targets are restored when it leaves.
            zones.Remove(this); evaluatedFrame=-1;
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (active==this) { Restore(); active=null; }
        }
        // Camera zones are local presentation. Only the owned player can activate a zone; priority and follow targets are restored when it leaves.
        bool Contains(HotelPlayer player)
        {
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            if (!Volume || !Volume.enabled || !Volume.isTrigger || !Camera || gameObject.scene.name!="Lobby") return false;
            // Read a component already serialized on this object; do not create a runtime replacement for missing authoring.
            var controller=player.GetComponent<CharacterController>();
            // Camera zones are local presentation. Only the owned player can activate a zone; priority and follow targets are restored when it leaves.
            var point=player.transform.TransformPoint(controller.center);
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            return Vector3.Distance(Volume.ClosestPoint(point),point)<=controller.radius;
        }
        // Service local presentation and authoritative simulation each frame, with ownership/readiness checks inside the path.
        void LateUpdate()
        {
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            if (evaluatedFrame==Time.frameCount) return;
            // Camera zones are local presentation. Only the owned player can activate a zone; priority and follow targets are restored when it leaves.
            evaluatedFrame=Time.frameCount;
            // Camera zones are local presentation. Only the owned player can activate a zone; priority and follow targets are restored when it leaves.
            HotelPlayer owner=null;
            // Look up objects already present in the loaded scenes; ownership and scene checks select the appropriate one.
            foreach (var player in FindObjectsByType<HotelPlayer>(FindObjectsSortMode.None))
                // Ownership restricts this path to the local player, so a remote avatar cannot take local input or camera focus.
                if (player.IsRelevantPlayer && (!player.Networked || player.Identity.IsSpawned)) { owner=player; break; }
            // Camera zones are local presentation. Only the owned player can activate a zone; priority and follow targets are restored when it leaves.
            LobbyCameraZone chosen=null;
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (owner)
                // Camera zones are local presentation. Only the owned player can activate a zone; priority and follow targets are restored when it leaves.
                foreach (var zone in zones)
                    // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
                    if (zone && zone.isActiveAndEnabled && zone.Contains(owner) &&
                        // Use Cinemachine priority to enter a local zone, retaining the original value so leaving restores the authored composition.
                        (!chosen || zone.Priority>chosen.Priority || (zone.Priority==chosen.Priority && zone.GetInstanceID()<chosen.GetInstanceID()))) chosen=zone;
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (active!=chosen)
            {
                // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
                if (active) active.Restore();
                // Camera zones are local presentation. Only the owned player can activate a zone; priority and follow targets are restored when it leaves.
                active=chosen;
                // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
                if (active)
                {
                    // Use Cinemachine priority to enter a local zone, retaining the original value so leaving restores the authored composition.
                    active.previousPriority=active.Camera.Priority;
                    // Assign the local owned character as the follow target of this authored virtual camera.
                    active.previousFollow=active.Camera.Follow;
                    // Use Cinemachine priority to enter a local zone, retaining the original value so leaving restores the authored composition.
                    active.Camera.Priority=active.Priority;
                }
            }
            // Assign the local owned character as the follow target of this authored virtual camera.
            if (active && active.FollowOwnedPlayer) active.Camera.Follow=owner.transform;
        }
        // Camera zones are local presentation. Only the owned player can activate a zone; priority and follow targets are restored when it leaves.
        void Restore()
        {
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            if (!Camera) return;
            // Use Cinemachine priority to enter a local zone, retaining the original value so leaving restores the authored composition.
            Camera.Priority=previousPriority;
            // Assign the local owned character as the follow target of this authored virtual camera.
            if (FollowOwnedPlayer) Camera.Follow=previousFollow;
        }
    }
}
