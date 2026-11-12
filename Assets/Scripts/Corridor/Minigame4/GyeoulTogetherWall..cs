
using UnityEngine;

public class GyeoulTogetherWall : MonoBehaviour
{
    [Header("플레이어와 겨울이")]
    public Transform player;
    public Transform gyeoul;
    public GyeoulClickerReunion reunion;
    public GyeoulFollowPlayer follower;

    [Header("열릴 벽")]
    public ClickerRisingWall risingWall;
    public Transform activationPoint;

    [Header("거리 조건")]
    public float playerActivationDistance = 4f;
    public float gyeoulActivationDistance = 4f;
    public float togetherDistance = 3.5f;

    [Header("반드시 통과해야 할 허들")]
    public GyeoulHurdleJump[] requiredHurdles;

    private bool opened;

    public void TryOpen()
    {
        if (opened)
            return;

        if (player == null ||
            gyeoul == null ||
            reunion == null ||
            follower == null ||
            risingWall == null ||
            activationPoint == null)
        {
            Debug.LogWarning(
                "겨울이 동행 벽의 참조를 확인해 주세요.",
                this
            );
            return;
        }

        if (!reunion.HasJoined || !follower.IsFollowing)
            return;

        if (HorizontalDistance(
                player.position,
                activationPoint.position
            ) > playerActivationDistance)
            return;

        if (requiredHurdles == null ||
            requiredHurdles.Length == 0)
        {
            Debug.LogWarning(
                "필수 허들을 연결해 주세요.",
                this
            );
            return;
        }

        foreach (GyeoulHurdleJump hurdle in requiredHurdles)
        {
            if (hurdle == null || !hurdle.IsCompleted)
            {
                PlayerInteractor.ShowHint(
                    "겨울이와 함께 넘어와야 한다."
                );
                return;
            }
        }

        if (follower.IsJumping ||
            !gyeoul.gameObject.activeInHierarchy)
            return;

        if (HorizontalDistance(
                gyeoul.position,
                activationPoint.position
            ) > gyeoulActivationDistance)
        {
            PlayerInteractor.ShowHint(
                "겨울이가 올 때까지 기다리자."
            );
            return;
        }

        if (HorizontalDistance(
                player.position,
                gyeoul.position
            ) > togetherDistance)
        {
            PlayerInteractor.ShowHint(
                "겨울이와 함께 있어야 한다."
            );
            return;
        }

        opened = true;

        risingWall.RaiseWall();

        PlayerInteractor.ShowHint(
            "겨울이와 함께 길을 열었다."
        );
    }

    private float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(a, b);
    }
}
