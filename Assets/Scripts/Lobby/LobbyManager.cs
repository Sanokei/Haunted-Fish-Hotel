using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HauntedFish.Multiplayer
{
    public sealed class LobbyManager : MonoBehaviour
    {
        [SerializeField] HotelLobbyPanel _Panel;
        bool _AtDesk, _MenuOpen;
        [SerializeField] GameObject _DeskControls;
        [SerializeField] Collider _DeskTrigger;
        [SerializeField] Collider _StairTrigger;
        [SerializeField] HauntedHotelMultiplayer _Lobby;
        GameObject _Menu;
        RectTransform _Rows;
        Button _Start;
        LobbyRoster _RenderedRoster;
        bool _RenderedLeader, _RenderedTransition, _RosterRendered;
        InputAction _ToggleMenu;
        sealed class MemberRow
        {
            public RectTransform Root;
            public TMP_Text Label;
            public GameObject Crown;
            public Button King, Kick;
        }
        readonly Dictionary<uint, MemberRow> _MemberRows = new Dictionary<uint, MemberRow>();
        readonly HashSet<uint> _PresentMembers = new HashSet<uint>();
        readonly List<uint> _RemovedMembers = new List<uint>();
        void Awake()
        {
            _ToggleMenu = new InputAction("Lobby menu", InputActionType.Button, "<Keyboard>/escape");
            _ToggleMenu.performed += ToggleMenu;
            BuildMenu();
            _DeskControls.SetActive(false);
        }
        void Start() => Refresh();
        public void Bind(HauntedHotelMultiplayer session)
        {
            Unsubscribe();
            _AtDesk = _MenuOpen = false;
            _Panel.ClearSelection();
            _Lobby = session;
            if (_Lobby)
            {
                _Lobby.BindStairs(_StairTrigger);
                if (isActiveAndEnabled) _Lobby.StateChanged += Refresh;
            }
            Refresh();
        }
        void OnEnable()
        {
            _Panel.CopyRequested += CopyCode;
            _Panel.JoinRequested += JoinRoom;
            _Panel.InputFocusChanged += OnInputFocusChanged;
            _ToggleMenu.Enable();
            HotelPlayer.LocalPlayerChanged += OnLocalPlayerChanged;
            if (_Lobby) _Lobby.StateChanged += Refresh;
            Refresh();
        }
        void OnDisable()
        {
            _Panel.CopyRequested -= CopyCode;
            _Panel.JoinRequested -= JoinRoom;
            _Panel.InputFocusChanged -= OnInputFocusChanged;
            _ToggleMenu.Disable();
            HotelPlayer.LocalPlayerChanged -= OnLocalPlayerChanged;
            Unsubscribe();
            _AtDesk = _MenuOpen = false;
            _Panel.ClearSelection();
            Refresh();
        }
        void Unsubscribe()
        {
            if (!_Lobby) return;
            _Lobby.StateChanged -= Refresh;
            _Lobby.SetInputFocused(false);
        }
        void Update()
        {
            var local = HotelPlayer.LocalPlayer;
            var desk = _Lobby && _Lobby.ReadyToPlay && LobbyTrigger.Contains(_DeskTrigger, local);
            if (desk != _AtDesk)
            {
                _AtDesk = desk;
                if (!desk)
                {
                    _Panel.ClearSelection();
                }
                Refresh();
            }
        }
        void ToggleMenu(InputAction.CallbackContext context)
        {
            _MenuOpen = !_MenuOpen && _Lobby && _Lobby.ReadyToPlay;
            Refresh();
        }
        void OnLocalPlayerChanged(HotelPlayer player)
        {
            _AtDesk = false;
            _Panel.ClearSelection();
            Refresh();
        }
        void Refresh()
        {
            var connected = isActiveAndEnabled && _Lobby && _Lobby.ReadyToPlay;
            var transition = !_Lobby || _Lobby.Transitioning;
            var roster = _Lobby ? _Lobby.Roster : default;
            var members = roster.Members ?? Array.Empty<LobbyMember>();
            var ready = 0;
            foreach (var member in members) if (member.Ready) ++ready;
            if (!connected) _AtDesk = _MenuOpen = false;
            _Panel.Render(connected, transition, _AtDesk, _MenuOpen, _Lobby ? _Lobby.Code : "",
                connected ? $"{ready}/{members.Length} players ready\nStand in the stairs to ready up" : (_Lobby ? _Lobby.Status : "Connecting..."));
            _DeskControls.SetActive(_AtDesk && connected && !_MenuOpen);
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
            if (_Lobby) _Lobby.SetInputFocused(active && (_MenuOpen || (CanUseDesk && _Panel.InputFocused)));
        }
        bool CanUseDesk => isActiveAndEnabled && _AtDesk && !_MenuOpen && _Lobby && _Lobby.ReadyToPlay && !_Lobby.Transitioning;
        void CopyCode() { if (CanUseDesk) GUIUtility.systemCopyBuffer = _Lobby.Code; }
        void JoinRoom(string code) { if (CanUseDesk) _Lobby.JoinLobby(code); }
        RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
        TMP_Text Label(string name, Transform parent, string text, Vector2 position, Vector2 size)
        {
            var rect = Rect(name, parent, position, size);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = _Panel.Font;
            label.fontSize = 20;
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }
        Button ActionButton(string name, Transform parent, Vector2 position, Vector2 size, Action action, string icon = null)
        {
            var rect = Rect(name, parent, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.15f, .19f, .24f, 1);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            // Buttons must not be reachable by navigation while the local desk is inaccessible.
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            if (icon != null)
            {
                var iconRect = Rect(name + " icon", rect, Vector2.zero, size - new Vector2(8, 8));
                var graphic = iconRect.gameObject.AddComponent<Image>();
                graphic.sprite = Resources.Load<Sprite>("LobbyIcons/" + icon);
                graphic.preserveAspect = true;
                graphic.raycastTarget = false;
            }
            else Label(name + " label", rect, name, Vector2.zero, size);
            return button;
        }
        void BuildMenu()
        {
            var root = Rect("Escape Lobby Menu", transform, Vector2.zero, new Vector2(620, 440));
            root.gameObject.AddComponent<Image>().color = new Color(.025f, .035f, .05f, .98f);
            _Menu = root.gameObject;
            Label("Title", root, "Lobby players", new Vector2(0, 180), new Vector2(580, 40));
            _Rows = Rect("Players", root, new Vector2(0, 25), new Vector2(580, 250));
            _Start = ActionButton("Start game", root, new Vector2(-125, -170), new Vector2(210, 44),
                () => { if (_Lobby) _Lobby.LobbyCommand(LobbyAction.Start); });
            ActionButton("Resume", root, new Vector2(125, -170), new Vector2(210, 44), () => { _MenuOpen = false; Refresh(); });
            _Menu.SetActive(false);
        }
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
            _PresentMembers.Clear();
            for (var i = 0; i < members.Length; ++i)
            {
                var member = members[i];
                _PresentMembers.Add(member.Id);
                if (!_MemberRows.TryGetValue(member.Id, out var row))
                {
                    var root = Rect("Player " + member.Id, _Rows, Vector2.zero, new Vector2(560, 50));
                    var crown = Rect("Crown", root, new Vector2(120, 0), new Vector2(40, 40)).gameObject.AddComponent<Image>();
                    crown.sprite = Resources.Load<Sprite>("LobbyIcons/King");
                    crown.preserveAspect = true;
                    crown.raycastTarget = false;
                    var id = member.Id;
                    row = new MemberRow
                    {
                        Root = root,
                        Label = Label("Player", root, "", new Vector2(-70, 0), new Vector2(350, 46)),
                        Crown = crown.gameObject,
                        King = ActionButton("King player " + id, root, new Vector2(180, 0), new Vector2(44, 44),
                            () => { if (_Lobby) _Lobby.LobbyCommand(LobbyAction.King, id); }, "Transferking"),
                        Kick = ActionButton("Kick player " + id, root, new Vector2(238, 0), new Vector2(44, 44),
                            () => { if (_Lobby) _Lobby.LobbyCommand(LobbyAction.Kick, id); }, "Kickplayer")
                    };
                    _MemberRows.Add(id, row);
                }
                row.Root.anchoredPosition = new Vector2(0, 90 - i * 58);
                row.Label.text = $"Player {member.Id}  {(member.Ready ? "Ready" : "Waiting")}" +
                    (member.Id == roster.TransportHost ? "\nConnection host" : "");
                row.Crown.SetActive(member.Id == roster.Leader);
                var controls = leader && member.Id != roster.Leader;
                row.King.gameObject.SetActive(controls);
                row.Kick.gameObject.SetActive(controls);
                row.King.interactable = !_Lobby.Transitioning;
                row.Kick.interactable = !_Lobby.Transitioning && member.Id != roster.TransportHost;
            }
            _RemovedMembers.Clear();
            foreach (var id in _MemberRows.Keys)
                if (!_PresentMembers.Contains(id)) _RemovedMembers.Add(id);
            foreach (var id in _RemovedMembers)
            {
                Destroy(_MemberRows[id].Root.gameObject);
                _MemberRows.Remove(id);
            }
        }
        void OnDestroy()
        {
            _ToggleMenu.performed -= ToggleMenu;
            _ToggleMenu.Dispose();
            if (_Menu) Destroy(_Menu);
        }
    }
}
