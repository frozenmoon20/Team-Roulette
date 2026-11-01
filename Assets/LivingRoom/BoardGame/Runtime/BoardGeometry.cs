using System.Collections.Generic;
using UnityEngine;

// Convex polygon clipping in board-local metres; rotation and containment are exact.
public static class BoardGeometry
{
    public static Vector2[] Corners(Vector2 center, Vector2 size, float degrees)
    {
        float a = degrees * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
        var result = new Vector2[4];
        var signs = new[] { new Vector2(-1,-1), new Vector2(1,-1), new Vector2(1,1), new Vector2(-1,1) };
        for (int i=0;i<4;i++) { Vector2 p=Vector2.Scale(signs[i],size)*.5f; result[i]=center+new Vector2(c*p.x-s*p.y,s*p.x+c*p.y); }
        return result;
    }
    static float Cross(Vector2 a, Vector2 b) => a.x*b.y-a.y*b.x;
    public static bool Fits(Vector2 p, Vector2 size, float angle, Vector2 boardSize)
    {
        foreach (var v in Corners(p,size,angle)) if (Mathf.Abs(v.x)>boardSize.x*.5f || Mathf.Abs(v.y)>boardSize.y*.5f) return false;
        return true;
    }
    public static float Overlap(Vector2 p, Vector2 size, float angle, Vector2 q, Vector2 otherSize, float otherAngle)
    {
        var polygon = new List<Vector2>(Corners(p,size,angle));
        var clip = Corners(q,otherSize,otherAngle);
        for(int i=0;i<4 && polygon.Count>0;i++)
        {
            Vector2 a=clip[i], edge=clip[(i+1)%4]-a;
            var input=polygon; polygon=new List<Vector2>();
            Vector2 previous=input[input.Count-1]; float before=Cross(edge,previous-a);
            foreach(var current in input)
            {
                float now=Cross(edge,current-a);
                if ((now>=0)!=(before>=0)) polygon.Add(previous+(current-previous)*(before/(before-now)));
                if(now>=0) polygon.Add(current);
                previous=current; before=now;
            }
        }
        float area=0;
        for(int i=0;i<polygon.Count;i++) area+=Cross(polygon[i],polygon[(i+1)%polygon.Count]);
        return Mathf.Abs(area)*.5f/Mathf.Min(size.x*size.y,otherSize.x*otherSize.y);
    }
}
