
using UnityEngine;

public class FirstToyPickupDialogue : MonoBehaviour
{
    public GrabbableItem[] toys;
    public DialogueStep dialogue;

    bool triggered;

    void Update()
    {
        if (triggered || dialogue == null)
            return;

        PlayerHand hand = PlayerHand.Instance;

        if (hand == null || hand.Held == null)
            return;

        foreach (GrabbableItem toy in toys)
        {
            if (toy != null && hand.Held == toy)
            {
                triggered = true;
                dialogue.Play();
                break;
            }
        }
    }
}
