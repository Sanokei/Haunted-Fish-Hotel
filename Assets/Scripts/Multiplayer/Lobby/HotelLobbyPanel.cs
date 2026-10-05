using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace HauntedFish.Multiplayer
{
    public sealed class HotelLobbyPanel : MonoBehaviour
    {
        [SerializeField] TMP_InputField _Code;
        [SerializeField] TMP_Text _Status;
        [SerializeField] TMP_Text _RoomCode;
        [SerializeField] Button _Share;
        [SerializeField] Button _Join;
        [SerializeField] Button _ToggleHidden;
        [SerializeField] LobbyTrigger _DeskZone;
        [SerializeField] GameObject _DeskControls;
        [SerializeField] GameObject _CopySection;
        [SerializeField] Image _ToggleIcon;
        bool _Hidden = true;

        Material _CodeMaterial, _OriginalCodeMaterial;
        bool _CanUseDesk;
        bool _Connected;
        bool _Transitioning, _MenuOpen;
        bool? _MaterialHidden;
        string _CurrentRoomCode = "";
        public event Action CopyRequested;
        public event Action<string> JoinRequested;
        public TMP_FontAsset Font => _Status.font;
        public bool InputFocused { get; private set; }
        public event Action<bool> InputFocusChanged;
        void SetInputFocused(bool focused)
        {
            if (InputFocused == focused) return;
            InputFocused = focused;
            InputFocusChanged?.Invoke(focused);
        }
        void OnEnable()
        {
            if (_DeskZone) _DeskZone.ZonePresenceChanged += OnZonePresenceChanged;
            HotelPlayer.LocalPlayerChanged += OnLocalPlayerChanged;
            RefreshDeskControls();
        }
        void OnDisable()
        {
            if (_DeskZone) _DeskZone.ZonePresenceChanged -= OnZonePresenceChanged;
            HotelPlayer.LocalPlayerChanged -= OnLocalPlayerChanged;
            RefreshDeskControls();
        }
        void OnZonePresenceChanged(Collider zone, HotelPlayer player, bool present)
        {
            if (player.IsRelevantPlayer) RefreshDeskControls();
        }
        void OnLocalPlayerChanged(HotelPlayer player) => RefreshDeskControls();
        void Awake()
        {
            _Share.onClick.AddListener(CopyCode);
            _Join.onClick.AddListener(JoinRoom);
            _Code.onValueChanged.AddListener(OnCodeChanged);
            _Code.onSelect.AddListener(OnSelected);
            _Code.onDeselect.AddListener(OnDeselected);
            _ToggleHidden.onClick.AddListener(ToggleHidden);
            _OriginalCodeMaterial = _RoomCode.fontSharedMaterial;
            _CodeMaterial = new Material(_OriginalCodeMaterial);
            _RoomCode.fontSharedMaterial = _CodeMaterial;
            _ToggleIcon.sprite = Resources.Load<Sprite>("LobbyIcons/HiddenIcon");
            _ToggleIcon.preserveAspect = true;
            RefreshRoomCode();
            RefreshDeskControls();
        }
        void CopyCode() { if (_CanUseDesk) CopyRequested?.Invoke(); }
        void JoinRoom() { if (_CanUseDesk) JoinRequested?.Invoke(_Code.text); }
        void ToggleHidden()
        {
            if (!_CanUseDesk) return;
            _Hidden = !_Hidden;
            RefreshRoomCode();
        }
        void OnCodeChanged(string value) => RefreshJoinAvailability();
        void OnSelected(string value) => SetInputFocused(_CanUseDesk && isActiveAndEnabled);
        void OnDeselected(string value) => SetInputFocused(false);
        public void ClearSelection()
        {
            SetInputFocused(false);
            _Code.DeactivateInputField();
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject == _Code.gameObject)
                EventSystem.current.SetSelectedGameObject(null);
        }
        public void Render(bool connected, bool transitioning, bool menuOpen, string roomCode, LobbyRoster roster, string status)
        {
            if (!connected) _Hidden = true;
            _Connected = connected;
            _Transitioning = transitioning;
            _MenuOpen = menuOpen;
            _CurrentRoomCode = roomCode;
            var members = roster.Members ?? Array.Empty<LobbyMember>();
            var ready = 0;
            foreach (var member in members) if (member.Ready) ++ready;
            _Status.text = connected ? $"{ready}/{members.Length}" : status;
            RefreshRoomCode();
            RefreshDeskControls();
        }
        void RefreshDeskControls()
        {
            var deskVisible = isActiveAndEnabled && _Connected && !_MenuOpen && _DeskZone &&
                _DeskZone.Contains(HotelPlayer.LocalPlayer);
            _CanUseDesk = deskVisible && !_Transitioning;
            if (!_CanUseDesk && InputFocused) ClearSelection();
            if (_DeskControls) SetVisible(_DeskControls, deskVisible);
            if (_CopySection) SetVisible(_CopySection, deskVisible);
            SetVisible(_Code.gameObject, deskVisible);
            SetVisible(_Share.gameObject, deskVisible);
            SetVisible(_Join.gameObject, deskVisible);
            SetVisible(_RoomCode.gameObject, deskVisible);
            SetVisible(_ToggleHidden.gameObject, deskVisible);
            _ToggleHidden.interactable = _CanUseDesk;
            _Code.interactable = _CanUseDesk;
            RefreshJoinAvailability();
            _Share.interactable = _CanUseDesk;
        }
        static void SetVisible(GameObject control, bool visible)
        {
            if (control.activeSelf != visible) control.SetActive(visible);
        }
        void RefreshJoinAvailability()
        {
            _Join.interactable = _CanUseDesk && LobbyCode.TryNormalize(_Code.text, out var code) &&
                !string.Equals(code, _CurrentRoomCode, StringComparison.OrdinalIgnoreCase);
        }
        void RefreshRoomCode()
        {
            // Never leave the real invitation in a hidden text mesh. Blur a neutral placeholder.
            _RoomCode.text = _Connected ? (_Hidden ? "XXXXXX" : _CurrentRoomCode) : "------";
            if (_CodeMaterial && _MaterialHidden != _Hidden)
            {
                _MaterialHidden = _Hidden;
                _CodeMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, _Hidden ? .35f : 0f);
                _CodeMaterial.SetFloat(ShaderUtilities.ID_OutlineSoftness, _Hidden ? 1f : 0f);
                _RoomCode.UpdateMeshPadding();
            }
            if (_ToggleIcon) _ToggleIcon.color = _Hidden ? new Color(.65f, .65f, .65f) : Color.white;
        }
        void OnDestroy()
        {
            _Share.onClick.RemoveListener(CopyCode);
            _Join.onClick.RemoveListener(JoinRoom);
            _Code.onValueChanged.RemoveListener(OnCodeChanged);
            _Code.onSelect.RemoveListener(OnSelected);
            _Code.onDeselect.RemoveListener(OnDeselected);
            _ToggleHidden.onClick.RemoveListener(ToggleHidden);
            if (_RoomCode) _RoomCode.fontSharedMaterial = _OriginalCodeMaterial;
            if (_CodeMaterial) Destroy(_CodeMaterial);
        }
    }
}
