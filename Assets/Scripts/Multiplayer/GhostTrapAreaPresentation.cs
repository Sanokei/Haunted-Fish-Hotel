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
        [SerializeField] Rect _OpaqueArtBounds = new Rect(0, 0, 1, 1);
        public Rect OpaqueArtBounds => _OpaqueArtBounds;
        Vector3 _Origin;
        Vector2 _Limits;
        int _Index;
        float _HalfWidth;
        GhostTrap _Trap;
        Image _AreaImage, _CartImage;
        [SerializeField] Image[] _WorldImages;
        [SerializeField] SpriteRenderer[] _WorldArt;
        [SerializeField] Sprite _DangerPattern, _RangeMarker;
        [Tooltip("Optional authored angler light/glow, enabled only while this trap is possessed.")]
        [SerializeField] Light _PossessedLight;
        [SerializeField] GameObject _PossessedGlow;
        [Tooltip("Enable when the supplied glow is a complete alternate image, so the base body is not drawn beneath it.")]
        [SerializeField] bool _PossessedGlowReplacesBody;
        [Tooltip("Authored pivot at the separated hanging line's top. Only its suspended children sway; the ceiling base stays fixed.")]
        [SerializeField] Transform _SuspendedPivot;
        [Tooltip("Optional authored full-art layout for the stationary ceiling canopy, separate from the falling suspended layout.")]
        [SerializeField] RectTransform _FixedCeilingLayout;
        [SerializeField] float _SwayDegrees = 3, _SwayFrequency = 2.2f;
        Quaternion _SuspendedRestRotation = Quaternion.identity;
        // Trap artwork faces left; mirror it while the trap travels right.
        float _Facing = 1, _LastX = float.NaN;
        public bool IsPossessed { get; private set; }
        // Read-only authored presentation observations used by native recording/QA.
        public bool PossessionLightVisible => _PossessedLight && _PossessedLight.enabled || _PossessedGlow && _PossessedGlow.activeInHierarchy;
        public bool DangerBandVisible => _PatternRenderer && _PatternRenderer.enabled && _PatternRenderer.gameObject.activeInHierarchy;
        public float DangerPatternTime => _PatternBlock == null ? 0 : _PatternBlock.GetFloat("_SharedTime");
        public Sprite DangerPattern => _DangerPattern;
        public Sprite RangeMarker => _RangeMarker;
        public Transform SuspendedPivot => _SuspendedPivot;
        public RectTransform FixedCeilingLayout => _FixedCeilingLayout;
        [SerializeField] MeshRenderer _PatternRenderer;
        MaterialPropertyBlock _PatternBlock;
        void PresentPattern(Camera camera)
        {
            if (!_PatternRenderer) return;
            _PatternRenderer.enabled = _Root.activeInHierarchy;
            if (!_PatternRenderer.enabled) return;
            float depth = camera.WorldToScreenPoint(transform.position).z;
            _Area.GetWorldCorners(_Corners);
            for (int i=0;i<4;i++) _Corners[i] = camera.ScreenToWorldPoint(new Vector3(_Corners[i].x, _Corners[i].y, depth));
            var art = _PatternRenderer.transform;
            art.position = (_Corners[0] + _Corners[2]) * .5f;
            art.rotation = camera.transform.rotation * _Area.rotation;
            float width = Vector3.Distance(_Corners[0], _Corners[3]), height = Vector3.Distance(_Corners[0], _Corners[1]);
            var scale = transform.lossyScale;
            art.localScale = new Vector3(width / Mathf.Max(.001f,Mathf.Abs(scale.x)), height / Mathf.Max(.001f,Mathf.Abs(scale.y)), 1);
            if (_PatternBlock == null) _PatternBlock = new MaterialPropertyBlock();
            var rect = _DangerPattern.textureRect;
            var texture = _DangerPattern.texture;
            _PatternBlock.SetVector("_TextureRect", new Vector4(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height));
            _PatternBlock.SetVector("_Tiles", new Vector4(width / 1.3f, height / 1.3f, 0, 0));
            _PatternBlock.SetFloat("_SharedTime", Time.unscaledTime);
            _PatternRenderer.SetPropertyBlock(_PatternBlock);
        }
        readonly Vector3[] _Corners = new Vector3[4];
        void CreateWorldArt()
        {
            // Renderer nodes and shared materials are saved in each production trap prefab.
            if (_WorldImages == null || _WorldArt == null || _WorldImages.Length != _WorldArt.Length)
                Debug.LogError("Trap presentation renderers are not paired on " + name, this);
            if (_WorldImages != null)
                foreach (var image in _WorldImages) if (image) { image.enabled = false; image.raycastTarget = false; }
            // Split chandelier uses this image only as a sizing reference, never a duplicate full-art layer.
            if (_CartImage) { _CartImage.enabled = false; _CartImage.raycastTarget = false; }
        }
        void UpdateWorldArt(Camera camera)
        {
            if (_WorldArt == null || _WorldImages == null) return;
            float depth = camera.WorldToScreenPoint(transform.position).z;
            int count = Mathf.Min(_WorldArt.Length, _WorldImages.Length);
            for (int i = 0; i < count; i++)
            {
                var image = _WorldImages[i];
                var art = _WorldArt[i];
                if (!art) continue;
                if (!image) { art.enabled = false; continue; }
                bool replacedBody = _PossessedGlowReplacesBody && IsPossessed && _PossessedGlow &&
                    _PossessedGlow.activeInHierarchy && image == _CartImage;
                // The area reads through the repeated ghost pattern only; the old cursor bound markers are retired.
                bool boundMarker = image == _Left || image == _Right;
                art.enabled = image != _AreaImage && !boundMarker && !replacedBody && _Root.activeInHierarchy && image.gameObject.activeInHierarchy && depth > camera.nearClipPlane;
                art.flipX = image == _Right;
                if (!art.enabled) continue;
                var sprite = image.sprite;
                if (!sprite) { art.enabled = false; continue; }
                art.sprite = sprite;
                art.color = image.color;
                // Retain authored layout, pivot and camera effects in the world
                // transparent pass, where sprite sorting can put fish in front.
                image.rectTransform.GetWorldCorners(_Corners);
                for (int c = 0; c < 4; c++)
                    _Corners[c] = camera.ScreenToWorldPoint(new Vector3(_Corners[c].x, _Corners[c].y, depth));
                art.transform.position = (_Corners[0] + _Corners[2]) * .5f;
                art.transform.rotation = camera.transform.rotation * image.rectTransform.rotation;
                var size = sprite.bounds.size;
                var parentScale = transform.lossyScale;
                float mirror = _Cart && image.transform.IsChildOf(_Cart) ? _Facing : 1;
                art.transform.localScale = new Vector3(mirror * Vector3.Distance(_Corners[0], _Corners[3]) / Mathf.Max(.0001f, size.x * Mathf.Abs(parentScale.x)),
                    Vector3.Distance(_Corners[0], _Corners[1]) / Mathf.Max(.0001f, size.y * Mathf.Abs(parentScale.y)), 1);
                art.transform.position -= art.transform.TransformVector(sprite.bounds.center);
            }
        }
        void OnDisable()
        {
            PresentPossession(false);
            if (_PatternRenderer) _PatternRenderer.enabled = false;
            if (_WorldArt != null) foreach (var art in _WorldArt) if (art) art.enabled = false;
        }
        void Awake()
        {
            if (_SuspendedPivot) _SuspendedRestRotation = _SuspendedPivot.localRotation;
            PresentPossession(false);
            if (_Area)
                _AreaImage = _Area.GetComponent<Image>();
            if (_Cart) _CartImage = _Cart.GetComponent<Image>();
            if (_RangeMarker) { _Left.sprite = _Right.sprite = _RangeMarker; }
        }

        void PresentPossession(bool possessed)
        {
            IsPossessed = possessed;
            if (_PossessedLight) _PossessedLight.enabled = possessed;
            if (_PossessedGlow && _PossessedGlow.activeSelf != possessed) _PossessedGlow.SetActive(possessed);
            if (_SuspendedPivot)
            {
                bool sway = possessed && _Trap && _Trap.Phase == TrapPhase.Armed;
                _SuspendedPivot.localRotation = _SuspendedRestRotation * Quaternion.Euler(0, 0,
                    sway ? _SwayDegrees * Mathf.Sin(Time.unscaledTime * _SwayFrequency) : 0);
            }
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
            CreateWorldArt();
        }

        void LateUpdate()
        {
            var camera = HotelViewCamera.Current;
            if (!camera || !_Root)
            {
                OnDisable();
                return;
            }
            if (_Trap)
            {
                _Origin = _Trap.CaptureState().Origin;
                _Limits = _Trap.Limits;
            }

            var projectedRight = camera.WorldToScreenPoint(transform.position + Vector3.right) - camera.WorldToScreenPoint(transform.position);
            var projectedUp = camera.WorldToScreenPoint(transform.position + Vector3.up) - camera.WorldToScreenPoint(transform.position);
            float angle = Mathf.Atan2(projectedRight.y, projectedRight.x) * Mathf.Rad2Deg;
            _Area.rotation = _Cart.rotation = Quaternion.Euler(0, 0, angle);
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
            float height = projectedUp.magnitude;
            _Cart.sizeDelta = _Trap ? _Trap.ObjectSize * height : new Vector2(height * 1.25f, height);
            if (_Trap)
            {
                _Area.position = camera.WorldToScreenPoint(_Origin + (Vector3)_Trap.BackgroundOffset);
                _Area.sizeDelta = new Vector2(_Trap.BackgroundSize.x * projectedRight.magnitude, _Trap.BackgroundSize.y * height);
                var bounds = _OpaqueArtBounds;
                float widthFraction = Mathf.Max(.01f, bounds.width), heightFraction = Mathf.Max(.01f, bounds.height);
                _Cart.pivot = new Vector2(bounds.center.x, bounds.yMin);
                _Cart.position = camera.WorldToScreenPoint(transform.position - Vector3.up * _Trap.BodyHalfSize.y);
                _Cart.sizeDelta = new Vector2(_Trap.ObjectSize.x * projectedRight.magnitude / widthFraction, _Trap.ObjectSize.y * height / heightFraction);
                if (_Trap.PlacementMode == TrapPlacementMode.HallwayCeiling)
                {
                    // The drop zone runs from just under the hanging chandelier down to the floor.
                    // Same default floor as GhostPlacementWorld when no Game scene owns one (tutorial stage).
                    var scene = GameSceneController.Current;
                    float floorY = scene ? scene.HallwayBounds.max.y : 0;
                    float topY = Mathf.Max(floorY, (_Origin + _Trap.ArmedOffset).y - _Trap.BodyHalfSize.y);
                    float dropHeight = topY - floorY;
                    float areaWidth = _Trap.BackgroundSize.x;
                    _Area.position = camera.WorldToScreenPoint(new Vector3(_Origin.x, floorY + dropHeight * .5f, _Origin.z));
                    _Area.sizeDelta = new Vector2(areaWidth * projectedRight.magnitude, dropHeight * height);
                    var art = _CartImage ? _CartImage.sprite : null;
                    float aspect = art ? art.rect.width * widthFraction / (art.rect.height * heightFraction) : 1;
                    float artHeight = Mathf.Min(_Trap.BodyHalfSize.y * 2, Mathf.Min(_Trap.ObjectSize.y, _Trap.ObjectSize.x / aspect));
                    _Cart.pivot = new Vector2(bounds.center.x, bounds.yMax);
                    _Cart.position = camera.WorldToScreenPoint(transform.position + _Trap.ColliderOffset + Vector3.up * _Trap.BodyHalfSize.y);
                    _Cart.sizeDelta = new Vector2(artHeight * aspect * projectedRight.magnitude / widthFraction, artHeight * height / heightFraction);
                    if (_FixedCeilingLayout)
                    {
                        // Origin + ArmedOffset is stable while the trap falls. Both layouts use
                        // the same original-art coordinates so the black cord meets the canopy.
                        _FixedCeilingLayout.pivot = _Cart.pivot;
                        _FixedCeilingLayout.sizeDelta = _Cart.sizeDelta;
                        _FixedCeilingLayout.rotation = _Cart.rotation;
                        _FixedCeilingLayout.localScale = Vector3.one;
                        _FixedCeilingLayout.position = camera.WorldToScreenPoint(_Origin + _Trap.ArmedOffset +
                            _Trap.ColliderOffset + Vector3.up * _Trap.BodyHalfSize.y);
                    }
                }
                float markerHeight = _Area.sizeDelta.y;
                _Left.rectTransform.sizeDelta = _Right.rectTransform.sizeDelta = new Vector2(markerHeight * (1176f / 1400f), markerHeight);
                if (_Trap.Phase == TrapPhase.Active && _Trap.InputMode == TrapInputMode.MouseClick)
                    _Cart.localScale = Vector3.one * (1 + .1f * Mathf.Sin(_Trap.CaptureState().Progress * Mathf.PI));
            }

            bool possessed = false;
            var players = HotelPlayer.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player.ControlsReady && player.ControlMode == HotelControlMode.Ghost && player.ControlledCube == _Index &&
                    (!_Trap || _Trap.World && player.RoundStateKey == _Trap.World.RoundKey && player.InventoryScope == _Trap.World.RoomScope))
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
                _Cart.localScale = Vector3.one * (nearby && (!_Trap || _Trap.PlacementMode != TrapPlacementMode.HallwayCeiling) ? 1.12f : 1);
            _Left.color = _Right.color = possessed ? HotelPalette.Peach : HotelPalette.Moss;
            PresentPossession(possessed || _Trap && _Trap.PiercesBodies && _Trap.Phase == TrapPhase.Active);
            if (_Trap && _Trap.Travels)
            {
                float x = transform.position.x;
                if (!float.IsNaN(_LastX) && Mathf.Abs(x - _LastX) > .0005f) _Facing = x > _LastX ? -1 : 1;
                _LastX = x;
            }
            PresentPattern(camera);
            UpdateWorldArt(camera);
        }
    }
}
