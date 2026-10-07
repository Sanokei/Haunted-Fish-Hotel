using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ValidateLightning
{
    public static void Run()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Lightning validation").AddComponent<LightningValidationRunner>();
        EditorSceneManager.SaveScene(scene, "Assets/Validation.unity");
        EditorApplication.EnterPlaymode();
    }
}

public sealed class LightningValidationRunner : MonoBehaviour
{
    int _Checks;
    Canvas _ObservedFlash;
    int _WhiteFrames;
    void LateUpdate() { if (_ObservedFlash && _ObservedFlash.enabled) ++_WhiteFrames; }
    static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
    static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Fields).GetValue(target);
    void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        ++_Checks;
    }
    IEnumerator Start()
    {
        var tests = Tests();
        while (true)
        {
            object next;
            try { if (!tests.MoveNext()) break; next = tests.Current; }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); yield break; }
            yield return next;
        }
        Debug.Log($"LIGHTNING VALIDATION PASSED: {_Checks} checks");
        EditorApplication.Exit(0);
    }
    IEnumerator Tests()
    {
        Application.runInBackground = true;
        ShaderUtil.allowAsyncCompilation = false;
        var shader = Resources.Load<Shader>("WindowLightning");
        Check(shader && shader.isSupported && !ShaderUtil.ShaderHasError(shader), "Projection shader compiles");
        var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
        camera.transform.position = new Vector3(0, 10, -8);
        camera.transform.LookAt(new Vector3(0, 0, 4));
        camera.orthographic = true;
        camera.orthographicSize = 7;
        camera.backgroundColor = new Color(.025f, .025f, .025f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = new Vector3(0, -.1f, 3);
        floor.transform.localScale = new Vector3(18, .2f, 18);
        var floorMaterial = new Material(Shader.Find("Unlit/Color"));
        floorMaterial.color = new Color(.07f, .06f, .05f);
        floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;
        var window = new GameObject("Window mask").AddComponent<SpriteRenderer>();
        window.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Window.png");
        window.transform.position = new Vector3(0, 3, -3);
        window.transform.localScale = Vector3.one * .5f;
        var effect = new GameObject("Lightning").AddComponent<LightningEffectManager>();
        Set(effect, "_Windows", new[] { window });
        Set(effect, "_ReceiverRoots", new[] { floor.transform });
        Set(effect, "_FadeSeconds", .7f);
        Set(effect, "_RandomIntervalSeconds", new Vector2(.1f, .1f));
        Time.timeScale = 0;
        effect.Play();
        var flash = Get<Canvas>(effect, "_Flash");
        _ObservedFlash = flash;
        var projections = Get<List<MeshRenderer>>(effect, "_Projections");
        var materials = Get<List<Material>>(effect, "_Materials");
        Check(effect.Playing && flash.enabled, "Completion immediately starts a white overlay");
        Check(!window.enabled, "Window artwork becomes a projection mask");
        bool captured = false;
        while (effect.Playing)
        {
            if (!flash.enabled && !captured && projections[0].enabled)
            {
                Check(materials[0].GetFloat("_Intensity") > 0, "Room projection follows the white flash");
                var image = Capture(camera);
                Debug.Log($"Projection: bounds={window.sprite.bounds}, ray={materials[0].GetVector("_Ray")}, " +
                    $"intensity={materials[0].GetFloat("_Intensity")}, mesh={projections[0].bounds}");
                var direction = new Vector3(0, -.45f, 1).normalized;
                // Opaque center mullion blocks light; an empty upper pane transmits it.
                var blocked = new Vector3(0, 3, -3);
                var transmitted = new Vector3(-1, 4, -3);
                blocked += direction * (-blocked.y / direction.y);
                transmitted += direction * (-transmitted.y / direction.y);
                float darkness = Sample(camera, image, blocked).grayscale;
                float brightness = Sample(camera, image, transmitted).grayscale;
                Check(brightness > darkness + .05f, "Window silhouette casts visible dark shapes within the projected light");
                Destroy(image);
                captured = true;
            }
            yield return null;
        }
        Check(_WhiteFrames == 3, $"White overlay lasts exactly three rendered frames (observed {_WhiteFrames})");
        Check(captured && !flash.enabled && !projections[0].enabled, "Unscaled fade finishes and disables its rendering passes");
        Check(!window.enabled && floor.GetComponent<MeshRenderer>().sharedMaterial == floorMaterial,
            "Mask artwork stays hidden while original room material is preserved");
        float deadline = Time.realtimeSinceStartup + 2;
        while (!effect.Playing && Time.realtimeSinceStartup < deadline) yield return null;
        Check(effect.Playing && !flash.enabled && projections[0].enabled,
            "Ambient lightning starts on its timer and lights the room without a full-screen white overlay");
        effect.Play();
        yield return null;
        effect.enabled = false;
        Check(!effect.Playing && !flash.enabled && window.enabled, "Disabling mid-flash restores authored visibility and cancels the effect");
        Time.timeScale = 1;
    }
    static Color Sample(Camera camera, Texture2D image, Vector3 point)
    {
        var screen = camera.WorldToViewportPoint(point);
        return image.GetPixel(Mathf.Clamp((int)(screen.x * image.width), 0, image.width - 1),
            Mathf.Clamp((int)(screen.y * image.height), 0, image.height - 1));
    }
    static Texture2D Capture(Camera camera)
    {
        var target = new RenderTexture(960, 720, 24);
        camera.targetTexture = target;
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(960, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 960, 720), 0, 0);
        image.Apply();
        var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Output"));
        Directory.CreateDirectory(output);
        File.WriteAllBytes(Path.Combine(output, "window-lightning.png"), image.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = previous;
        Destroy(target);
        return image;
    }
}
