
using UnityEngine;

public class GyeoulFollowPlayer : MonoBehaviour
{
    [Header("플레이어")]
    public Transform player;

    [Header("애니메이션")]
    public DogAnimator dogAnimator;

    [Header("따라오기 거리")]
    public float followDistance = 1.8f;
    public float trotDistance = 3f;
    public float runDistance = 5f;

    [Header("이동 속도")]
    public float walkSpeed = 0.8f;
    public float trotSpeed = 1.5f;
    public float runSpeed = 5.5f;

    [Header("이동 부드러움")]
    public float acceleration = 6f;
    public float deceleration = 8f;
    public float rotationSpeed = 8f;

    [Header("앉기")]
    public bool autoSit = true;
    public float sitDelay = 2.5f;

    private bool isFollowing;
    private bool sitRequested;
    private float currentSpeed;
    private float idleTimer;

    private void Awake()
    {
        if (dogAnimator == null)
            dogAnimator = GetComponent<DogAnimator>();
    }

    public void StartFollowing(Transform target)
    {
        player = target;
        isFollowing = true;
        currentSpeed = 0f;
        idleTimer = 0f;
        sitRequested = false;
        enabled = true;
    }

    private void Update()
    {
        if (!isFollowing || player == null)
            return;

        Vector3 direction = player.position - transform.position;
        direction.y = 0f;

        float distance = direction.magnitude;

        if (distance <= followDistance)
        {
            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                0f,
                deceleration * Time.deltaTime
            );

            idleTimer += Time.deltaTime;

            if (autoSit &&
                !sitRequested &&
                idleTimer >= sitDelay &&
                dogAnimator != null)
            {
                dogAnimator.Sit();
                sitRequested = true;
            }

            return;
        }

        idleTimer = 0f;
        sitRequested = false;

        float targetSpeed;

        if (distance >= runDistance)
            targetSpeed = runSpeed;
        else if (distance >= trotDistance)
            targetSpeed = trotSpeed;
        else
            targetSpeed = walkSpeed;

        float slowdown = Mathf.Clamp01(
            (distance - followDistance) / 0.6f
        );

        targetSpeed *= slowdown;

        float rate = targetSpeed > currentSpeed
            ? acceleration
            : deceleration;

        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            targetSpeed,
            rate * Time.deltaTime
        );

        Vector3 moveDirection = direction.normalized;

        Quaternion targetRotation =
            Quaternion.LookRotation(moveDirection);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );

        float moveDistance = Mathf.Min(
            currentSpeed * Time.deltaTime,
            distance - followDistance
        );

        transform.position += moveDirection * moveDistance;
    }
}
