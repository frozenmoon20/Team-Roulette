
using System.Collections;
using UnityEngine;

public class SokobanExitDoor : MonoBehaviour
{
    [Header("소코반 게임 매니저")]
    public SokobanGameManager gameManager;

    [Header("출구 문")]
    public Transform door;

    [Header("문 열림 설정")]
    public float openAngle = 90f;
    public float openDuration = 1.5f;

    private Quaternion closedRotation;
    private bool opened;

    private void Start()
    {
        if (door == null)
            door = transform;

        closedRotation = door.localRotation;
    }

    private void Update()
    {
        if (opened || gameManager == null)
            return;

        if (gameManager.IsCleared)
            OpenDoor();
    }

    public void OpenDoor()
    {
        if (opened)
            return;

        opened = true;
        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        Quaternion targetRotation =
            closedRotation * Quaternion.Euler(0f, openAngle, 0f);

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

            door.localRotation = Quaternion.Slerp(
                closedRotation,
                targetRotation,
                t
            );

            yield return null;
        }

        door.localRotation = targetRotation;

        PlayerInteractor.ShowHint("문이 열렸다.");
    }
}
