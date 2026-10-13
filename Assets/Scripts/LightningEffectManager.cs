using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// One strike clock. Every visual is authored in a scene/prefab before Play.
public sealed class LightningEffectManager : MonoBehaviour
{
    [Serializable] public sealed class WindowPass
    {
        public SpriteRenderer Window;
        public MeshRenderer Pass;
    }
    [SerializeField] SpriteRenderer[] _Windows;
    [SerializeField] Transform[] _ReceiverRoots; // Retained only for Editor migration.
    [SerializeField] WindowPass[] _AuthoredWindowPasses = Array.Empty<WindowPass>();
    [SerializeField] Light[] _AuthoredRoomLights = Array.Empty<Light>();
    [SerializeField] Canvas _FlashCanvas;
    [SerializeField] SpriteRenderer _OpponentShadow;
    [SerializeField] SpriteRenderer _OpponentRenderer;
    [SerializeField] Vector3 _OpponentShadowOffset = new Vector3(.16f, .08f, .08f);
    [SerializeField, Range(.5f, 2)] float _OpponentShadowScaleMultiplier = 1.12f;
    [SerializeField] Vector3 _LightDirection = new Vector3(0, -.45f, 1);
    [SerializeField, ColorUsage(false, true)] Color _LightColor = new Color(.72f, .83f, 1);
    [SerializeField, Min(0)] float _Intensity = 1.8f;
    [SerializeField, Min(.01f)] float _FadeSeconds = .7f;
    [SerializeField] bool _WindowsAreMasksOnly = true;
    [Header("Ambient storm")]
    [SerializeField] bool _RandomLightning = true;
    [SerializeField] bool _StartAmbientOnEnable;
    [SerializeField] Vector2 _RandomIntervalSeconds = new Vector2(8, 22);

    readonly List<SpriteRenderer> _MaskWindows = new List<SpriteRenderer>();
    readonly List<bool> _WindowVisibility = new List<bool>();
    MaterialPropertyBlock[] _PassBlocks, _OriginalBlocks;
    bool[] _PassEnabled, _LightEnabled;
    float[] _LightIntensity;
    Color[] _LightColors;
    bool _FlashEnabled, _ShadowEnabled, _Prepared, _AmbientStarted;
    Sprite _ShadowSprite;
    Vector3 _ShadowPosition, _ShadowScale;
    Color _ShadowColor;
    Quaternion _ShadowRotation;
    bool _ShadowFlipX, _ShadowFlipY;
    Coroutine _Strike;
    float _NextLightning;
    long _StrikeId;
    public bool Playing => _Strike != null;
    public IReadOnlyList<Transform> AuthoringReceiverRoots => _ReceiverRoots;
    public bool AmbientActive => _AmbientStarted && isActiveAndEnabled;
    public event Action<bool> AmbientStateChanged;
    public event Action<long> StrikeStarted;

    public bool ValidateAuthoredBindings(out string error)
    {
        error = null;
        if (!_FlashCanvas) { error = "Assign the authored lightning white-frame Canvas."; return false; }
        int passes = _AuthoredWindowPasses?.Length ?? 0;
        int lights = _AuthoredRoomLights?.Length ?? 0;
        if (passes == 0 && lights == 0) { error = "Author window receiver passes or room lights before Play."; return false; }
        var unique = new HashSet<MeshRenderer>();
        for (int i = 0; i < passes; i++)
        {
            var binding = _AuthoredWindowPasses[i];
            if (binding == null || !binding.Window || !binding.Window.sprite || !binding.Pass ||
                !binding.Pass.TryGetComponent<MeshFilter>(out var mesh) || !mesh.sharedMesh)
            { error = "Incomplete authored window pass " + i; return false; }
            if (!unique.Add(binding.Pass)) { error = "A receiver pass is bound more than once."; return false; }
            var materials = binding.Pass.sharedMaterials;
            if (materials.Length != mesh.sharedMesh.subMeshCount)
            { error = "Window pass material slots must match receiver submeshes."; return false; }
            foreach (var material in materials)
                if (!material || !material.shader || material.shader.name != "HauntedFish/WindowLightning")
                { error = "Window pass must use the authored WindowLightning material."; return false; }
        }
        var uniqueLights = new HashSet<Light>();
        for (int i = 0; i < lights; i++)
            if (!_AuthoredRoomLights[i] || !uniqueLights.Add(_AuthoredRoomLights[i]))
            { error = "Missing or duplicate authored room light " + i; return false; }
        return true;
    }

