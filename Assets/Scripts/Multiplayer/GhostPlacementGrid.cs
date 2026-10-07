// Adapted from Sanokei/UntitledFriendSlop, menu-editor-play-modes: SpectatorGrid.
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    public class GhostPlacementGrid : MonoBehaviour
    {
        public Transform target;
        public float gridRadius = 5f;
        public float gridStep = 1f;
        public float lineThickness = 0.08f; // Adjustable thickness for hand-drawn quads
        public Color gridColor = HotelPalette.Moss;
        public Color xAxisColor = HotelPalette.Rust;
        public Color yAxisColor = HotelPalette.Sage;
        public Color zAxisColor = HotelPalette.Clay;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh gridMesh;
        private void Start()
        {
            ValidateSettings();
            // Ensure this GameObject is world-aligned
            transform.rotation = Quaternion.identity;
            // Create MeshFilter and MeshRenderer if they don't exist
            meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            // Setup Material using the custom toon grid shader
            Shader toonShader = Resources.Load<Shader>("GhostPlacementGrid");
            if (toonShader == null)
            {
                toonShader = Shader.Find("Sprites/Default");
            }

            if (toonShader == null)
            {
                toonShader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
            }

            Material mat = new Material(toonShader);
            mat.name = "SpectatorGridMaterial";
            // Set properties for toon SDF / noise feathering
            if (toonShader.name == "HauntedFish/GhostPlacementGrid")
            {
                mat.SetFloat("_SDFRadius", gridRadius);
                mat.SetFloat("_SDFFeather", 1.2f);
                mat.SetFloat("_NoiseScale", 2.2f);
                mat.SetFloat("_NoiseAmplitude", 0.4f);
                mat.SetFloat("_NoiseSpeed", 4.0f);
                mat.SetFloat("_ToonSteps", 3.0f);
                // Wobble settings
                mat.SetFloat("_WobbleScale", 15.0f);
                mat.SetFloat("_WobbleAmplitude", 0.3f);
                mat.SetFloat("_WobbleSpeed", 6.0f);
            }
            else if (mat.HasProperty("_Color"))
            {
                mat.color = Color.white;
            }

            meshRenderer.sharedMaterial = mat;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            GenerateGridMesh();
        }

        private void LateUpdate()
        {
            // If the target preview is destroyed, destroy ourselves
            if (target == null)
            {
                Destroy(gameObject);
                return;
            }

            // Follow the target position but keep world orientation
            transform.position = target.position;
            transform.rotation = Quaternion.identity;
        }

        private void OnValidate() => ValidateSettings();
        private void ValidateSettings()
        {
            if (float.IsNaN(gridRadius) || float.IsInfinity(gridRadius) || gridRadius <= 0)
                gridRadius = 5;
            gridRadius = Mathf.Min(gridRadius, 100);
            if (float.IsNaN(gridStep) || float.IsInfinity(gridStep) || gridStep <= 0)
                gridStep = 1;
            if (float.IsNaN(lineThickness) || float.IsInfinity(lineThickness) || lineThickness <= 0)
                lineThickness = .08f;
        }

        private void GenerateGridMesh()
        {
            ValidateSettings();
            var vertices = new System.Collections.Generic.List<Vector3>();
            var colors = new System.Collections.Generic.List<Color>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var indices = new System.Collections.Generic.List<int>();
            int vertexIndex = 0;
            // Generate quads for thick lines
            void AddThickLine(Vector3 start, Vector3 end, Vector3 widthDirection, Color color)
            {
                float halfThick = lineThickness * 0.5f;
                Vector3 offset = widthDirection * halfThick;
                // 4 vertices of the line segment quad
                vertices.Add(start - offset);
                vertices.Add(start + offset);
                vertices.Add(end - offset);
                vertices.Add(end + offset);
                colors.Add(color);
                colors.Add(color);
                colors.Add(color);
                colors.Add(color);
                // UVs used by fragment shader to determine distance from center of line (transverse)
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(1f, 1f));
                indices.Add(vertexIndex);
                indices.Add(vertexIndex + 1);
                indices.Add(vertexIndex + 2);
                indices.Add(vertexIndex + 2);
                indices.Add(vertexIndex + 1);
                indices.Add(vertexIndex + 3);
                vertexIndex += 4;
            }

            // Draw slightly beyond the core radius so that lines can fade smoothly inside the SDF feather zone
            float renderRadius = gridRadius + 1.5f;
            // Integer iteration also terminates when tiny float increments would
            // round back to the same coordinate. Keep the authored mesh bounded.
            const int maximumLinesPerAxis = 256;
            float spacing = Mathf.Max(gridStep, 2 * renderRadius / (maximumLinesPerAxis - 1));
            int lineCount = Mathf.Min(maximumLinesPerAxis, Mathf.FloorToInt(2 * renderRadius / spacing) + 1);
            // Generate XZ plane lines (horizontal)
            for (int line = 0; line < lineCount; line++)
            {
                float x = -renderRadius + line * spacing;
                Color col = Mathf.Approximately(x, 0f) ? zAxisColor : gridColor;
                AddThickLine(new Vector3(x, 0f, -renderRadius), new Vector3(x, 0f, renderRadius), Vector3.right, col);
            }

            for (int line = 0; line < lineCount; line++)
            {
                float z = -renderRadius + line * spacing;
                Color col = Mathf.Approximately(z, 0f) ? xAxisColor : gridColor;
                AddThickLine(new Vector3(-renderRadius, 0f, z), new Vector3(renderRadius, 0f, z), Vector3.forward, col);
            }

            // Generate XY plane lines (vertical)
            for (int line = 0; line < lineCount; line++)
            {
                float x = -renderRadius + line * spacing;
                Color col = Mathf.Approximately(x, 0f) ? yAxisColor : gridColor;
                AddThickLine(new Vector3(x, -renderRadius, 0f), new Vector3(x, renderRadius, 0f), Vector3.right, col);
            }

            for (int line = 0; line < lineCount; line++)
            {
                float y = -renderRadius + line * spacing;
                if (Mathf.Approximately(y, 0f))
                    continue; // Already drawn
                Color col = Mathf.Approximately(y, 0f) ? xAxisColor : gridColor;
                AddThickLine(new Vector3(-renderRadius, y, 0f), new Vector3(renderRadius, y, 0f), Vector3.up, col);
            }

            // Generate YZ plane lines (vertical)
            for (int line = 0; line < lineCount; line++)
            {
                float z = -renderRadius + line * spacing;
                Color col = Mathf.Approximately(z, 0f) ? yAxisColor : gridColor;
                AddThickLine(new Vector3(0f, -renderRadius, z), new Vector3(0f, renderRadius, z), Vector3.forward, col);
            }

            for (int line = 0; line < lineCount; line++)
            {
                float y = -renderRadius + line * spacing;
                if (Mathf.Approximately(y, 0f))
                    continue; // Already drawn
                Color col = Mathf.Approximately(y, 0f) ? zAxisColor : gridColor;
                AddThickLine(new Vector3(0f, y, -renderRadius), new Vector3(0f, y, renderRadius), Vector3.up, col);
            }

            if (gridMesh)
                Destroy(gridMesh);
            gridMesh = new Mesh();
            gridMesh.name = "SpectatorGridMesh";
            gridMesh.vertices = vertices.ToArray();
            gridMesh.colors = colors.ToArray();
            gridMesh.uv = uvs.ToArray();
            gridMesh.SetIndices(indices.ToArray(), MeshTopology.Triangles, 0);
            meshFilter.mesh = gridMesh;
        }

        private void OnDestroy()
        {
            if (gridMesh != null)
            {
                Destroy(gridMesh);
            }

            if (meshRenderer != null && meshRenderer.sharedMaterial != null)
            {
                Destroy(meshRenderer.sharedMaterial);
            }
        }
    }
}
