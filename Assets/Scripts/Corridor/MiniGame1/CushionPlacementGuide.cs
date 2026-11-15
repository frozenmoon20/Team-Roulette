
using UnityEngine;

public class CushionPlacementGuide : MonoBehaviour
{
    public GrabbableItem targetCushion;
    public PlaceSpot placeSpot;
    public GameObject ghostVisual;
    public Collider guideCollider;

    Renderer[] ghostRenderers;

    void Awake()
    {
        if (ghostVisual != null)
            ghostRenderers = ghostVisual.GetComponentsInChildren<Renderer>(true);
    }

    void Update()
    {
        bool show = false;

        if (targetCushion != null && placeSpot != null)
        {
            PlayerHand hand = PlayerHand.Instance;

            show = placeSpot.PlacedCount == 0
                && hand != null
                && hand.Held == targetCushion;
        }

        if (ghostRenderers != null)
        {
            foreach (Renderer renderer in ghostRenderers)
            {
                if (renderer != null)
                    renderer.enabled = show;
            }
        }

        if (guideCollider != null)
            guideCollider.enabled = show;
    }
}
