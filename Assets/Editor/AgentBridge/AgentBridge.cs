using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HauntedFish.EditorTools
{
    // File-based remote control for an already-open editor, used for scripted visual QA.
    // Drop Temp/AgentBridge/request.json; the result is written to response.json.
    //   {"id":"1","op":"refresh"}
    //   {"id":"2","op":"open","scene":"Assets/Scenes/Lobby.unity"}
    //   {"id":"3","op":"capture","camera":"Main Camera","width":1280,"height":720,"path":"Temp/AgentBridge/shot.png"}
    //   {"id":"4","op":"invoke","type":"Namespace.Class","method":"StaticMethod"}
    //   {"id":"5","op":"play"} / {"op":"stop"} / {"op":"save"}
    //   {"id":"6","op":"gameview","path":"Temp/AgentBridge/game.png"}  (play mode screen capture)
    [InitializeOnLoad]
    static class AgentBridge
    {
        const string Folder = "Temp/AgentBridge";
        static double _NextPoll;

        [Serializable]
        class Request
        {
            public string id, op, scene, camera, path, type, method, arg;
            public int width = 1280, height = 720;
        }

        static AgentBridge()
        {
            Directory.CreateDirectory(Folder);
            EditorApplication.update += Poll;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    File.AppendAllText(System.IO.Path.Combine(Folder, "errors.log"), $"[{DateTime.Now:HH:mm:ss}] {type}: {message}\n{stack}\n");
            };
            Write("ready", "ok", "domain loaded " + DateTime.Now.ToString("HH:mm:ss"));
        }

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _NextPoll) return;
            _NextPoll = EditorApplication.timeSinceStartup + .5;
            var file = System.IO.Path.Combine(Folder, "request.json");
            if (!File.Exists(file)) return;
            Request request;
            try { request = JsonUtility.FromJson<Request>(File.ReadAllText(file)); }
            catch (Exception e) { File.Delete(file); Write("?", "error", e.Message); return; }
            File.Delete(file);
            try { Write(request.id, "ok", Run(request)); }
            catch (Exception e) { Write(request.id, "error", e.ToString()); }
        }

        static string Run(Request r)
        {
            switch (r.op)
            {
                case "refresh":
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    return "refreshed";
                case "open":
                    EditorSceneManager.OpenScene(r.scene, OpenSceneMode.Single);
                    return "opened " + r.scene;
                case "save":
                    EditorSceneManager.SaveOpenScenes();
                    AssetDatabase.SaveAssets();
                    return "saved";
                case "play":
                    EditorApplication.isPlaying = true;
                    return "entering play mode";
                case "stop":
                    EditorApplication.isPlaying = false;
                    return "leaving play mode";
                case "capture":
                    return Capture(r);
                case "gameview":
                    ScreenCapture.CaptureScreenshot(r.path ?? System.IO.Path.Combine(Folder, "game.png"));
                    return "game view capture requested";
                case "invoke":
                    var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(r.type)).FirstOrDefault(t => t != null)
                        ?? throw new ArgumentException("type not found: " + r.type);
                    var method = type.GetMethod(r.method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                        ?? throw new ArgumentException("method not found: " + r.method);
                    var result = method.GetParameters().Length == 1 ? method.Invoke(null, new object[] { r.arg }) : method.Invoke(null, null);
                    return result?.ToString() ?? "invoked";
                default:
                    throw new ArgumentException("unknown op " + r.op);
            }
        }

        static string Capture(Request r)
        {
            var camera = string.IsNullOrEmpty(r.camera) ? Camera.main
                : UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.name == r.camera);
            if (!camera) throw new ArgumentException("camera not found: " + r.camera);
            var target = RenderTexture.GetTemporary(r.width, r.height, 24, RenderTextureFormat.ARGB32);
            var previous = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(r.width, r.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, r.width, r.height), 0, 0);
                image.Apply();
                var path = r.path ?? System.IO.Path.Combine(Folder, "shot.png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
                return "captured " + path;
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        static void Write(string id, string status, string message) =>
            File.WriteAllText(System.IO.Path.Combine(Folder, "response.json"),
                JsonUtility.ToJson(new Response { id = id, status = status, message = message, time = DateTime.Now.ToString("HH:mm:ss") }));

        [Serializable] class Response { public string id, status, message, time; }
    }
}
