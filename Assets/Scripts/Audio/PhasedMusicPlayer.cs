using HauntedFish.BossFight;
using HauntedFish.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HauntedFish.Audio
{
    // Plays sections cut from GameMusic.mp3. Without an arena it rotates through the
    // calm phases; with one, the boss's fish score picks the matching intensity stage.
    public sealed class PhasedMusicPlayer : MonoBehaviour
    {
        [SerializeField] AudioClip[] _Phases = new AudioClip[0];
        [SerializeField] BossArenaManager _Arena;
        [SerializeField, Range(0, 1)] float _Volume = 0.6f;
        [SerializeField] float _Crossfade = 2.5f;

        static PhasedMusicPlayer _Boss;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            bool boss = scene.name == "BossFight";
            if (!boss && scene.name != "Game") return;
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<PhasedMusicPlayer>(true)) return;
            var go = new GameObject(boss ? "Boss Music" : "Game Music");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.SetActive(false);
            var player = go.AddComponent<PhasedMusicPlayer>();
            player._Phases = boss
                ? new[] { Clip("BossStage1"), Clip("BossStage2"), Clip("BossStage3") }
                : new[] { Clip("GameCalmA"), Clip("GameCalmB") };
            if (boss) player._Arena = Object.FindFirstObjectByType<BossArenaManager>();
            go.SetActive(true);
        }

        static AudioClip Clip(string name) => Resources.Load<AudioClip>("Music/" + name);

        AudioSource _A, _B;
        int _Phase = -1;
        float _Fade = 1;

        void Awake()
        {
            _A = MakeSource();
            _B = MakeSource();
            if (_Arena) _Boss = this;
        }

        void OnDestroy()
        {
            if (_Boss == this) _Boss = null;
        }

        AudioSource MakeSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.volume = 0;
            HotelAudioRouting.RouteMusic(source);
            return source;
        }

        void Update()
        {
            if (_Phases.Length == 0) return;
            int wanted = _Arena ? Mathf.Clamp(_Arena.FishScore, 0, _Phases.Length - 1) : RotatingPhase();
            if (wanted != _Phase) Switch(wanted);

            _Fade = Mathf.MoveTowards(_Fade, 1, Time.unscaledDeltaTime / Mathf.Max(0.01f, _Crossfade));
            // The boss scene loads over Game; the calm score ducks out while it plays.
            float duck = !_Arena && _Boss ? 0 : 1;
            _A.volume = _Volume * _Fade * duck;
            _B.volume = _Volume * (1 - _Fade) * duck;
            if (_B.volume <= 0 && _B.isPlaying) _B.Stop();
        }

        // Calm mode: loop each phase once, then move to the next.
        int RotatingPhase()
        {
            if (_Phase < 0) return 0;
            bool nearEnd = _A.clip && _A.time >= _A.clip.length - _Crossfade;
            return nearEnd && _Fade >= 1 ? (_Phase + 1) % _Phases.Length : _Phase;
        }

        void Switch(int phase)
        {
            _Phase = phase;
            var clip = _Phases[phase];
            if (!clip || clip == _A.clip && _A.isPlaying) return;
            (_A, _B) = (_B, _A);
            _Fade = _B.isPlaying ? 0 : 1;
            _A.clip = clip;
            _A.loop = _Arena || _Phases.Length == 1;
            _A.Play();
        }
    }
}
