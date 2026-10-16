using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HauntedFish.EditorTools
{
    // Command-line entry points for the closed project:
    //   Unity.exe -batchmode -projectPath . -executeMethod HauntedFish.EditorTools.BatchQA.Capture
    //     -qaScene Assets/Scenes/Lobby.unity -qaCamera "Main Camera" -qaOut Logs/QA/lobby.png -logFile - -quit
    // Output goes under Logs/QA: Unity deletes Temp on quit.
    // -qaCamera may repeat as a comma list; each camera writes <out>-<camera>.png.
    // Omit -nographics, rendering needs a graphics device.
    public static class BatchQA
    {
        // Relative -qaOut paths resolve against the project folder, not the editor's working directory.
        static string ProjectPath(string path) =>
            System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), path));

        static string[] _ArgsOverride;
        // Bridge entry: runs a batch method with tab-separated "-flag	value" pairs instead of the command line.
        public static string WithArgs(string arg)
        {
            var parts = arg.Split('	');
            _ArgsOverride = parts.Skip(1).ToArray();
            try { typeof(BatchQA).GetMethod(parts[0]).Invoke(null, null); return "ran " + parts[0]; }
            finally { _ArgsOverride = null; }
        }
        static string Arg(string name, string fallback = null)
        {
            var args = _ArgsOverride ?? Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        public static void Capture()
        {
            var scene = Arg("-qaScene") ?? throw new ArgumentException("-qaScene required");
            var output = ProjectPath(Arg("-qaOut", "Logs/QA/shot.png"));
            int width = int.Parse(Arg("-qaWidth", "1280")), height = int.Parse(Arg("-qaHeight", "720"));
            EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var names = Arg("-qaCamera");
            var chosen = string.IsNullOrEmpty(names) ? cameras.Where(c => c.isActiveAndEnabled).ToArray()
                : names.Split(',').Select(n => cameras.FirstOrDefault(c => c.name == n.Trim())).Where(c => c).ToArray();
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));
            // Optional framing override: -qaCameraX 48 moves along the hallway, -qaCameraPos "x,y,z" sets it outright.
            var moveX = Arg("-qaCameraX");
            var setPos = Arg("-qaCameraPos");
            var setRot = Arg("-qaCameraRot");
            var activate = Arg("-qaActivate");
            if (activate != null)
                foreach (var name in activate.Split(';'))
                    foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                        if (root.name == name) root.SetActive(true);
            foreach (var camera in chosen)
            {
                if (moveX != null) camera.transform.position = new Vector3(float.Parse(moveX), camera.transform.position.y, camera.transform.position.z);
                if (setPos != null)
                {
                    var p = setPos.Split(',').Select(float.Parse).ToArray();
                    camera.transform.position = new Vector3(p[0], p[1], p[2]);
                }
                if (setRot != null)
                {
                    var r = setRot.Split(',').Select(float.Parse).ToArray();
                    camera.transform.rotation = Quaternion.Euler(r[0], r[1], r[2]);
                }
                if (Arg("-qaFov") != null) camera.fieldOfView = float.Parse(Arg("-qaFov"));
                var path = chosen.Length == 1 ? output : System.IO.Path.Combine(System.IO.Path.GetDirectoryName(output),
                    System.IO.Path.GetFileNameWithoutExtension(output) + "-" + camera.name.Replace(' ', '_') + ".png");
                Render(camera, width, height, path);
                Debug.Log("[BatchQA] captured " + camera.name + " -> " + path);
            }
            Debug.Log("[BatchQA] cameras in scene: " + string.Join(", ", cameras.Select(c => c.name + (c.isActiveAndEnabled ? "" : " (off)"))));
        }

        // Prints a scene's hierarchy (name, active, world position) for layout work.
        public static void Dump()
        {
            var scene = Arg("-qaScene") ?? throw new ArgumentException("-qaScene required");
            var filter = Arg("-qaFilter", "");
            bool prefab = scene.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
            var prefabRoot = prefab ? PrefabUtility.LoadPrefabContents(scene) : null;
            var roots = prefab ? new[] { prefabRoot } : EditorSceneManager.OpenScene(scene, OpenSceneMode.Single).GetRootGameObjects();
            var output = ProjectPath(Arg("-qaOut", "Logs/QA/hierarchy.txt"));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));
            using var writer = new StreamWriter(output);
            foreach (var root in roots)
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var path = AnimationUtility.CalculateTransformPath(t, null);
                    if (filter.Length > 0 && path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var renderer = t.GetComponent<Renderer>();
                    var rect = t as RectTransform;
                    var image = t.GetComponent<UnityEngine.UI.Graphic>();
                    writer.WriteLine($"{path}\t{(t.gameObject.activeInHierarchy ? "on" : "off")}\t{t.position.x:F2},{t.position.y:F2},{t.position.z:F2}" +
                        (renderer ? $"\tbounds {renderer.bounds.min.x:F2}..{renderer.bounds.max.x:F2} y {renderer.bounds.min.y:F2}..{renderer.bounds.max.y:F2}" : ""));
                }
            if (prefabRoot) PrefabUtility.UnloadPrefabContents(prefabRoot);
            Debug.Log("[BatchQA] hierarchy -> " + output);
        }


        // Edits a scene or prefab by hierarchy path and saves it:
        //   -qaAsset Assets/Scenes/Game.unity -qaDelete "Root/Child;Root/Other" -qaDisable "Root/X"
        public static void Edit()
        {
            var asset = Arg("-qaAsset") ?? throw new ArgumentException("-qaAsset required");
            bool prefab = asset.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
            GameObject prefabRoot = null;
            UnityEngine.SceneManagement.Scene scene = default;
            if (prefab) prefabRoot = PrefabUtility.LoadPrefabContents(asset);
            else scene = EditorSceneManager.OpenScene(asset, OpenSceneMode.Single);
            Transform Find(string path)
            {
                var roots = prefab ? new[] { prefabRoot } : scene.GetRootGameObjects();
                foreach (var root in roots)
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        if (AnimationUtility.CalculateTransformPath(t, null) == path) return t;
                throw new ArgumentException("not found: " + path);
            }
            foreach (var path in (Arg("-qaDelete", "")).Split(';').Where(p => p.Length > 0))
            {
                UnityEngine.Object.DestroyImmediate(Find(path).gameObject);
                Debug.Log("[BatchQA] deleted " + path);
            }
            foreach (var path in (Arg("-qaDisable", "")).Split(';').Where(p => p.Length > 0))
            {
                Find(path).gameObject.SetActive(false);
                Debug.Log("[BatchQA] disabled " + path);
            }
            if (prefab) { PrefabUtility.SaveAsPrefabAsset(prefabRoot, asset); PrefabUtility.UnloadPrefabContents(prefabRoot); }
            else EditorSceneManager.SaveScene(scene);
            Debug.Log("[BatchQA] saved " + asset);
        }


        // Renders a UI prefab with chosen objects switched on (their parents too):
        //   -qaPrefab Assets/Resources/LobbyIntroductionUI.prefab -qaShow "A/B;A/C" -qaHide "A/D" -qaOut Logs/QA/ui.png
        public static void CaptureUI()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Arg("-qaPrefab") ?? throw new ArgumentException("-qaPrefab required"));
            var output = ProjectPath(Arg("-qaOut", "Logs/QA/ui.png"));
            int width = int.Parse(Arg("-qaWidth", "1600")), height = int.Parse(Arg("-qaHeight", "900"));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("UI camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.magenta;
            camera.orthographic = true;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            foreach (var canvas in instance.GetComponentsInChildren<Canvas>(true))
                if (canvas.isRootCanvas && canvas.renderMode != RenderMode.WorldSpace) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 5; }
            Transform Find(string path)
            {
                foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                    if (AnimationUtility.CalculateTransformPath(t, instance.transform) == path) return t;
                throw new ArgumentException("not found: " + path);
            }
            foreach (var path in Arg("-qaShow", "").Split(';').Where(p => p.Length > 0))
                for (var t = Find(path); t && t != instance.transform; t = t.parent) t.gameObject.SetActive(true);
            foreach (var path in Arg("-qaHide", "").Split(';').Where(p => p.Length > 0)) Find(path).gameObject.SetActive(false);
            // -qaOffset "path=x,y" slides a RectTransform, e.g. to frame a later panel of a push strip.
            foreach (var entry in Arg("-qaOffset", "").Split(';').Where(p => p.Contains('=')))
            {
                var parts = entry.Split('=');
                var xy = parts[1].Split(',').Select(float.Parse).ToArray();
                ((RectTransform)Find(parts[0])).anchoredPosition = new Vector2(xy[0], xy[1]);
            }
            // -qaParallax "exterior=gate_arrival" poses a StoryUI parallax rig at that pose's end state.
            var parallax = Arg("-qaParallax");
            if (parallax != null)
            {
                var parts = parallax.Split('=');
                var ui = instance.GetComponentInChildren<Monologue.Dialogue.StoryUI>(true);
                var rigs = (Monologue.Dialogue.StoryParallaxRig[])typeof(Monologue.Dialogue.StoryUI)
                    .GetField("_ParallaxRigs", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(ui);
                var rig = rigs.First(r => r.Tag == parts[0]);
                var pose = rig.Poses.First(p => p.Tag == parts[1]);
                foreach (var layer in rig.Layers)
                {
                    var position = layer.Target.anchoredPosition;
                    float scale = Monologue.Dialogue.StoryAnimationMath.Magnification(layer.Depth, pose.Push);
                    layer.Target.anchoredPosition = new Vector2(
                        Monologue.Dialogue.StoryAnimationMath.Project(position.x, rig.ProjectionOrigin.x, pose.Camera.x, layer.Depth, pose.Push),
                        Monologue.Dialogue.StoryAnimationMath.Project(position.y, rig.ProjectionOrigin.y, pose.Camera.y, layer.Depth, pose.Push));
                    layer.Target.localScale = Vector3.Scale(layer.Target.localScale, new Vector3(scale, scale, 1));
                }
            }
            // -qaLayout "letter_under_door" poses StoryUI layout targets (position, size, scale, panel slant).
            var layoutTag = Arg("-qaLayout");
            if (layoutTag != null)
            {
                var ui = instance.GetComponentInChildren<Monologue.Dialogue.StoryUI>(true);
                var layouts = (System.Array)typeof(Monologue.Dialogue.StoryUI)
                    .GetField("_Layouts", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(ui);
                foreach (var layout in layouts.Cast<Monologue.Dialogue.StoryUI.Layout>().Where(l => l.Tag == layoutTag))
                    foreach (var pose in layout.Poses)
                    {
                        pose.Target.anchoredPosition = pose.Position;
                        if (pose.Size != Vector2.zero) pose.Target.sizeDelta = pose.Size;
                        pose.Target.localScale = pose.Scale;
                        var shape = pose.Target.GetComponent<HauntedFish.Multiplayer.IntroductionPanelShape>();
                        if (shape) shape.Slant = pose.Slant;
                    }
            }
            Canvas.ForceUpdateCanvases();
            // Authored render-texture cameras inside the prefab (e.g. a 3D set filmed into a panel) render first.
            foreach (var canvas in instance.GetComponentsInChildren<Canvas>(true))
                if (canvas.renderMode == RenderMode.WorldSpace)
                    foreach (var graphic in canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>())
                        { var corners = new Vector3[4]; graphic.rectTransform.GetWorldCorners(corners); Debug.Log($"[BatchQA] world UI {graphic.name} corners={corners[0]:F3} {corners[1]:F3} {corners[2]:F3} {corners[3]:F3} lossy={graphic.transform.lossyScale:F4} rot={graphic.transform.eulerAngles}"); }
            // Async shader compilation draws invisible placeholders on first use; compile synchronously
            // and render twice so captures are deterministic.
            EditorSettings.asyncShaderCompilation = false;
            for (int pass = 0; pass < 2; pass++)
                foreach (var feed in instance.GetComponentsInChildren<Camera>(true))
                    if (feed.targetTexture && feed.gameObject.activeInHierarchy) feed.Render();
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));
            File.WriteAllBytes(output, image.EncodeToPNG());
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(target);
            Debug.Log("[BatchQA] captured UI -> " + output);
        }


        // Samples an authored animation clip on a prefab camera at given times and renders that camera:
        //   arg "Assets/Resources/X.prefab|Path/To/Camera|0,2.5,5|Logs/QA/jib"
        public static string SampleCamera(string arg)
        {
            var parts = arg.Split('|');
            EditorSettings.asyncShaderCompilation = false;
            var root = PrefabUtility.LoadPrefabContents(parts[0]);
            try
            {
                var camera = root.GetComponentsInChildren<Camera>(true).First(c => AnimationUtility.CalculateTransformPath(c.transform, root.transform) == parts[1]);
                for (var t = camera.transform; t; t = t.parent) t.gameObject.SetActive(true);
                camera.scene = root.scene;
                var clip = camera.GetComponent<Animator>().runtimeAnimatorController.animationClips[0];
                var outputs = new System.Collections.Generic.List<string>();
                foreach (var time in parts[2].Split(',').Select(float.Parse))
                {
                    clip.SampleAnimation(camera.gameObject, time);
                    var path = ProjectPath(parts[3] + "-" + time.ToString("0.0") + ".png");
                    Render(camera, 960, 540, path);
                    Render(camera, 960, 540, path);
                    outputs.Add(path);
                }
                return string.Join(";", outputs);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }


        // Play-mode diagnostic: lobby gating flags and the theatre portal's bindings.
        public static string LobbyState()
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            var lobby = UnityEngine.Object.FindFirstObjectByType<HauntedFish.Multiplayer.LobbyManager>();
            var session = UnityEngine.Object.FindFirstObjectByType<HauntedFish.Multiplayer.HauntedHotelMultiplayer>();
            var portal = HauntedFish.Theatre.TheatrePortal.Current;
            var player = HauntedFish.Multiplayer.HotelPlayer.LocalPlayer;
            string Field(object o, string n) { var f = o?.GetType().GetField(n, flags); var pr = o?.GetType().GetProperty(n, flags); var v = f != null ? f.GetValue(o) : pr?.GetValue(o); return v is UnityEngine.Object u ? (u ? u.name : "MISSING") : v?.ToString() ?? "null"; }
            return $"intro={lobby?.IntroductionPlaying} step={lobby?.IntroductionStep} inspect={lobby?.InspectionAllowed} menu={Field(lobby, "MenuOpen")} ready={session?.ReadyToPlay} transitioning={session?.Transitioning} " +
                $"portal={(portal ? "yes" : "none")} entry={Field(portal, "_Entry")} safe={Field(portal, "_SafeReturn")} lobbyRef={Field(portal, "_Lobby")} sessionRef={Field(portal, "_Session")} " +
                $"player={(player ? player.transform.position.ToString() : "none")} returnReq={(player && player.GetComponent<HauntedFish.Theatre.TheatreReturnRequest>() ? "yes" : "no")} " +
                $"locked={(player ? player.Movement.SharedDialogueLocked.ToString() : "-")}";
        }


        // Play-mode: teleports the local player onto a named scene object's position (e.g. a trigger) and
        // reports which scenes are loaded a moment later via LobbyState/Scenes.
        public static string TeleportLocalTo(string objectName)
        {
            var player = HauntedFish.Multiplayer.HotelPlayer.LocalPlayer;
            var target = GameObject.Find(objectName) ?? UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).First(t => t.name == objectName).gameObject;
            var collider = target.GetComponent<Collider>();
            var point = collider ? collider.bounds.center : target.transform.position;
            player.Teleport(new Vector3(point.x, player.transform.position.y, point.z));
            return "moved to " + point;
        }
        public static string Scenes() => string.Join(", ", Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
            .Select(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i)).Select(sc => sc.name + (sc.isLoaded ? "" : "(loading)")));


        // Lists colliders overlapping an x range (y/z wide) in a scene: "Assets/Scenes/Game.unity|45|56"
        public static string CollidersInRange(string arg)
        {
            var p = arg.Split('|');
            var scene = EditorSceneManager.OpenScene(p[0], OpenSceneMode.Single);
            float min = float.Parse(p[1]), max = float.Parse(p[2]);
            return string.Join(" | ", scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider>(true))
                .Where(c => c.bounds.max.x >= min && c.bounds.min.x <= max)
                .Select(c => $"{AnimationUtility.CalculateTransformPath(c.transform, null)} trigger={c.isTrigger} on={c.enabled && c.gameObject.activeInHierarchy} x {c.bounds.min.x:F2}..{c.bounds.max.x:F2} y {c.bounds.min.y:F2}..{c.bounds.max.y:F2} z {c.bounds.min.z:F2}..{c.bounds.max.z:F2}"));
        }


        // Play-mode ghost test: "role" switches the local player like the dev button;
        // "place|family|x" places a trap through GhostPlacementWorld.TryPlace at hallway x.
        public static string GhostTest(string arg)
        {
            var p = arg.Split('|');
            if (p[0] == "asset")
            {
                var f2 = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                return string.Join(" || ", new[] { "ShoppingCartTrap", "AnglerFishTrap", "ClickTrap", "FallingChandelierTrap" }.Select(n =>
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Traps/" + n + ".prefab");
                    var pres = go.GetComponentsInChildren<HauntedFish.Multiplayer.GhostTrapAreaPresentation>(true);
                    return n + " presentations=" + pres.Length + " " + string.Join(";", pres.Select(pr =>
                    {
                        var a = (System.Array)typeof(HauntedFish.Multiplayer.GhostTrapAreaPresentation).GetField("_WorldArt", f2).GetValue(pr);
                        var b = (System.Array)typeof(HauntedFish.Multiplayer.GhostTrapAreaPresentation).GetField("_WorldImages", f2).GetValue(pr);
                        return pr.name + " imgs=" + b?.Length + " arts=" + a?.Length + " [" + string.Join(",", a.Cast<UnityEngine.Object>().Select(o => o ? o.name : "NULL")) + "]";
                    }));
                }));
            }
            var player = HauntedFish.Multiplayer.HotelPlayer.LocalPlayer;
            if (!player) return "no local player";
            if (p[0] == "role") { player.SwitchEditorRole(); return "mode " + player.ControlMode; }
            if (p[0] == "skipintro") { UnityEngine.Object.FindFirstObjectByType<HauntedFish.Multiplayer.LobbyManager>()?.SkipIntroduction(); return "skipped"; }
            if (p[0] == "stairs")
            {
                var manager = UnityEngine.Object.FindFirstObjectByType<HauntedFish.Multiplayer.LobbyManager>();
                var zone = (Component)typeof(HauntedFish.Multiplayer.LobbyManager).GetField("_StairZone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(manager);
                var c = zone.GetComponent<Collider>().bounds.center;
                player.Teleport(new Vector3(c.x, player.transform.position.y, c.z));
                return "on stairs " + c;
            }
            if (p[0] == "inspect")
            {
                var f = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                return string.Join(" || ", UnityEngine.Object.FindObjectsByType<HauntedFish.Multiplayer.GhostTrapAreaPresentation>(FindObjectsSortMode.None).Select(pr =>
                {
                    var imgs = (System.Array)typeof(HauntedFish.Multiplayer.GhostTrapAreaPresentation).GetField("_WorldImages", f).GetValue(pr);
                    var arts = (System.Array)typeof(HauntedFish.Multiplayer.GhostTrapAreaPresentation).GetField("_WorldArt", f).GetValue(pr);
                    string Names(System.Array a) => a == null ? "null" : string.Join(",", a.Cast<UnityEngine.Object>().Select(o => o ? o.name + (o is Renderer rr ? $"@z{rr.bounds.center.z:F2}/en{rr.enabled}/q{rr.sharedMaterial?.renderQueue}/o{rr.sortingOrder}" : "") : "NULL"));
                    var world = UnityEngine.Object.FindFirstObjectByType<HauntedFish.Multiplayer.GhostPlacementWorld>();
                    var trap = pr.GetComponentInParent<HauntedFish.Multiplayer.GhostTrap>();
                    var def = world && trap ? world.Definition(trap.FamilyTag) : null;
                    var defPres = def ? def.GetComponentInChildren<HauntedFish.Multiplayer.GhostTrapAreaPresentation>(true) : null;
                    var defArts = defPres ? (System.Array)typeof(HauntedFish.Multiplayer.GhostTrapAreaPresentation).GetField("_WorldArt", f).GetValue(defPres) : null;
                    return pr.name + " imgs=[" + Names(imgs) + "] arts=[" + Names(arts) + "] def=" + (def ? AssetDatabase.GetAssetPath(def) + " defArts=" + (defArts == null ? "null" : defArts.Length.ToString()) : "none") +
                        " grace=" + (world ? world.GraceBounds.min.x + ".." + world.GraceBounds.max.x : "-");
                }));
            }
            if (p[0] == "boss")
            {
                var coordinator = HauntedFish.Multiplayer.BossArenaCoordinator.Current;
                if (!coordinator) return "no boss coordinator";
                coordinator.EditorGoToBoss();
                return $"requested; mode {player.ControlMode} released {player.RoundReleased} ghostId {player.GhostId} netId {player.NetId} returnLocked {player.BossReturnLocked}";
            }
            if (p[0] == "bossstate")
            {
                var co = HauntedFish.Multiplayer.BossArenaCoordinator.Current;
                var fl = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
                string F(object o, string n) { var f = o?.GetType().GetField(n, fl); var pr = o?.GetType().GetProperty(n, fl); var v = f != null ? f.GetValue(o) : pr?.GetValue(o); return v is UnityEngine.Object u ? (u ? u.name : "null") : v?.ToString() ?? "null"; }
                var arena = co ? co.GetType().GetField("_Arena", fl).GetValue(co) : null;
                return $"arena={F(co, "_Arena")} preview={F(co, "_EditorPreview")} inArena={F(co, "_CameraInArena")} loading={F(co, "_Loading")} practicing={F(arena, "EditorPracticing")} views={F(arena, "_Views")} cam={F(arena, "_Camera")} active={F(arena, "Active")}";
            }
            if (p[0] == "rig")
            {
                var rig = UnityEngine.Object.FindFirstObjectByType<HauntedFish.BossFight.BossSegmentedRig>(FindObjectsInactive.Include);
                if (!rig) return "no rig";
                var fl = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var variants = (System.Array)rig.GetType().GetField("_Variants", fl).GetValue(rig);
                var parts = new System.Collections.Generic.List<string> { $"rig {rig.name} active={rig.isActiveAndEnabled} visible={rig.GetType().GetField("_Visible", fl)?.GetValue(rig)} index={rig.GetType().GetField("_Index", fl)?.GetValue(rig)}" };
                foreach (var v in variants)
                {
                    var root = (GameObject)v.GetType().GetField("Root").GetValue(v);
                    var rs = root ? root.GetComponentsInChildren<SpriteRenderer>(true) : new SpriteRenderer[0];
                    parts.Add($"{(root ? root.name : "null")} active={(root && root.activeInHierarchy)} renderers={rs.Length} enabled={rs.Count(r => r.enabled)} sample={(rs.Length > 0 ? rs[0].sprite?.name + "@" + rs[0].bounds.center.ToString("F1") + " layer " + LayerMask.LayerToName(rs[0].gameObject.layer) : "-")}");
                }
                foreach (var cm in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    parts.Add($"cam {cm.name} pos={cm.transform.position:F1} fwd={cm.transform.forward:F2} mask={cm.cullingMask}");
                return string.Join(" | ", parts);
            }
            if (p[0] == "cameras") return string.Join(" | ", UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(cm => $"{cm.gameObject.scene.name}/{cm.name} en={cm.enabled && cm.gameObject.activeInHierarchy} depth={cm.depth} rt={(cm.targetTexture ? cm.targetTexture.name : "-")}"));
            if (p[0] == "scenes") return string.Join(",", Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Select(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name));
            if (p[0] == "state") return $"mode {player.ControlMode} released {player.RoundReleased} ready {player.ControlsReady} scene {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}";
            var world = UnityEngine.Object.FindFirstObjectByType<HauntedFish.Multiplayer.GhostPlacementWorld>();
            if (!world) return "no placement world";
            if (!world.PlacementPoint(new Vector3(float.Parse(p[2]), 0, 0), p[1], out var grounded)) return "placement point rejected at " + p[2];
            player.HeldTrapFamily = p[1];
            bool ok = world.TryPlace(player, grounded, p[1]);
            return (ok ? "placed " : "TryPlace refused ") + p[1] + " at " + grounded + " mode " + player.ControlMode + " released " + player.RoundReleased;
        }


        // Edit mode: world bounds and facing of objects whose name contains the argument, in the open scene.
        public static string BoundsOf(string part) => string.Join(" | ", UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(r => r.name.Contains(part)).Take(6)
            .Select(r => $"{r.name}: min {r.bounds.min:F2} max {r.bounds.max:F2} fwd {r.transform.forward:F2} up {r.transform.up:F2}"));

        // Compile-only check: reaching this method means scripts compiled.
        public static void Compiled() => Debug.Log("[BatchQA] scripts compiled");

        static void Render(Camera camera, int width, int height, string path)
        {
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = camera.targetTexture;
            bool wasEnabled = camera.enabled;
            try
            {
                camera.enabled = true;
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = previous;
                camera.enabled = wasEnabled;
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
