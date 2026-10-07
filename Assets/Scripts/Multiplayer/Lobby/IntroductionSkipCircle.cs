using UnityEngine;
using UnityEngine.UI;

namespace HauntedFish.Multiplayer
{
    // A radial fill drawn entirely with UI vertices; no sprite or texture asset is needed.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class IntroductionSkipCircle : MaskableGraphic
    {
        float _Progress;
        public float Progress
        {
            get => _Progress;
            set
            {
                var next = Mathf.Clamp01(value);
                if (Mathf.Approximately(next, _Progress)) return;
                _Progress = next;
                SetVerticesDirty();
            }
        }
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            var bounds = GetPixelAdjustedRect();
            float radius = Mathf.Min(bounds.width, bounds.height) / 2;
            DrawSector(vertices, bounds.center, radius, 1, HotelPalette.Pine);
            if (_Progress > 0) DrawSector(vertices, bounds.center, radius, _Progress, color);
        }
        static void DrawSector(VertexHelper vertices, Vector2 center, float radius, float progress, Color tint)
        {
            int start = vertices.currentVertCount;
            vertices.AddVert(center, tint, Vector2.zero);
            int segments = Mathf.Max(1, Mathf.CeilToInt(progress * 64));
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.PI / 2 - progress * 2 * Mathf.PI * i / segments;
                vertices.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
                if (i > 0) vertices.AddTriangle(start, start + i, start + i + 1);
            }
        }
    }
}
