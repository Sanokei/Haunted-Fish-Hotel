using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System;

namespace HauntedFish.Multiplayer
{
    // Binds an authored, single-page role guide. No video, page navigation or UI construction.
    public sealed class RoundHowToPage : MonoBehaviour
    {
        [SerializeField] Canvas _Canvas;
        [SerializeField] Button _Continue;
        [SerializeField] Text _ContinueLabel;
        [SerializeField] Text _Status;
        [SerializeField] RoundHowToSafeArea _Layout;
        [SerializeField, Min(0)] float _MinimumReadSeconds = 1.5f;
        readonly RoundHowToGate _Gate = new RoundHowToGate();
        bool _ReadyRequested;
        float MinimumReadSeconds => GameplayPresentationPolicy.Finite(_MinimumReadSeconds) ? Mathf.Max(0, _MinimumReadSeconds) : 1.5f;
        public bool Showing => _Gate.Showing;

        public bool ValidateAuthoredBindings(out string error)
        {
            error = "";
            if (!_Canvas || !_Continue || !_ContinueLabel || !_Status || !_Layout || !_Layout.HasBindings)
                error = "Assign the authored canvas, ready button/label, status and safe-area layout.";
            return error.Length == 0;
        }

        public void Begin(uint local, uint ghost, Action finished)
        {
            Cancel();
            _Gate.Begin(local, ghost, finished);
            if (_Canvas) _Canvas.enabled = true;
            if (_Continue) _Continue.onClick.AddListener(RequestReady);
            RenderStatus(CurrentStatus(HotelPlayer.LocalPlayer), HotelPlayer.LocalPlayer);
        }

        void RequestReady()
        {
            // Validate at the event itself as well as the frame: hidden/menu clicks cannot queue readiness.
            if (CurrentStatus(HotelPlayer.LocalPlayer) == RoundHowToStatus.CanReady && _Gate.Showing)
                _ReadyRequested = true;
        }

        RoundHowToStatus CurrentStatus(HotelPlayer local) => RoundHowToState.Status(local,
            local && local.RoundCinematicComplete, local && local.RoundReady, _Gate.Submitted,
            local && local.InputBlocked, _Canvas && _Canvas.enabled, _Gate.ReadSeconds, MinimumReadSeconds);

        static bool ReadyPressed()
        {
            var keyboard = Keyboard.current;
            var pad = Gamepad.current;
            // Escape belongs to the pause/menu owner. These are not gameplay actions.
            return keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) ||
                pad != null && pad.buttonSouth.wasPressedThisFrame;
        }

        void Update()
        {
            var local = HotelPlayer.LocalPlayer;
            var status = CurrentStatus(local);
            bool canRead = local && local.RoundCinematicComplete && !local.InputBlocked && _Canvas && _Canvas.enabled;
            bool deliberateReady = status == RoundHowToStatus.CanReady && (_ReadyRequested || ReadyPressed());
            _ReadyRequested = false; // Never carry a click through a hidden page, menu or reconnect.
            _Gate.Tick(canRead ? Time.unscaledDeltaTime : 0, deliberateReady, MinimumReadSeconds);
            RenderStatus(CurrentStatus(local), local);
        }

        void RenderStatus(RoundHowToStatus state, HotelPlayer local)
        {
            if (_Continue) _Continue.interactable = state == RoundHowToStatus.CanReady && _Gate.Showing;
            string label = RoundHowToState.Label(state);
            if (_ContinueLabel && _ContinueLabel.text != label) _ContinueLabel.text = label;
            string progress = RoundHowToState.Progress(local ? local.RoundReadyCount : 0, local ? local.RoundParticipantCount : 0);
            if (_Status && _Status.text != progress) _Status.text = progress;
        }

        public void SetVisible(bool visible)
        {
            if (_Canvas) _Canvas.enabled = visible;
            if (!visible) _ReadyRequested = false;
            RenderStatus(CurrentStatus(HotelPlayer.LocalPlayer), HotelPlayer.LocalPlayer);
        }

        public void Cancel()
        {
            _Gate.Cancel();
            _ReadyRequested = false;
            if (_Continue) { _Continue.onClick.RemoveListener(RequestReady); _Continue.interactable = false; }
        }
        void OnDisable() => Cancel();
    }
}
