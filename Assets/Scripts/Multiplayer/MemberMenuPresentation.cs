using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HauntedFish.Multiplayer
{
    // All visual objects, graphics and controls are authored in MemberMenu.prefab.
    public sealed class MemberMenuPresentation : MonoBehaviour
    {
        [SerializeField] GameObject _Root, _Roster, _Settings, _HowTo;
        [SerializeField] MemberMenuTutorial _Tutorial;
        [SerializeField] Canvas _Canvas;
        [SerializeField] Image _Blocker;
        [SerializeField] CanvasGroup _ModalGroup;
        [SerializeField] Button _Resume, _RoomTab, _SettingsTab, _HowToTab, _Apply, _Cancel, _DisplayChange;
        [SerializeField] Slider _Volume, _MusicVolume, _EffectsVolume;
        [SerializeField] TMP_Text _VolumeLabel, _MusicVolumeLabel, _EffectsVolumeLabel, _DisplayLabel, _Status, _Heading;
        readonly MemberMenuSettingsDraft _Draft = new MemberMenuSettingsDraft();
        readonly MemberMenuNavigation _Tabs = new MemberMenuNavigation();
        bool _Initialized;
        public bool SettingsOpen => _Settings && _Settings.activeSelf;
        public bool HowToOpen => _HowTo && _HowTo.activeSelf;
        public TMP_Text Heading => _Heading;
        public bool HasAuthoredBindings => _Root && _Roster && _Settings && _HowTo && _Tutorial &&
            _Canvas && _Blocker && _ModalGroup && _Resume && _RoomTab && _SettingsTab && _HowToTab &&
            _Apply && _Cancel && _DisplayChange && _Volume && _MusicVolume && _EffectsVolume &&
            _VolumeLabel && _MusicVolumeLabel && _EffectsVolumeLabel && _DisplayLabel && _Status && _Heading &&
            _Tutorial.HasAuthoredBindings;

        public bool Initialize()
        {
            if (_Initialized) return true;
            if (!HasAuthoredBindings || !_Tutorial.Initialize()) { Debug.LogError("MemberMenu.prefab is missing authored presentation bindings.", this); return false; }
            _RoomTab.onClick.AddListener(ShowRoster); _SettingsTab.onClick.AddListener(ShowSettings); _HowToTab.onClick.AddListener(ShowHowTo);
            _Apply.onClick.AddListener(Apply); _Cancel.onClick.AddListener(ShowRoster);
            _DisplayChange.onClick.AddListener(ChangeDisplay);
            _Volume.onValueChanged.AddListener(ChangeVolume); _MusicVolume.onValueChanged.AddListener(ChangeMusic); _EffectsVolume.onValueChanged.AddListener(ChangeEffects);
            _DisplayChange.interactable = !Application.isEditor;
            _Initialized = true; ShowRoster(); return true;
        }
        public void Open()
        {
            if (!Initialize()) return;
            _Canvas.enabled = true;
            _ModalGroup.alpha = 1; _ModalGroup.interactable = true; _ModalGroup.blocksRaycasts = true;
            ShowRoster(); Select(_Resume);
        }
        public void Close()
        {
            _Draft.Cancel(); _Tabs.Close(); if (_Tutorial) _Tutorial.Close();
            if (_Settings) _Settings.SetActive(false); if (_HowTo) _HowTo.SetActive(false);
        }
        public bool Back()
        {
            if (!_Tabs.Back()) return false;
            ShowRoster(); return true;
        }
        void ShowTab(MemberMenuTab tab)
        {
            _Draft.Cancel(); _Tutorial.Close(); _Tabs.Select(tab);
            _Roster.SetActive(tab == MemberMenuTab.Room); _Settings.SetActive(tab == MemberMenuTab.Settings); _HowTo.SetActive(tab == MemberMenuTab.HowTo);
            _RoomTab.interactable = tab != MemberMenuTab.Room; _SettingsTab.interactable = tab != MemberMenuTab.Settings; _HowToTab.interactable = tab != MemberMenuTab.HowTo;
        }
        void ShowRoster() { ShowTab(MemberMenuTab.Room); Select(_SettingsTab); }
        void ShowHowTo() { ShowTab(MemberMenuTab.HowTo); _Tutorial.Open(); }
        void ShowSettings()
        {
            ShowTab(MemberMenuTab.Settings);
            _Draft.Reopen(HotelSettings.MasterVolume, HotelSettings.MusicVolume, HotelSettings.SoundEffectsVolume, HotelSettings.DisplayMode == FullScreenMode.Windowed);
            _Volume.SetValueWithoutNotify(_Draft.Volume); _MusicVolume.SetValueWithoutNotify(_Draft.MusicVolume); _EffectsVolume.SetValueWithoutNotify(_Draft.SoundEffectsVolume);
            ChangeVolume(_Draft.Volume); ChangeMusic(_Draft.MusicVolume); ChangeEffects(_Draft.SoundEffectsVolume); UpdateDisplay();
            _Status.text = Application.isEditor ? "Display mode is available in the standalone game." : "Changes are saved only when you press Apply.";
            Select(_Volume);
        }
        void ChangeVolume(float value) { _Draft.Volume = value; _VolumeLabel.text = "Master volume  " + Mathf.RoundToInt(_Draft.Volume * 100) + "%"; }
        void ChangeMusic(float value) { _Draft.MusicVolume = value; _MusicVolumeLabel.text = "Music  " + Mathf.RoundToInt(_Draft.MusicVolume * 100) + "%"; }
        void ChangeEffects(float value) { _Draft.SoundEffectsVolume = value; _EffectsVolumeLabel.text = "Sound effects  " + Mathf.RoundToInt(_Draft.SoundEffectsVolume * 100) + "%"; }
        void ChangeDisplay() { _Draft.Windowed = !_Draft.Windowed; UpdateDisplay(); }
        void UpdateDisplay() => _DisplayLabel.text = "Display mode  " + (_Draft.Windowed ? "Windowed" : "Borderless fullscreen");
        void Apply()
        {
            _Draft.Apply((volume, music, effects, windowed) => HotelSettings.Save(volume, music, effects,
                Application.isEditor ? HotelSettings.DisplayMode : (windowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow)));
            _Status.text = "Settings saved.";
        }
        static void Select(Selectable selectable) { if (EventSystem.current && selectable.IsActive()) EventSystem.current.SetSelectedGameObject(selectable.gameObject); }
        public void Dispose()
        {
            if (!_Initialized) return;
            _RoomTab.onClick.RemoveListener(ShowRoster); _SettingsTab.onClick.RemoveListener(ShowSettings); _HowToTab.onClick.RemoveListener(ShowHowTo);
            _Apply.onClick.RemoveListener(Apply); _Cancel.onClick.RemoveListener(ShowRoster); _DisplayChange.onClick.RemoveListener(ChangeDisplay);
            _Volume.onValueChanged.RemoveListener(ChangeVolume); _MusicVolume.onValueChanged.RemoveListener(ChangeMusic); _EffectsVolume.onValueChanged.RemoveListener(ChangeEffects);
            _Tutorial.Dispose(); _Initialized = false;
        }
        void OnDestroy() => Dispose();
    }
}
