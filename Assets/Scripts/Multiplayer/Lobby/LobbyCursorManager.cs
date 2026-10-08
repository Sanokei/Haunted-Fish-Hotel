using Monologue.Dialogue;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace HauntedFish.Multiplayer
{
    // Adapts the reference raycast/release interaction to owned input, authored refs and Lobby gates.
    public sealed class LobbyCursorManager : MonoBehaviour
    {
        [SerializeField] LobbyManager _Lobby;
        [SerializeField] Transform _InteractionOrigin, _Facing;
        [SerializeField] Vector3 _FrontDirectionLocal = Vector3.back;
        public Vector3 FrontDirection => _Facing ? Vector3.ProjectOnPlane(_Facing.TransformDirection(_FrontDirectionLocal), Vector3.up).normalized : Vector3.zero;
        [SerializeField] Collider _Target;
        [SerializeField] EnterDialogueMode _Dialogue;
        [SerializeField] Texture2D _InspectCursor;
        [SerializeField] Vector2 _Hotspot;
        [SerializeField, Min(.1f)] float _Range = 3;
        [SerializeField, Range(-1, 1)] float _MinimumFrontDot = .25f;
        [SerializeField, Min(0)] float _HeightTolerance = 1.5f;
        InputAction _Click;
        RaycastHit[] _Hits = new RaycastHit[8];
        bool _DeskActive, _AtDesk, _Menu, _Pressed;
        Texture2D _DeskCursor, _Applied;
        bool _Visible, _Initialized;
        public bool Hovering { get; private set; }
        public void SetDesk(bool active, bool atDesk, bool menu, Texture2D cursor)
        { _DeskActive = active; _AtDesk = atDesk; _Menu = menu; _DeskCursor = cursor; }
        void Awake() => _Click = new InputAction("Inspect Lobby item", InputActionType.Button, "<Mouse>/leftButton");
        void OnEnable() { _Click.Enable(); _Initialized = false; }
        public bool InFront(HotelPlayer player)
        {
            if (!player || !player.IsRelevantPlayer || !player.ControlsReady || player.ControlMode != HotelControlMode.Lobby || !_InteractionOrigin || !_Facing) return false;
            var delta = player.transform.position - _InteractionOrigin.position;
            if (Mathf.Abs(delta.y) > _HeightTolerance) return false;
            delta.y = 0;
            var forward = FrontDirection;
            return delta.sqrMagnitude > .0001f && delta.sqrMagnitude <= _Range * _Range && Vector3.Dot(delta.normalized, forward) >= _MinimumFrontDot;
        }
        public bool HitTarget(Ray ray, HotelPlayer player)
        {
            if (!_Target || !_Target.enabled || !_Target.Raycast(ray, out var target, 100)) return false;
            int count;
            while ((count = Physics.RaycastNonAlloc(ray, _Hits, target.distance, ~0, QueryTriggerInteraction.Ignore)) == _Hits.Length)
                System.Array.Resize(ref _Hits, _Hits.Length * 2);
            for (int i = 0; i < count; i++)
                if (_Hits[i].collider != _Target && _Hits[i].collider.GetComponentInParent<HotelPlayer>() != player)
                    return false;
            return true;
        }
        void Update()
        {
            var player = HotelPlayer.LocalPlayer;
            var camera = HotelViewCamera.Current;
            bool dialogue = DialogueManager.Instance && DialogueManager.Instance.ActiveDialoguePanel;
            bool nearby = _Lobby && _Lobby.InspectionAllowed && !dialogue && player && !player.InputBlocked && InFront(player);
            bool blocked = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
            Hovering = nearby && !blocked && camera && Mouse.current != null && HitTarget(camera.ScreenPointToRay(Mouse.current.position.ReadValue()), player);
            if (_Click.WasPressedThisFrame()) _Pressed = Hovering;
            if (_Click.WasReleasedThisFrame())
            {
                bool interact = _Pressed && Hovering;
                _Pressed = false;
                if (interact && _Dialogue) _Dialogue.EnterDialogue();
            }
            if (!nearby) _Pressed = false;
            bool visible = _DeskActive && (_AtDesk || _Menu) || nearby || dialogue;
            Apply(visible, Hovering ? _InspectCursor : _DeskActive && _AtDesk ? _DeskCursor : null);
        }
        void Apply(bool visible, Texture2D texture)
        {
            if (_Initialized && _Visible == visible && _Applied == texture) return;
            _Initialized = true; _Visible = visible; _Applied = texture;
            Cursor.visible = visible;
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.SetCursor(texture, texture == _InspectCursor ? _Hotspot : Vector2.zero, CursorMode.Auto);
        }
        void OnDisable()
        {
            _Click.Disable(); _Pressed = Hovering = false;
            Apply(false, null); _Initialized = false;
        }
        void OnDestroy() => _Click?.Dispose();
    }
}