    bool Prepare()
    {
        if (_Prepared) return true;
        if (!ValidateAuthoredBindings(out var error)) { Debug.LogError(error, this); return false; }
        int passes = _AuthoredWindowPasses?.Length ?? 0;
        _PassBlocks = new MaterialPropertyBlock[passes];
        _OriginalBlocks = new MaterialPropertyBlock[passes];
        _PassEnabled = new bool[passes];
        for (int i = 0; i < passes; i++)
        {
            var binding = _AuthoredWindowPasses[i];
            _PassBlocks[i] = new MaterialPropertyBlock();
            _OriginalBlocks[i] = new MaterialPropertyBlock();
            binding.Pass.GetPropertyBlock(_OriginalBlocks[i]);
            _PassEnabled[i] = binding.Pass.enabled;
            RememberWindow(binding.Window);
        }
        if (_Windows != null) foreach (var window in _Windows) if (window) RememberWindow(window);
        int lights = _AuthoredRoomLights?.Length ?? 0;
        _LightEnabled = new bool[lights]; _LightIntensity = new float[lights]; _LightColors = new Color[lights];
        for (int i = 0; i < lights; i++)
        { _LightEnabled[i] = _AuthoredRoomLights[i].enabled; _LightIntensity[i] = _AuthoredRoomLights[i].intensity; _LightColors[i] = _AuthoredRoomLights[i].color; }
        _FlashEnabled = _FlashCanvas.enabled;
        if (_OpponentShadow)
        {
            _ShadowEnabled = _OpponentShadow.enabled; _ShadowSprite = _OpponentShadow.sprite;
            _ShadowScale = _OpponentShadow.transform.localScale; _ShadowColor = _OpponentShadow.color;
            _ShadowPosition = _OpponentShadow.transform.position; _ShadowRotation = _OpponentShadow.transform.rotation;
            _ShadowFlipX = _OpponentShadow.flipX; _ShadowFlipY = _OpponentShadow.flipY;
        }
        _Prepared = true;
        StopVisuals();
        return true;
    }
    void RememberWindow(SpriteRenderer window)
    {
        if (_MaskWindows.Contains(window)) return;
        _MaskWindows.Add(window); _WindowVisibility.Add(window.enabled);
    }
    public void SetOpponentRenderer(SpriteRenderer opponent) => _OpponentRenderer = opponent;
    public void Play()
    {
        if (!isActiveAndEnabled || !Prepare()) return;
        StopEffect();
        BeginAmbient();
        _Strike = StartCoroutine(Strike(true));
    }
    public void StartAmbient()
    {
        if (!isActiveAndEnabled || !Prepare() || _AmbientStarted) return;
        BeginAmbient(); UseWindowMasks(); ScheduleLightning();
    }
    void BeginAmbient()
    {
        if (_AmbientStarted) return;
        _AmbientStarted = true; AmbientStateChanged?.Invoke(true);
    }
    void UseWindowMasks()
    { if (_WindowsAreMasksOnly) foreach (var window in _MaskWindows) if (window) window.enabled = false; }
    void Update()
    {
        if (!_AmbientStarted || !_RandomLightning || Playing || Time.unscaledTime < _NextLightning) return;
        _Strike = StartCoroutine(Strike(false));
    }
    void ScheduleLightning()
    {
        float minimum = Mathf.Max(.1f, _RandomIntervalSeconds.x);
        _NextLightning = Time.unscaledTime + UnityEngine.Random.Range(minimum, Mathf.Max(minimum, _RandomIntervalSeconds.y));
    }
    IEnumerator Strike(bool whiteFrame)
    {
        StrikeStarted?.Invoke(++_StrikeId);
        UseWindowMasks();
        if (whiteFrame)
        {
            _FlashCanvas.enabled = true;
            for (int frame = 0; frame < 3; frame++) yield return null;
            _FlashCanvas.enabled = false;
        }
        for (float elapsed = 0; elapsed < _FadeSeconds; elapsed += Time.unscaledDeltaTime)
        {
            float t = elapsed / _FadeSeconds;
            float pulse = Mathf.Exp(-6 * t) + .45f * Mathf.Exp(-Mathf.Pow((t - .3f) / .055f, 2));
            ApplyPulse(_Intensity * pulse);
            yield return null;
        }
        StopVisuals(); _Strike = null; ScheduleLightning();
    }
    void ApplyPulse(float intensity)
    {
        for (int i = 0; i < (_AuthoredWindowPasses?.Length ?? 0); i++)
        {
            var binding = _AuthoredWindowPasses[i];
            if (!binding.Window || !binding.Window.sprite || !binding.Pass) continue;
            var sprite = binding.Window.sprite; var bounds = sprite.bounds; var rect = sprite.textureRect;
            var block = _PassBlocks[i]; block.Clear();
            block.SetTexture("_WindowMask", sprite.texture);
            block.SetVector("_WindowBounds", new Vector4(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y));
            block.SetVector("_TextureRect", new Vector4(rect.x / sprite.texture.width, rect.y / sprite.texture.height,
                rect.width / sprite.texture.width, rect.height / sprite.texture.height));
            var worldToWindow = binding.Window.transform.worldToLocalMatrix;
            var direction = _LightDirection.sqrMagnitude > .0001f ? _LightDirection.normalized : Vector3.forward;
            block.SetMatrix("_WorldToWindow", worldToWindow);
            block.SetVector("_Ray", worldToWindow.MultiplyVector(direction));
            block.SetVector("_LightDirection", direction); block.SetColor("_LightColor", _LightColor);
            block.SetVector("_Flip", new Vector4(binding.Window.flipX ? 1 : 0, binding.Window.flipY ? 1 : 0, 0, 0));
            block.SetFloat("_Intensity", intensity);
            binding.Pass.SetPropertyBlock(block); binding.Pass.enabled = intensity > 0;
        }
        if (_AuthoredRoomLights != null) foreach (var light in _AuthoredRoomLights) if (light)
        { light.color = _LightColor; light.intensity = intensity; light.enabled = intensity > 0; }
        if (_OpponentShadow)
        {
            bool show = intensity > 0 && _OpponentRenderer && _OpponentRenderer.sprite && _OpponentRenderer.enabled &&
                _OpponentRenderer.gameObject.activeInHierarchy;
            if (show)
            {
                _OpponentShadow.sprite = _OpponentRenderer.sprite;
                var parentScale = _OpponentShadow.transform.parent ? _OpponentShadow.transform.parent.lossyScale : Vector3.one;
                var sourceScale = _OpponentRenderer.transform.lossyScale * _OpponentShadowScaleMultiplier;
                _OpponentShadow.transform.localScale = new Vector3(sourceScale.x / Mathf.Max(.0001f, Mathf.Abs(parentScale.x)), sourceScale.y / Mathf.Max(.0001f, Mathf.Abs(parentScale.y)), sourceScale.z / Mathf.Max(.0001f, Mathf.Abs(parentScale.z)));
                var tint = _ShadowColor; tint.a *= Mathf.Clamp01(intensity / Mathf.Max(.0001f, _Intensity)); _OpponentShadow.color = tint;
                _OpponentShadow.flipX = _OpponentRenderer.flipX; _OpponentShadow.flipY = _OpponentRenderer.flipY;
                _OpponentShadow.transform.SetPositionAndRotation(_OpponentRenderer.transform.position + _OpponentShadowOffset,
                    _OpponentRenderer.transform.rotation);
            }
            _OpponentShadow.enabled = show;
        }
    }
    void StopVisuals()
    {
        if (_FlashCanvas) _FlashCanvas.enabled = false;
        if (_AuthoredWindowPasses != null) foreach (var binding in _AuthoredWindowPasses)
            if (binding != null && binding.Pass) binding.Pass.enabled = false;
        if (_AuthoredRoomLights != null) foreach (var light in _AuthoredRoomLights) if (light)
        { light.enabled = false; light.intensity = 0; }
        if (_OpponentShadow) _OpponentShadow.enabled = false;
    }
    void StopEffect()
    {
        if (_Strike != null) StopCoroutine(_Strike);
        _Strike = null; StopVisuals();
    }
    public void StopAmbient()
    {
        if (!_Prepared && !_AmbientStarted && _Strike == null) return;
        bool notify = _AmbientStarted;
        _AmbientStarted = false; _NextLightning = float.PositiveInfinity;
        StopEffect();
        if (_Prepared)
        {
            for (int i = 0; i < _MaskWindows.Count; i++)
                if (_MaskWindows[i]) _MaskWindows[i].enabled = _WindowVisibility[i];
            for (int i = 0; i < (_AuthoredWindowPasses?.Length ?? 0); i++)
                if (_AuthoredWindowPasses[i].Pass)
                { _AuthoredWindowPasses[i].Pass.SetPropertyBlock(_OriginalBlocks[i]); _AuthoredWindowPasses[i].Pass.enabled = _PassEnabled[i]; }
            for (int i = 0; i < (_AuthoredRoomLights?.Length ?? 0); i++)
                if (_AuthoredRoomLights[i])
                { _AuthoredRoomLights[i].enabled = _LightEnabled[i]; _AuthoredRoomLights[i].intensity = _LightIntensity[i]; _AuthoredRoomLights[i].color = _LightColors[i]; }
            if (_FlashCanvas) _FlashCanvas.enabled = _FlashEnabled;
            if (_OpponentShadow)
            {
                _OpponentShadow.enabled = _ShadowEnabled; _OpponentShadow.sprite = _ShadowSprite;
                _OpponentShadow.transform.localScale = _ShadowScale; _OpponentShadow.color = _ShadowColor;
                _OpponentShadow.flipX = _ShadowFlipX; _OpponentShadow.flipY = _ShadowFlipY;
                _OpponentShadow.transform.SetPositionAndRotation(_ShadowPosition, _ShadowRotation);
            }
        }
        _Prepared = false; _MaskWindows.Clear(); _WindowVisibility.Clear();
        _PassBlocks = _OriginalBlocks = null; _PassEnabled = _LightEnabled = null; _LightIntensity = null; _LightColors = null;
        if (notify) AmbientStateChanged?.Invoke(false);
    }
    void OnEnable() { if (Application.isPlaying && _StartAmbientOnEnable) StartAmbient(); }
    void OnDisable() => StopAmbient();
    void OnDestroy() => StopAmbient();
}
