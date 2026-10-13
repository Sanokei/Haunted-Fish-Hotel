using UnityEngine;

namespace HauntedFish.Multiplayer
{
    public sealed class HotelPlayerAudio : MonoBehaviour
    {
        [SerializeField] AudioClip[] _Steps;
        [SerializeField] AudioClip _Jump, _Interact;
        AudioSource _Source;
        float _NextStep;
        int _Step;
        void Awake()
        {
            _Source = gameObject.AddComponent<AudioSource>();
            HotelAudioRouting.RouteEffects(_Source);
            _Source.playOnAwake = false; _Source.spatialBlend = 1; _Source.dopplerLevel = 0;
            _Source.minDistance = 1; _Source.maxDistance = 18; _Source.rolloffMode = AudioRolloffMode.Linear;
        }
        public void Present(bool walking, bool running)
        {
            if (!walking) { _NextStep = 0; return; }
            if (Time.unscaledTime < _NextStep || _Steps == null || _Steps.Length == 0) return;
            _NextStep = Time.unscaledTime + (running ? .26f : .4f);
            var clip = _Steps[_Step++ % _Steps.Length];
            if (clip) _Source.PlayOneShot(clip, .3f);
        }
        public void PlayEvent(int kind)
        {
            var clip = kind == 0 ? _Jump : _Interact;
            if (clip) _Source.PlayOneShot(clip, kind == 0 ? .55f : .4f);
        }
        void OnDisable() { if (_Source) _Source.Stop(); _NextStep = 0; }
    }
}
