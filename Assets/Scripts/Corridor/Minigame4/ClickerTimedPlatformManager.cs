
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class ClickerTimedPlatformManager : MonoBehaviour
{
    [Header("발판")]
    public Transform[] platforms;

    [Header("플레이어와 클릭커")]
    public FirstPersonPlayer player;
    public ClickerPocketController clickerController;

    [Header("위치 설정")]
    public Transform activationPoint;
    public Transform respawnPoint;
    public Transform goalPoint;

    [Header("작동 거리")]
    public float activationDistance = 4f;
    public float goalDistance = 2f;

    [Header("발판 움직임")]
    public float riseHeight = 3.5f;
    public float moveDuration = 0.6f;
    public float platformInterval = 0.15f;

    [Header("시간제한")]
    public float holdDuration = 8f;
    public float warningTime = 2f;

    [Header("낙하 판정")]
    public float fallDepth = 2.5f;
    public float fallAreaWidth = 5f;

    [Header("안내 문구")]
    public float hintDistance = 6f;
    public string hintMessage =
        "클릭커를 눌러 발판을 올려보자.";

    [Header("발판 상승 및 하강 효과음")]
    public AudioClip platformRiseSound;
    public AudioClip platformLowerSound;

    [Range(0f, 1f)]
    public float soundVolume = 0.8f;

    [Header("시간제한 타이머 효과음")]
    public AudioClip timerTickSound;

    [Range(0f, 1f)]
    public float timerTickVolume = 0.7f;

    [Header("클리어 이벤트")]
    public UnityEvent onCompleted;

    private Vector3[] raisedPositions;
    private Vector3[] loweredPositions;

    private Coroutine cycleRoutine;
    private CharacterController characterController;
    private AudioSource timerAudioSource;

    private bool running;
    private bool completed;
    private bool hintShown;

    private void Awake()
    {
        if (player == null)
            player = FindFirstObjectByType<FirstPersonPlayer>();

        if (clickerController == null)
            clickerController =
                FindFirstObjectByType<ClickerPocketController>();

        if (player != null)
            characterController =
                player.GetComponent<CharacterController>();

        // 타이머 전용 AudioSource
        timerAudioSource = gameObject.AddComponent<AudioSource>();

        timerAudioSource.playOnAwake = false;
        timerAudioSource.loop = false;
        timerAudioSource.spatialBlend = 0f;
    }

    private void Start()
    {
        if (platforms == null || platforms.Length == 0)
        {
            Debug.LogError(
                "발판을 연결해 주세요.", this
            );

            enabled = false;
            return;
        }

        raisedPositions = new Vector3[platforms.Length];
        loweredPositions = new Vector3[platforms.Length];

        for (int i = 0; i < platforms.Length; i++)
        {
            if (platforms[i] == null)
            {
                Debug.LogError(
                    "비어 있는 발판이 있습니다.", this
                );

                enabled = false;
                return;
            }

            raisedPositions[i] = platforms[i].position;

            loweredPositions[i] =
                raisedPositions[i] +
                Vector3.down * riseHeight;
        }

        // 시작할 때 발판은 아래에 배치
        // 시작 효과음은 재생하지 않음
        SetPlatforms(false);
    }

    private void Update()
    {
        // 타이머가 재생 중이라면 현재 볼륨과
        // 효과음 해금 상태를 반영
        if (timerAudioSource != null &&
            timerAudioSource.isPlaying)
        {
            if (!AudioManager.SfxUnlocked)
            {
                StopTimerSound();
            }
            else
            {
                timerAudioSource.volume =
                    timerTickVolume * AudioManager.Volume;
            }
        }

        if (player == null || completed)
            return;

        // 기존 안내 문구
        if (!hintShown &&
            !running &&
            activationPoint != null)
        {
            if (HorizontalDistance(
                player.transform.position,
                activationPoint.position
            ) <= hintDistance)
            {
                hintShown = true;

                PlayerInteractor.ShowHint(
                    hintMessage
                );
            }
        }

        // 기존 낙하 판정
        if (respawnPoint != null &&
            activationPoint != null &&
            goalPoint != null &&
            IsInsideFallArea() &&
            player.transform.position.y <
            respawnPoint.position.y - fallDepth)
        {
            FailAttempt();
            return;
        }

        if (!running || goalPoint == null)
            return;

        // 기존 목표 도착 판정
        if (characterController != null &&
            characterController.isGrounded &&
            HorizontalDistance(
                player.transform.position,
                goalPoint.position
            ) <= goalDistance)
        {
            CompleteSection();
        }
    }

    public void OnClickerPressed()
    {
        if (!enabled ||
            completed ||
            running ||
            player == null)
            return;

        if (activationPoint == null)
            return;

        if (clickerController != null &&
            !clickerController.IsHoldingClicker)
            return;

        float distance = HorizontalDistance(
            player.transform.position,
            activationPoint.position
        );

        if (distance > activationDistance)
            return;

        cycleRoutine = StartCoroutine(
            PlatformCycle()
        );
    }

    private IEnumerator PlatformCycle()
    {
        running = true;

        PlayerInteractor.ShowHint(
            "발판이 올라오고 있다!"
        );

        // 발판 상승
        yield return MovePlatforms(true);

        PlayerInteractor.ShowHint(
            "발판이 올라왔다. 빨리 건너자!"
        );

        // 발판 상승 완료 후 타이머 시작
        PlayTimerSound();

       // 효과음이 있으면 효과음 길이를 제한시간으로 사용
// 효과음이 없으면 기존 Hold Duration 사용
float remaining = timerTickSound != null
    ? timerTickSound.length
    : holdDuration;

bool warned = false;

        while (remaining > 0f && !completed)
        {
            remaining -= Time.deltaTime;

            if (!warned && remaining <= warningTime)
            {
                warned = true;

                PlayerInteractor.ShowHint(
                    "곧 발판이 내려간다!"
                );
            }

            yield return null;
        }

        // 제한시간 종료 시 타이머 정지
        StopTimerSound();

        if (completed)
            yield break;

        // 발판 하강
        yield return MovePlatforms(false);

        running = false;
        cycleRoutine = null;
    }

    private IEnumerator MovePlatforms(bool rising)
    {
        float duration = Mathf.Max(
            0.01f,
            moveDuration
        );

        float interval = Mathf.Max(
            0f,
            platformInterval
        );

        float totalDuration =
            duration +
            interval * (platforms.Length - 1);

        float elapsed = 0f;

        // 발판이 여러 개여도 시작할 때 한 번만 재생
        AudioManager.Play(
            rising ? platformRiseSound : platformLowerSound,
            soundVolume
        );

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;

            for (int i = 0; i < platforms.Length; i++)
            {
                float delay = interval * i;

                float t = Mathf.Clamp01(
                    (elapsed - delay) / duration
                );

                t = Mathf.SmoothStep(0f, 1f, t);

                Vector3 start = rising
                    ? loweredPositions[i]
                    : raisedPositions[i];

                Vector3 end = rising
                    ? raisedPositions[i]
                    : loweredPositions[i];

                platforms[i].position = Vector3.Lerp(
                    start,
                    end,
                    t
                );
            }

            yield return null;
        }

        SetPlatforms(rising);
    }

   private void PlayTimerSound()
{
    if (timerAudioSource == null)
        return;

    timerAudioSource.Stop();

    if (timerTickSound == null || !AudioManager.SfxUnlocked)
        return;

    timerAudioSource.clip = timerTickSound;
    timerAudioSource.volume =
        timerTickVolume * AudioManager.Volume;

    // 반복하지 않고 딱 한 번만 재생
    timerAudioSource.loop = false;
    timerAudioSource.pitch = 1f;

    timerAudioSource.Play();
}

    // 째깍째깍 타이머 효과음 정지
    private void StopTimerSound()
    {
        if (timerAudioSource != null)
            timerAudioSource.Stop();
    }

    private void SetPlatforms(bool raised)
    {
        for (int i = 0; i < platforms.Length; i++)
        {
            if (platforms[i] == null)
                continue;

            platforms[i].position = raised
                ? raisedPositions[i]
                : loweredPositions[i];
        }
    }

    private void FailAttempt()
    {
        if (completed ||
            respawnPoint == null ||
            player == null)
            return;

        if (cycleRoutine != null)
        {
            StopCoroutine(cycleRoutine);
            cycleRoutine = null;
        }

        running = false;

        // 낙하 즉시 타이머 정지
        StopTimerSound();

        // 기존 발판 위치 초기화
        SetPlatforms(false);

        if (characterController != null)
            characterController.enabled = false;

        player.transform.SetPositionAndRotation(
            respawnPoint.position,
            respawnPoint.rotation
        );

        if (characterController != null)
            characterController.enabled = true;

        PlayerInteractor.ShowHint(
            "다시 클릭커를 눌러 도전해 보자."
        );
    }

    private void CompleteSection()
    {
        if (completed)
            return;

        completed = true;
        running = false;

        if (cycleRoutine != null)
        {
            StopCoroutine(cycleRoutine);
            cycleRoutine = null;
        }

        // 클리어 즉시 타이머 정지
        StopTimerSound();

        // 기존 클리어 후 발판 위에 고정
        SetPlatforms(true);

        PlayerInteractor.ShowHint(
            "무사히 건너왔다."
        );

        onCompleted?.Invoke();

        Debug.Log(
            "시간제한 발판 구간 클리어!"
        );
    }

    private void OnDisable()
    {
        StopTimerSound();
    }

    private bool IsInsideFallArea()
    {
        Vector2 a = new Vector2(
            activationPoint.position.x,
            activationPoint.position.z
        );

        Vector2 b = new Vector2(
            goalPoint.position.x,
            goalPoint.position.z
        );

        Vector2 p = new Vector2(
            player.transform.position.x,
            player.transform.position.z
        );

        Vector2 ab = b - a;

        float t = ab.sqrMagnitude > 0.0001f
            ? Mathf.Clamp01(
                Vector2.Dot(p - a, ab) / ab.sqrMagnitude
            )
            : 0f;

        Vector2 nearest = a + ab * t;

        return Vector2.Distance(
            p,
            nearest
        ) <= fallAreaWidth;
    }

    private float HorizontalDistance(
        Vector3 a,
        Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(a, b);
    }
}
