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
        public void Exit() => Bind(null);
        public Vector3 SpawnPosition(int index) => new Vector3(index * 2.2f, 1.1f, 2f);

        [SerializeField] HotelLobbyPanel _Panel;
        bool _AtDesk, _WasOnStairs;
        bool MenuOpen => _Lobby && _Lobby.MemberMenuOpen;
        [SerializeField] LobbyTrigger _DeskZone;
        [SerializeField] LobbyTrigger _StairZone;
        [SerializeField] Texture2D _DeskCursor;
        [SerializeField] LobbyCursorManager _CursorManager;
        public bool InspectionAllowed => isActiveAndEnabled && _Lobby && _Lobby.ReadyToPlay && !_Lobby.Transitioning && !MenuOpen && !IntroductionPlaying;
        [SerializeField] HauntedHotelMultiplayer _Lobby;
        [Header("Introduction (compiled Ink JSON)")]
        [SerializeField] TextAsset _IntroductionInk;
        [SerializeField, Min(.1f)] float _SkipHoldSeconds = 1f;
        [SerializeField] LobbyIntroductionPresentation _IntroductionPresentation;
        [SerializeField] LightningEffectManager _LightningEffect;
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
            if (_Lobby) _Lobby.SetIntroductionPlaying(IntroductionPlaying);
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
        void OnDisable()
        {
            _SkipIntroduction.Disable();
            CancelIntroduction(false);
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
        bool IsInReadyZone(HotelPlayer player) => isActiveAndEnabled && !IntroductionPlaying && _StairZone && player &&
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
            if (_Panel) _Panel.Render(connected, transition, MenuOpen || IntroductionPlaying, _Lobby ? _Lobby.Code : "",
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
            if (_Lobby) _Lobby.SetInputFocused(active && CanUseDesk && _Panel.InputFocused);
        }
        bool CanUseDesk => isActiveAndEnabled && _AtDesk && !MenuOpen && !IntroductionPlaying && _Lobby && _Lobby.ReadyToPlay && !_Lobby.Transitioning;
        void CopyCode() { if (CanUseDesk) GUIUtility.systemCopyBuffer = _Lobby.Code; }
        void JoinRoom(string code) { if (CanUseDesk) _Lobby.JoinLobby(code); }

        public void PrepareIntroduction()
        {
            if (_IntroductionPlayed || !_IntroductionInk || !isActiveAndEnabled) return;
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
            _IntroductionPlayed = true;
            try
            {
                var steps = StoryFunctions.ReadAnimationSequence(_IntroductionInk.text);
                if (!_IntroductionPresentation) throw new InvalidOperationException("Assign the introduction UI presentation.");
                _IntroductionPresentation.ValidateSequence(steps);
                IntroductionPlaying = true;
                if (_Lobby) _Lobby.SetIntroductionPlaying(true);
                _IntroductionPresentation.Prepare();
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
            IntroductionPlaying = false;
            _SkipHeldSeconds = 0;
            _Introduction = null;
            if (_IntroductionPresentation) _IntroductionPresentation.Close();
            if (_Lobby) _Lobby.SetIntroductionPlaying(false);
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
        void OnDestroy()
        {
            _SkipIntroduction.Dispose();
        }
    }
}
