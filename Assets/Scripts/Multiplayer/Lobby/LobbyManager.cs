using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Events;

namespace HauntedFish.Multiplayer
{
    public sealed class LobbyManager : MonoBehaviour
    {
        [SerializeField] HotelLobbyPanel _Panel;
        bool _AtDesk, _MenuOpen;
        [SerializeField] LobbyTrigger _DeskZone;
        [SerializeField] LobbyTrigger _StairZone;
        [SerializeField] Texture2D _DeskCursor;
        [SerializeField] HauntedHotelMultiplayer _Lobby;
        [SerializeField] GameObject _Menu;
        [SerializeField] Button _Start;
        [SerializeField] Button _Resume;
        [SerializeField] MemberRow[] _MemberRows;
        LobbyRoster _RenderedRoster;
        bool _RenderedLeader, _RenderedTransition, _RosterRendered;
        InputAction _ToggleMenu;
        [Serializable]
        sealed class MemberRow
        {
            public RectTransform Root;
            public TMP_Text Label;
            public GameObject Crown;
            public Button King, Kick;
            [NonSerialized] public uint Id;
            [NonSerialized] public UnityAction Promote, Remove;
        }
        readonly HashSet<HotelPlayer> _DeskPlayers = new HashSet<HotelPlayer>();
        void Awake()
        {
            _ToggleMenu = new InputAction("Lobby menu", InputActionType.Button, "<Keyboard>/escape");
            _ToggleMenu.performed += ToggleMenu;
            foreach (var row in _MemberRows)
            {
                row.Promote = () => { if (_Lobby) _Lobby.LobbyCommand(LobbyAction.King, row.Id); };
                row.Remove = () => { if (_Lobby) _Lobby.LobbyCommand(LobbyAction.Kick, row.Id); };
            }
        }
        void Start()
        {
            // OnEnable may run before the authored session has initialized its network.
            // Rebind after Awake rather than leaving readiness and UI on an early binding.
            if (_Lobby) Bind(_Lobby);
            else Refresh();
        }
        public void Bind(HauntedHotelMultiplayer session)
        {
            Unsubscribe();
            _AtDesk = _MenuOpen = false;
            if (_Panel) _Panel.ClearSelection();
            _Lobby = session;
            if (_Lobby)
            {
                _Lobby.BindReadyZone(IsInReadyZone);
                if (isActiveAndEnabled) _Lobby.StateChanged += Refresh;
            }
            Refresh();
        }
        void OnEnable()
        {
            _Start.onClick.AddListener(StartGame);
            _Resume.onClick.AddListener(Resume);
            foreach (var row in _MemberRows)
            {
                row.King.onClick.AddListener(row.Promote);
                row.Kick.onClick.AddListener(row.Remove);
            }
            _Panel.CopyRequested += CopyCode;
            _Panel.JoinRequested += JoinRoom;
            _Panel.InputFocusChanged += OnInputFocusChanged;
            _ToggleMenu.Enable();
            HotelPlayer.LocalPlayerChanged += OnLocalPlayerChanged;
            if (_Lobby) _Lobby.StateChanged += Refresh;
            if (_Lobby) _Lobby.BindReadyZone(IsInReadyZone);
            if (_DeskZone)
            {
                _DeskZone.ZonePresenceChanged += OnZonePresenceChanged;
                _DeskZone.Replay(OnZonePresenceChanged);
            }
            Refresh();
        }
        void OnDisable()
        {
            if (_Start) _Start.onClick.RemoveListener(StartGame);
            if (_Resume) _Resume.onClick.RemoveListener(Resume);
            foreach (var row in _MemberRows)
            {
                if (row.King) row.King.onClick.RemoveListener(row.Promote);
                if (row.Kick) row.Kick.onClick.RemoveListener(row.Remove);
            }
            if (_DeskZone) _DeskZone.ZonePresenceChanged -= OnZonePresenceChanged;
            _Panel.CopyRequested -= CopyCode;
            _Panel.JoinRequested -= JoinRoom;
            _Panel.InputFocusChanged -= OnInputFocusChanged;
            _ToggleMenu.Disable();
            HotelPlayer.LocalPlayerChanged -= OnLocalPlayerChanged;
            Unsubscribe();
            _DeskPlayers.Clear();
            _AtDesk = _MenuOpen = false;
            if (_Panel) _Panel.ClearSelection();
            Refresh();
        }
        void Unsubscribe()
        {
            if (!_Lobby) return;
            _Lobby.StateChanged -= Refresh;
            _Lobby.BindReadyZone(null);
            _Lobby.SetInputFocused(false);
        }
        void OnZonePresenceChanged(Collider zone, HotelPlayer player, bool present)
        {
            if (!isActiveAndEnabled || !zone || !player || !player.CompareTag("Player")) return;
            var players = zone.CompareTag("Desk") ? _DeskPlayers : null;
            if (players == null) return;
            if (present) players.Add(player);
            else players.Remove(player);
            if (players == _DeskPlayers && player.IsRelevantPlayer)
            {
                _AtDesk = _Lobby && _Lobby.ReadyToPlay && _DeskPlayers.Contains(player);
                RefreshInputFocus();
            }
        }
        void ToggleMenu(InputAction.CallbackContext context)
        {
            _MenuOpen = !_MenuOpen && _Lobby && _Lobby.ReadyToPlay;
            Refresh();
        }
        bool IsInReadyZone(HotelPlayer player) => isActiveAndEnabled && _StairZone && player &&
            player.gameObject.activeInHierarchy && (_StairZone.Contains(player) || _StairZone.ContainsPosition(player));
        void OnLocalPlayerChanged(HotelPlayer player)
        {
            _AtDesk = false;
            if (_Panel) _Panel.ClearSelection();
            Refresh();
        }
        void Refresh()
        {
            var connected = isActiveAndEnabled && _Lobby && _Lobby.ReadyToPlay;
            _AtDesk = connected && _DeskPlayers.Contains(HotelPlayer.LocalPlayer);
            var transition = !_Lobby || _Lobby.Transitioning;
            var roster = _Lobby ? _Lobby.Roster : default;
            var members = roster.Members ?? Array.Empty<LobbyMember>();
            if (!connected) _AtDesk = _MenuOpen = false;
            if (_Panel) _Panel.Render(connected, transition, _MenuOpen, _Lobby ? _Lobby.Code : "",
                roster, _Lobby ? _Lobby.Status : "Connecting...");
            if (_Menu)
            {
                _Menu.SetActive(_MenuOpen);
                _Start.gameObject.SetActive(connected && _Lobby.IsLobbyLeader);
                _Start.interactable = connected && !transition && LobbyRules.AllReady(members);
                if (_MenuOpen) RefreshMembers(roster);
            }
            RefreshInputFocus();
        }
        void OnInputFocusChanged(bool focused) => RefreshInputFocus();
        void RefreshInputFocus()
        {
            var active = isActiveAndEnabled && _Lobby && _Lobby.ReadyToPlay;
            var cursor = active && (_AtDesk || _MenuOpen);
            Cursor.visible = cursor;
            Cursor.lockState = cursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.SetCursor(active && _AtDesk ? _DeskCursor : null, Vector2.zero, CursorMode.Auto);
            if (_Lobby) _Lobby.SetInputFocused(active && (_MenuOpen || (CanUseDesk && _Panel.InputFocused)));
        }
        bool CanUseDesk => isActiveAndEnabled && _AtDesk && !_MenuOpen && _Lobby && _Lobby.ReadyToPlay && !_Lobby.Transitioning;
        void CopyCode() { if (CanUseDesk) GUIUtility.systemCopyBuffer = _Lobby.Code; }
        void JoinRoom(string code) { if (CanUseDesk) _Lobby.JoinLobby(code); }
        void StartGame()
        {
            if (_Lobby && _Lobby.ReadyToPlay && !_Lobby.Transitioning && _Lobby.IsLobbyLeader)
                _Lobby.LobbyCommand(LobbyAction.Start);
        }
        void Resume() { _MenuOpen = false; Refresh(); }
        void RefreshMembers(LobbyRoster roster)
        {
            var members = roster.Members ?? Array.Empty<LobbyMember>();
            var leader = _Lobby && _Lobby.IsLobbyLeader;
            var transition = _Lobby && _Lobby.Transitioning;
            if (_RosterRendered && _RenderedLeader == leader && _RenderedTransition == transition &&
                LobbyRules.RosterMatches(_RenderedRoster, roster.Leader, roster.TransportHost, members)) return;
            _RosterRendered = true;
            _RenderedLeader = leader;
            _RenderedTransition = transition;
            _RenderedRoster = roster;
            for (var i = 0; i < _MemberRows.Length; ++i)
            {
                var row = _MemberRows[i];
                var occupied = i < members.Length;
                row.Root.gameObject.SetActive(occupied);
                if (!occupied) { row.Id = 0; continue; }
                var member = members[i];
                row.Id = member.Id;
                row.Label.text = $"Player {member.Id}  {(member.Ready ? "Ready" : "Waiting")}" +
                    (member.Id == roster.TransportHost ? "\nConnection host" : "");
                row.Crown.SetActive(member.Id == roster.Leader);
                var controls = leader && member.Id != roster.Leader;
                row.King.gameObject.SetActive(controls);
                row.Kick.gameObject.SetActive(controls);
                row.King.interactable = !transition;
                row.Kick.interactable = !transition && member.Id != roster.TransportHost;
            }
        }
        void OnDestroy()
        {
            _ToggleMenu.performed -= ToggleMenu;
            _ToggleMenu.Dispose();
        }
    }
}
