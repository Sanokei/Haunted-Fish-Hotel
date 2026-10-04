using TMPro;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    public sealed class HotelConnectionScreen : MonoBehaviour
    {
        [SerializeField] HauntedHotelMultiplayer _Lobby;
        [SerializeField] CanvasGroup _Overlay;
        [SerializeField] RectTransform _ProgressFill;
        [SerializeField] TMP_Text _Status;

        void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        public void Bind(HauntedHotelMultiplayer session)
        {
            Unsubscribe();
            _Lobby = session;
            if (isActiveAndEnabled)
                Subscribe();
            Refresh();
        }

        void Subscribe()
        {
            if (_Lobby)
                _Lobby.StateChanged += Refresh;
        }

        void Unsubscribe()
        {
            if (_Lobby)
                _Lobby.StateChanged -= Refresh;
        }

        void Refresh()
        {
            var ready = _Lobby && _Lobby.ReadyToPlay;
            _Overlay.alpha = ready ? 0f : 1f;
            _Overlay.blocksRaycasts = !ready;
            _Overlay.interactable = !ready;
            _ProgressFill.anchorMax = new Vector2(_Lobby ? _Lobby.ConnectionProgress : 0f, 1f);
            _Status.text = _Lobby ? _Lobby.Status : "Connecting...";
        }
    }
}
