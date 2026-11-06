
using System.Collections;
using UnityEngine;

public class LoopExitSwingDoor : MonoBehaviour
{
    public Transform doorHinge;
    public float openAngle = 90f;
    public float openDuration = 1.5f;

    Quaternion closedRotation;
    bool opened;

    void Awake()
    {
        if (doorHinge != null)
            closedRotation = doorHinge.localRotation;
    }

    public void OpenDoor()
    {
        if (opened || doorHinge == null)
            return;

        opened = true;
        StartCoroutine(OpenRoutine());
    }

    IEnumerator OpenRoutine()
    {
        Quaternion targetRotation =
            closedRotation * Quaternion.Euler(0f, openAngle, 0f);

        float time = 0f;

        while (time < openDuration)
        {
            time += Time.deltaTime;

            float t = Mathf.SmoothStep(
                0f, 1f,
                Mathf.Clamp01(time / openDuration)
            );

            doorHinge.localRotation =
                Quaternion.Slerp(closedRotation, targetRotation, t);

            yield return null;
        }

        doorHinge.localRotation = targetRotation;
    }
}
