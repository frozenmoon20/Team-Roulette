using UnityEngine;
using UnityEngine.UI;

// A mesh-only countdown: only the remaining sector has vertices. No background,
// full-circle track, or invisible rectangle is drawn behind the missing sector.
[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer),typeof(RectTransform))]
public sealed class MealPieTimer : MaskableGraphic
{
    public MealGameController game;
    [Range(0,1)] public float remaining = 1;
    [Range(0, .08f)] public float edgeWidth = .025f;
    public Color edgeColor = new Color(.08f,.08f,.08f,.9f);
    public float Remaining => remaining;

    protected override void OnEnable()
    {
        useLegacyMeshGeneration=false;
        base.OnEnable();raycastTarget=false;
    }
    void Update()
    {
        if(!Application.isPlaying || !game) return;
        float value = game.ToastReady ? 0 : game.CookingStarted ?
            Mathf.Clamp01(game.SecondsRemaining / Mathf.Max(.01f, game.toastSeconds)) : 1;
        if(Mathf.Abs(value-remaining) < .00001f) return;
        remaining=value;SetVerticesDirty();
    }
    public void Preview(float value) { remaining=Mathf.Clamp01(value);SetVerticesDirty(); }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if(remaining<=0) return;
        Rect r=GetPixelAdjustedRect();
        Vector2 center=r.center;
        float radius=Mathf.Min(r.width,r.height)*.5f;
        int steps=Mathf.Max(1,Mathf.CeilToInt(128*remaining));
        for(int i=0;i<steps;i++)
        {
            float start=90-360*(1-remaining);
            float a=(start-360*remaining*i/steps)*Mathf.Deg2Rad;
            float b=(start-360*remaining*(i+1)/steps)*Mathf.Deg2Rad;
            Vector2 p=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
            Vector2 q=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
            Triangle(vh,center,center+p*radius,center+q*radius,color);
            if(edgeWidth>0)
            {
                float inner=radius*(1-edgeWidth);
                Triangle(vh,center+p*inner,center+p*radius,center+q*radius,edgeColor);
                Triangle(vh,center+p*inner,center+q*radius,center+q*inner,edgeColor);
            }
        }
    }
    static void Triangle(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Color tint)
    {
        int n=vh.currentVertCount;
        vh.AddVert(a,tint,Vector2.zero);vh.AddVert(b,tint,Vector2.zero);vh.AddVert(c,tint,Vector2.zero);
        vh.AddTriangle(n,n+1,n+2);
    }
}
