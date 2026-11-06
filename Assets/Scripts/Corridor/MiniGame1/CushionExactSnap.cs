
using UnityEngine;

public class CushionExactSnap : MonoBehaviour
{
    public PlaceSpot placeSpot;
    public GrabbableItem cushion;
    public Transform ghost;

    bool snapped;

    void LateUpdate()
    {
        if (snapped || placeSpot == null || cushion == null || ghost == null)
            return;

        if (placeSpot.PlacedCount < 1)
            return;

        snapped = true;

        Rigidbody rb = cushion.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Transform actual = cushion.transform;

        actual.SetParent(ghost.parent, false);
        actual.localPosition = ghost.localPosition;
        actual.localRotation = ghost.localRotation;
        actual.localScale = ghost.localScale;

        Physics.SyncTransforms();
    }
}
