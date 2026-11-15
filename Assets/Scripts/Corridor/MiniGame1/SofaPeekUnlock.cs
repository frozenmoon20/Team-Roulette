
using UnityEngine;

public class SofaPeekUnlock : MonoBehaviour
{
    public PlaceSpot toyBox;
    public Collider sofaPeekCollider;
    public int requiredToyCount = 3;

    bool unlocked;

    void Start()
    {
        if (sofaPeekCollider != null)
            sofaPeekCollider.enabled = false;
    }

    void Update()
    {
        if (unlocked || toyBox == null || sofaPeekCollider == null)
            return;

        if (toyBox.PlacedCount >= requiredToyCount)
        {
            unlocked = true;
            sofaPeekCollider.enabled = true;
        }
    }
}
