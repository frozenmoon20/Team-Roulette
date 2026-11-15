using UnityEngine;
using UnityEngine.UI;

namespace TeamRoulette.Room3.BallPlay
{
    // Geometry-based UI: no imported sprites or additional fonts are required.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BallProgressCircle : MaskableGraphic
    {
        public bool filled;
        public float thickness = 3f;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            Vector2 center = rect.center;
            float outer = Mathf.Min(rect.width, rect.height) * .5f;
            float inner = filled ? 0 : Mathf.Max(0, outer - thickness);
            const int segments = 64;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                Vector2 d1 = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), d2 = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                int n = vh.currentVertCount;
                vh.AddVert(center + d1 * outer, color, Vector2.zero);
                vh.AddVert(center + d2 * outer, color, Vector2.zero);
                vh.AddVert(center + d2 * inner, color, Vector2.zero);
                vh.AddVert(center + d1 * inner, color, Vector2.zero);
                vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
            }
        }
        public void SetFilled(bool value) { filled = value; SetVerticesDirty(); }
    }
}
