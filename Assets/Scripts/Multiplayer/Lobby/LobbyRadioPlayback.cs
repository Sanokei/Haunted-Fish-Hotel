using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // Owns one authored radio voice. Music and ads therefore cannot overlap.
    public sealed class LobbyRadioPlayback : MonoBehaviour
    {
        [SerializeField] AudioSource _Source;
        [SerializeField] AudioClip _Music;
        [Tooltip("Authored advertisements. Null/failed entries are ignored; another valid clip avoids an immediate repeat.")]
        [SerializeField] AudioClip[] _Advertisements = new AudioClip[0];

        bool _Ready, _PlayingAdvertisement, _ObservedPlayback;
        AudioClip _CurrentAdvertisement, _LastAdvertisement;
        int _MusicSample;
        double _StartDeadline;
        public bool PlayingAdvertisement => _PlayingAdvertisement;

        void Awake()
        {
            if (!_Music && _Source) _Music = _Source.clip;
            if (_Source) _Source.playOnAwake = false;
            StopPlayback();
        }

        public void SetReady(bool ready)
        {
            ready &= isActiveAndEnabled;
            if (_Ready == ready) return;
            if (!ready) { StopPlayback(); return; }
            _Ready = true;
            RestoreMusic();
        }

        public bool PlayAdvertisement()
        {
            if (!_Ready || !isActiveAndEnabled || !_Source || !_Source.isActiveAndEnabled) return false;
            if (_PlayingAdvertisement && !RadioAdvertisementSelection.DialPlaysAdvertisement(true, Random.value))
            {
                RestoreMusic();
                return true;
            }
            var clip = RadioAdvertisementSelection.Choose(_Advertisements, _LastAdvertisement, Available, Random.value);
            if (!clip) return false;
            // Capture music once; replacement ads must not overwrite it with an ad position.
            if (!_PlayingAdvertisement && _Source.clip == _Music && _Source.isPlaying) _MusicSample = _Source.timeSamples;
            _Source.Stop();
            _CurrentAdvertisement = _LastAdvertisement = clip;
            _PlayingAdvertisement = true;
            _Source.loop = false;
            _Source.clip = clip;
            _Source.Play();
            _ObservedPlayback = _Source.isPlaying;
            // Allow deferred audio-data startup before deciding playback failed.
            _StartDeadline = Time.realtimeSinceStartupAsDouble + 1;
            return true;
        }

        static bool Available(AudioClip clip) => clip && clip.length > 0 && clip.loadState != AudioDataLoadState.Failed;

        void Update()
        {
            if (!_Ready) return;
            if (!_Source) { _PlayingAdvertisement = false; _CurrentAdvertisement = null; return; }
            if (!_Source.isActiveAndEnabled)
            {
                if (_PlayingAdvertisement) RestoreMusic();
                return;
            }
            if (AudioListener.pause && !_Source.ignoreListenerPause) return;
            if (_PlayingAdvertisement)
            {
                if (_Source.clip != _CurrentAdvertisement || !Available(_CurrentAdvertisement)) { RestoreMusic(); return; }
                if (_Source.isPlaying) { _ObservedPlayback = true; return; }
                if (!_ObservedPlayback && Time.realtimeSinceStartupAsDouble < _StartDeadline) return;
                RestoreMusic();
            }
            else if (!_Source.isPlaying && Available(_Music) && Time.realtimeSinceStartupAsDouble >= _StartDeadline) RestoreMusic();
        }

        void RestoreMusic()
        {
            _PlayingAdvertisement = _ObservedPlayback = false;
            _CurrentAdvertisement = null;
            if (!_Source) return;
            _Source.Stop();
            _Source.clip = _Music;
            _Source.loop = true;
            if (!_Ready || !_Source.isActiveAndEnabled || !Available(_Music)) return;
            _Source.timeSamples = Mathf.Clamp(_MusicSample, 0, Mathf.Max(0, _Music.samples - 1));
            _Source.Play();
            _StartDeadline = Time.realtimeSinceStartupAsDouble + 1;
        }

        void StopPlayback()
        {
            _Ready = false;
            _MusicSample = 0;
            RestoreMusic();
        }
        void OnDisable() => StopPlayback();
        void OnDestroy() => StopPlayback();
    }
}
