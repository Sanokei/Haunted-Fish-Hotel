using Monologue.Dialogue;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

namespace HauntedFish.Multiplayer
{
    // Pointer-based lobby inspection with owned input and inspect cursor priority.
    public sealed class LobbyCursorManager : MonoBehaviour
    {
        public static LobbyCursorManager Instance { get; private set; }
        [SerializeField] Collider _Radio, _Dial, _Bell, _Lamp;
        [SerializeField] AudioSource _RadioAudio, _BellAudio, _ClickAudio;
        [SerializeField] LobbyRadioPlayback _RadioPlayback;
        [SerializeField] Texture2D _ActionCursor;
        [SerializeField] SpriteRenderer[] _MusicWaves;
        Vector3[] _WaveScales;
        Quaternion[] _WaveRotations;
        Color[] _WaveColors;
        CinemachineCamera _InspectionCamera;
        bool _RadioOpen, _MusicReady;
        Collider _Hovered, _PressedTarget;
        bool _SuppressClickUntilRelease;
        float _NextPropTime;
        [SerializeField] LobbyManager _Lobby;
        [SerializeField] Transform _InteractionOrigin, _Facing;
        [SerializeField] Vector3 _FrontDirectionLocal = Vector3.back;
        public Vector3 FrontDirection => _Facing ? Vector3.ProjectOnPlane(_Facing.TransformDirection(_FrontDirectionLocal), Vector3.up).normalized : Vector3.zero;
        [SerializeField] Collider _Target;
        [SerializeField] EnterDialogueMode _Dialogue;
        [SerializeField] Texture2D _InspectCursor;
        [SerializeField] Vector2 _Hotspot;
        InputAction _Click;
        RaycastHit[] _Hits = new RaycastHit[8];
        bool _DeskActive, _AtDesk, _Menu, _Pressed;
        Texture2D _DeskCursor, _Applied;
        bool _Visible, _Initialized;
        public bool Hovering { get; private set; }
        public bool InspectingRadio => _RadioOpen;
        public Vector3 RadioFocus => _Radio ? _Radio.bounds.center : transform.position;
        public bool CloseRadioInspection()
        {
            if (!_RadioOpen) return false;
            CloseInspection();
            return true;
        }
        public void SetDesk(bool active, bool atDesk, bool menu, Texture2D cursor)
        { _DeskActive = active; _AtDesk = atDesk; _Menu = menu; _DeskCursor = cursor; }
        void Awake()
        {
            Instance = this;
            if (_MusicWaves != null)
            {
                _WaveScales = new Vector3[_MusicWaves.Length];
                _WaveRotations = new Quaternion[_MusicWaves.Length];
                _WaveColors = new Color[_MusicWaves.Length];
                for (int i = 0; i < _MusicWaves.Length; i++)
                    if (_MusicWaves[i]) { _WaveScales[i] = _MusicWaves[i].transform.localScale; _WaveRotations[i] = _MusicWaves[i].transform.localRotation; _WaveColors[i] = _MusicWaves[i].color; }
            }
            _Click = new InputAction("Inspect Lobby item", InputActionType.Button, "<Mouse>/leftButton");
            if (!_RadioPlayback) ConfigureAudio(_RadioAudio, true);
            ConfigureAudio(_BellAudio, false); ConfigureAudio(_ClickAudio, false);
        }
        static void ConfigureAudio(AudioSource source, bool loop)
        {
            if (!source) return;
            source.Stop(); source.playOnAwake = false; source.loop = loop;
            source.spatialBlend = 1; source.dopplerLevel = 0;
            source.minDistance = 1.5f; source.maxDistance = 22;
            source.rolloffMode = AudioRolloffMode.Linear;
        }
        void OnEnable() { _Click.Enable(); _Initialized = false; DialogueManager.OnDialogueEndEvent += OnDialogueEnded; }
        public bool CanInspect(HotelPlayer player)
        {
            return player && player.IsRelevantPlayer && player.ControlsReady && player.ControlMode == HotelControlMode.Lobby && !player.InputBlocked;
        }
        public bool HitTarget(Ray ray, HotelPlayer player)
            => HitTarget(ray, player, _Target);
        bool HitTarget(Ray ray, HotelPlayer player, Collider collider)
        {
            if (!collider || !collider.enabled || !collider.Raycast(ray, out var target, 100)) return false;
            int count;
            while ((count = Physics.RaycastNonAlloc(ray, _Hits, target.distance, ~0, QueryTriggerInteraction.Ignore)) == _Hits.Length)
                System.Array.Resize(ref _Hits, _Hits.Length * 2);
            for (int i = 0; i < count; i++)
                if (_Hits[i].collider != collider && _Hits[i].collider.GetComponentInParent<HotelPlayer>() != player &&
                    !((collider == _Radio || collider == _Dial) && _Hits[i].collider.GetComponentInParent<HotelPlayer>()))
                    return false;
            return true;
        }
        void Update()
        {
            var player = HotelPlayer.LocalPlayer;
            var camera = HotelViewCamera.Current;
            bool dialogue = DialogueManager.Instance && DialogueManager.Instance.ActiveDialoguePanel;
            // Menus and dialogue do not interrupt music after entry; connection/travel/intro do.
            bool musicReady = _Lobby && _Lobby.AudioReady;
            if (_RadioPlayback) _RadioPlayback.SetReady(musicReady);
            if (_MusicReady != musicReady)
            {
                _MusicReady = musicReady;
                if (!_RadioPlayback && _RadioAudio) { if (musicReady) _RadioAudio.Play(); else _RadioAudio.Stop(); }
            }
            AnimateWaves();
            if (!_MusicReady) CloseInspection();
            if (_RadioOpen && _Lobby && !_Lobby.InspectionAllowed) CloseInspection();
            if (_RadioOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CloseInspection();
            // Lobby inspection depends on the pointer ray, never the player's location.
            bool inspectionAllowed = _Lobby && _Lobby.InspectionAllowed && !dialogue && (CanInspect(player) || (_RadioOpen && player && player.ControlsReady && player.ControlMode == HotelControlMode.Lobby));
            bool blocked = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
            _Hovered = null;
            if (inspectionAllowed && !blocked && camera && Mouse.current != null)
            {
                var ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (_RadioOpen && HitTarget(ray, player, _Dial)) _Hovered = _Dial;
                else if (!_RadioOpen)
                {
                    if (HitTarget(ray, player, _Target)) _Hovered = _Target;
                    else if (HitTarget(ray, player, _Radio)) _Hovered = _Radio;
                    else if (HitTarget(ray, player, _Bell)) _Hovered = _Bell;
                    else if (HitTarget(ray, player, _Lamp)) _Hovered = _Lamp;
                }
            }
            Hovering = _Hovered;
            bool suppressClick = _SuppressClickUntilRelease || dialogue;
            if (suppressClick) _Pressed = false;
            if (_SuppressClickUntilRelease && _Click.WasReleasedThisFrame()) _SuppressClickUntilRelease = false;
            if (!suppressClick && _Click.WasPressedThisFrame()) { _Pressed = Hovering; _PressedTarget = _Hovered; }
            if (!suppressClick && _Click.WasReleasedThisFrame())
            {
                bool interact = _Pressed && Hovering && _PressedTarget == _Hovered;
                _Pressed = false;
                if (interact)
                {
                    if (player) player.RequestInteractionSound();
                    if (_Hovered == _Target && _Dialogue) { Inspect(_Target); _Dialogue.EnterDialogue(); }
                    else if (_Hovered == _Radio) { _RadioOpen = true; Inspect(_Radio); }
                    else if (player) player.RequestLobbyProp(_Hovered == _Dial ? 0 : _Hovered == _Bell ? 1 : 2);
                }
                else if (_RadioOpen && !blocked && !_Hovered) CloseInspection();
            }
            if (!inspectionAllowed) _Pressed = false;
            bool visible = _DeskActive && (_AtDesk || _Menu) || inspectionAllowed || dialogue;
            Apply(visible, Hovering ? _Hovered == _Target ? _InspectCursor : _ActionCursor : _DeskActive && _AtDesk ? _DeskCursor : null);
        }
        void AnimateWaves()
        {
            if (_MusicWaves == null) return;
            for (int i = 0; i < _MusicWaves.Length; i++)
            {
                var wave = _MusicWaves[i]; if (!wave) continue;
                wave.enabled = _MusicReady;
                if (!_MusicReady) continue;
                float phase = Mathf.Repeat(Time.unscaledTime * .35f + i / (float)_MusicWaves.Length, 1);
                wave.transform.localScale = _WaveScales[i] * Mathf.Lerp(.72f, 1.4f, phase);
                wave.transform.localRotation = _WaveRotations[i] * Quaternion.Euler(0, 0, Time.unscaledTime * (i % 2 == 0 ? 12 : -10));
                var color = _WaveColors[i]; color.a *= Mathf.Sin(phase * Mathf.PI) * .8f; wave.color = color;
            }
        }
        void Inspect(Collider target)
        {
            if (!_InspectionCamera)
            {
                var go = new GameObject("Lobby inspection camera"); go.transform.SetParent(transform, false);
                _InspectionCamera = go.AddComponent<CinemachineCamera>();
            }
            var centre = target.bounds.center;
            var front = target.transform.TransformDirection(Vector3.back).normalized;
            float distance = Mathf.Max(1.3f, target.bounds.size.magnitude * 1.35f);
            if (target == _Radio) distance *= 1.2f;
            _InspectionCamera.transform.SetPositionAndRotation(centre + front * distance, Quaternion.LookRotation(-front, Vector3.up));
            var lens = _InspectionCamera.Lens; lens.FieldOfView = 40; lens.NearClipPlane = .05f; _InspectionCamera.Lens = lens;
            _InspectionCamera.Priority = 100; _InspectionCamera.gameObject.SetActive(true);
        }
        void CloseInspection()
        {
            _RadioOpen = false;
            if (_InspectionCamera) _InspectionCamera.gameObject.SetActive(false);
        }
        void OnDialogueEnded()
        {
            _Pressed = false; _PressedTarget = null;
            _SuppressClickUntilRelease = _Click != null && _Click.IsPressed();
            CloseInspection();
        }
        public bool AcceptProp(HotelPlayer player, int prop)
        {
            if (!_Lobby || !_Lobby.AudioReady || !player || player.ControlMode != HotelControlMode.Lobby || prop < 0 || prop > 2 || Time.unscaledTime < _NextPropTime) return false;
            _NextPropTime = Time.unscaledTime + .15f;
            return true;
        }
        public void PlayProp(int prop)
        {
            if (!_MusicReady) return;
            if (prop == 0)
            {
                if (_RadioPlayback) _RadioPlayback.PlayAdvertisement();
                if (_Dial) _Dial.transform.Rotate(0, 0, 30, Space.Self);
            }
            if (prop == 1 && _BellAudio && _BellAudio.clip) _BellAudio.PlayOneShot(_BellAudio.clip);
            if (prop == 2)
            {
                if (_ClickAudio && _ClickAudio.clip) _ClickAudio.PlayOneShot(_ClickAudio.clip);
            }
        }
        void Apply(bool visible, Texture2D texture)
        {
            if (_Initialized && _Visible == visible && _Applied == texture) return;
            _Initialized = true; _Visible = visible; _Applied = texture;
            Cursor.visible = visible;
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.SetCursor(texture, texture == _InspectCursor ? _Hotspot : Vector2.zero, CursorMode.Auto);
        }
        void OnDisable()
        {
            DialogueManager.OnDialogueEndEvent -= OnDialogueEnded;
            CloseInspection(); _MusicReady = false;
            if (_RadioPlayback) _RadioPlayback.SetReady(false);
            if (_RadioAudio) _RadioAudio.Stop();
            _Click.Disable(); _Pressed = Hovering = false;
            Apply(false, null); _Initialized = false;
        }
        void OnDestroy() { _Click?.Dispose(); if (Instance == this) Instance = null; }
    }
}
