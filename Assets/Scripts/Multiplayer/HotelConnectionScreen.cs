using TMPro;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    public sealed class HotelConnectionScreen : MonoBehaviour
    {
        [SerializeField] HauntedHotelMultiplayer _Lobby;
        [SerializeField] LobbyManager _Introduction;
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

        void LateUpdate() => Refresh();
        public static bool CoverRequired(bool ready, bool pending, bool playing, bool presentationReady)
        {
            if (playing && presentationReady) return false;
            return !ready || ((pending || playing) && !presentationReady);
        }
        void Refresh()
        {
            var ready = _Lobby && _Lobby.ReadyToPlay && (!_Introduction || HotelPlayer.LocalPlayer);
            bool pending = _Introduction && _Introduction.IntroductionPending;
            bool playing = _Introduction && _Introduction.IntroductionPlaying;
            bool presented = _Introduction && _Introduction.IntroductionPresentationReady;
            bool cover = CoverRequired(ready, pending, playing, presented);
            _Overlay.alpha = cover ? 1f : 0f;
            _Overlay.blocksRaycasts = cover;
            _Overlay.interactable = cover;
            _ProgressFill.anchorMax = new Vector2(_Lobby ? _Lobby.ConnectionProgress : 0f, 1f);
            _Status.text = (pending || playing) && !presented ? "Loading introduction..." : (_Lobby ? _Lobby.Status : "Connecting...");
        }
    }
}

