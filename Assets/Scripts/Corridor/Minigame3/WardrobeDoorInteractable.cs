
using System.Collections;
using UnityEngine;

public class WardrobeDoorInteractable : MonoBehaviour, IInteractable, IHasName
{
    public string displayName = "옷장 문";

    [Header("문 열림 설정")]
    public float openAngle = 100f;
    public float openDuration = 0.8f;

    private Quaternion closedRotation;
    private bool isOpen;
    private bool isMoving;

    public string DisplayName => displayName;

    private void Awake()
    {
        closedRotation = transform.localRotation;
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

        Quaternion startRotation = transform.localRotation;

        Quaternion targetRotation = isOpen
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

            transform.localRotation = Quaternion.Slerp(
                startRotation,
                targetRotation,
                t
            );

            yield return null;
        }

        transform.localRotation = targetRotation;

        isOpen = !isOpen;
        isMoving = false;
    }
}
