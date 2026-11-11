
using UnityEngine;

public class WinterFollowPlayer : MonoBehaviour
{
    [Header("따라갈 대상")]
    public Transform player;

    [Header("이동 설정")]
    public float followDistance = 1.8f;
    public float moveSpeed = 3f;
    public float rotationSpeed = 8f;

    private bool isFollowing;

    public void StartFollowing(Transform target)
    {
        player = target;
        isFollowing = true;
        enabled = true;
    }

    private void Update()
    {
        if (!isFollowing || player == null)
            return;

        Vector3 direction =
            player.position - transform.position;

        direction.y = 0f;

        float distance = direction.magnitude;

        if (distance <= followDistance)
            return;

        Vector3 moveDirection = direction.normalized;

        transform.position +=
            moveDirection * Mathf.Min(
                moveSpeed * Time.deltaTime,
                distance - followDistance
            );

        Quaternion targetRotation =
            Quaternion.LookRotation(moveDirection);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}
