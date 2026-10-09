using UnityEngine;

public class Minigame1EntryTrigger : MonoBehaviour
{
    public GameObject mazeRoot;
    public GameObject entranceBlocker;

    bool activated;

    void OnTriggerEnter(Collider other)
    {
        if (activated)
            return;

        FirstPersonPlayer player =
            other.GetComponentInParent<FirstPersonPlayer>();

        if (player == null)
            return;

        activated = true;

        if (mazeRoot != null)
            Destroy(mazeRoot);

        if (entranceBlocker != null)
            entranceBlocker.SetActive(true);
    }
}