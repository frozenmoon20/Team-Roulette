
using System.Collections;
using UnityEngine;

public class GyeoulHurdleJump : MonoBehaviour
{
    [Header("플레이어와 겨울이")]
    public Transform player;
    public Transform gyeoul;
    public GyeoulFollowPlayer follower;
    public DogAnimator dogAnimator;

    [Header("허들")]
    public Transform hurdle;
    public Collider hurdleCollider;

    [Header("겨울이 이동 위치")]
    public Transform approachPoint;
    public Transform takeoffPoint;
    public Transform landingPoint;

    [Header("클릭커 조건")]
    public float activationDistance = 5.5f;
    public float playerPassedDistance = 0.6f;
    public float dogReadyDistance = 1.2f;

    [Header("허들 통과 제한")]
    public float stopBeforeHurdle = 0.3f;

    [Header("점프 설정")]
    public float jumpHeight = 0.8f;
    public float clearanceMargin = 0.25f;
    public float animationSpeed = 1f;


    [Header("마지막 허들 엔딩 설정")]
    [Tooltip("체크하면 겨울이가 이 허들을 절대 점프하지 않습니다.")]
    public bool neverJumpForEnding = false;


    private bool completed;
    private bool jumping;

    public bool IsCompleted => completed;

    private void Awake()
    {
        if (hurdle == null)
            hurdle = transform;

        if (follower == null && gyeoul != null)
            follower = gyeoul.GetComponent<GyeoulFollowPlayer>();

        if (dogAnimator == null && gyeoul != null)
            dogAnimator = gyeoul.GetComponent<DogAnimator>();
    }

    public bool ShouldWait(Transform targetPlayer, Transform dog)
    {
        if (!enabled || completed || jumping ||
            targetPlayer == null ||
            dog == null ||
            hurdle == null ||
            approachPoint == null)
            return false;

        return Side(targetPlayer.position) >= playerPassedDistance &&
               Side(dog.position) <= 0.25f;
    }

    public Vector3 RestrictMovement(Vector3 current, Vector3 next)
    {
        if (!enabled || completed || jumping || hurdle == null)
            return next;

        float limit = GetStopLimit();
        float nextSide = Side(next);

        if (nextSide > limit)
            next -= Forward() * (nextSide - limit);

        return next;
    }

    
public void TryJump()
{
    // 마지막 허들은 클릭커를 눌러도 겨울이가 넘지 않는다.
    if (neverJumpForEnding)
    {
        Debug.Log(
            "[마지막 엔딩] 겨울이는 허들 앞에 남아 있습니다.",
            this
        );
        return;
    }

    if (!enabled || completed || jumping)
        return;

    // 아래는 기존 코드 그대로 유지

        if (!enabled || completed || jumping)
            return;

        if (player == null ||
            gyeoul == null ||
            follower == null ||
            dogAnimator == null ||
            dogAnimator.animator == null ||
            hurdle == null ||
            approachPoint == null ||
            takeoffPoint == null ||
            landingPoint == null)
        {
            Debug.LogWarning(
                "겨울이 허들 점프의 연결 항목을 확인해 주세요.",
                this
            );
            return;
        }

        if (!follower.IsFollowing || follower.IsJumping)
            return;

        if (HorizontalDistance(
            player.position,
            hurdle.position
        ) > activationDistance)
            return;

        if (Side(player.position) < playerPassedDistance)
        {
            PlayerInteractor.ShowHint(
                "먼저 허들을 넘어가자."
            );
            return;
        }

        if (Side(gyeoul.position) > 0.25f)
        {
            Debug.LogWarning(
                "겨울이가 이미 허들 반대편에 있습니다.",
                this
            );
            return;
        }

        if (Side(approachPoint.position) >= GetStopLimit() ||
            Side(takeoffPoint.position) >= 0f ||
            Side(landingPoint.position) <= 0f)
        {
            Debug.LogWarning(
                "허들 방향이나 점프 포인트 위치가 잘못되었습니다.",
                this
            );

            PlayerInteractor.ShowHint(
                "허들 점프 위치를 확인해야 한다."
            );
            return;
        }

        if (HorizontalDistance(
            gyeoul.position,
            approachPoint.position
        ) > dogReadyDistance)
        {
            PlayerInteractor.ShowHint(
                "겨울이가 허들 앞에 올 때까지 기다리자."
            );
            return;
        }

        StartCoroutine(JumpRoutine());
    }

