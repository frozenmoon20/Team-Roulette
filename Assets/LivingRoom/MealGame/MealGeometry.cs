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

    public static void RestOnHand(GameObject item, Vector3 palmPosition, Quaternion viewRotation, GameObject handModel)
    {
        Quaternion toView = Quaternion.Inverse(viewRotation);
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        foreach (var renderer in item.GetComponentsInChildren<MeshRenderer>())
        {
            Bounds bounds = renderer.localBounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                Vector3 relative = toView * (renderer.transform.TransformPoint(corner) - palmPosition);
                min = Vector3.Min(min, relative);
                max = Vector3.Max(max, relative);
            }
        }
        if (float.IsInfinity(min.y)) return;
        Vector3 bottom = new Vector3((min.x + max.x) * .5f, min.y, (min.z + max.z) * .5f);
        Vector3 offset = new Vector3(0, 0, -.018f);
        float supportHeight = 0;
        // Fit above the posed fingers, not just the palm marker. This is evaluated
        // once on pickup, then the resulting pose stays local to the hand.
        if (handModel)
        {
            var baked = new Mesh();
            foreach (var renderer in handModel.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                renderer.BakeMesh(baked);
                foreach (var vertex in baked.vertices)
                {
                    Vector3 p = toView * (renderer.transform.TransformPoint(vertex) - palmPosition);
                    if (Mathf.Abs(p.x - offset.x) > (max.x - min.x) * .5f ||
                        Mathf.Abs(p.z - offset.z) > (max.z - min.z) * .5f) continue;
                    supportHeight = Mathf.Max(supportHeight, p.y);
                }
                baked.Clear();
            }
            Object.Destroy(baked);
        }
        offset.y = supportHeight + .0015f;
        item.transform.position += viewRotation * (offset - bottom);
    }
}
