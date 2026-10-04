using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed class BubbleSceneTransition : MonoBehaviour
{
    static BubbleSceneTransition instance;
    VideoPlayer player;
    RawImage bubbles;
    Image curtain;
    RenderTexture texture;
    Material material;
    bool busy, failed, ended;

    static BubbleSceneTransition Instance
    {
        get
        {
            if (!instance)
            {
                var root = new GameObject("Bubble Scene Transition");
                DontDestroyOnLoad(root);
                instance = root.AddComponent<BubbleSceneTransition>();
                instance.Initialize();
            }
            return instance;
        }
    }

    public static void Load(string scene)
    {
        if (!Instance.busy)
            Instance.StartCoroutine(Travel(scene));
    }

    public static IEnumerator Travel(string scene, Action covered = null, Action loaded = null)
    {
        var transition = Instance;
        while (transition.busy) yield return null;
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError("Scene missing from build: " + scene);
            yield break;
        }
        transition.busy = true;
        try
        {
            yield return transition.Run(scene, covered, loaded);
        }
        finally
        {
            transition.player.Stop();
            transition.bubbles.enabled = false;
            transition.curtain.gameObject.SetActive(false);
            transition.busy = false;
        }
    }

    void Initialize()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;
        gameObject.AddComponent<GraphicRaycaster>();
        curtain = CreateGraphic<Image>("Loading curtain");
        curtain.color = Color.clear;
        curtain.gameObject.SetActive(false);
        bubbles = CreateGraphic<RawImage>("Keyed bubbles");
        material = new Material(Resources.Load<Shader>("BubbleChromaKey"));
        bubbles.material = material;
        bubbles.raycastTarget = false;
        bubbles.enabled = false;
        texture = new RenderTexture(2560, 1440, 0, RenderTextureFormat.ARGB32);
        texture.Create();
        bubbles.texture = texture;
        player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = false;
        player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = texture;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.clip = Resources.Load<BubbleTransitionSettings>("BubbleTransitionSettings").clip;
        player.errorReceived += (source, message) => { failed = true; Debug.LogWarning(message); };
        player.loopPointReached += source => ended = true;
    }

    T CreateGraphic<T>(string label) where T : Graphic
    {
        var child = new GameObject(label, typeof(RectTransform));
        child.transform.SetParent(transform, false);
        var rect = (RectTransform)child.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return child.AddComponent<T>();
    }

    IEnumerator Run(string scene, Action covered, Action loaded)
    {
        failed = ended = false;
        player.Prepare();
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!player.isPrepared && !failed && Time.realtimeSinceStartup < deadline)
            yield return null;
        bool video = player.isPrepared && !failed;
        curtain.gameObject.SetActive(true);
        if (video)
        {
            // Clear the previous frame to the key color before displaying the texture.
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            GL.Clear(true, true, Color.green);
            RenderTexture.active = previous;
            bubbles.enabled = true;
            player.Play();
        }
        yield return Fade(0, 1, .5f);
        if (video) player.Pause();
        covered?.Invoke();
        yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
        loaded?.Invoke();
        // Let the new scene render before revealing it.
        yield return null;
        if (video && !failed) player.Play();
        yield return Fade(1, 0, .4f);
        deadline = Time.realtimeSinceStartup + 4f;
        while (video && !failed && !ended && Time.realtimeSinceStartup < deadline)
            yield return null;
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            curtain.color = new Color(0, 0, 0, Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (texture) { texture.Release(); Destroy(texture); }
        if (material) Destroy(material);
    }
}
