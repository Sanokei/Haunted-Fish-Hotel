using UnityEngine;
using HauntedFish.InputPrompts;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HauntedFish.Multiplayer
{
    // Owns only the local ghost's preview, placement input, HUD and camera target.
    public sealed class GhostPlacementController : MonoBehaviour
    {
        [SerializeField] Image _ForbiddenArea;
        void HideForbiddenArea() { if (_ForbiddenArea) _ForbiddenArea.gameObject.SetActive(false); }
        void ShowForbiddenArea(Camera view, Bounds bounds)
        {
            if (!view || !_World || !_Hud || !_ForbiddenArea) return;

            var parent = _ForbiddenArea.transform.parent as RectTransform;
            var canvas = _ForbiddenArea.GetComponentInParent<Canvas>();
            if (!parent || !canvas) return;
            var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            // Project all eight authored corners, covering the whole hallway volume and floor.
            for (int i = 0; i < 8; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var screen = view.WorldToScreenPoint(corner);
                var viewport = view.pixelRect;
                if (!GameplayPresentationPolicy.ClipProjection(screen.x, screen.y, screen.z, view.nearClipPlane,
                    viewport.xMin, viewport.yMin, viewport.xMax, viewport.yMax, out float clippedX, out float clippedY)) return;
                screen.x = clippedX; screen.y = clippedY;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out var point)) return;
                if (!GameplayPresentationPolicy.Finite(point.x) || !GameplayPresentationPolicy.Finite(point.y)) return;
                minimum = Vector2.Min(minimum, point); maximum = Vector2.Max(maximum, point);
            }
            if (maximum.x <= minimum.x || maximum.y <= minimum.y) return;
            var areaRect = _ForbiddenArea.rectTransform;
            areaRect.anchorMin = areaRect.anchorMax = parent.pivot;
            areaRect.pivot = new Vector2(.5f, .5f);
            areaRect.anchoredPosition = (minimum + maximum) * .5f;
            areaRect.sizeDelta = maximum - minimum;
            _ForbiddenArea.gameObject.SetActive(true);
        }
        [SerializeField] GameObject _PlacementPreview;
        [SerializeField] SpriteRenderer _PlacementPreviewSprite;
        void HidePlacementPreview() { if (_PlacementPreview) _PlacementPreview.SetActive(false); }
        void PresentPlacementPreview(GhostTrap definition, Vector3 placement, bool clear)
        {
            if (!_PlacementPreview || !_PlacementPreviewSprite || !_PreviewSprite || !_PreviewSprite.sprite || !definition) return;

            // The carried artwork follows flight. This separate destination uses
            // exactly the final body center validated by PlacementPoint and RPCs.
            _PlacementPreview.transform.SetPositionAndRotation(placement + definition.ArmedOffset + definition.ColliderOffset, Quaternion.identity);
            _PlacementPreviewSprite.sprite = _PreviewSprite.sprite;
            _PlacementPreviewSprite.sharedMaterial = _PreviewSprite.sharedMaterial;
            _PlacementPreviewSprite.sortingLayerID = _PreviewSprite.sortingLayerID;
            _PlacementPreviewSprite.sortingOrder = _PreviewSprite.sortingOrder;
            var presentation = definition.GetComponentInChildren<GhostTrapAreaPresentation>(true);
            var bounds = presentation ? presentation.OpaqueArtBounds : new Rect(0, 0, 1, 1);
            float widthFraction = Mathf.Max(.01f, bounds.width), heightFraction = Mathf.Max(.01f, bounds.height);
            var sprite = _PreviewSprite.sprite;
            float aspect = sprite.rect.width * widthFraction / (sprite.rect.height * heightFraction);
            float artHeight = Mathf.Min(definition.BodyHalfSize.y * 2, Mathf.Min(definition.ObjectSize.y, definition.ObjectSize.x / aspect));
            var size = sprite.bounds.size;
            float scaleX = artHeight * aspect / (widthFraction * Mathf.Max(.001f, size.x));
            float scaleY = artHeight / (heightFraction * Mathf.Max(.001f, size.y));
            float centerX = sprite.bounds.min.x + size.x * bounds.center.x;
            float topY = sprite.bounds.min.y + size.y * bounds.yMax;
            // Fit the opaque artwork exactly like the released trap, including
            // transparent texture padding and authored sprite pivots.
            _PlacementPreviewSprite.transform.localPosition = new Vector3(-centerX * scaleX, definition.BodyHalfSize.y - topY * scaleY, 0);
            _PlacementPreviewSprite.transform.localRotation = Quaternion.identity;
            _PlacementPreviewSprite.transform.localScale = new Vector3(scaleX, scaleY, 1);
            var color = clear ? HotelPalette.Light : HotelPalette.Rust;
            color.a *= .6f;
            _PlacementPreviewSprite.color = color;
            _PlacementPreview.SetActive(true);
        }
        GameCameraOwner _Camera;
        GameSceneController _Scene;
        [SerializeField]
        GameObject _Preview, _Grid, _Hud;
        TrapManager _TrapManager;
        GhostTrapSupply _Supply;
        [SerializeField]
        SpriteRenderer _GhostSprite;
        [SerializeField]
        SpriteRenderer _PreviewSprite;
        [SerializeField]
        Text _Label;
        [SerializeField] InputPromptSpriteText _InputPrompts;
        InputActionAsset _Actions;
        InputActionMap _Map;
        InputAction _Move, _Confirm, _Click, _Activate, _Point, _Dispose;
        string _Family = "";
        Vector3 _Position;
        public Vector3 FlightPosition => _Position;
        public static bool HallwayPointer(Camera camera, Vector2 screen, out Vector3 point)
        {
            point = Vector3.zero;
            if (!camera) return false;
            var ray = camera.ScreenPointToRay(screen);
            if (!new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float distance)) return false;
            point = ray.GetPoint(distance); point.z = 0;
            return !float.IsNaN(point.x) && !float.IsInfinity(point.x) && !float.IsNaN(point.y) && !float.IsInfinity(point.y);
        }
        public int NearbyTrapId { get; private set; } = -1;

        int _HudMode = -1;
        GhostTrap _HudTrap;
        GhostTrapSupply _HudSupply;
        string _HudFamily;
        bool _HudClear;
        float _NextTrapSend, _NextFlightSend;
        [SerializeField]
        Transform _Flight;
        GhostPlacementWorld _World;
        bool _Possessed, _Pending, _Prepared;
        int _Reply;
        float _FeedbackUntil;
        string _Feedback;
        void ExplainPlacement(string message)
        {
            _Feedback = message;
            _FeedbackUntil = Time.unscaledTime + 3;
            _HudMode = -1;
        }
        string _PreparedRoundKey;
        const float PossessedGhostAlpha = .35f;
        public void Configure(GameCameraOwner camera, GameSceneController scene)
        {
            _Camera = camera;
            _Scene = scene;
            _World = GetComponent<GhostPlacementWorld>();
        }

        public void InjectSupplies(TrapManager manager) => _TrapManager = manager;
        void Awake()
        {
            _Actions = Instantiate(Resources.Load<InputActionAsset>("HotelMultiplayerActions"));
            _Map = _Actions.FindActionMap("Ghost", true);
            InputPromptActions.Register(_Map);
            _Move = _Map.FindAction("Move", true);
            _Confirm = _Map.FindAction("Confirm", true);
            _Click = _Map.FindAction("TrapClick", true);
            _Activate = _Map.FindAction("TrapActivate", true);
            _Point = _Map.FindAction("TrapPoint", true);
            _Dispose = _Map.FindAction("Dispose", true);
            if (_InputPrompts) _InputPrompts.SetActionMap(_Map);
        }

        void Update()
        {
            HideForbiddenArea();
            HidePlacementPreview();
            var player = HotelPlayer.LocalPlayer;
            bool active = player && player.ControlsReady && !player.InBossFight && !player.BossHallwayLocked && player.ControlMode == HotelControlMode.Ghost && !player.InputBlocked;
            if (!active)
            {
                if (_Map.enabled) StopOwnedMotion(player);
                _Map.Disable();
                NearbyTrapId = -1;
                _HudMode = -1;
                if (_TrapManager)
                    _TrapManager.HighlightPackage(null);
                if (player && (player.ControlMode != HotelControlMode.Ghost || player.InBossFight || player.BossHallwayLocked))
                    { _Possessed = _Pending = _Prepared = false; }
                if (_Preview)
                    _Preview.SetActive(false);
                if (_Grid)
                    _Grid.SetActive(false);
                if (_Hud)
                    _Hud.SetActive(false);
                if (_Flight)
                    _Flight.gameObject.SetActive(false);
                if (_Camera)
                    _Camera.FollowOverride = null;
                return;
            }

            if (!_Prepared || _PreparedRoundKey != player.RoundStateKey)
            {
                _PreparedRoundKey = player.RoundStateKey;
                _Pending = _Possessed = false;
                Prepare(player.GhostFlightReady ? player.GhostFlightPosition : _Scene.SpawnPosition(0) + Vector3.up * 1.5f);
            }

            _Map.Enable();
            _Hud.SetActive(true);
            _Supply = _TrapManager ? _TrapManager.ClosestPackage(_Position) : null;
            _Flight.gameObject.SetActive(true);
            if (_Pending && player.GhostPlacementReply != _Reply)
            {
                _Pending = false;
                if (!player.GhostPlacementAccepted)
                    ExplainPlacement("Request rejected - keep the trap, move to a clear legal area and try again");
            }

            _Possessed = !string.IsNullOrEmpty(player.HeldTrapFamily);
            if (_Possessed)
            {
                _Family = player.HeldTrapFamily;
                var definition = _World.Definition(_Family);
                if (_PreviewSprite && definition)
                {
                    var supplied = _TrapManager ? _TrapManager.Definition(_Family) : null;
                    var artwork = supplied != null ? supplied.Artwork : definition.Icon;
                    if (artwork)
                        _PreviewSprite.sprite = artwork;
                    var size = _PreviewSprite.sprite ? _PreviewSprite.sprite.bounds.size : Vector3.one;
                    if (definition.PlacementMode == TrapPlacementMode.HallwayCeiling)
                    {
                        float scale = Mathf.Min(definition.ObjectSize.x / Mathf.Max(.001f, size.x), Mathf.Min(definition.ObjectSize.y, definition.BodyHalfSize.y * 2) / Mathf.Max(.001f, size.y));
                        _PreviewSprite.transform.localScale = new Vector3(scale, scale, 1);
                        _PreviewSprite.transform.localPosition = new Vector3(0, definition.BodyHalfSize.y - _PreviewSprite.sprite.bounds.max.y * scale, 0);
                    }
                    else
                    {
                        _PreviewSprite.transform.localScale = new Vector3(definition.ObjectSize.x / Mathf.Max(.001f, size.x), definition.ObjectSize.y / Mathf.Max(.001f, size.y), 1);
                        _PreviewSprite.transform.localPosition = Vector3.zero;
                    }
                }
            }

            if (!_Pending && _Dispose.WasPressedThisFrame() && (_Possessed || player.ControlledCube >= 0))
            {
                if (player.Networked)
                {
                    _Reply = player.GhostPlacementReply;
                    _Pending = true;
                    player.RequestDisposeTrap(player.ControlledCube, player.RoundStateKey, player.RoundVersion);
                }
                else
                    _World.Dispose(player, player.ControlledCube);
                _Preview.SetActive(false);
                _Grid.SetActive(false);
                return;
            }

            _Preview.SetActive(_Possessed);
            _Grid.SetActive(_Possessed);
            if (_Camera)
                _Camera.FollowOverride = _Flight;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            var move = Vector2.ClampMagnitude(_Move.ReadValue<Vector2>(), 1);
            if (player.Networked && Time.unscaledTime >= _NextFlightSend)
            {
                _NextFlightSend = Time.unscaledTime + .05f;
                player.SendGhostFlightInput(_Pending || player.ControlledCube >= 0 ? Vector2.zero : move, player.RoundStateKey, player.RoundVersion);
            }

            if (player.Networked && !player.IsServer && player.GhostFlightReady && player.ControlledCube < 0 && Vector3.Distance(_Position, player.GhostFlightPosition) > 2)
                _Position = player.GhostFlightPosition;
            if (_GhostSprite && Mathf.Abs(move.x) > .01f)
                _GhostSprite.flipX = move.x < 0;
            if (_GhostSprite)
            {
                // Let the possessed trap show through the ghost.
                var tint = _GhostSprite.color;
                tint.a = Mathf.MoveTowards(tint.a, player.ControlledCube >= 0 ? PossessedGhostAlpha : 1, Time.unscaledDeltaTime * 4);
                _GhostSprite.color = tint;
            }
            if (player.ControlledCube >= 0)
            {
                NearbyTrapId = -1;
                if (_TrapManager)
                    _TrapManager.HighlightPackage(null);
                _Position = _World.CubePosition(player.ControlledCube);
                _Flight.position = _Position;
                _Preview.SetActive(false);
                _Grid.SetActive(false);
                var trap = _World.Trap(player.ControlledCube);
                if (!trap)
                    return;
                RefreshLabel(1, trap, null, false);
                if (trap.InputMode == TrapInputMode.Movement && Time.unscaledTime >= _NextTrapSend)
                {
                    _NextTrapSend = Time.unscaledTime + .05f;
                    if (player.Networked)
                        player.SendScopedTrapAction(player.ControlledCube, (int)TrapInputKind.Move, move.x, Vector3.zero, player.RoundStateKey, player.RoundVersion);
                    else
                        _World.AcceptTrapInput(player, player.ControlledCube, move.x);
                }

                bool overUI = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
                if (!overUI && _Activate.WasPressedThisFrame() && trap.InputMode == TrapInputMode.Space)
                    Dispatch(player, TrapInputKind.Activate, Vector3.zero);
                var view = HotelViewCamera.Current;
                if (!overUI && _Click.WasPressedThisFrame() && trap.InputMode == TrapInputMode.MouseClick && view)
                {
                    var ray = view.ScreenPointToRay(_Point.ReadValue<Vector2>());
                    if (Physics.Raycast(ray, out var hit, 1000, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<GhostTrap>() == trap)
                        Dispatch(player, TrapInputKind.Click, hit.point);
                }

                if (_Confirm.WasPressedThisFrame())
                {
                    if (player.Networked)
                        player.RequestScopedTrapPossession(-1, _Position, player.RoundStateKey, player.RoundVersion);
                    else
                        _World.Possess(player, -1, _Position);
                }

                return;
            }

            if (!_Pending)
                _Position += new Vector3(move.x, move.y, 0) * (_Scene.GhostFlightSpeed * Time.unscaledDeltaTime);
            var clamped = _Scene.ClampGhost(_Position);
            _Position = new Vector3(clamped.x, clamped.y, 0);
            _Flight.position = _Position;
            var heldDefinition = _World.Definition(_Family);
            bool ceilingPlacement = _Possessed && heldDefinition && heldDefinition.PlacementMode == TrapPlacementMode.HallwayCeiling;
            var target = _Position;
            if (ceilingPlacement && Mouse.current != null && HallwayPointer(HotelViewCamera.Current, _Point.ReadValue<Vector2>(), out var pointed)) target = pointed;
            bool clear = _World.PlacementPoint(target, _Family, out var placement);
            if (ceilingPlacement)
            {
                float floor = _Scene.HallwayBounds.max.y, top = _Scene.HallwayCeilingY;
                clear = clear && target.y >= floor - GhostPlacementWorld.InteractionTolerance && target.y <= top + GhostPlacementWorld.InteractionTolerance && _World.PlacementNearFlight(player, placement, _Family);
                // Carry the artwork with the ghost; ceiling alignment belongs to the placed trap.
                _Preview.transform.SetPositionAndRotation(_Position, Quaternion.identity);
            }
            else
            {
                _Preview.transform.SetPositionAndRotation(_Position, Quaternion.identity);
                clear = clear && Mathf.Abs(_Position.y - placement.y) <= .6f;
            }
            // Mirror the command adapter's reach gate for every family, including floor traps.
            clear = clear && _World.PlacementNearFlight(player, placement, _Family);
            var forbidden = _Possessed && !_Pending && heldDefinition
                ? _World.ForbiddenZone(placement + heldDefinition.ArmedOffset + heldDefinition.ColliderOffset, heldDefinition.BodyHalfSize) : null;
            if (forbidden.HasValue) ShowForbiddenArea(HotelViewCamera.Current, forbidden.Value);
            if (_PreviewSprite)
                _PreviewSprite.color = clear ? HotelPalette.Light : HotelPalette.Rust;
            if (ceilingPlacement && !_Pending) PresentPlacementPreview(heldDefinition, placement, clear);
            int nearbyTrap = NearbyTrapId = _World.NearbyCube(_Position, player);
            bool nearby = _TrapManager && _TrapManager.InPickupRange(_Position, _Supply);
            if (_TrapManager)
                _TrapManager.HighlightPackage(nearby && !_Possessed ? _Supply : null);
            int mode = _Pending ? 0 : _Possessed ? 2 : nearbyTrap >= 0 ? 3 : nearby ? 4 : 5;
            RefreshLabel(mode, nearbyTrap >= 0 ? _World.Trap(nearbyTrap) : null, nearby ? _Supply : null, clear);
            bool pointerOverUI = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
            bool placePressed = _Confirm.WasPressedThisFrame() || (_Possessed && _Click.WasPressedThisFrame());
            if (!_Pending && placePressed && !pointerOverUI)
            {
                if (!_Possessed && nearbyTrap >= 0)
                {
                    if (player.Networked)
                        player.RequestScopedTrapPossession(nearbyTrap, _Position, player.RoundStateKey, player.RoundVersion);
                    else
                        _World.Possess(player, nearbyTrap, _Position);
                }
                else if (!_Possessed && nearby)
                {
                    if (player.Networked)
                    {
                        _Reply = player.GhostPlacementReply;
                        _Pending = true;
                        player.RequestConveyorPackage(_Supply.PackageId, _Position, player.RoundStateKey, player.RoundVersion);
                    }
                    else
                        _TrapManager.TryTake(player, _Supply.PackageId, _Position, player.RoundStateKey, player.RoundVersion);
                }
                else if (_Possessed && clear)
                {
                    if (player.Networked)
                    {
                        _Reply = player.GhostPlacementReply;
                        _Pending = true;
                        player.RequestPlaceHeldTrap(placement, _Family, player.RoundStateKey, player.RoundVersion);
                    }
                    else if (_World.PlacementNearFlight(player, placement, _Family) && _World.TryPlace(player, placement, _Family))
                        _Possessed = false;
                    else
                        ExplainPlacement("Placement rejected - keep the trap and try a clear legal area");
                }
                else if (_Possessed)
                    ExplainPlacement(forbidden.HasValue ? "No traps here - keep the start and attic entrance clear" :
                        !player.GhostSetupReady ? "Wait for ghost setup to finish" :
                        "Move close to the trap destination and choose a clear hallway space");
            }
        }

        void RefreshLabel(int mode, GhostTrap trap, GhostTrapSupply supply, bool clear)
        {
            if (mode != 0 && Time.unscaledTime < _FeedbackUntil)
            {
                _Label.text = _Feedback;
                if (_InputPrompts) _InputPrompts.SetTemplate("");
                _HudMode = -1;
                return;
            }
            // Build captions when their meaning changes, not on every flight frame.
            if (_HudMode == mode && _HudTrap == trap && _HudSupply == supply && _HudFamily == _Family && _HudClear == clear)
                return;
            _HudMode = mode;
            _HudTrap = trap;
            _HudSupply = supply;
            _HudFamily = _Family;
            _HudClear = clear;
            string prompts = "";
            switch (mode)
            {
                case 0:
                    _Label.text = "Placing trap...";
                    break;
                case 1:
                    _Label.text = trap.DisplayName;
                    string use = trap.InputMode == TrapInputMode.Movement ? "{input:Ghost/Move} Drive" :
                        trap.InputMode == TrapInputMode.MouseClick ? "{input:Ghost/TrapClick} Activate" : "{input:Ghost/TrapActivate} Drop";
                    prompts = use + "   {input:Ghost/Confirm} Release";
                    break;
                case 2:
                    _Label.text = clear ? "Choose a clear trap destination" : "Place on the hallway floor in a clear space";
                    prompts = "{input:Ghost/Move} Fly   {input:Ghost/Confirm} Place   {input:Ghost/Dispose} Discard";
                    break;
                case 3:
                    _Label.text = trap.DisplayName + " - " + trap.ControlHint;
                    prompts = "{input:Ghost/Confirm} Possess";
                    break;
                case 4:
                    _Label.text = _TrapManager.Definition(supply.FamilyTag).Prefab.DisplayName;
                    prompts = "{input:Ghost/Confirm} Open package";
                    break;
                default:
                    _Label.text = "Fly to the package conveyor above the hallway";
                    // Free flight shows no button strip; prompts appear only near something usable.
                    break;
            }
            if (_InputPrompts) _InputPrompts.SetTemplate(prompts);
        }

        void Dispatch(HotelPlayer player, TrapInputKind kind, Vector3 point)
        {
            if (player.Networked)
                player.SendScopedTrapAction(player.ControlledCube, (int)kind, 0, point, player.RoundStateKey, player.RoundVersion);
            else
                _World.AcceptTrapAction(player, player.ControlledCube, new TrapInput(kind, 0, point));
        }

        void Prepare(Vector3 position)
        {
            _FeedbackUntil = 0;
            _Prepared = true;
            _Position = position;
            _Flight.position = position;
        }

        void StopOwnedMotion(HotelPlayer player)
        {
            if (!player || !player.IsRelevantPlayer || !player.ControlsReady ||
                player.ControlMode != HotelControlMode.Ghost || !player.GhostSetupReady) return;
            if (player.Networked)
            {
                player.SendGhostFlightInput(Vector2.zero, player.RoundStateKey, player.RoundVersion);
                if (player.ControlledCube >= 0)
                    player.SendScopedTrapAction(player.ControlledCube, (int)TrapInputKind.Move, 0,
                        Vector3.zero, player.RoundStateKey, player.RoundVersion);
            }
            else if (_World && player.ControlledCube >= 0)
                _World.AcceptTrapInput(player, player.ControlledCube, 0);
        }
        void OnDisable()
        {
            HideForbiddenArea();
            HidePlacementPreview();
            // Disable/cancellation is a command boundary, not a delayed timeout.
            StopOwnedMotion(HotelPlayer.LocalPlayer);
            _Map?.Disable();
            NearbyTrapId = -1;
            _HudMode = -1;
            if (_TrapManager)
                _TrapManager.HighlightPackage(null);
            _Possessed = _Pending = false;
            _FeedbackUntil = 0;
            if (_Camera)
                _Camera.FollowOverride = null;
            if (_Preview) _Preview.SetActive(false);
            if (_Grid) _Grid.SetActive(false);
            if (_Hud) _Hud.SetActive(false);
            if (_Flight)
                _Flight.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            InputPromptActions.Unregister(_Map);
            if (_Actions)
                Destroy(_Actions);
        }
    }
}
