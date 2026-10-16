using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HauntedFish.Multiplayer
{
    public sealed class GameMouseSpotlight : MonoBehaviour
    {
        [Range(.02f, .5f)] public float Radius = .18f;
        [Range(.001f, .3f)] public float Softness = .12f;
        [Range(0, 1)] public float Darkness = .92f;
        Material _Material;
        readonly Vector4[] _Lights = new Vector4[4];
        [SerializeField] Canvas _Canvas;
        [SerializeField] Image _Image;
        [SerializeField] HauntedFish.Presentation.WatchingFrameGallery _WatchingFrames;

        void Awake()
        {
            var shader = Resources.Load<Shader>("GameMouseSpotlight");
            if (!shader)
            {
                Debug.LogError("Missing GameMouseSpotlight shader.", this);
                return;
            }
            _Material = new Material(shader);
            if (_Image) _Image.material = _Material;
        }

        void LateUpdate()
        {
            var player = HotelPlayer.LocalPlayer;
            // A ghost inside a hallway trap sees the fish's darkness and their lamps.
            bool possessing = player && player.ControlMode == HotelControlMode.Ghost && player.ControlledCube >= 0;
            if (_Canvas) _Canvas.enabled = player && (player.ControlMode == HotelControlMode.Fish || possessing) && !player.InBossFight && !player.BossWatching;
            PublishBeam(false, Vector2.zero);
            if (player && (player.InBossFight || player.BossWatching)) return;
            if (possessing) { PresentFishLamps(); return; }
            if (player && player.ControlMode == HotelControlMode.Ghost) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (!_Material) return;
            var mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width, Screen.height) * .5f;
            _Material.SetVector("_Mouse", new Vector4(mouse.x / Mathf.Max(1, Screen.width), mouse.y / Mathf.Max(1, Screen.height), 0, 0));
            _Material.SetFloat("_Aspect", Screen.width / (float)Mathf.Max(1, Screen.height));
            _Material.SetFloat("_Radius", Radius);
            _Material.SetFloat("_Softness", Softness);
            _Material.SetFloat("_Darkness", Darkness);
            _Material.SetFloat("_LightCount", 0);
            PublishBeam(Darkness > 0 && _Canvas && _Canvas.isActiveAndEnabled && _Image && _Image.isActiveAndEnabled,
                new Vector2(mouse.x / Mathf.Max(1, Screen.width), mouse.y / Mathf.Max(1, Screen.height)));
        }

        void PresentFishLamps()
        {
            if (!_Material) return;
            var view = HotelViewCamera.Current;
            int count = 0;
            var players = HotelPlayer.ActivePlayers;
            for (int i = 0; i < players.Count && count < _Lights.Length; i++)
            {
                var fish = players[i];
                if (!view || !fish.ControlsReady || fish.ControlMode != HotelControlMode.Fish || fish.IsDead) continue;
                var screen = view.WorldToViewportPoint(fish.LampWorldPosition);
                if (screen.z <= 0) continue;
                _Lights[count++] = new Vector4(screen.x, screen.y, 0, 0);
            }
            _Material.SetVector("_Mouse", new Vector4(0, 0, 1, 0));
            _Material.SetVectorArray("_Lights", _Lights);
            _Material.SetFloat("_LightCount", count);
            _Material.SetFloat("_Aspect", Screen.width / (float)Mathf.Max(1, Screen.height));
            _Material.SetFloat("_Radius", Radius);
            _Material.SetFloat("_Softness", Softness);
            _Material.SetFloat("_Darkness", Darkness);
        }

        void PublishBeam(bool active, Vector2 center)
        {
            if (_WatchingFrames) _WatchingFrames.SetFlashlightBeam(active, center, Radius, Softness,
                Screen.width / (float)Mathf.Max(1, Screen.height));
        }

        void OnEnable() { if (_Canvas) _Canvas.enabled = true; }
        void OnDisable() { if (_Canvas) _Canvas.enabled = false; PublishBeam(false, Vector2.zero); }
        void OnDestroy() { if (_Material) Destroy(_Material); }
    }
}