    private IEnumerator JumpRoutine()
    {
        jumping = true;
        follower.BeginJump();

        float speed = Mathf.Max(0.1f, animationSpeed);

        Vector3 start = gyeoul.position;
        float groundY = start.y;

        Vector3 takeoff = takeoffPoint.position;
        takeoff.y = groundY;

        Vector3 landing = landingPoint.position;
        landing.y = groundY;

        Vector3 direction = landing - start;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            gyeoul.rotation =
                Quaternion.LookRotation(direction);
        }

        float prepareTime = dogAnimator.runJumpLiftoff / speed;
        float airTime = dogAnimator.RunJumpAir / speed;

        float finishTime = Mathf.Max(
            0f,
            dogAnimator.runJumpLength -
            dogAnimator.runJumpTouchdown
        ) / speed;

        float actualJumpHeight = jumpHeight;

        if (hurdleCollider != null)
        {
            float requiredHeight =
                hurdleCollider.bounds.max.y -
                groundY + clearanceMargin;

            actualJumpHeight = Mathf.Max(
                jumpHeight,
                requiredHeight
            );
        }

        dogAnimator.PlayRunJump(speed);

        float elapsed = 0f;

        while (elapsed < prepareTime)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / Mathf.Max(0.01f, prepareTime)
            );

            gyeoul.position = Vector3.Lerp(
                start,
                takeoff,
                t
            );

            yield return null;
        }

        gyeoul.position = takeoff;
        elapsed = 0f;

        while (elapsed < airTime)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / Mathf.Max(0.01f, airTime)
            );

            Vector3 position = Vector3.Lerp(
                takeoff,
                landing,
                t
            );

            position.y +=
                Mathf.Sin(t * Mathf.PI) * actualJumpHeight;

            gyeoul.position = position;

            yield return null;
        }

        gyeoul.position = landing;

        if (finishTime > 0f)
            yield return new WaitForSeconds(finishTime);

        dogAnimator.Interrupt();

        dogAnimator.animator.speed = 1f;

        dogAnimator.animator.CrossFadeInFixedTime(
            "Idle",
            0.15f,
            0,
            0f
        );

        dogAnimator.current = "Idle";

        completed = true;
        jumping = false;

        follower.EndJump();

        PlayerInteractor.ShowHint(
            "겨울이가 허들을 넘어왔다!"
        );

        Debug.Log(
            "겨울이 허들 점프 완료!",
            this
        );
    }

    private float GetStopLimit()
    {
        float limit = -Mathf.Max(
            0.05f,
            stopBeforeHurdle
        );

        if (hurdleCollider != null)
        {
            Bounds bounds = hurdleCollider.bounds;
            Vector3 forward = Forward();

            float center = Vector3.Dot(
                bounds.center - hurdle.position,
                forward
            );

            float extent =
                Mathf.Abs(forward.x) * bounds.extents.x +
                Mathf.Abs(forward.z) * bounds.extents.z;

            limit = Mathf.Min(
                limit,
                center - extent - stopBeforeHurdle
            );
        }

        return limit;
    }

    private float Side(Vector3 position)
    {
        Vector3 difference =
            position - hurdle.position;

        difference.y = 0f;

        return Vector3.Dot(
            difference,
            Forward()
        );
    }

    private Vector3 Forward()
    {
        Vector3 direction = hurdle.forward;
        direction.y = 0f;

        return direction.sqrMagnitude > 0.001f
            ? direction.normalized
            : Vector3.forward;
    }

    private float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(a, b);
    }
}
