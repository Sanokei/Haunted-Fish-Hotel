using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;
using System;
namespace HauntedFish.Multiplayer
{
    public sealed class RoundHowToPage : MonoBehaviour
    {
        [SerializeField] Canvas _Canvas;
        [SerializeField] VideoPlayer _Video;
        [SerializeField] GameObject _MissingRecording;
        [SerializeField] Button _Continue;
        [SerializeField] Text _Status;
        [SerializeField, Min(0)] float _MinimumReadSeconds = 1.5f;
        readonly RoundHowToGate _Gate = new RoundHowToGate();
        bool _Skip;
        float MinimumReadSeconds => GameplayPresentationPolicy.Finite(_MinimumReadSeconds) ? Mathf.Max(0, _MinimumReadSeconds) : 1.5f;
        public bool Showing => _Gate.Showing;
        public bool HasRecording => _Video && _Video.clip;
        public void Begin(uint local, uint ghost, Action finished)
        {
            Cancel(); _Gate.Begin(local, ghost, finished); _Skip = false;
            if (_Continue) _Continue.onClick.AddListener(RequestSkip);
            if (_MissingRecording) _MissingRecording.SetActive(!HasRecording);
            if (HasRecording) { _Video.time = 0; _Video.Play(); }
        }
        void RequestSkip() { if (_Continue && _Continue.interactable) _Skip = true; }
        void Update()
        {
            var keyboard = Keyboard.current; var pad = Gamepad.current;
            bool key = keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame);
            bool button = pad != null && pad.buttonSouth.wasPressedThisFrame;
            // Escape belongs to the pause/menu owner. This reads no gameplay actions.
            var local = HotelPlayer.LocalPlayer;
            bool blocked = local && local.InputBlocked;
            bool introVerified = local && local.RoundCinematicComplete;
            bool ready = introVerified && !blocked && _Gate.ReadSeconds >= MinimumReadSeconds && _Gate.Showing;
            if (_Continue) _Continue.interactable = ready;
            if (_Status && local) _Status.text = (local.RoundReady ? "YOU ARE READY - " : "READY UP TO BEGIN - ") +
                local.RoundReadyCount + " / " + local.RoundParticipantCount + " READY";
            _Gate.Tick(blocked ? 0 : Time.unscaledDeltaTime, ready && (_Skip || key || button), MinimumReadSeconds);
        }
        public void SetVisible(bool visible) { if (_Canvas) _Canvas.enabled = visible; }
        public void Cancel()
        {
            _Gate.Cancel(); _Skip = false;
            if (_Continue) _Continue.onClick.RemoveListener(RequestSkip);
            if (_Video) _Video.Stop();
        }
        void OnDisable() => Cancel();
    }
}
