using UnityEngine;
using UnityEngine.UI;

namespace HauntedFish.Multiplayer
{
    public sealed class GhostSelectionWheel : MaskableGraphic
    {
        static readonly Color[] Sectors = { HotelPalette.Moss, HotelPalette.Rust, HotelPalette.Clay,
            HotelPalette.Mulberry, HotelPalette.Rose, HotelPalette.Pine, HotelPalette.Wood,
            HotelPalette.Peach, HotelPalette.Sage, HotelPalette.Plum, HotelPalette.Paper };
        public static Color SectorColor(int index) => Sectors[index % Sectors.Length];
        public int Count = 1;
        [System.NonSerialized] public int Selected = -1;
        [System.NonSerialized] public float SelectionPulse;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f;
            int count = Mathf.Max(1, Count);
            for (int sector = 0; sector < count; sector++)
            {
                var tint = SectorColor(sector);
                if (sector == Selected) tint = Color.Lerp(tint, HotelPalette.Light, .3f + .5f * SelectionPulse);
                float sectorRadius = radius * (sector == Selected ? 1 + .045f * SelectionPulse : 1);
                int steps = Mathf.Max(8, Mathf.CeilToInt(96f / count));
                for (int step = 0; step < steps; step++)
                {
                    float start = (sector + step / (float)steps) * Mathf.PI * 2 / count;
                    float end = (sector + (step + 1f) / steps) * Mathf.PI * 2 / count;
                    int index = mesh.currentVertCount;
                    mesh.AddVert(Vector3.zero, tint, Vector2.zero);
                    mesh.AddVert(new Vector3(Mathf.Sin(start), Mathf.Cos(start)) * sectorRadius, tint, Vector2.zero);
                    mesh.AddVert(new Vector3(Mathf.Sin(end), Mathf.Cos(end)) * sectorRadius, tint, Vector2.zero);
                    mesh.AddTriangle(index, index + 1, index + 2);
                }
            }
        }
    }
}
