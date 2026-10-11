using UnityEngine;
using UnityEngine.UI;

namespace TeamRoulette.UI
{
    // Simple vector UI: one central pad, then four toes. No texture dependency.
    [RequireComponent(typeof(CanvasRenderer))]
    public class PawProgressGraphic : MaskableGraphic
    {
        [SerializeField, Range(0, 5)] int progress;
        public int Progress => progress;
        public void SetProgress(int value) { progress = Mathf.Clamp(value, 0, 5); SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (progress == 0) return;
            Pad(mesh, new Vector2(.5f, .3f), new Vector2(.30f, .23f), 0, true);
            if (progress > 1) Pad(mesh, new Vector2(.17f, .59f), new Vector2(.105f, .15f), 24);
            if (progress > 2) Pad(mesh, new Vector2(.38f, .79f), new Vector2(.105f, .16f), 10);
            if (progress > 3) Pad(mesh, new Vector2(.63f, .79f), new Vector2(.105f, .16f), -10);
            if (progress > 4) Pad(mesh, new Vector2(.84f, .59f), new Vector2(.105f, .15f), -24);
        }

        void Pad(VertexHelper mesh, Vector2 center, Vector2 radius, float angle, bool central = false)
        {
            const int segments = 64;
            Rect rect = GetPixelAdjustedRect();
            int first = mesh.currentVertCount;
            Vector2 c = rect.min + Vector2.Scale(center, rect.size);
            mesh.AddVert(c, color, Vector2.zero);
            Quaternion rotation = Quaternion.Euler(0, 0, angle);
            for (int i = 0; i < segments; i++)
            {
                float t = i * Mathf.PI * 2 / segments;
                float shape = central ? 1 + .10f * Mathf.Cos(3 * t + Mathf.PI / 2) : 1;
                Vector2 p = rotation * new Vector2(Mathf.Cos(t) * radius.x * shape, Mathf.Sin(t) * radius.y * shape);
                mesh.AddVert(c + Vector2.Scale(p, rect.size), color, Vector2.zero);
                mesh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % segments);
            }
        }
    }
}
