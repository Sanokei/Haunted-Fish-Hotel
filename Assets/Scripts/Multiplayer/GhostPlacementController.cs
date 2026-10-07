using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HauntedFish.Multiplayer
{
    // Owns only the local ghost's preview, placement input, HUD and camera target.
    public sealed class GhostPlacementController : MonoBehaviour
    {
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
        InputActionAsset _Actions;
        InputActionMap _Map;
        InputAction _Move, _Confirm, _Reset, _Click, _Activate, _Point, _Dispose;
        string _Family = "";
        Vector3 _Position;
        public Vector3 FlightPosition => _Position;
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
        string _PreparedRoundKey;
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
            _Move = _Map.FindAction("Move", true);
            _Confirm = _Map.FindAction("Confirm", true);
            _Reset = _Map.FindAction("Reset", true);
            _Click = _Map.FindAction("TrapClick", true);
            _Activate = _Map.FindAction("TrapActivate", true);
            _Point = _Map.FindAction("TrapPoint", true);
            _Dispose = _Map.FindAction("Dispose", true);
        }

        void Update()
        {
            var player = HotelPlayer.LocalPlayer;
            bool active = player && player.ControlsReady && player.ControlMode == HotelControlMode.Ghost && !player.InputBlocked;
            if (!active)
            {
                if (_Map.enabled) StopOwnedMotion(player);
                _Map.Disable();
                NearbyTrapId = -1;
                _HudMode = -1;
                if (_TrapManager)
                    _TrapManager.HighlightPackage(null);
                if (player && player.ControlMode != HotelControlMode.Ghost)
                    _Possessed = _Pending = false;
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
                Prepare(_Scene.SpawnPosition(0) + Vector3.up * 1.5f);
            }

            _Map.Enable();
            _Hud.SetActive(true);
            _Supply = _TrapManager ? _TrapManager.ClosestPackage(_Position) : null;
            _Flight.gameObject.SetActive(true);
            if (_Pending && player.GhostPlacementReply != _Reply)
            {
                _Pending = false;
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
                    _PreviewSprite.transform.localScale = new Vector3(definition.ObjectSize.x / Mathf.Max(.001f, size.x), definition.ObjectSize.y / Mathf.Max(.001f, size.y), 1);
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

                if (!overUI && _Reset.WasPressedThisFrame())
                    Dispatch(player, TrapInputKind.Reset, Vector3.zero);
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
                _Position += new Vector3(move.x, move.y, 0) * (6 * Time.unscaledDeltaTime);
            var clamped = new Vector3(Mathf.Clamp(_Position.x, -15, 15), Mathf.Clamp(_Position.y, .5f, 16), 0);
            _Position = new Vector3(clamped.x, clamped.y, 0);
            _Flight.position = _Position;
            bool clear = _World.PlacementPoint(_Position, _Family, out var placement);
            _Preview.transform.SetPositionAndRotation(_Position, Quaternion.identity);
            clear = clear && Mathf.Abs(_Position.y - placement.y) <= .6f;
            if (_PreviewSprite)
                _PreviewSprite.color = clear ? HotelPalette.Light : HotelPalette.Rust;
            int nearbyTrap = NearbyTrapId = _World.NearbyCube(_Position, player);
            bool nearby = _TrapManager && _TrapManager.InPickupRange(_Position, _Supply);
            if (_TrapManager)
                _TrapManager.HighlightPackage(nearby && !_Possessed ? _Supply : null);
            int mode = _Pending ? 0 : _Possessed ? 2 : nearbyTrap >= 0 ? 3 : nearby ? 4 : 5;
            RefreshLabel(mode, nearbyTrap >= 0 ? _World.Trap(nearbyTrap) : null, nearby ? _Supply : null, clear);
            bool pointerOverUI = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
            if (!_Pending && _Confirm.WasPressedThisFrame() && !pointerOverUI)
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
                    else if (_World.TryPlace(player, placement, _Family))
                        _Possessed = false;
                }
            }
        }

        void RefreshLabel(int mode, GhostTrap trap, GhostTrapSupply supply, bool clear)
        {
            // Build captions when their meaning changes, not on every flight frame.
            if (_HudMode == mode && _HudTrap == trap && _HudSupply == supply && _HudFamily == _Family && _HudClear == clear)
                return;
            _HudMode = mode;
            _HudTrap = trap;
            _HudSupply = supply;
            _HudFamily = _Family;
            _HudClear = clear;
            switch (mode)
            {
                case 0:
                    _Label.text = "Placing " + _Family + "...";
                    break;
                case 1:
                    _Label.text = trap.DisplayName + " - " + trap.ControlHint + " - E / X: unpossess - Q: dispose";
                    break;
                case 2:
                    _Label.text = "WASD / stick: fly - E / X: place - Q: dispose" + (clear ? "" : " - Place on the hallway floor in a clear space");
                    break;
                case 3:
                    _Label.text = "E / X: possess " + trap.DisplayName + " - " + trap.ControlHint;
                    break;
                case 4:
                    _Label.text = "E / X: open " + _TrapManager.Definition(supply.FamilyTag).Prefab.DisplayName;
                    break;
                default:
                    _Label.text = "Fly to the package conveyor above the hallway - E / X: open";
                    break;
            }
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
            _Prepared = true;
            _Position = position;
            _Flight.position = position;
        }

        void StopOwnedMotion(HotelPlayer player)
        {
            if (!player || !player.IsRelevantPlayer || !player.ControlsReady ||
                player.ControlMode != HotelControlMode.Ghost || !player.RoundReleased) return;
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
            // Disable/cancellation is a command boundary, not a delayed timeout.
            StopOwnedMotion(HotelPlayer.LocalPlayer);
            _Map?.Disable();
            NearbyTrapId = -1;
            _HudMode = -1;
            if (_TrapManager)
                _TrapManager.HighlightPackage(null);
            _Possessed = _Pending = false;
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
            if (_Actions)
                Destroy(_Actions);
        }
    }
}
