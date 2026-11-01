using UnityEngine;

public class TimedDoorPassTrigger : MonoBehaviour
{
    public TimedLeverSection section;

    void OnTriggerEnter(Collider other)
    {
        FirstPersonPlayer player =
            other.GetComponentInParent<FirstPersonPlayer>();

        if (player == null)
            return;

        if (section != null)
            section.CompleteSection();
    }
}