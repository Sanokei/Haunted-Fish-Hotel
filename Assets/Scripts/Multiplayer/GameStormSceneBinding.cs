using UnityEngine;
namespace HauntedFish.Multiplayer
{
    // Scene lifecycle only. The shared lightning owner is the sole strike clock.
    public sealed class GameStormSceneBinding : MonoBehaviour
    {
        [SerializeField] LightningEffectManager _Lightning;
        [SerializeField] LobbyStormAudio _Audio;
        [SerializeField] GameObject _LightningRoot, _AudioRoot;
        [Tooltip("Enable only after the shared authored-visual controller is imported and validated.")]
        [SerializeField] bool _ActivationApproved;
        readonly GameStormEntryState _Entry = new GameStormEntryState();
        public bool ActivationApproved => _ActivationApproved;
        bool BossWatching => HotelPlayer.LocalPlayer && HotelPlayer.LocalPlayer.BossWatching;
        public void Begin()
        {
            if (!_ActivationApproved || !_Lightning || !_Audio || !_LightningRoot || !_AudioRoot) return;
            if (!_Lightning.ValidateAuthoredBindings(out var error)) { Debug.LogError(error, this); return; }
            if (!_Entry.TryEnter(BossWatching)) return;
            StartOwnedStorm();
        }
        void StartOwnedStorm()
        {
            if (!_ActivationApproved || !_Lightning || !_Audio || !_LightningRoot || !_AudioRoot || BossWatching) return;
            _AudioRoot.SetActive(true);
            _LightningRoot.SetActive(true);
            _Lightning.StartAmbient();
        }
        void LateUpdate()
        {
            // Only an entry deferred during Boss viewing is ours to resume. Normal returns stay Boss-owned.
            if (_Entry.TryResumeDeferredEntry(BossWatching)) StartOwnedStorm();
        }
        public void Stop()
        {
            _Entry.Exit();
            if (_Lightning) _Lightning.StopAmbient();
            if (_LightningRoot) _LightningRoot.SetActive(false);
            if (_Audio) _Audio.StopAudio();
            if (_AudioRoot) _AudioRoot.SetActive(false);
        }
        void OnDisable() => Stop();
    }
}
