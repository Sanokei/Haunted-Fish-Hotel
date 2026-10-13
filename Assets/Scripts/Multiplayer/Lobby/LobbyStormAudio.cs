using Monologue.Dialogue;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // The lightning owner supplies strikes; DSP time schedules audio, never another lightning timer.
    public sealed class LobbyStormAudio : MonoBehaviour
    {
        [SerializeField] LightningEffectManager _Lightning;
        [Header("Dedicated authored audio sources")]
        [SerializeField] AudioSource _RainSource;
        [SerializeField] AudioSource _ThunderSource;
        [SerializeField] AudioSource _ThunderSourceB;
        [SerializeField] AudioClip _RainLoop;
        [SerializeField] AudioClip[] _ThunderClips;
        [Tooltip("Parallel arrays match Medium Roll, Soft Crack, Distant Long, Far Rumble.")]
        [SerializeField] float[] _ThunderOnsetSeconds = { .060f, .060f, .085f, .100f };
        [SerializeField] float[] _ThunderCueGains = { .8f, .7f, 1f, 1f };
        [SerializeField] Vector2[] _ThunderCueDelays = {
            new Vector2(1, 2.2f), new Vector2(.6f, 1.3f),
            new Vector2(2.2f, 3.8f), new Vector2(1.8f, 3.2f) };
        [SerializeField] int[] _ThunderCueWeights = { 3, 2, 3, 3 };
        [Header("Mastered assets retain their own headroom")]
        [SerializeField, Range(0, 1)] float _RainGain = 1f;
        [SerializeField, Range(0, 1)] float _ThunderGain = 1f;
        [Tooltip("Fallback acoustic delay for clips without matching per-cue metadata.")]
        [SerializeField] Vector2 _ThunderDelaySeconds = new Vector2(.5f, 1.8f);
        [SerializeField, Range(0, .2f)] float _ThunderGainVariation = 0;
        [SerializeField, Min(.05f)] float _SchedulingLeadSeconds = .05f;
        [SerializeField, Min(.01f)] float _RainFadeInSeconds = 1.5f;
        [SerializeField, Min(.01f)] float _RainFadeOutSeconds = 2f;
        [Header("Dialogue ducking")]
        [SerializeField, Range(0, 1)] float _RainDuringDialogue = .55f;
        [SerializeField, Range(0, 1)] float _ThunderDuringDialogue = .65f;
        [SerializeField, Min(.01f)] float _GainFadeSeconds = .5f;
        readonly LobbyStormAudioState _State = new LobbyStormAudioState();
        AudioSource[] _Voices = System.Array.Empty<AudioSource>();
        double[] _VoiceEnds = System.Array.Empty<double>();
        float[] _VoiceGains = System.Array.Empty<float>();
        int _LastCue = -1;
        long _LastReceivedStrike = long.MinValue;
        float _RainEnvelope, _FadeOutElapsed, _FadeOutVolume;
        bool _FadingOut, _RainPlaying, _IntroductionTalking;
        public bool Running => _State.Running;
        public int PendingThunder => _State.PendingCount;

        void OnEnable()
        {
            if (!_Lightning || !_RainSource || !_ThunderSource || _RainSource == _ThunderSource ||
                (_ThunderSourceB && (_ThunderSourceB == _ThunderSource || _ThunderSourceB == _RainSource)))
            {
                Debug.LogError("Assign the lightning owner and distinct dedicated storm audio sources.", this);
                return;
            }
            _Voices = _ThunderSourceB ? new[] { _ThunderSource, _ThunderSourceB } : new[] { _ThunderSource };
            _VoiceEnds = new double[_Voices.Length]; _VoiceGains = new float[_Voices.Length];
            _RainSource.playOnAwake = false; _RainSource.loop = true; _RainSource.volume = 0;
            float pitch = Mathf.Clamp(_ThunderSource.pitch, .1f, 3f);
            foreach (var voice in _Voices)
            {
                voice.pitch = pitch; voice.playOnAwake = false; voice.loop = false; voice.volume = 0;
            }
            _Lightning.AmbientStateChanged += AmbientChanged;
            _Lightning.StrikeStarted += Strike;
            DialogueManager.OnDialogueStartEvent += Duck;
            DialogueManager.OnDialogueEndEvent += Unduck;
            _State.SetDialogue(DialogueManager.Instance && DialogueManager.Instance.ActiveDialoguePanel);
            AmbientChanged(_Lightning.AmbientActive);
        }

        void AmbientChanged(bool active)
        {
            if (!active)
            {
                _FadeOutVolume = _RainSource ? _RainSource.volume : 0;
                _FadeOutElapsed = 0; _FadingOut = _RainPlaying;
                StopThunder();
                _State.Stop();
                return;
            }
            if (_State.Running) return;
            _FadingOut = false;
            _State.Start();
            if (_RainLoop && !_RainPlaying)
            {
                _RainEnvelope = 0;
                _RainSource.clip = _RainLoop;
                _RainSource.Play();
                _RainPlaying = true;
            }
        }

        float Weight(int index) => _ThunderCueWeights != null && index < _ThunderCueWeights.Length
            ? Mathf.Max(0, _ThunderCueWeights[index]) : 1;
        int ChooseCue()
        {
            float total = 0, alternative = 0;
            for (int i = 0; i < _ThunderClips.Length; i++)
                if (_ThunderClips[i]) { total += Weight(i); if (i != _LastCue) alternative += Weight(i); }
            bool excludeLast = alternative > 0;
            if (excludeLast) total = alternative;
            if (total <= 0) return -1;
            float roll = Random.Range(0, total);
            int fallback = -1;
            for (int i = 0; i < _ThunderClips.Length; i++)
            {
                if (!_ThunderClips[i] || (excludeLast && i == _LastCue) || Weight(i) <= 0) continue;
                fallback = i; roll -= Weight(i);
                if (roll < 0) return i;
            }
            return fallback;
        }

        void Strike(long id)
        {
            if (!_State.Running || _ThunderClips == null || _ThunderClips.Length == 0) return;
            if (id <= _LastReceivedStrike) return;
            _LastReceivedStrike = id;
            int index = ChooseCue();
            if (index < 0) return;
            var distances = _ThunderCueDelays != null && index < _ThunderCueDelays.Length
                ? _ThunderCueDelays[index] : _ThunderDelaySeconds;
            float minimum = Mathf.Max(0, distances.x), maximum = Mathf.Max(minimum, distances.y);
            float onset = _ThunderOnsetSeconds != null && index < _ThunderOnsetSeconds.Length
                ? _ThunderOnsetSeconds[index] : 0;
            float gain = _ThunderCueGains != null && index < _ThunderCueGains.Length
                ? Mathf.Clamp(_ThunderCueGains[index], 0, 1) : 1;
            float variation = Mathf.Clamp(_ThunderGainVariation, 0, .2f);
            double delay = LobbyStormAudioState.PlaybackDelay(Random.Range(minimum, maximum), onset, _ThunderSource.pitch);
            _State.Strike(id, AudioSettings.dspTime, delay, index, gain * Random.Range(1 - variation, 1 + variation));
            _LastCue = index;
        }

        void Duck() => _State.SetDialogue(true);
        void Unduck() => _State.SetDialogue(_IntroductionTalking);
        public void SetIntroductionTalking(bool talking)
        {
            _IntroductionTalking = talking;
            _State.SetDialogue(talking || (DialogueManager.Instance && DialogueManager.Instance.ActiveDialoguePanel));
        }

        void Update()
        {
            if (_FadingOut)
            {
                _FadeOutElapsed += Time.unscaledDeltaTime;
                _RainSource.volume = _FadeOutVolume * (1 - Mathf.Clamp01(_FadeOutElapsed / Mathf.Max(.01f, _RainFadeOutSeconds)));
                if (_FadeOutElapsed >= _RainFadeOutSeconds)
                {
                    _RainSource.Stop(); _RainPlaying = false; _FadingOut = false; _RainEnvelope = 0;
                }
            }
            if (!_State.Running) return;
            _State.Advance(Time.unscaledDeltaTime, _RainGain, _ThunderGain,
                _RainDuringDialogue, _ThunderDuringDialogue, _GainFadeSeconds);
            _RainEnvelope = Mathf.MoveTowards(_RainEnvelope, 1, Time.unscaledDeltaTime / Mathf.Max(.01f, _RainFadeInSeconds));
            _RainSource.volume = _State.RainGain * _RainEnvelope;
            double now = AudioSettings.dspTime;
            for (int i = 0; i < _Voices.Length; i++)
            {
                var voice = _Voices[i];
                if (_VoiceEnds[i] <= now && _State.TryTakeNext(out var cue))
                {
                    if (_ThunderClips == null || cue.Clip >= _ThunderClips.Length || !_ThunderClips[cue.Clip]) continue;
                    var clip = _ThunderClips[cue.Clip];
                    double start = System.Math.Max(cue.Due, now + Mathf.Max(.05f, _SchedulingLeadSeconds));
                    _VoiceGains[i] = cue.Gain;
                    voice.clip = clip;
                    voice.PlayScheduled(start);
                    _VoiceEnds[i] = start + clip.length / Mathf.Max(.1f, voice.pitch) + .02;
                }
                voice.volume = _State.ThunderGain * _VoiceGains[i];
            }
        }

        void StopThunder()
        {
            foreach (var voice in _Voices) if (voice) { voice.Stop(); voice.volume = 0; }
            System.Array.Clear(_VoiceEnds, 0, _VoiceEnds.Length);
            _LastCue = -1;
            _LastReceivedStrike = long.MinValue;
        }
        public void StopAudio()
        {
            _State.Stop(); StopThunder();
            _FadingOut = false; _RainPlaying = false; _RainEnvelope = 0;
            if (_RainSource) { _RainSource.Stop(); _RainSource.volume = 0; }
        }
        void OnDisable()
        {
            if (_Lightning)
            {
                _Lightning.AmbientStateChanged -= AmbientChanged;
                _Lightning.StrikeStarted -= Strike;
            }
            DialogueManager.OnDialogueStartEvent -= Duck;
            DialogueManager.OnDialogueEndEvent -= Unduck;
            StopAudio();
        }
    }
}
