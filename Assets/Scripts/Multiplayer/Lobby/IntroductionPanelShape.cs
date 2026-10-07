using UnityEngine;
using UnityEngine.UI;

namespace HauntedFish.Multiplayer
{
    // The same primitive supplies the panel background and its stencil clipping shape.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class IntroductionPanelShape : MaskableGraphic
    {
        [SerializeField] float _Slant = 120;
        public float Slant { get => _Slant; set { _Slant = value; SetVerticesDirty(); } }
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            var r = GetPixelAdjustedRect();
            float slant = Mathf.Min(_Slant, r.width * .25f);
            vertices.AddVert(new Vector2(r.xMin, r.yMin), color, Vector2.zero);
            vertices.AddVert(new Vector2(r.xMin + slant, r.yMax), color, Vector2.zero);
            vertices.AddVert(new Vector2(r.xMax, r.yMax), color, Vector2.zero);
            vertices.AddVert(new Vector2(r.xMax - slant, r.yMin), color, Vector2.zero);
            vertices.AddTriangle(0, 1, 2);
            vertices.AddTriangle(2, 3, 0);
        }
    }
}
