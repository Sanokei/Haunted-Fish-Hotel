using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HauntedFish.Multiplayer
{
    public sealed class MemberMenu : MonoBehaviour
    {
        [SerializeField] GameObject _Menu;
        [SerializeField] Button _Start, _Resume;
        [SerializeField] MemberRow[] _MemberRows;
        HauntedHotelMultiplayer _Lobby;
        LobbyRoster _RenderedRoster;
        bool _RenderedLeader, _RenderedTransition, _RosterRendered;
        InputAction _ToggleMenu;
        [SerializeField] MemberMenuPresentation _Presentation;
        [SerializeField] GameObject _EventSystemPrefab;
        GameObject _EventSystem;
        readonly MemberMenuInputCapture _Capture = new MemberMenuInputCapture();
        public bool IsOpen => _Capture.IsOpen;
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

        void Awake()
        {
            _ToggleMenu = new InputAction("Member menu", InputActionType.Button, "<Keyboard>/escape");
            _ToggleMenu.performed += ToggleMenu;
            foreach (var row in _MemberRows)
            {
                row.Promote = () => { if (_Lobby) _Lobby.LobbyCommand(LobbyAction.King, row.Id); };
                row.Remove = () => { if (_Lobby) _Lobby.LobbyCommand(LobbyAction.Kick, row.Id); };
                row.King.onClick.AddListener(row.Promote);
                row.Kick.onClick.AddListener(row.Remove);
            }
            _Start.onClick.AddListener(StartGame);
            _Resume.onClick.AddListener(Resume);
            if (!_Presentation || !_Presentation.Initialize()) { enabled = false; return; }
            _Menu.SetActive(false);
        }
        public void Bind(HauntedHotelMultiplayer session)
        {
            if (_Lobby) _Lobby.StateChanged -= Refresh;
            _Lobby = session;
            _Lobby.StateChanged += Refresh;
            Refresh();
        }
        void OnEnable()
        {
            _ToggleMenu?.Enable();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        void OnDisable()
        {
            _ToggleMenu?.Disable();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Resume();
            _Capture.Reset();
            if (_Lobby) _Lobby.SetMemberMenuOpen(false);
        }
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _RosterRendered = false;
            Resume();
            // Use each scene's authored EventSystem; provide one for scenes without UI.
            bool authored = false;
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<EventSystem>(true)) authored = true;
            if (authored && _EventSystem) Destroy(_EventSystem);
            Refresh();
        }
        void EnsureEventSystem()
        {
            if (EventSystem.current) return;
            if (!_EventSystemPrefab) { Debug.LogError("Assign the authored menu EventSystem prefab.", this); return; }
            _EventSystem = Instantiate(_EventSystemPrefab, transform);
            _EventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        void ToggleMenu(InputAction.CallbackContext context)
        {
            if (!_Presentation) return;
            if (IsOpen && _Presentation.Back()) return;
            if (!IsOpen && LobbyCursorManager.Instance && LobbyCursorManager.Instance.CloseRadioInspection()) return;
            SetOpen(!IsOpen && _Lobby && MemberMenuInputCapture.CanOpen(_Lobby.ReadyToPlay, _Lobby.Transitioning, _Lobby.IntroductionPlaying));
        }
        void SetOpen(bool open)
        {
            if (open == IsOpen) return;
            _Capture.SetOpen(open, Time.frameCount);
            if (open) { EnsureEventSystem(); _Menu.SetActive(true); _Presentation.Open(); }
            else if (_Presentation) _Presentation.Close();
            if (!open && EventSystem.current && EventSystem.current.currentSelectedGameObject &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(_Menu.transform))
                EventSystem.current.SetSelectedGameObject(null);
            // Capture the closing click through release so Resume cannot also place a trap.
            if (_Lobby && open) _Lobby.SetMemberMenuOpen(true);
            Refresh();
        }
        void Resume() => SetOpen(false);
        void StartGame()
        {
            if (_Lobby && _Lobby.ReadyToPlay && !_Lobby.Transitioning && _Lobby.IsLobbyLeader &&
                _Lobby.SceneTravel.TargetScene == "Lobby") _Lobby.LobbyCommand(LobbyAction.Start);
        }
        void Refresh()
        {
            bool connected = isActiveAndEnabled && _Lobby && _Lobby.ReadyToPlay;
            if ((!connected || _Lobby.Transitioning) && IsOpen)
            {
                SetOpen(false);
                return;
            }
            _Menu.SetActive(IsOpen);
            _Start.gameObject.SetActive(connected && _Lobby.IsLobbyLeader && _Lobby.SceneTravel.TargetScene == "Lobby");
            _Start.interactable = connected && !_Lobby.Transitioning && LobbyRules.CanStart(_Lobby.Roster.Members, _Lobby.Roster.Quickplay, Application.isEditor);
            if (IsOpen)
            {
                EnsureEventSystem();
                RefreshMembers(_Lobby.Roster);
            }
            if (IsOpen || SceneManager.GetActiveScene().name != "Lobby")
            {
                Cursor.visible = IsOpen;
                Cursor.lockState = IsOpen ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
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
            for (var i = 0; i < _MemberRows.Length; ++i)
            {
                var row = _MemberRows[i];
                var occupied = i < members.Length;
                row.Root.gameObject.SetActive(occupied);
                if (!occupied) { row.Id = 0; continue; }
                var member = members[i];
                row.Id = member.Id;
                row.Label.text = $"Player {member.Id}" +
                    (_Lobby.SceneTravel.TargetScene == "Lobby" ? $"  {(member.Ready ? "Ready" : "Waiting")}" : "") +
                    (member.Id == roster.TransportHost ? "\nConnection host" : "");
                row.Crown.SetActive(member.Id == roster.Leader);
                var controls = leader && member.Id != roster.Leader;
                row.King.gameObject.SetActive(controls);
                row.Kick.gameObject.SetActive(controls);
                row.King.interactable = !transition;
                row.Kick.interactable = !transition && member.Id != roster.TransportHost;
            }
        }
        void LateUpdate()
        {
            if (IsOpen && _Lobby && _Lobby.Transitioning) Resume();
            ReleaseInputCapture(Time.frameCount, Mouse.current == null ? (bool?)null : Mouse.current.leftButton.isPressed);
            if (IsOpen) { Cursor.visible = true; Cursor.lockState = CursorLockMode.None; }
        }
        // Unity supplies frame/device state; the effect can also be exercised by isolated native validation.
        void ReleaseInputCapture(int frame, bool? pointerPressed)
        {
            if (_Capture.TryRelease(frame, pointerPressed) && _Lobby) _Lobby.SetMemberMenuOpen(false);
        }
        void OnDestroy()
        {
            if (_Lobby) _Lobby.StateChanged -= Refresh;
            _Presentation?.Dispose();
            if (_ToggleMenu != null) { _ToggleMenu.performed -= ToggleMenu; _ToggleMenu.Dispose(); }
        }
    }
}
