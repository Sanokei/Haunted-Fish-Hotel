using UnityEngine;
using UnityEngine.UI;

namespace HauntedFish.Multiplayer
{
    // All peers see the same fixed aisle and moving cart, including through fish darkness.
    public sealed class GhostTrapAreaPresentation : MonoBehaviour
    {
        [SerializeField]
        GameObject _Root, _SceneArt;
        [SerializeField]
        RectTransform _Area, _Cart;
        [SerializeField]
        Image _Left, _Right;
        Vector3 _Origin;
        Vector2 _Limits;
        int _Index;
        float _HalfWidth;
        GhostTrap _Trap;
        Image _AreaImage;
        void Awake()
        {
            if (_Area)
                _AreaImage = _Area.GetComponent<Image>();
        }

        public void ConfigureTrap(GhostTrap trap)
        {
            _Trap = trap;
            var state = trap.CaptureState();
            Configure(null, null, state.Origin, trap.Index, state.Rotation);
        }

        public void Configure(Sprite cart, Sprite aisle, Vector3 origin, int index, float rotation)
        {
            _Origin = origin;
            _HalfWidth = .5f * (Mathf.Abs(Mathf.Cos(rotation * Mathf.Deg2Rad)) + Mathf.Abs(Mathf.Sin(rotation * Mathf.Deg2Rad)));
            _Limits = GhostPlacementWorld.TravelBounds(origin);
            _Index = index;
            if (_SceneArt)
                _SceneArt.SetActive(false);
            _Root.SetActive(true);
        }

        void LateUpdate()
        {
            var camera = HotelViewCamera.Current;
            if (!camera || !_Root)
                return;
            if (_Trap)
            {
                _Origin = _Trap.CaptureState().Origin;
                _Limits = _Trap.Limits;
            }

            var left = camera.WorldToScreenPoint(new Vector3(_Limits.x - _HalfWidth, _Origin.y + .25f, _Origin.z));
            var right = camera.WorldToScreenPoint(new Vector3(_Limits.y + _HalfWidth, _Origin.y + .25f, _Origin.z));
            var cart = camera.WorldToScreenPoint(transform.position);
            _Root.SetActive(left.z > 0 && right.z > 0 && cart.z > 0);
            var top = camera.WorldToScreenPoint(_Origin + Vector3.up);
            var bottom = camera.WorldToScreenPoint(_Origin - Vector3.up * .5f);
            _Area.position = (left + right) * .5f;
            _Area.sizeDelta = new Vector2(Mathf.Abs(right.x - left.x), Mathf.Abs(top.y - bottom.y));
            _Left.rectTransform.sizeDelta = _Right.rectTransform.sizeDelta = new Vector2(4, _Area.sizeDelta.y);
            _Cart.position = cart;
            var upper = camera.WorldToScreenPoint(transform.position + Vector3.up * .5f);
            var lower = camera.WorldToScreenPoint(transform.position - Vector3.up * .5f);
            float height = Mathf.Abs(upper.y - lower.y);
            _Cart.sizeDelta = _Trap ? _Trap.ObjectSize * height : new Vector2(height * 1.25f, height);
            if (_Trap)
            {
                _Area.position = camera.WorldToScreenPoint(_Origin + (Vector3)_Trap.BackgroundOffset);
                _Area.sizeDelta = _Trap.BackgroundSize * height;
                _Left.rectTransform.sizeDelta = _Right.rectTransform.sizeDelta = new Vector2(4, _Area.sizeDelta.y);
                if (_Trap.Phase == TrapPhase.Active && _Trap.InputMode == TrapInputMode.MouseClick)
                    _Cart.localScale = Vector3.one * (1 + .1f * Mathf.Sin(_Trap.CaptureState().Progress * Mathf.PI));
            }

            bool possessed = false;
            var players = HotelPlayer.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player.ControlsReady && player.ControlMode == HotelControlMode.Ghost && player.ControlledCube == _Index && (!_Trap || player.RoundStateKey == _Trap.World.RoundKey))
                {
                    possessed = true;
                    break;
                }
            }

            var local = HotelPlayer.LocalPlayer;
            var ghost = _Trap && _Trap.World ? _Trap.World.Controls : null;
            bool nearby = local && local.ControlMode == HotelControlMode.Ghost && local.ControlledCube < 0 && ghost && ghost.NearbyTrapId == _Index;
            if (_AreaImage)
                _AreaImage.color = nearby ? HotelPalette.Peach : HotelPalette.Light;
            if (!_Trap || _Trap.InputMode != TrapInputMode.MouseClick || _Trap.Phase != TrapPhase.Active)
                _Cart.localScale = Vector3.one * (nearby ? 1.12f : 1);
            _Left.color = _Right.color = possessed ? HotelPalette.Peach : HotelPalette.Moss;
        }
    }
}
