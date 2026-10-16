using System;
using System.Collections;
using System.Collections.Generic;
using Monologue.Dialogue;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace HauntedFish.Multiplayer
{
    public sealed class LobbyManager : MonoBehaviour, IHotelScene
    {
        public void Enter(HauntedHotelMultiplayer session) => Bind(session);
        public void Exit() { CancelIntroduction(false); StopLobbyStorm(); Bind(null); }
        public Vector3 SpawnPosition(int index) => new Vector3(index * 2.2f, 1.1f, 2f);

        [SerializeField] HotelLobbyPanel _Panel;
        bool _AtDesk, _WasOnStairs;
        bool MenuOpen => _Lobby && _Lobby.MemberMenuOpen;
        [SerializeField] LobbyTrigger _DeskZone;
        [SerializeField] LobbyTrigger _StairZone;
        [SerializeField] Texture2D _DeskCursor;
        [SerializeField] LobbyCursorManager _CursorManager;
        public bool InspectionAllowed => isActiveAndEnabled && _Lobby && _Lobby.ReadyToPlay && !_Lobby.Transitioning && !MenuOpen && !CinematicPlaying;
        // The radio comes in under the intro's final crane so the music is already playing when the lobby is handed over.
        public bool AudioReady => isActiveAndEnabled && _Lobby && _Lobby.ReadyToPlay && !_Lobby.Transitioning && (!IntroductionPlaying || _IntroductionMusicCue);
        bool _IntroductionMusicCue;
        [SerializeField] HauntedHotelMultiplayer _Lobby;
        [Header("Introduction (compiled Ink JSON)")]
        [SerializeField] TextAsset _IntroductionInk;
        [SerializeField, Min(.1f)] float _SkipHoldSeconds = 1f;
        [SerializeField] LobbyIntroductionPresentation _IntroductionPresentation;
        [SerializeField] LightningEffectManager _LightningEffect;
        [Header("Outro and credits")]
        [SerializeField] LobbyOutroSequence _Outro;
        [Tooltip("Walking into this zone rolls the credits while they are still pending.")]
        [SerializeField] LobbyTrigger _LeaveZone;
        bool _WasLeaving;
        // Introduction, outro and credits all lock lobby input and the ready zone the same way.
        bool CinematicPlaying => IntroductionPlaying || (_Outro && _Outro.Playing);
        [Tooltip("Restore custom scene state here; called after completion, skipping, or an error.")]
        [SerializeField] UnityEvent _IntroductionFinished = new UnityEvent();
        InputAction _SkipIntroduction;
        Coroutine _Introduction;
        bool _IntroductionPlayed;
        const string IntroductionSeenKey = "HauntedFish.IntroductionSeen.v1";
        // Editor and development builds replay without reading or writing the saved flag.
        static bool RememberIntroduction => !Application.isEditor && !Debug.isDebugBuild;
        float _SkipHeldSeconds;
        public bool IntroductionPlaying { get; private set; }
        // The step the introduction is currently waiting on, for diagnostics.
        public string IntroductionStep { get; private set; } = "";
        public bool IntroductionPresentationReady => _IntroductionPresentation && _IntroductionPresentation.IsReady;
        public bool IntroductionPending => !_IntroductionPlayed && _IntroductionInk && !(RememberIntroduction && PlayerPrefs.GetInt(IntroductionSeenKey, 0) == 1);
        readonly HashSet<HotelPlayer> _DeskPlayers = new HashSet<HotelPlayer>();
        void Awake()
        {
            _SkipIntroduction = new InputAction("Skip lobby introduction", InputActionType.Button, "<Keyboard>/space");
        }
        void Update()
        {
            // Also covers teleport/spawn occupancy without a trigger callback.
            var onStairs = IsInReadyZone(HotelPlayer.LocalPlayer);
            if (_WasOnStairs != onStairs) { _WasOnStairs = onStairs; Refresh(); }
            var atDesk = _Lobby && _Lobby.ReadyToPlay && _DeskZone && _DeskZone.ContainsPosition(HotelPlayer.LocalPlayer);
            if (_AtDesk != atDesk) Refresh();
            var leaving = _Outro && _LeaveZone && _LeaveZone.ContainsPosition(HotelPlayer.LocalPlayer);
            if (leaving && !_WasLeaving && !CinematicPlaying && _Lobby && _Lobby.ReadyToPlay && !_Lobby.Transitioning && !MenuOpen)
                _Outro.PlayCredits();
            _WasLeaving = leaving;
            if (!IntroductionPlaying) { _SkipHeldSeconds = 0; return; }
            _SkipHeldSeconds = _SkipIntroduction.IsPressed() ? _SkipHeldSeconds + Time.unscaledDeltaTime : 0;
            var progress = Mathf.Clamp01(_SkipHeldSeconds / Mathf.Max(.1f, _SkipHoldSeconds));
            if (_IntroductionPresentation) _IntroductionPresentation.SetSkipProgress(progress);
            if (progress >= 1) SkipIntroduction();
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
            _AtDesk = false;
            if (_Panel) _Panel.ClearSelection();
            _Lobby = session;
            if (_Lobby) _Lobby.SetIntroductionPlaying(CinematicPlaying);
            if (_Lobby)
            {
                _Lobby.BindReadyZone(IsInReadyZone);
                if (isActiveAndEnabled) _Lobby.StateChanged += Refresh;
            }
            Refresh();
        }
        void OnEnable()
        {
            _SkipIntroduction.Enable();
            if (_IntroductionPresentation) _IntroductionPresentation.SkipRequested += SkipIntroduction;
            if (_Outro) _Outro.PlayingChanged += OnOutroPlayingChanged;
            PrepareIntroduction();
            _Panel.CopyRequested += CopyCode;
            _Panel.JoinRequested += JoinRoom;
            _Panel.InputFocusChanged += OnInputFocusChanged;
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
        void StopLobbyStorm()
        {
            if (!_LightningEffect) return;
            _LightningEffect.StopAmbient();
            var audio = _LightningEffect.GetComponentInChildren<LobbyStormAudio>(true);
            if (audio) audio.StopAudio();
        }
        void OnDisable()
        {
            _SkipIntroduction.Disable();
            if (_IntroductionPresentation) _IntroductionPresentation.SkipRequested -= SkipIntroduction;
            if (_Outro) _Outro.PlayingChanged -= OnOutroPlayingChanged;
            CancelIntroduction(false);
            StopLobbyStorm();
            if (_DeskZone) _DeskZone.ZonePresenceChanged -= OnZonePresenceChanged;
            _Panel.CopyRequested -= CopyCode;
            _Panel.JoinRequested -= JoinRoom;
            _Panel.InputFocusChanged -= OnInputFocusChanged;
            HotelPlayer.LocalPlayerChanged -= OnLocalPlayerChanged;
            Unsubscribe();
            _DeskPlayers.Clear();
            _AtDesk = false;
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
                _AtDesk = _Lobby && _Lobby.ReadyToPlay && _DeskZone && _DeskZone.ContainsPosition(player);
                RefreshInputFocus();
            }
        }
        bool IsInReadyZone(HotelPlayer player) => isActiveAndEnabled && !CinematicPlaying && _StairZone && player &&
            !(player.IsRelevantPlayer && HauntedFish.Theatre.TheatrePortal.Visiting) &&
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
            _AtDesk = connected && _DeskZone && _DeskZone.ContainsPosition(HotelPlayer.LocalPlayer);
            var transition = !_Lobby || _Lobby.Transitioning;
            var roster = _Lobby ? _Lobby.Roster : default;
            if (!connected) _AtDesk = false;
            if (_Panel) _Panel.Render(connected, transition, MenuOpen || CinematicPlaying, _Lobby ? _Lobby.Code : "",
                roster, _Lobby ? _Lobby.Status : "Connecting...", IsInReadyZone(HotelPlayer.LocalPlayer));
            RefreshInputFocus();
        }
        void OnInputFocusChanged(bool focused) => RefreshInputFocus();
        void RefreshInputFocus()
        {
            var active = isActiveAndEnabled && _Lobby && _Lobby.ReadyToPlay;
            var cursor = active && (_AtDesk || MenuOpen);
            if (MenuOpen)
            {
                if (_CursorManager) _CursorManager.SetDesk(active, _AtDesk, true, _DeskCursor);
                if (_Lobby) _Lobby.SetInputFocused(false);
                return;
            }
            if (_CursorManager)
            {
                _CursorManager.SetDesk(active, _AtDesk, MenuOpen, _DeskCursor);
            }
            else
            {
            Cursor.visible = cursor;
            Cursor.lockState = cursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.SetCursor(active && _AtDesk ? _DeskCursor : null, Vector2.zero, CursorMode.Auto);
            }
            if (_Lobby) _Lobby.SetInputFocused(active && ((_CursorManager && _CursorManager.InspectingRadio) || (CanUseDesk && _Panel.InputFocused)));
        }
        bool CanUseDesk => isActiveAndEnabled && _AtDesk && !MenuOpen && !CinematicPlaying && _Lobby && _Lobby.ReadyToPlay && !_Lobby.Transitioning;
        void CopyCode() { if (CanUseDesk) GUIUtility.systemCopyBuffer = _Lobby.Code; }
        void JoinRoom(string code) { if (CanUseDesk) _Lobby.JoinLobby(code); }

        public void PrepareIntroduction()
        {
            if (!isActiveAndEnabled) return;
            // Coming back from a match plays that match's outro instead of the opening.
            if (!IntroductionPlaying && _Outro && LobbyOutcome.Pending != LobbyOutroKind.None)
            {
                _IntroductionPlayed = true;
                if (_LightningEffect) _LightningEffect.StartAmbient();
                _Outro.PlayOutcome(LobbyOutcome.Take());
                return;
            }
            if (_IntroductionPlayed)
            {
                if (!IntroductionPlaying && _LightningEffect) _LightningEffect.StartAmbient();
                return;
            }
            if (!_IntroductionInk) return;
            if (RememberIntroduction && PlayerPrefs.GetInt(IntroductionSeenKey, 0) == 1)
            {
                _IntroductionPlayed = true;
                if (_LightningEffect) _LightningEffect.StartAmbient();
                return;
            }
            BeginIntroduction();
        }
        void BeginIntroduction()
        {
            try
            {
                var steps = StoryFunctions.ReadAnimationSequence(_IntroductionInk.text);
                if (!_IntroductionPresentation) throw new InvalidOperationException("Assign the introduction UI presentation.");
                _IntroductionPresentation.ValidateSequence(steps);
                IntroductionPlaying = true; LobbyFireplaceFlicker.CrackleMuted = true;
                if (_Lobby) _Lobby.SetIntroductionPlaying(true);
                _IntroductionPresentation.Prepare();
                _IntroductionPlayed = true;
                _Introduction = StartCoroutine(PlayIntroduction(steps));
            }
            catch (Exception error)
            {
                Debug.LogError("Unable to play lobby introduction: " + error.Message, this);
                FinishIntroduction();
            }
        }
        IEnumerator PlayIntroduction(IReadOnlyList<StorySequenceStep> steps)
        {
            // Defer execution so the coroutine handle exists even for an empty sequence.
            yield return null;
            try
            {
                foreach (var step in steps)
                {
                    IntroductionStep = step.Kind + " " + step.Target + " " + step.Variant;
                    if (step.Kind == StorySequenceStepKind.LobbyShot && step.Target == "tunnel") _IntroductionMusicCue = true;
                    if (step.Kind == StorySequenceStepKind.Wait)
                    {
                        yield return new WaitForSecondsRealtime(step.Seconds);
                        continue;
                    }
                    yield return _IntroductionPresentation.Play(step);
                }
                FinishIntroduction(true);
            }
            finally { FinishIntroduction(); }
        }
        public void SkipIntroduction()
        {
            CancelIntroduction(true);
        }
        void CancelIntroduction(bool completed)
        {
            if (!IntroductionPlaying) return;
            var routine = _Introduction;
            _Introduction = null;
            FinishIntroduction(completed);
            if (routine != null) StopCoroutine(routine);
        }
        void FinishIntroduction(bool completed = false)
        {
            var wasPlaying = IntroductionPlaying;
            IntroductionPlaying = false; LobbyFireplaceFlicker.CrackleMuted = false;
            _IntroductionMusicCue = false;
            _SkipHeldSeconds = 0;
            _Introduction = null;
            if (_IntroductionPresentation) _IntroductionPresentation.Close();
            if (_Lobby) _Lobby.SetIntroductionPlaying(CinematicPlaying);
            if (wasPlaying)
            {
                if (completed && _LightningEffect) _LightningEffect.Play();
                if (completed && RememberIntroduction)
                {
                    PlayerPrefs.SetInt(IntroductionSeenKey, 1);
                    PlayerPrefs.Save();
                }
                _IntroductionFinished?.Invoke();
                Refresh();
            }
        }
        void OnOutroPlayingChanged()
        {
            if (_Lobby) _Lobby.SetIntroductionPlaying(CinematicPlaying);
            Refresh();
        }
        void OnDestroy()
        {
            _SkipIntroduction.Dispose();
        }
    }
}
