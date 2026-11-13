using UnityEngine;
namespace TeamRoulette.Room3.DollMemory
{
    public sealed class DollYardBindings : MonoBehaviour
    {
        public Transform dryingPoint, houseDoor, dogStand, view, insidePoint, approachPoint; public DollDoor door;
        public Transform[] returnRoute;
        public Collider[] walkingSurfaces;
    }
}
