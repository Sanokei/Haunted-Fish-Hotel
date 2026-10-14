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
            if (_Canvas) _Canvas.enabled = player && player.ControlMode == HotelControlMode.Fish && !player.InBossFight && !player.BossWatching;
            PublishBeam(false, Vector2.zero);
            if (player && (player.InBossFight || player.BossWatching)) return;
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
            PublishBeam(Darkness > 0 && _Canvas && _Canvas.isActiveAndEnabled && _Image && _Image.isActiveAndEnabled,
                new Vector2(mouse.x / Mathf.Max(1, Screen.width), mouse.y / Mathf.Max(1, Screen.height)));
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
