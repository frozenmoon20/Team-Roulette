using UnityEngine;
using UnityEngine.UI;

namespace TeamRoulette.Room3.Nosework
{
    // A real flat UI graphic; no 3D duplicates, textures or extra camera.
    public sealed class NoseworkCarrotIcon : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            Triangle(vh, r, new Vector2(.26f,.7f),new Vector2(.76f,.7f),new Vector2(.48f,.06f),new Color32(247,143,46,255));
            Triangle(vh, r, new Vector2(.43f,.68f),new Vector2(.16f,.95f),new Vector2(.5f,.83f),new Color32(83,164,72,255));
            Triangle(vh, r, new Vector2(.46f,.69f),new Vector2(.53f,1),new Vector2(.6f,.7f),new Color32(108,190,85,255));
            Triangle(vh, r, new Vector2(.53f,.7f),new Vector2(.88f,.94f),new Vector2(.64f,.72f),new Color32(69,148,62,255));
        }
        static void Triangle(VertexHelper vh, Rect r, Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            int n = vh.currentVertCount;
            foreach(var p in new[]{a,b,c}) vh.AddVert(new Vector3(r.x+p.x*r.width,r.y+p.y*r.height),color,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);
        }
    }
}
