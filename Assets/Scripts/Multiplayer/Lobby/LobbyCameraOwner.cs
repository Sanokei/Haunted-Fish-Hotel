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
        public void Reset() => _Target = null;
        public void Bind(Transform target)
        {
            _Target = target;
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
            var progress = Mathf.Clamp01(Vector3.Dot(target.position - _StartPosition, forward)
                / Mathf.Max(.01f, ForwardDistance));
            // Knot units explicitly limit movement to the first segment, even if the rail is extended.
            return spline.Spline.ConvertIndexUnit(progress, PathIndexUnit.Knot, positionUnits);
        }
    }
}
