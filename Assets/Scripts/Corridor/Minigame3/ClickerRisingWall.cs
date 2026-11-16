
using System.Collections;
using UnityEngine;

public class ClickerRisingWall : MonoBehaviour
{
    [Header("플레이어")]
    public Transform player;

    [Header("클릭커 반응 거리")]
    public Transform activationPoint;
    public float activationDistance = 4f;

    [Header("첫 번째 벽 안내")]
    public bool showFirstWallHint = true;
    public float hintDistance = 6f;
    public string hintMessage =
        "벽 근처에서 클릭커를 눌러보자.";

    [Header("벽 상승 설정")]
    public float riseHeight = 3.5f;
    public float riseDuration = 2.5f;
    public float riseDelay = 0.12f;

    [Header("돌벽 상승 효과음")]
    public AudioClip wallRiseSound;

    [Range(0f, 1f)]
    public float soundVolume = 0.8f;

    private Vector3 closedPosition;
    private bool isRising;
    private bool isOpened;
    private bool hintShown;

    private void Awake()
    {
        closedPosition = transform.position;

        if (player == null)
        {
            FirstPersonPlayer target =
                FindFirstObjectByType<FirstPersonPlayer>();

            if (target != null)
                player = target.transform;
        }
    }

    private void Update()
    {
        if (!showFirstWallHint ||
            hintShown ||
            isRising ||
            isOpened)
        {
            return;
        }

        if (player == null)
            return;

        Vector3 point = activationPoint != null
            ? activationPoint.position
            : closedPosition;

        Vector3 difference =
            player.position - point;

        difference.y = 0f;

        if (difference.sqrMagnitude <=
            hintDistance * hintDistance)
        {
            hintShown = true;

            PlayerInteractor.ShowHint(
                hintMessage
            );
        }
    }

    public void RaiseWall()
    {
        // 이미 움직이거나 열린 벽은 다시 작동하지 않음
        if (isRising || isOpened || player == null)
            return;

        Vector3 point = activationPoint != null
            ? activationPoint.position
            : closedPosition;

        Vector3 difference =
            player.position - point;

        difference.y = 0f;

        // 기존 거리 판정 유지
        if (difference.sqrMagnitude >
            activationDistance * activationDistance)
        {
            return;
        }

        hintShown = true;

        StartCoroutine(RaiseRoutine());
    }

    private IEnumerator RaiseRoutine()
    {
        isRising = true;

        // 기존 벽 상승 지연 유지
        if (riseDelay > 0f)
        {
            yield return new WaitForSeconds(
                riseDelay
            );
        }

        Vector3 start = transform.position;

        Vector3 target =
            closedPosition + Vector3.up * riseHeight;

        float elapsed = 0f;

        float duration = Mathf.Max(
            0.01f,
            riseDuration
        );

        // 추가: 실제 벽 상승 시작 시 효과음 재생
        AudioManager.Play(
            wallRiseSound,
            soundVolume
        );

        // 기존 벽 상승 애니메이션
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / duration
            );

            t = Mathf.SmoothStep(
                0f,
                1f,
                t
            );

            transform.position = Vector3.Lerp(
                start,
                target,
                t
            );

            yield return null;
        }

        transform.position = target;

        isOpened = true;
        isRising = false;
    }
}
