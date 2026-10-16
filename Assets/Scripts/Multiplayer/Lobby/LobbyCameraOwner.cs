using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Splines;

namespace HauntedFish.Multiplayer
{
    // Bind the spline dolly to this client's player, including late network spawns.
    [RequireComponent(typeof(CinemachineCamera))]
    public sealed class LobbyCameraOwner : MonoBehaviour
    {
        CinemachineCamera _Camera;
        Transform _PreviousFollow;

        void OnEnable()
        {
            _Camera = GetComponent<CinemachineCamera>();
            _PreviousFollow = _Camera.Follow;
            HotelPlayer.LocalPlayerChanged += BindPlayer;
            BindPlayer(HotelPlayer.LocalPlayer);
        }

        void OnDisable()
        {
            HotelPlayer.LocalPlayerChanged -= BindPlayer;
            if (_Camera) _Camera.Follow = _PreviousFollow;
        }

        void BindPlayer(HotelPlayer player)
        {
            if (!_Camera) return;
            _Camera.Follow = player ? player.transform : null;
            var dolly = _Camera.GetComponent<CinemachineSplineDolly>();
            if (dolly && dolly.AutomaticDolly.Method is LobbyForwardDolly follow)
            {
                follow.Bind(_Camera.Follow);
                dolly.CameraPosition = 0;
            }
            _Camera.PreviousStateIsValid = false;
        }
    }

    [System.Serializable]
    public sealed class LobbyForwardDolly : SplineAutoDolly.ISplineAutoDolly
    {
        [Tooltip("Player forward travel in world units needed to reach knot 1.")]
        [Min(.01f)] public float ForwardDistance = 7f;
        [System.NonSerialized] Transform _Target;
        [System.NonSerialized] Vector3 _StartPosition;

        public bool RequiresTrackingTarget => true;
        public void Validate() => ForwardDistance = Mathf.Max(.01f, ForwardDistance);
        [Tooltip("How far the player can walk forward before the camera starts following.")]
        [Min(0f)] public float DeadZoneForward = 2f;
        [Tooltip("How far the player can walk back toward the camera before it retreats.")]
        [Min(0f)] public float DeadZoneBack = 1f;
        [System.NonSerialized] bool _HasStart;
        [System.NonSerialized] float _Travel;

        // Cinemachine calls Reset on re-enable/cut; keep the spawn anchor so the mapping stays
        // position-based and the camera rails back when the player returns toward it.
        public void Reset() { }
        public void Bind(Transform target)
        {
            if (target == _Target && _HasStart) return;
            _Target = target;
            _HasStart = target;
            _Travel = 0f;
            if (target) _StartPosition = target.position;
        }

        public float GetSplinePosition(MonoBehaviour sender, Transform target, SplineContainer spline,
            float currentPosition, PathIndexUnit positionUnits, float deltaTime)
        {
            if (!target || !spline || spline.Spline.Count < 2) return currentPosition;
            if (_Target != target) Bind(target);
            var start = spline.transform.TransformPoint((Vector3)spline.Spline[0].Position);
            var end = spline.transform.TransformPoint((Vector3)spline.Spline[1].Position);
            var forward = Vector3.ProjectOnPlane(end - start, Vector3.up).normalized;
            // Dead zone: the camera holds while the player moves inside a window around its
            // current spot and only follows once they pass the window's front or back edge.
            var travel = Vector3.Dot(target.position - _StartPosition, forward);
            if (travel > _Travel + DeadZoneForward) _Travel = travel - DeadZoneForward;
            else if (travel < _Travel - DeadZoneBack) _Travel = travel + DeadZoneBack;
            _Travel = Mathf.Clamp(_Travel, 0f, ForwardDistance);
            var progress = _Travel / Mathf.Max(.01f, ForwardDistance);
            // Knot units explicitly limit movement to the first segment, even if the rail is extended.
            return spline.Spline.ConvertIndexUnit(progress, PathIndexUnit.Knot, positionUnits);
        }
    }
}
