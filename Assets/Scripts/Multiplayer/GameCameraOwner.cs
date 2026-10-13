using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
namespace HauntedFish.Multiplayer
{
    // Game-only framing for the local player. Cinemachine still owns camera positioning.
    public sealed class GameCameraOwner : MonoBehaviour
    {
        public CinemachineCamera Camera;
        public Transform FollowOverride { get; set; }
        [Min(.1f)] public float RestingSize = 3.8f;
        [Range(0, .5f)] public float MovingExpansion = .15f;
        [Range(0, .25f)] public float MousePadding = .08f;
        [Min(.01f)] public float MouseSmoothTime = .18f;
        [Min(.01f)] public float ZoomSmoothTime = .35f;
        public Vector2 FollowDamping = new Vector2(.12f, .2f);

        HotelPlayer _Player;
        HotelPlayerMovement _Movement;
        CinemachinePositionComposer _Composer;
        CinemachineCamera _BoundCamera;
        Vector3 _PreviousPosition, _MouseOffset, _OffsetVelocity;
        float _ZoomVelocity;
        void OnEnable()
        {
            HotelPlayer.LocalPlayerChanged += BindPlayer;
            BindPlayer(HotelPlayer.LocalPlayer);
        }
        void OnDisable()
        {
            HotelPlayer.LocalPlayerChanged -= BindPlayer;
            if (Camera) Camera.Follow = null;
            _Player = null;
            _Movement = null;
        }
        public void ResetFollow() => BindPlayer(HotelPlayer.LocalPlayer);

        void BindPlayer(HotelPlayer player)
        {
            if (_BoundCamera != Camera)
            {
                if (_BoundCamera) _BoundCamera.Follow = null;
                _BoundCamera=Camera;
                _Composer=Camera ? Camera.GetComponent<CinemachinePositionComposer>() : null;
            }
            if (!Camera) return;
            _Player = player;
            _Movement = player ? player.Movement : null;
            _MouseOffset = _OffsetVelocity = Vector3.zero;
            _ZoomVelocity = 0;
            if (player) _PreviousPosition = player.transform.position;
            Camera.Lens.OrthographicSize = RestingSize;
            Camera.Follow = ViewTarget();
            Camera.PreviousStateIsValid = false;
            if (_Composer)
            {
                _Composer.TargetOffset = Vector3.zero;
                _Composer.Damping = new Vector3(FollowDamping.x, FollowDamping.y, 0);
            }
        }

        Transform ViewTarget()
        {
            var local = HotelPlayer.LocalPlayer;
            if (!local || local.ControlMode != HotelControlMode.Spectator)
                return FollowOverride ? FollowOverride : local ? local.transform : null;
            HotelPlayer living = null, ghost = null;
            foreach (var member in HotelPlayer.ActivePlayers)
            {
                if (!member || member.RoundStateKey != local.RoundStateKey || member.InventoryScope != local.InventoryScope) continue;
                if (member.ControlMode == HotelControlMode.Fish && !member.IsDead && (!living || member.NetId < living.NetId)) living = member;
                if (member.NetId == local.GhostId) ghost = member;
            }
            return living ? living.transform : ghost ? ghost.transform : local.transform;
        }
        void Update()
        {
            if (!Camera) return;
            // Recover if the camera or local player was assigned after activation.
            if (_BoundCamera != Camera || _Player != HotelPlayer.LocalPlayer || Camera.Follow != ViewTarget())
                BindPlayer(HotelPlayer.LocalPlayer);
            if (!_Player || !_Composer) return;

            Camera.Lens.OrthographicSize = RestingSize;

            var output = HotelViewCamera.Current;
            var offset = Vector3.zero;
            if (_Player.ControlMode == HotelControlMode.Fish && output && Mouse.current != null && Application.isFocused)
            {
                var pixelRect = output.pixelRect;
                var mouse = Mouse.current.position.ReadValue();
                // Clamp to a small percentage of the view, even with the cursor outside the window.
                var x = Mathf.Clamp((mouse.x - pixelRect.x) / Mathf.Max(1, pixelRect.width) * 2 - 1, -1, 1);
                var y = Mathf.Clamp((mouse.y - pixelRect.y) / Mathf.Max(1, pixelRect.height) * 2 - 1, -1, 1);
                var height = Camera.Lens.OrthographicSize * 2;
                offset = new Vector3(x * height * output.aspect, y * height, 0) * MousePadding;
            }
            _MouseOffset = Vector3.SmoothDamp(_MouseOffset, offset, ref _OffsetVelocity, MouseSmoothTime);
            // Composer offsets are local to the target; keep the padding aligned to the game plane.
            _Composer.TargetOffset = _Player.ControlMode == HotelControlMode.Ghost ? Vector3.zero : Quaternion.Inverse(_Player.transform.rotation) * Vector3.ClampMagnitude(_MouseOffset, .65f);
        }
    }
}
