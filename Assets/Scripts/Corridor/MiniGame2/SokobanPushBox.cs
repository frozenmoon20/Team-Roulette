
using System.Collections;
using UnityEngine;

public enum SokobanColor
{
    Red,
    Blue,
    Yellow,
    Green
}

[RequireComponent(typeof(BoxCollider))]
public class SokobanPushBox : MonoBehaviour, IInteractable, IHasName
{
    [Header("상자 정보")]
    public string displayName = "상자";
    public SokobanColor boxColor;

    [Header("상자 이동")]
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

        startPosition = transform.localPosition;
        startRotation = transform.localRotation;
    }

    public void Interact()
    {
        if (locked || moving)
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
                Mathf.Sign(difference.x), 0f, 0f
            );
        }
        else
        {
            direction = new Vector3(
                0f, 0f, Mathf.Sign(difference.z)
            );
        }

        if (!CanMove(direction))
        {
            PlayerInteractor.ShowHint("더 이상 밀 수 없다.");
            return;
        }

        if (SokobanHandPush.Instance != null)
            SokobanHandPush.Instance.PlayPush();

        StartCoroutine(Push(direction));
    }

    bool CanMove(Vector3 direction)
    {
        Bounds bounds = boxCollider.bounds;

        Vector3 halfExtents = new Vector3(
            bounds.extents.x * 0.8f,
            bounds.extents.y * 0.7f,
            bounds.extents.z * 0.8f
        );

        RaycastHit[] hits = Physics.BoxCastAll(
            bounds.center,
            halfExtents,
            direction,
            Quaternion.identity,
            pushDistance,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        foreach (RaycastHit hit in hits)
        {
            Collider other = hit.collider;

            if (other == null ||
                other == boxCollider ||
                other.transform == transform ||
                other.transform.IsChildOf(transform))
                continue;

            if (other.GetComponentInParent<FirstPersonPlayer>() != null)
                continue;

            if (other.bounds.max.y <= bounds.min.y + 0.05f)
                continue;

            return false;
        }

        return true;
    }

    IEnumerator Push(Vector3 direction)
    {
        moving = true;

        Vector3 target =
            transform.position + direction * pushDistance;

        while (Vector3.Distance(transform.position, target) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(
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

        if (currentGoal != null && currentGoal != foundGoal)
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
