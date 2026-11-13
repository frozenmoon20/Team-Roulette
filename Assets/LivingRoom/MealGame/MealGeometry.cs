using UnityEngine;

// Geometry adapter for purchased, multi-mesh items. Shared grabbing is unchanged.
public sealed class MealGeometry : MonoBehaviour
{
    public Vector3 heldEuler;
    public static Quaternion FlatRotation(GameObject go)
    {
        MeshFilter largest=null; float area=-1;
        foreach(var mesh in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if(!mesh.sharedMesh)continue;
            float size=mesh.sharedMesh.bounds.size.sqrMagnitude;
            if(size>area){area=size;largest=mesh;}
        }
        if(!largest)return Quaternion.identity;
        var s=Vector3.Scale(largest.sharedMesh.bounds.size,largest.transform.lossyScale);
        Vector3 normal=s.x<s.y&&s.x<s.z?Vector3.right:s.z<s.y?Vector3.forward:Vector3.up;
        return Quaternion.FromToRotation(largest.transform.TransformDirection(normal),Vector3.up)*go.transform.rotation;
    }
    public static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        Bounds bounds = new Bounds(go.transform.position, Vector3.zero);
        foreach (var r in renderers)
        {
            if (!r.enabled || r.GetComponentInParent<TMPro.TMP_Text>()) continue;
            if (!found) { bounds = r.bounds; found = true; } else bounds.Encapsulate(r.bounds);
        }
        return bounds;
    }
    public static void BottomAt(GameObject go, Vector3 point)
    {
        Bounds b = BoundsOf(go);
        go.transform.position += point - new Vector3(b.center.x, b.min.y, b.center.z);
    }
}
