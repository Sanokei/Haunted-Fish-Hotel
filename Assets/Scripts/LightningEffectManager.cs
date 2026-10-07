using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public sealed class LightningEffectManager : MonoBehaviour
{
    [SerializeField] SpriteRenderer[] _Windows;
    [SerializeField] Transform[] _ReceiverRoots;
    [Tooltip("World-space direction in which lightning travels into the room.")]
    [SerializeField] Vector3 _LightDirection = new Vector3(0, -.45f, 1);
    [SerializeField, ColorUsage(false, true)] Color _LightColor = new Color(.72f, .83f, 1);
    [SerializeField, Min(0)] float _Intensity = 1.8f;
    [SerializeField, Min(.01f)] float _FadeSeconds = .7f;
    [SerializeField] bool _WindowsAreMasksOnly = true;
    [Header("Ambient storm (starts after the introduction)")]
    [SerializeField] bool _RandomLightning = true;
    [SerializeField] Vector2 _RandomIntervalSeconds = new Vector2(8, 22);

    readonly List<Material> _Materials = new List<Material>();
    readonly List<MeshRenderer> _Projections = new List<MeshRenderer>();
    readonly List<SpriteRenderer> _MaskWindows = new List<SpriteRenderer>();
    readonly List<bool> _WindowVisibility = new List<bool>();
    Canvas _Flash;
    Coroutine _Strike;
    bool _Prepared;
    bool _AmbientStarted;
    float _NextLightning;
    public bool Playing => _Strike != null;

    public void Play()
    {
        if (!isActiveAndEnabled || !Prepare()) return;
        StopEffect();
        _AmbientStarted = true;
        _Strike = StartCoroutine(Strike(true));
    }

    public void StartAmbient()
    {
        if (!isActiveAndEnabled || !Prepare()) return;
        _AmbientStarted = true;
        UseWindowMasks();
        ScheduleLightning();
    }

    void UseWindowMasks()
    {
        if (_WindowsAreMasksOnly)
            foreach (var window in _MaskWindows) if (window) window.enabled = false;
    }

    void Update()
    {
        if (!_AmbientStarted || !_RandomLightning || Playing || Time.unscaledTime < _NextLightning) return;
        _Strike = StartCoroutine(Strike(false));
    }

    void ScheduleLightning()
    {
        float minimum = Mathf.Max(.1f, _RandomIntervalSeconds.x);
        float maximum = Mathf.Max(minimum, _RandomIntervalSeconds.y);
        _NextLightning = Time.unscaledTime + Random.Range(minimum, maximum);
    }

    bool Prepare()
    {
        if (_Prepared) return true;
        var shader = Resources.Load<Shader>("WindowLightning");
        if (!shader || _Windows == null || _ReceiverRoots == null)
        {
            Debug.LogError("Assign lightning windows and receiver roots, and include the WindowLightning shader.", this);
            return false;
        }

        var receivers = new HashSet<MeshRenderer>();
        foreach (var root in _ReceiverRoots)
            if (root)
                foreach (var receiver in root.GetComponentsInChildren<MeshRenderer>(true))
                    receivers.Add(receiver);

        foreach (var window in _Windows)
        {
            if (!window || !window.sprite) continue;
            var sprite = window.sprite;
            var material = new Material(shader) { name = "Window lightning projection" };
            var bounds = sprite.bounds;
            var rect = sprite.textureRect;
            material.SetTexture("_WindowMask", sprite.texture);
            material.SetVector("_WindowBounds", new Vector4(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y));
            material.SetVector("_TextureRect", new Vector4(rect.x / sprite.texture.width, rect.y / sprite.texture.height,
                rect.width / sprite.texture.width, rect.height / sprite.texture.height));
            var worldToWindow = window.transform.worldToLocalMatrix;
            material.SetMatrix("_WorldToWindow", worldToWindow);
            var direction = _LightDirection.sqrMagnitude > .0001f ? _LightDirection.normalized : Vector3.forward;
            material.SetVector("_Ray", worldToWindow.MultiplyVector(direction));
            material.SetVector("_LightDirection", direction);
            material.SetColor("_LightColor", _LightColor);
            material.SetVector("_Flip", new Vector4(window.flipX ? 1 : 0, window.flipY ? 1 : 0, 0, 0));
            material.SetFloat("_Intensity", 0);
            _Materials.Add(material);
            _MaskWindows.Add(window);
            _WindowVisibility.Add(window.enabled);

            // A separate additive pass preserves the room's existing materials and lighting.
            foreach (var receiver in receivers)
            {
                if (!receiver.TryGetComponent<MeshFilter>(out var source) || !source.sharedMesh) continue;
                var projection = new GameObject("Window lightning pass", typeof(MeshFilter), typeof(MeshRenderer));
                projection.layer = receiver.gameObject.layer;
                projection.transform.SetParent(receiver.transform, false);
                projection.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                var renderer = projection.GetComponent<MeshRenderer>();
                var materials = new Material[source.sharedMesh.subMeshCount];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.enabled = false;
                _Projections.Add(renderer);
            }
        }
        if (_Materials.Count == 0 || _Projections.Count == 0)
        {
            Debug.LogError("Lightning needs at least one sprite window and one mesh receiver.", this);
            Release();
            return false;
        }

        var flash = new GameObject("Lightning white frame", typeof(RectTransform), typeof(Canvas));
        flash.transform.SetParent(transform, false);
        _Flash = flash.GetComponent<Canvas>();
        _Flash.renderMode = RenderMode.ScreenSpaceOverlay;
        _Flash.sortingOrder = short.MaxValue;
        var imageObject = new GameObject("White", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(flash.transform, false);
        var image = imageObject.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;
        var transformRect = imageObject.GetComponent<RectTransform>();
        transformRect.anchorMin = Vector2.zero;
        transformRect.anchorMax = Vector2.one;
        transformRect.offsetMin = transformRect.offsetMax = Vector2.zero;
        _Flash.enabled = false;
        _Prepared = true;
        return true;
    }

    IEnumerator Strike(bool whiteFrame)
    {
        UseWindowMasks();
        if (whiteFrame)
        {
            _Flash.enabled = true;
            // Count rendered frames, independent of time scale or display frame rate.
            for (int frame = 0; frame < 3; frame++) yield return null;
            _Flash.enabled = false;
        }
        foreach (var projection in _Projections) if (projection) projection.enabled = true;
        for (float elapsed = 0; elapsed < _FadeSeconds; elapsed += Time.unscaledDeltaTime)
        {
            float t = elapsed / _FadeSeconds;
            float pulse = Mathf.Exp(-6 * t) + .45f * Mathf.Exp(-Mathf.Pow((t - .3f) / .055f, 2));
            foreach (var material in _Materials) material.SetFloat("_Intensity", _Intensity * pulse);
            yield return null;
        }
        StopVisuals();
        _Strike = null;
        ScheduleLightning();
    }

    void StopVisuals()
    {
        if (_Flash) _Flash.enabled = false;
        foreach (var projection in _Projections) if (projection) projection.enabled = false;
        foreach (var material in _Materials) if (material) material.SetFloat("_Intensity", 0);
    }

    void StopEffect()
    {
        if (_Strike != null) StopCoroutine(_Strike);
        _Strike = null;
        StopVisuals();
        for (int i = 0; i < _MaskWindows.Count; i++)
            if (_MaskWindows[i]) _MaskWindows[i].enabled = _WindowVisibility[i];
    }

    void Release()
    {
        StopEffect();
        foreach (var renderer in _Projections) if (renderer) Destroy(renderer.gameObject);
        foreach (var material in _Materials) if (material) Destroy(material);
        if (_Flash) Destroy(_Flash.gameObject);
        _Projections.Clear();
        _Materials.Clear();
        _MaskWindows.Clear();
        _WindowVisibility.Clear();
        _Prepared = false;
    }

    void OnEnable()
    {
        if (!_AmbientStarted) return;
        UseWindowMasks();
        ScheduleLightning();
    }
    void OnDisable() => StopEffect();
    void OnDestroy() => Release();
}
