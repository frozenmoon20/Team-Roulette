
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

    [Header("상자 밀기 접근 제한")]
    [Min(0.1f)]
    public float maxPushReach = 0.9f;

    [Min(0f)]
    public float sideTolerance = 0.35f;

    [Header("상자 효과음")]
    public AudioClip pushSound;
    public AudioClip blockedSound;

    [Range(0f, 1f)]
    public float soundVolume = 0.8f;

    [Min(0f)]
    public float blockedSoundCooldown = 0.3f;

    float lastBlockedSoundTime = -999f;

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

        // 추가: 가까운 면에서 직접 밀 수 있는지 확인
        if (!CanPushFromPosition(
            player.transform.position,
            direction))
        {
            ShowBlocked();
            return;
        }

        // 기존: 목적지에 장애물이 있는지 확인
        if (!CanMove(direction))
        {
            ShowBlocked();
            return;
        }

        if (SokobanHandPush.Instance != null)
            SokobanHandPush.Instance.PlayPush();

        AudioManager.Play(pushSound, soundVolume);

        StartCoroutine(Push(direction));
    }

    // 플레이어가 상자를 직접 밀 수 있는 위치인지 확인
    bool CanPushFromPosition(
        Vector3 playerPosition,
        Vector3 pushDirection)
    {
        Bounds bounds = boxCollider.bounds;

        // 상자의 가장 가까운 표면까지 거리 검사
        Vector3 playerAtBoxHeight = new Vector3(
            playerPosition.x,
            bounds.center.y,
            playerPosition.z
        );

        Vector3 nearestPoint =
            bounds.ClosestPoint(playerAtBoxHeight);

        Vector3 reachOffset =
            nearestPoint - playerAtBoxHeight;

        reachOffset.y = 0f;

        if (reachOffset.magnitude > maxPushReach)
            return false;

        // 상자의 측면과 심하게 어긋난 위치에서
        // 대각선으로 미는 행동 방지
        Vector3 difference =
            bounds.center - playerAtBoxHeight;

        if (Mathf.Abs(pushDirection.x) > 0.5f)
        {
            if (Mathf.Abs(difference.z) >
                bounds.extents.z + sideTolerance)
                return false;
        }
        else
        {
            if (Mathf.Abs(difference.x) >
                bounds.extents.x + sideTolerance)
                return false;
        }

        // 플레이어와 선택한 상자 사이에
        // 다른 소코반 상자가 있는지 검사
        Vector3 rayStart = playerAtBoxHeight;
        Vector3 rayEnd = bounds.center;

        Vector3 rayVector = rayEnd - rayStart;
        rayVector.y = 0f;

        float distance = rayVector.magnitude;

        if (distance < 0.001f)
            return false;

        RaycastHit[] hits = Physics.RaycastAll(
            rayStart,
            rayVector.normalized,
            distance,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        foreach (RaycastHit hit in hits)
        {
            SokobanPushBox otherBox =
                hit.collider.GetComponentInParent<SokobanPushBox>();

            // 다른 소코반 상자가 앞을 가로막음
            if (otherBox != null && otherBox != this)
                return false;
        }

        return true;
    }

    void ShowBlocked()
    {
        PlayerInteractor.ShowHint("이 방향에서는 밀 수 없다.");

        if (Time.time - lastBlockedSoundTime <
            blockedSoundCooldown)
            return;

        lastBlockedSoundTime = Time.time;

        AudioManager.Play(
            blockedSound,
            soundVolume
        );
    }

    // 기존 이동 가능 여부 검사
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

    // 기존 상자 이동 애니메이션
    IEnumerator Push(Vector3 direction)
    {
        moving = true;

        Vector3 target =
            transform.position + direction * pushDistance;

        while (Vector3.Distance(
            transform.position, target) > 0.01f)
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
