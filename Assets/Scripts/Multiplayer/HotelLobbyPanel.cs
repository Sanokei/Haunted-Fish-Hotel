using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HauntedFish.Multiplayer
{
    public sealed class HotelLobbyPanel : MonoBehaviour
    {
        [SerializeField] TMP_InputField Code;
        [SerializeField] TMP_Text Status;
        [SerializeField] Button Share;
        [SerializeField] Button Join;
        [SerializeField] HauntedHotelMultiplayer lobby;

        void Awake()
        {
            Share.onClick.AddListener(CopyCode);
            Join.onClick.AddListener(JoinRoom);
            Code.onValueChanged.AddListener(OnCodeChanged);
            Code.onSelect.AddListener(OnSelected);
            Code.onDeselect.AddListener(OnDeselected);
        }
        void Start() => Refresh();
        public void Bind(HauntedHotelMultiplayer session)
        {
            Unsubscribe();
            lobby = session;
            if (lobby && isActiveAndEnabled) lobby.StateChanged += Refresh;
            Refresh();
        }
        void OnEnable() { if (lobby) { lobby.StateChanged += Refresh; Refresh(); } }
        void OnDisable() => Unsubscribe();
        void Unsubscribe()
        {
            if (!lobby) return;
            lobby.StateChanged -= Refresh;
            lobby.SetInputFocused(false);
        }
        void CopyCode() { if (lobby) GUIUtility.systemCopyBuffer = lobby.Code; }
        void JoinRoom() { if (lobby) lobby.JoinLobby(Code.text); }
        void OnCodeChanged(string value) => Refresh();
        void OnSelected(string value) { if (lobby) lobby.SetInputFocused(true); }
        void OnDeselected(string value) { if (lobby) lobby.SetInputFocused(false); }
        void Refresh()
        {
            var transition = !lobby || lobby.Transitioning;
            var connected = lobby && !string.IsNullOrEmpty(lobby.Code);
            Status.text = lobby ? (connected ? "Room code: " + lobby.Code + "\n" : "") + lobby.Status : "Waiting for multiplayer...";
            Code.interactable = !transition;
            Join.interactable = !transition && LobbyCode.TryNormalize(Code.text, out _);
            Share.interactable = connected && !transition;
        }
        void OnDestroy()
        {
            Unsubscribe();
            Share.onClick.RemoveListener(CopyCode);
            Join.onClick.RemoveListener(JoinRoom);
            Code.onValueChanged.RemoveListener(OnCodeChanged);
            Code.onSelect.RemoveListener(OnSelected);
            Code.onDeselect.RemoveListener(OnDeselected);
        }
    }
}
