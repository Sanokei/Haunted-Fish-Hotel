using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // Local presentation only; restore the authored camera settings when leaving a zone.
    public sealed class LobbyCameraZone : MonoBehaviour
    {
        public Collider Volume;
        public CinemachineCamera Camera;
        public int Priority = 20;
        [Tooltip("Only set a Follow target when this existing zone camera is authored to follow.")]
        public bool FollowOwnedPlayer;

        static readonly HashSet<LobbyCameraZone> _Zones = new HashSet<LobbyCameraZone>();
        static LobbyCameraZone _Active;
        static int _EvaluatedFrame = -1;
        PrioritySettings _PreviousPriority;
        Transform _PreviousFollow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetStatics()
        {
            _Zones.Clear();
            _Active = null;
            _EvaluatedFrame = -1;
        }

        void Awake() { if (!Volume) Volume = GetComponent<Collider>(); }
        void OnEnable() { _Zones.Add(this); _EvaluatedFrame = -1; }
        void OnDisable()
        {
            _Zones.Remove(this);
            _EvaluatedFrame = -1;
            if (_Active != this) return;
            Restore();
            _Active = null;
        }

        bool Contains(Vector3 point, float radius)
        {
            if (!Volume || !Volume.enabled || !Volume.isTrigger || !Camera || gameObject.scene.name != "Lobby") return false;
            return (Volume.ClosestPoint(point) - point).sqrMagnitude <= radius * radius;
        }

        void LateUpdate()
        {
            // Evaluate all zones once, using synchronized poses even when client physics is disabled.
            if (_EvaluatedFrame == Time.frameCount) return;
            _EvaluatedFrame = Time.frameCount;
            var owner = HotelPlayer.LocalPlayer;
            var controller = owner ? owner.GetComponent<CharacterController>() : null;
            LobbyCameraZone chosen = null;
            if (controller)
            {
                var point = owner.transform.TransformPoint(controller.center);
                foreach (var zone in _Zones)
                    if (zone && zone.isActiveAndEnabled && zone.Contains(point, controller.radius) &&
                        (!chosen || zone.Priority > chosen.Priority ||
                        (zone.Priority == chosen.Priority && zone.GetInstanceID() < chosen.GetInstanceID())))
                        chosen = zone;
            }
            if (_Active != chosen)
            {
                if (_Active) _Active.Restore();
                _Active = chosen;
                if (_Active)
                {
                    _Active._PreviousPriority = _Active.Camera.Priority;
                    _Active._PreviousFollow = _Active.Camera.Follow;
                    _Active.Camera.Priority = _Active.Priority;
                }
            }
            if (_Active && _Active.Camera && _Active.FollowOwnedPlayer && _Active.Camera.Follow != owner.transform)
                _Active.Camera.Follow = owner.transform;
        }

        void Restore()
        {
            if (!Camera) return;
            Camera.Priority = _PreviousPriority;
            if (FollowOwnedPlayer) Camera.Follow = _PreviousFollow;
        }
    }
}
