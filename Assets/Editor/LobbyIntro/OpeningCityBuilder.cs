using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HauntedFish.EditorTools
{
    // Builds the opening night city as real geometry (facade photos on boxes, rooftop water
    // towers, fire escapes, parapets) in a throwaway scene and renders each depth band to the
    // transparent parallax layers the intro storyboard already uses:
    //   Assets/Art/LobbyIntro/City/OpeningCity{Far,Mid,Back,Near}.png
    // Facades come from Tools/city/prepare_facades.py. Re-run after changing either:
    //   Tools/unity-batch.sh with -executeMethod HauntedFish.EditorTools.OpeningCityBuilder.Render
    public static class OpeningCityBuilder
    {
        const string NightFolder = "Assets/Art/LobbyIntro/CityFacades/Night/";
        const string FireEscapeModel = "Assets/Art/LobbyIntro/CityFacades/Props/FireEscape_PolyHaven/modular_fire_escape_1k.fbx";
        const string StreetLampModel = "Assets/Art/LobbyIntro/CityFacades/Props/StreetLamp_PolyHaven01/street_lamp_01_1k.fbx";
        const string OutputFolder = "Assets/Art/LobbyIntro/City/";
        const int Width = 1600, Height = 900;
        static readonly Color Fog = new Color(.13f, .16f, .24f);

        // Storeys are ~3.2 m. Photo facades are sized from their image aspect.
        static readonly string[] Tenements =
        {
            "Tenement_2ndAve808_HABS", "Tenement_2ndAve863_HABS", "Tenement_2ndAve873_HABS",
            "Tenement_2ndAve867_FireEscape_HABS", "Tenement_2ndAve887_HABS", "CastIron_PerryBlock_HABS",
        };
        static readonly string[] Brick = { "ACG_Facade018A", "ACG_Facade019A", "ACG_Facade020A" };
        static readonly string[] Towers = { "ACG_Facade012", "ACG_Facade013", "ACG_Facade014", "ACG_Facade015", "ACG_Facade016" };

        class Band
        {
            public string Name;
            public float Depth, Fade;
            public int Layer;
        }

        static Shader _Unlit;
        static readonly Dictionary<string, Material> _Materials = new Dictionary<string, Material>();
        static System.Random _Random;

        [MenuItem("Haunted Fish/Lobby Intro/Render Opening City Layers")]
        public static void Render()
        {
            // Async shader compilation renders placeholders on first use; these renders must be final.
            bool asyncShaders = EditorSettings.asyncShaderCompilation;
            EditorSettings.asyncShaderCompilation = false;
            _Random = new System.Random(19);
            _Materials.Clear();
            _Unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bands = new[]
            {
                new Band { Name = "Far", Depth = 150, Fade = .62f, Layer = 26 },
                new Band { Name = "Mid", Depth = 85, Fade = .42f, Layer = 27 },
                new Band { Name = "Back", Depth = 48, Fade = .2f, Layer = 28 },
                new Band { Name = "Near", Depth = 24, Fade = 0, Layer = 29 },
            };
            var camera = new GameObject("City camera").AddComponent<Camera>();
            // Street-level, looking up into the storm so the skyline and clouds share the frame.
            camera.transform.SetPositionAndRotation(new Vector3(0, 1.7f, -6), Quaternion.Euler(-13, 0, 0));
            camera.fieldOfView = 52;
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 600;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0, 0, 0, 0);
            camera.allowHDR = false;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;

            foreach (var band in bands) BuildBand(band, camera);
            foreach (var band in bands)
            {
                camera.cullingMask = 1 << band.Layer;
                var path = OutputFolder + "OpeningCity" + band.Name + ".png";
                RenderLayer(camera, path);
                RenderLayer(camera, path);
                Debug.Log("[BatchQA] rendered " + path);
            }
            AssetDatabase.Refresh();
            EditorSettings.asyncShaderCompilation = asyncShaders;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        static void BuildBand(Band band, Camera camera)
        {
            // Cover the camera frustum width at this depth, with margin for parallax drift.
            float halfWidth = Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad) * band.Depth * (Width / (float)Height) * 1.35f;
            float x = -halfWidth;
            var root = new GameObject("Band " + band.Name);
            while (x < halfWidth)
            {
                string facade;
                float width, height;
                if (band.Name == "Far")
                {
                    facade = Pick(Towers);
                    width = Range(14, 26);
                    height = Range(40, 95);
                }
                else if (band.Name == "Mid")
                {
                    bool tower = _Random.NextDouble() < .45;
                    facade = tower ? Pick(Towers) : Pick(Brick);
                    width = tower ? Range(12, 20) : Range(14, 22);
                    height = tower ? Range(30, 60) : Range(18, 30);
                }
                else
                {
                    bool photo = _Random.NextDouble() < (band.Name == "Near" ? .9 : .6);
                    facade = photo ? Pick(Tenements) : Pick(Brick);
                    var texture = Texture(facade);
                    height = photo ? Range(15, 19) : Range(16, 24);
                    width = photo ? height * texture.width / texture.height : Range(12, 18);
                }
                var building = Building(root.transform, facade, new Vector3(x + width * .5f, 0, band.Depth + Range(-2, 3)), width, height, band);
                if (band.Name != "Far") Roof(building, width, height, band);
                if ((band.Name == "Near" || band.Name == "Back") && Brick.Contains(facade) && _Random.NextDouble() < .7)
                    FireEscape(building, width, height, band);
                x += width + Range(-.5f, 1.5f);
            }
            if (band.Name == "Near") Street(root.transform, band);
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = band.Layer;
        }

        static Transform Building(Transform parent, string facade, Vector3 basePosition, float width, float height, Band band)
        {
            var building = new GameObject(facade).transform;
            building.SetParent(parent, false);
            building.position = basePosition;
            var texture = Texture(facade);
            bool photo = Tenements.Contains(facade);

            var front = GameObject.CreatePrimitive(PrimitiveType.Quad);
            front.name = "Facade";
            Object.DestroyImmediate(front.GetComponent<Collider>());
            front.transform.SetParent(building, false);
            front.transform.localPosition = new Vector3(0, height * .5f, 0);
            front.transform.localScale = new Vector3(width, height, 1);
            var material = new Material(Material(facade, band));
            if (!photo)
            {
                // Tileable facades repeat at a real-world scale: one texture ~ 16 m square.
                material.mainTextureScale = new Vector2(width / 16f, height / 16f);
                material.SetTextureScale("_BaseMap", material.mainTextureScale);
            }
            front.GetComponent<Renderer>().sharedMaterial = material;

            // Body behind the facade, slightly shorter than a photo's roofline so the cut sky stays open.
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(building, false);
            float bodyHeight = photo ? height * .7f : height;
            float depth = Range(10, 18);
            body.transform.localPosition = new Vector3(0, bodyHeight * .5f, depth * .5f + .02f);
            body.transform.localScale = new Vector3(width * .995f, bodyHeight, depth);
            body.GetComponent<Renderer>().sharedMaterial = Flat(new Color(.035f, .04f, .06f), new Band { Name = band.Name, Fade = band.Fade * .3f });
            return building;
        }

        // Wet asphalt and a kerbed pavement in front of the nearest row.
        static void Street(Transform root, Band band)
        {
            Box(root, "Street", new Vector3(0, -.05f, 10), new Vector3(400, .1f, 40), new Color(.035f, .04f, .06f), band);
            Box(root, "Pavement", new Vector3(0, .08f, band.Depth - 2.5f), new Vector3(400, .16f, 5), new Color(.06f, .065f, .085f), band);
            Box(root, "Kerb", new Vector3(0, .1f, band.Depth - 5), new Vector3(400, .2f, .25f), new Color(.1f, .11f, .14f), band);
            // Victorian cast-iron street lamps (Poly Haven street_lamp_01, CC0) along the kerb.
            var lamp = AssetDatabase.LoadAssetAtPath<GameObject>(StreetLampModel);
            var iron = Flat(new Color(.05f, .055f, .07f), band);
            var glass = Flat(new Color(1f, .8f, .45f), band);
            for (float x = -60; x < 60; x += Range(10, 14))
            {
                var post = (GameObject)Object.Instantiate(lamp, root, false);
                post.name = "Street lamp";
                var bounds = Bounds(post);
                post.transform.localScale *= 4.4f / Mathf.Max(.01f, bounds.size.y);
                bounds = Bounds(post);
                post.transform.position += new Vector3(x - bounds.center.x, -bounds.min.y, band.Depth - 4.6f - bounds.center.z);
                foreach (var renderer in post.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials = renderer.sharedMaterials
                        .Select(m => m && (m.name.ToLower().Contains("glass") || m.name.ToLower().Contains("light")) ? glass : iron).ToArray();
            }
        }

        static void Roof(Transform building, float width, float height, Band band)
        {
            bool photo = Tenements.Contains(building.name);
            float roof = photo ? height * .7f : height;
            // Photo facades carry their own cornice; only plain boxes get a parapet ledge.
            if (!photo) Box(building, "Parapet", new Vector3(0, roof + .35f, .3f), new Vector3(width, .7f, .6f), new Color(.06f, .07f, .1f), band);
            int clutter = _Random.Next(1, 4);
            for (int i = 0; i < clutter; i++)
            {
                var size = new Vector3(Range(.8f, 2.5f), Range(1, 3.5f), Range(.8f, 2.5f));
                Box(building, "Chimney", new Vector3(Range(-width * .4f, width * .4f), roof + size.y * .5f, photo ? Range(5, 9) : Range(2, 8)), size, new Color(.05f, .06f, .09f), band);
            }
            if (_Random.NextDouble() < .65) WaterTower(building, new Vector3(Range(-width * .3f, width * .3f), roof, photo ? Range(6, 9) : Range(3, 7)), band);
        }

        static void WaterTower(Transform building, Vector3 roofPoint, Band band)
        {
            var tower = new GameObject("Water tower").transform;
            tower.SetParent(building, false);
            tower.localPosition = roofPoint;
            float legs = Range(2.2f, 3.2f), tankHeight = Range(3.2f, 4.2f), radius = Range(1.6f, 2.2f);
            var wood = new Color(.11f, .085f, .075f);
            var iron = new Color(.04f, .045f, .06f);
            for (int i = 0; i < 4; i++)
            {
                float angle = (45 + 90 * i) * Mathf.Deg2Rad;
                Box(tower, "Leg", new Vector3(Mathf.Cos(angle) * radius * .8f, legs * .5f, Mathf.Sin(angle) * radius * .8f),
                    new Vector3(.18f, legs, .18f), iron, band);
            }
            Box(tower, "Cross brace", new Vector3(0, legs * .55f, -radius * .55f), new Vector3(radius * 1.7f, .1f, .1f), iron, band)
                .localRotation = Quaternion.Euler(0, 0, 32);
            Box(tower, "Platform", new Vector3(0, legs, 0), new Vector3(radius * 2.3f, .15f, radius * 2.3f), iron, band);
            var tank = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tank.name = "Tank";
            Object.DestroyImmediate(tank.GetComponent<Collider>());
            tank.transform.SetParent(tower, false);
            tank.transform.localPosition = new Vector3(0, legs + tankHeight * .5f, 0);
            tank.transform.localScale = new Vector3(radius * 2, tankHeight * .5f, radius * 2);
            tank.GetComponent<Renderer>().sharedMaterial = Staves(wood, band);
            // Hoops around the staves.
            for (int i = 0; i < 3; i++)
            {
                var hoop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                hoop.name = "Hoop";
                Object.DestroyImmediate(hoop.GetComponent<Collider>());
                hoop.transform.SetParent(tower, false);
                hoop.transform.localPosition = new Vector3(0, legs + tankHeight * (.2f + .3f * i), 0);
                hoop.transform.localScale = new Vector3(radius * 2.04f, .04f, radius * 2.04f);
                hoop.GetComponent<Renderer>().sharedMaterial = Flat(iron, band);
            }
            var cone = new GameObject("Roof cone", typeof(MeshFilter), typeof(MeshRenderer));
            cone.transform.SetParent(tower, false);
            cone.transform.localPosition = new Vector3(0, legs + tankHeight, 0);
            cone.GetComponent<MeshFilter>().sharedMesh = Cone(radius * 1.12f, radius * .85f, 20);
            cone.GetComponent<MeshRenderer>().sharedMaterial = Flat(new Color(.07f, .06f, .06f), band);
            Box(tower, "Finial", new Vector3(0, legs + tankHeight + radius * .85f + .25f, 0), new Vector3(.12f, .5f, .12f), iron, band);
        }

        static void FireEscape(Transform building, float width, float height, Band band)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FireEscapeModel);
            var iron = Flat(new Color(.025f, .03f, .04f), band);
            if (model)
            {
                var escape = (GameObject)Object.Instantiate(model, building, false);
                escape.name = "Fire escape";
                var bounds = Bounds(escape);
                float scale = height * .78f / Mathf.Max(.01f, bounds.size.y);
                escape.transform.localScale = Vector3.one * scale;
                bounds = Bounds(escape);
                escape.transform.position += new Vector3(building.position.x + Range(-width * .2f, width * .2f) - bounds.center.x,
                    building.position.y + 3 - bounds.min.y, building.position.z - .05f - bounds.max.z);
                foreach (var renderer in escape.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials = Enumerable.Repeat(iron, renderer.sharedMaterials.Length).ToArray();
                return;
            }
            // Fallback: landings and ladders from boxes.
            for (float y = 4; y < height * .8f; y += 3.2f)
            {
                Box(building, "Landing", new Vector3(0, y, -.7f), new Vector3(width * .35f, .08f, 1.3f), Color.black, band);
                Box(building, "Rail", new Vector3(0, y + .5f, -1.35f), new Vector3(width * .35f, .06f, .06f), Color.black, band);
            }
        }

        static Bounds Bounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        static Transform Box(Transform parent, string name, Vector3 position, Vector3 size, Color color, Band band)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = Flat(color, band);
            return box.transform;
        }

        static Mesh Cone(float radius, float height, int segments)
        {
            var vertices = new List<Vector3> { new Vector3(0, height, 0) };
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                vertices.Add(new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius));
                if (i > 0) triangles.AddRange(new[] { 0, i + 1, i });
            }
            var mesh = new Mesh { vertices = vertices.ToArray(), triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            return mesh;
        }

        static Texture2D Texture(string facade)
        {
            var path = NightFolder + facade + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer && (importer.wrapMode != TextureWrapMode.Repeat || !importer.alphaIsTransparency))
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path) ?? throw new FileNotFoundException(path + " - run Tools/city/prepare_facades.py");
        }

        static Material Material(string facade, Band band)
        {
            var key = facade + band.Name;
            if (_Materials.TryGetValue(key, out var material)) return material;
            material = new Material(_Unlit);
            material.SetTexture("_BaseMap", Texture(facade));
            material.SetColor("_BaseColor", Color.Lerp(Color.white, Fog * 2.2f, band.Fade));
            material.SetFloat("_AlphaClip", 1);
            material.SetFloat("_Cutoff", .5f);
            material.EnableKeyword("_ALPHATEST_ON");
            return _Materials[key] = material;
        }

        static Material Flat(Color color, Band band)
        {
            var key = "flat" + ColorUtility.ToHtmlStringRGB(color) + band.Name;
            if (_Materials.TryGetValue(key, out var material)) return material;
            material = new Material(_Unlit);
            material.SetColor("_BaseColor", Color.Lerp(color, Fog, band.Fade));
            return _Materials[key] = material;
        }

        static Material Staves(Color wood, Band band)
        {
            var key = "staves" + band.Name;
            if (_Materials.TryGetValue(key, out var material)) return material;
            // Vertical planks, darker seams, a little moonlight on the upper edge.
            var texture = new Texture2D(64, 64) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            for (int x = 0; x < 64; x++)
                for (int y = 0; y < 64; y++)
                {
                    float seam = x % 8 == 0 ? .55f : 1;
                    float grain = 1 + .08f * Mathf.Sin(x * 1.7f + y * .15f);
                    texture.SetPixel(x, y, wood * seam * grain * (1 + .25f * y / 64f));
                }
            texture.Apply();
            material = new Material(_Unlit);
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", new Vector2(3, 1));
            material.SetColor("_BaseColor", Color.Lerp(Color.white * 1.6f, Fog * 4, band.Fade));
            return _Materials[key] = material;
        }

        static void RenderLayer(Camera camera, string path)
        {
            var target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 4);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            camera.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(target);
        }

        static float Range(float min, float max) => min + (float)_Random.NextDouble() * (max - min);
        static string Pick(string[] options) => options[_Random.Next(options.Length)];
    }
}
