
using System.Collections;
using UnityEngine;

public class LoopStartDoor : MonoBehaviour, IInteractable, IHasName
{
    [Header("문 경첩")]
    public Transform doorHinge;

    [Header("문 회전")]
    public float openAngle = 90f;
    public float openDuration = 0.7f;
    public float closeDuration = 0.8f;

    [Header("자동 닫힘")]
    public float autoCloseDelay = 3f;
    public float safeDistance = 1.5f;

    [Header("플레이어")]
    public Transform player;

    Quaternion closedRotation;
    Quaternion openRotation;

    Coroutine doorRoutine;

    bool isOpen;
    bool isMoving;

    public bool IsClosed =>
        !isOpen && !isMoving;

    public string DisplayName =>
        isOpen || isMoving ? "" : "문 열기";

    void Awake()
    {
        if (doorHinge == null)
            doorHinge = transform;

        closedRotation = doorHinge.localRotation;

        openRotation =
            closedRotation *
            Quaternion.Euler(0f, openAngle, 0f);
    }

    void Start()
    {
        if (player == null)
        {
            FirstPersonPlayer found =
                FindFirstObjectByType<FirstPersonPlayer>();

            if (found != null)
                player = found.transform;
        }
    }

    public void Interact()
    {
        if (isOpen || isMoving)
            return;

        if (doorRoutine != null)
            StopCoroutine(doorRoutine);

        doorRoutine = StartCoroutine(DoorRoutine());
    }

    IEnumerator DoorRoutine()
    {
        isMoving = true;

        yield return RotateDoor(
            openRotation,
            openDuration
        );

        isOpen = true;
        isMoving = false;

        yield return new WaitForSeconds(autoCloseDelay);

        while (IsPlayerNearDoor())
            yield return null;

        isMoving = true;

        while (true)
        {
            if (IsPlayerNearDoor())
            {
                yield return RotateDoor(
                    openRotation,
                    openDuration
                );

                isMoving = false;

                while (IsPlayerNearDoor())
                    yield return null;

                isMoving = true;
            }

            if (Quaternion.Angle(
                doorHinge.localRotation,
                closedRotation
            ) < 0.1f)
            {
                break;
            }

            doorHinge.localRotation =
                Quaternion.RotateTowards(
                    doorHinge.localRotation,
                    closedRotation,
                    (Mathf.Abs(openAngle) /
                     Mathf.Max(0.01f, closeDuration)) *
                    Time.deltaTime
                );

            yield return null;
        }

        doorHinge.localRotation = closedRotation;

        isOpen = false;
        isMoving = false;
        doorRoutine = null;
    }

    IEnumerator RotateDoor(
        Quaternion targetRotation,
        float duration
    )
    {
        Quaternion startRotation = doorHinge.localRotation;

        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;

            float t = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(
                    time / Mathf.Max(0.01f, duration)
                )
            );

            doorHinge.localRotation =
                Quaternion.Slerp(
                    startRotation,
                    targetRotation,
                    t
                );

            yield return null;
        }

        doorHinge.localRotation = targetRotation;
    }

    bool IsPlayerNearDoor()
    {
        if (player == null || doorHinge == null)
            return false;

        Vector3 a = player.position;
        Vector3 b = doorHinge.position;

        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(a, b) < safeDistance;
    }
}
