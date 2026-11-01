using System.Collections;
using UnityEngine;

public enum SokobanColor
{
    Red,
    Blue,
    Yellow,
    Green
}

public class SokobanPushBox : MonoBehaviour, IInteractable, IHasName
{
    public string displayName = "상자";
    public SokobanColor boxColor;

    public float pushDistance = 1f;
    public float pushSpeed = 5f;

    bool moving;
    bool locked;

    BoxCollider boxCollider;
    SokobanGoal currentGoal;

    Vector3 startPosition;
    Quaternion startRotation;

    public string DisplayName => displayName;

    void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();

        // 게임 시작 당시 위치 저장
        startPosition = transform.localPosition;
        startRotation = transform.localRotation;
    }

    public void Interact()
    {
        if (locked)
            return;

        if (moving)
            return;

        if (OptionState.Instance == null ||
            !OptionState.Instance.IsUnlocked(OptionType.Push))
        {
            PlayerInteractor.ShowHint("아직 밀 수 없다.");
            return;
        }

        FirstPersonPlayer player =
            FindFirstObjectByType<FirstPersonPlayer>();

        if (player == null)
            return;

        Vector3 difference =
            transform.position - player.transform.position;

        difference.y = 0f;

        Vector3 direction;

        if (Mathf.Abs(difference.x) > Mathf.Abs(difference.z))
        {
            direction = new Vector3(
                Mathf.Sign(difference.x),
                0f,
                0f
            );
        }
        else
        {
            direction = new Vector3(
                0f,
                0f,
                Mathf.Sign(difference.z)
            );
        }

        if (!CanMove(direction))
        {
            PlayerInteractor.ShowHint("더 이상 밀 수 없다.");
            return;
        }

        StartCoroutine(Push(direction));
    }

    bool CanMove(Vector3 direction)
    {
        if (boxCollider == null)
            return false;

        Vector3 center =
            boxCollider.bounds.center;

        Vector3 halfExtents =
            boxCollider.bounds.extents * 0.85f;

        RaycastHit[] hits = Physics.BoxCastAll(
            center,
            halfExtents,
            direction,
            transform.rotation,
            pushDistance,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == boxCollider)
                continue;

            if (hit.transform == transform ||
                hit.transform.IsChildOf(transform))
                continue;

            return false;
        }

        return true;
    }

    IEnumerator Push(Vector3 direction)
    {
        moving = true;

        Vector3 start =
            transform.position;

        Vector3 target =
            start + direction * pushDistance;

        while (
            Vector3.Distance(
                transform.position,
                target
            ) > 0.01f
        )
        {
            transform.position =
                Vector3.MoveTowards(
                    transform.position,
                    target,
                    pushSpeed * Time.deltaTime
                );

            yield return null;
        }

        transform.position = target;

        moving = false;

        CheckGoalBelow();
    }

    void CheckGoalBelow()
    {
        SokobanGoal foundGoal = null;

        RaycastHit[] hits = Physics.RaycastAll(
            boxCollider.bounds.center,
            Vector3.down,
            boxCollider.bounds.extents.y + 1f,
            ~0,
            QueryTriggerInteraction.Collide
        );

        foreach (RaycastHit hit in hits)
        {
            SokobanGoal goal =
                hit.collider.GetComponentInParent<SokobanGoal>();

            if (goal != null)
            {
                foundGoal = goal;
                break;
            }
        }

        if (currentGoal != null &&
            currentGoal != foundGoal)
        {
            currentGoal.RemoveBox();
            currentGoal = null;
        }

        if (foundGoal != null)
        {
            currentGoal = foundGoal;
            currentGoal.PlaceBox(this);
        }
    }

    public void LockBox()
    {
        locked = true;
    }

    public void ResetBox()
    {
        StopAllCoroutines();

        moving = false;
        locked = false;
        currentGoal = null;

        transform.localPosition = startPosition;
        transform.localRotation = startRotation;
    }
}