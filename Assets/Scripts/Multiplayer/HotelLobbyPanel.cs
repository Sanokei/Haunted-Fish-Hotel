using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HauntedFish.Multiplayer
{
    public sealed class HotelLobbyPanel : MonoBehaviour
    {
        [SerializeField] TMP_InputField _Code;
        [SerializeField] TMP_Text _Status;
        [SerializeField] Button _Share;
        [SerializeField] Button _Join;
        [SerializeField] HauntedHotelMultiplayer _Lobby;

        void Awake()
        {
            _Share.onClick.AddListener(CopyCode);
            _Join.onClick.AddListener(JoinRoom);
            _Code.onValueChanged.AddListener(OnCodeChanged);
            _Code.onSelect.AddListener(OnSelected);
            _Code.onDeselect.AddListener(OnDeselected);
        }
        void Start() => Refresh();
        public void Bind(HauntedHotelMultiplayer session)
        {
            Unsubscribe();
            _Lobby = session;
            if (_Lobby && isActiveAndEnabled) _Lobby.StateChanged += Refresh;
            Refresh();
        }
        void OnEnable()
        {
            if (_Lobby)
                _Lobby.StateChanged += Refresh;
            Refresh();
        }
        void OnDisable() => Unsubscribe();
        void Unsubscribe()
        {
            if (!_Lobby) return;
            _Lobby.StateChanged -= Refresh;
            _Lobby.SetInputFocused(false);
        }
        void CopyCode()
        {
            if (_Lobby)
                GUIUtility.systemCopyBuffer = _Lobby.Code;
        }
        void JoinRoom()
        {
            if (_Lobby)
                _Lobby.JoinLobby(_Code.text);
        }
        void OnCodeChanged(string value) => Refresh();
        void OnSelected(string value)
        {
            if (_Lobby)
                _Lobby.SetInputFocused(true);
        }
        void OnDeselected(string value)
        {
            if (_Lobby)
                _Lobby.SetInputFocused(false);
        }
        void Refresh()
        {
            var transition = !_Lobby || _Lobby.Transitioning;
            var connected = _Lobby && !string.IsNullOrEmpty(_Lobby.Code);
            _Status.text = _Lobby ? (connected ? "Room code: " + _Lobby.Code + "\n" : "") + _Lobby.Status : "Waiting for multiplayer...";
            _Code.interactable = !transition;
            _Join.interactable = !transition && LobbyCode.TryNormalize(_Code.text, out _);
            _Share.interactable = connected && !transition;
        }
        void OnDestroy()
        {
            Unsubscribe();
            _Share.onClick.RemoveListener(CopyCode);
            _Join.onClick.RemoveListener(JoinRoom);
            _Code.onValueChanged.RemoveListener(OnCodeChanged);
            _Code.onSelect.RemoveListener(OnSelected);
            _Code.onDeselect.RemoveListener(OnDeselected);
        }
    }
}
