
using System.Collections;
using UnityEngine;

public class WardrobeDoorInteractable : MonoBehaviour,
    IInteractable, IHasName
{
    public string displayName = "옷장 문";

    [Header("회전할 경첩 오브젝트")]
    public Transform doorPivot;

    [Header("열리는 각도")]
    public float openAngle = 100f;
    public float openDuration = 0.8f;

    private Quaternion closedRotation;
    private bool isOpen;
    private bool isMoving;

    public string DisplayName => displayName;

    private void Awake()
    {
        if (doorPivot == null)
            doorPivot = transform;

        closedRotation = doorPivot.localRotation;
    }

    public void Interact()
    {
        if (isMoving)
            return;

        StartCoroutine(RotateDoor());
    }

    private IEnumerator RotateDoor()
    {
        isMoving = true;

        Quaternion start = doorPivot.localRotation;

        Quaternion target = isOpen
            ? closedRotation
            : closedRotation * Quaternion.Euler(0f, openAngle, 0f);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, openDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(elapsed / duration)
            );

            doorPivot.localRotation =
                Quaternion.Slerp(start, target, t);

            yield return null;
        }

        doorPivot.localRotation = target;

        isOpen = !isOpen;
        isMoving = false;
    }
}
