using UnityEngine;
using UnityEngine.Events;

public class PlaceSpot : MonoBehaviour, IInteractable, IHasName
{
    public string displayName = "";

    public GrabbableItem[] acceptItems;

    public Transform placePoint;
    public Transform[] placePoints;

    public bool hideOnPlace;
    public string wrongItemMessage = "여기에 둘 건 아니다.";
    public UnityEvent onAllPlaced;

    public string DisplayName => displayName;
    public int PlacedCount { get; private set; }

    public bool CanAccept(GrabbableItem item)
    {
        if (item == null || acceptItems == null)
            return false;

        return System.Array.IndexOf(acceptItems, item) >= 0;
    }

    public void Interact()
    {
        PlayerHand hand = PlayerHand.Instance;

        if (hand == null || hand.Held == null)
            return;

        if (!CanAccept(hand.Held))
        {
            PlayerInteractor.ShowHint(wrongItemMessage);
            return;
        }

        Transform targetPoint = GetNextPlacePoint();

        hand.PlaceHeld(
            targetPoint,
            hideOnPlace
        );

        PlacedCount++;

        if (PlacedCount >= acceptItems.Length)
        {
            enabled = false;
            onAllPlaced.Invoke();
        }
    }

    Transform GetNextPlacePoint()
    {
        if (placePoints != null &&
            PlacedCount < placePoints.Length &&
            placePoints[PlacedCount] != null)
        {
            return placePoints[PlacedCount];
        }

        if (placePoint != null)
            return placePoint;

        return transform;
    }
}