using UnityEngine;
using System.Collections.Generic;
namespace TeamRoulette.Room3.DollMemory
{
    public sealed class DollYardBindings : MonoBehaviour
    {
        [Header("인형을 말리는 위치와 카메라")]
        public Transform dryingPoint;
        public Transform dogStand, view;

        [Header("집으로 돌아가는 길")]
        public Transform houseDoor;
        public Transform insidePoint, approachPoint;
        public DollDoor door;
        [Tooltip("겨울이가 집에 들어갈 때 차례대로 지나가는 지점입니다.")]
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
