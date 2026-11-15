using UnityEngine;
using UnityEngine.UI;

namespace TeamRoulette.Room3
{
    // A resolution-independent, translucent play triangle; no additional image asset needed.
    public sealed class TVPlayIcon : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            vh.AddVert(new Vector3(r.xMin, r.yMin), color, Vector2.zero);
            vh.AddVert(new Vector3(r.xMin, r.yMax), color, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.center.y), color, Vector2.zero);
            vh.AddTriangle(0, 1, 2);
        }
    }
}
