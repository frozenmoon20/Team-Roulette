
using System.Collections;
using UnityEngine;

public class DrawerPuzzleDrawer : MonoBehaviour,
    IInteractable, IHasName
{
    [Header("상호작용 이름")]
    public string displayName = "서랍";

    [Header("서랍 이동 설정")]
    public Vector3 pullDirection = Vector3.left;
    public float maxPullDistance = 1.5f;

    DrawerPuzzleManager manager;
    int drawerIndex;

    Vector3 closedPosition;
    Coroutine moveCoroutine;

    public string DisplayName => displayName;

    public void Initialize(
        DrawerPuzzleManager puzzleManager,
        int index)
    {
        manager = puzzleManager;
        drawerIndex = index;

        closedPosition = transform.localPosition;
    }

    public void Interact()
    {
        if (manager == null)
            return;

        manager.PullDrawer(drawerIndex);
    }

    public void MoveToState(int state, float duration)
    {
        if (moveCoroutine != null)
            StopCoroutine(moveCoroutine);

        Vector3 direction = pullDirection.normalized;

        float distance =
            maxPullDistance * (state / 2f);

        Vector3 target =
            closedPosition + direction * distance;

        moveCoroutine = StartCoroutine(
            MoveRoutine(target, duration)
        );
    }

    IEnumerator MoveRoutine(
        Vector3 target,
        float duration)
    {
        Vector3 start = transform.localPosition;

        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0f, 1f, t);

            transform.localPosition = Vector3.Lerp(
                start,
                target,
                t
            );

            yield return null;
        }

        transform.localPosition = target;
        moveCoroutine = null;
    }
}
