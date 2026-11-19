using UnityEngine;
using System.Collections.Generic;
namespace TeamRoulette.Room3.DollMemory
{
    public sealed class DollYardBindings : MonoBehaviour
    {
        public Transform dryingPoint, houseDoor, dogStand, view, insidePoint, approachPoint; public DollDoor door;
        public Transform[] returnRoute;
        public Collider[] walkingSurfaces;
        public void RefreshWalkingSurfaces()
        {
            var surfaces=new HashSet<Collider>();
            if(walkingSurfaces!=null)
                foreach(var surface in walkingSurfaces)if(surface)surfaces.Add(surface);
            // Replacing the ground or house prefab can invalidate serialized collider references.
            foreach(var root in gameObject.scene.GetRootGameObjects())
                foreach(var collider in root.GetComponentsInChildren<Collider>(true))
                    for(var node=collider.transform;node;node=node.parent)
                        if(node.name.StartsWith("Ground",System.StringComparison.OrdinalIgnoreCase)
                            || node.name.StartsWith("Entry / concrete step",System.StringComparison.OrdinalIgnoreCase)
                            || node.name.StartsWith("Veranda / floor",System.StringComparison.OrdinalIgnoreCase))
                        {surfaces.Add(collider);break;}
            walkingSurfaces=new Collider[surfaces.Count];surfaces.CopyTo(walkingSurfaces);
        }
    }
}
