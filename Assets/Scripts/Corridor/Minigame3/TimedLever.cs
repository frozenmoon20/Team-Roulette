using UnityEngine;

public class TimedLever : MonoBehaviour, IInteractable, IHasName
{
    public string displayName = "레버";
    public TimedLeverSection section;

    public string DisplayName => displayName;

    public void Interact()
    {
        if (section == null)
            return;

        section.StartAttempt();
    }
}