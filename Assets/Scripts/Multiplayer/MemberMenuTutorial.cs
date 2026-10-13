using System.Text;
using HauntedFish.Tutorials;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

namespace HauntedFish.Multiplayer
{
    // Shared content belongs to the recorder; this component owns only authored presentation.
    public sealed class MemberMenuTutorial : MonoBehaviour
    {
        [SerializeField] TutorialRoleContent _FishContent, _GhostContent;
        [SerializeField] Button _Fish, _Ghost, _Playback, _Replay;
        [SerializeField] TMP_Text _Title, _Instructions, _Status, _PlaybackLabel;
        [SerializeField] ScrollRect _Scroll;
        [SerializeField] RawImage _Surface;
        [SerializeField] VideoPlayer _Video;
        [SerializeField] AudioSource _Audio;
        [SerializeField] Image _GhostPreview;
        bool _Initialized, _Opened, _GhostSelected;
        public bool HasAuthoredBindings => _Fish && _Ghost && _Playback && _Replay && _Title &&
            _Instructions && _Status && _PlaybackLabel && _Scroll && _Surface && _Video && _Audio && _GhostPreview;

        public bool Initialize()
        {
            if (_Initialized) return true;
            if (!HasAuthoredBindings) { Debug.LogError("Assign the authored How To controls and video/audio presentation.", this); return false; }
            _Fish.onClick.AddListener(ShowFish); _Ghost.onClick.AddListener(ShowGhost);
            _Playback.onClick.AddListener(TogglePlayback); _Replay.onClick.AddListener(Replay);
            _Video.prepareCompleted += Prepared; _Video.errorReceived += Failed;
            _Video.loopPointReached += Finished;
            _Video.playOnAwake = false;
            HotelAudioRouting.RouteEffects(_Audio);
            _Initialized = true;
            return true;
        }
        public void Open()
        {
            if (!Initialize()) return;
            _Opened = true;
            if (!_FishContent) _FishContent = Resources.Load<TutorialRoleContent>("Tutorials/FishTutorialContent");
            if (!_GhostContent) _GhostContent = Resources.Load<TutorialRoleContent>("Tutorials/GhostTutorialContent");
            ShowFish();
        }
        public void Close() { _Opened = false; if (_Video) _Video.Stop(); if (_Audio) _Audio.Stop(); }
        void ShowFish() => ShowRole(false);
        void ShowGhost() => ShowRole(true);
        void ShowRole(bool ghost)
        {
            if (!_Opened) return;
            _Video.Stop(); _Audio.Stop(); _Surface.enabled = false;
            _GhostSelected = ghost; _GhostPreview.enabled = ghost;
            _Fish.interactable = ghost; _Ghost.interactable = !ghost;
            var content = ghost ? _GhostContent : _FishContent;
            _Title.text = content && !string.IsNullOrEmpty(content.Title) ? content.Title : (ghost ? "PLAY AS THE GHOST" : "PLAY AS A FISH");
            _Instructions.text = content ? FormatInstructions(content) : "Tutorial instructions are unavailable.";
            Canvas.ForceUpdateCanvases(); _Scroll.verticalNormalizedPosition = 1;
            _Video.clip = content ? content.Recording : null;
            _Video.isLooping = content && content.Loop;
            var hasAudio = content && content.HasAudio;
            _Audio.mute = !hasAudio;
            _Video.audioOutputMode = hasAudio ? VideoAudioOutputMode.AudioSource : VideoAudioOutputMode.None;
            _Playback.interactable = _Replay.interactable = false;
            _PlaybackLabel.text = "Play";
            _Status.text = _Video.clip ? "Loading video..." : "Video unavailable; use the instructions.";
            if (_Video.clip)
            {
                _Video.controlledAudioTrackCount = hasAudio ? (ushort)Mathf.Min(1, _Video.clip.audioTrackCount) : (ushort)0;
                if (_Video.controlledAudioTrackCount > 0) { _Video.EnableAudioTrack(0, true); _Video.SetTargetAudioSource(0, _Audio); }
                _Video.Prepare();
            }
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject((ghost ? _Fish : _Ghost).gameObject);
        }
        static string FormatInstructions(TutorialRoleContent content)
        {
            var text = new StringBuilder();
            if (content.Instructions != null)
                foreach (var section in content.Instructions)
                {
                    if (section == null || (string.IsNullOrEmpty(section.Heading) && string.IsNullOrEmpty(section.Body))) continue;
                    if (text.Length > 0) text.Append("\n\n");
                    if (!string.IsNullOrEmpty(section.Heading)) text.Append("<size=26><b>").Append(section.Heading).Append("</b></size>\n");
                    text.Append(section.Body);
                }
            return text.Length > 0 ? text.ToString() : content.Summary;
        }
        void Prepared(VideoPlayer player)
        {
            if (!_Opened || !gameObject.activeInHierarchy) return;
            _Surface.enabled = true; _GhostPreview.enabled = false; _Playback.interactable = _Replay.interactable = true;
            player.Play(); _PlaybackLabel.text = "Pause"; _Status.text = "Scroll to read all instructions.";
        }
        void Failed(VideoPlayer player, string message)
        {
            if (!_Opened) return;
            player.Stop(); _Surface.enabled = false; _GhostPreview.enabled = _GhostSelected; _Playback.interactable = _Replay.interactable = false;
            _Status.text = "Video unavailable; use the instructions.";
            Debug.LogWarning("How To video could not be played: " + message, this);
        }
        void Finished(VideoPlayer player) { if (_Opened && !player.isLooping) _PlaybackLabel.text = "Play"; }
        void TogglePlayback()
        {
            if (!_Opened || !_Video.isPrepared) return;
            if (_Video.isPlaying) { _Video.Pause(); _PlaybackLabel.text = "Play"; }
            else { _Video.Play(); _PlaybackLabel.text = "Pause"; }
        }
        void Replay()
        {
            if (!_Opened || !_Video.isPrepared) return;
            _Video.time = 0; _Video.Play(); _PlaybackLabel.text = "Pause";
        }
        public void Dispose()
        {
            Close();
            if (!_Initialized) return;
            _Fish.onClick.RemoveListener(ShowFish); _Ghost.onClick.RemoveListener(ShowGhost);
            _Playback.onClick.RemoveListener(TogglePlayback); _Replay.onClick.RemoveListener(Replay);
            _Video.prepareCompleted -= Prepared; _Video.errorReceived -= Failed; _Video.loopPointReached -= Finished;
            _Initialized = false;
        }
        void OnDisable() => Close();
        void OnDestroy() => Dispose();
    }
}
