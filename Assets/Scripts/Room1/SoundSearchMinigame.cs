using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 미니게임 1: 소리 근원 찾기 (여러 번 반복)
// 처음부터 개 짖는 소리가 먹먹하게 반복됨
// 마우스를 누른 채 움직이면 커서 자리에서 파동이 퍼짐. 목표에 가까울수록 촘촘하고 소리가 커짐
// 목표 위에서 계속 누르면 게이지가 차고, 찾을 때마다 짖는 소리가 한 단계씩 또렷해짐
// 마지막까지 찾으면 효과음(SFX) 해금
public class SoundSearchMinigame : MonoBehaviour
{
    [Header("UI 연결")]
    public RectTransform rippleContainer;   // Ripples
    public Image ripplePrefab;              // Assets/Prefabs/UI/Ripple
    public Image holdGauge;                 // HoldGauge
    public Image hint;                      // Hint

    [Header("소리 연결")]
    public AudioClip barkClip;              // 개 짖는 소리 (짧은 한두 번 짖음)
    public AudioSource humSource;           // 웅웅 소리 (보조, 비워도 됨)
    public AudioLowPassFilter humFilter;    // 같은 오브젝트의 소리를 모두 먹먹하게 함

    [Header("짖는 소리")]
    public float barkIntervalMin = 1.2f;    // 짖는 간격 (초)
    public float barkIntervalMax = 2.2f;
    public bool keepBarkingAfterClear = true;   // 클리어 후에도 계속 짖기 (미니게임 2로 이어짐)

    [Header("목표")]
    [Tooltip("회차별 목표 위치 (0~1). 개수 = 찾는 횟수")]
    public Vector2[] targetPositions = { new Vector2(0.2f, 0.5f), new Vector2(0.75f, 0.3f), new Vector2(0.55f, 0.8f) };
    [Tooltip("목표 반경 (화면 높이 대비)")]
    public float targetRadius = 0.08f;
    [Tooltip("이 거리보다 멀면 가장 드문드문 (화면 높이 대비)")]
    public float maxRange = 0.9f;

    [Header("또렷함 (Hz, 낮을수록 먹먹)")]
    public float mutedCutoff = 400f;        // 처음
    public float clearCutoff = 22000f;      // 마지막 (필터가 없는 것과 같음)
    public float roundPause = 1.5f;         // 찾은 뒤 다음 회차까지 쉬는 시간

    [Header("파동")]
    public float farInterval = 0.8f;        // 멀 때 파동 간격(초)
    public float nearInterval = 0.08f;      // 가까울 때 파동 간격(초)
    public float rippleLife = 0.6f;         // 파동 하나가 퍼졌다 사라지는 시간

    [Header("게이지 / 힌트")]
    public float holdTime = 2f;             // 목표 위에서 눌러야 하는 시간
    public float drainSpeed = 2f;           // 게이지가 줄어드는 속도 (초당)
    public float hintDelay = 30f;           // 회차마다 힌트가 나오기까지 시간

    [Header("누르기 안내")]
    public Image pressHint;                 // 마우스 아이콘 (비워도 됨)
    public float pressHintDelay = 3f;       // 안 누르고 이 시간이 지나면 표시
    public float pressHintCycle = 4f;       // 나타났다 사라지는 한 번의 길이 (초)

    [Header("기타")]
    public bool playOnStart = true;
    [Tooltip("미니게임 동안 꺼둘 것 (PlayerInteractor, 조준점 등)")]
    public Behaviour[] disableWhilePlaying;
    [Tooltip("클리어 후 실행할 것 (나중에 미니게임 2 시작 연결)")]
    public UnityEvent onCleared;

    bool playing;
    bool waiting;       // 회차 사이 쉬는 중
    bool barking;
    bool everPressed;   // 한 번이라도 눌렀는지
    int round;          // 지금까지 찾은 횟수
    float spawnTimer;
    float barkTimer;
    float barkVolume = 0.4f;
    float gauge;
    float elapsed;
    AudioSource barkSource;

    int Rounds => targetPositions.Length;

    void Start()
    {
        holdGauge.fillAmount = 0f;
        SetAlpha(hint, 0f);
        if (pressHint != null) SetAlpha(pressHint, 0f);
        // 짖는 소리를 같은 오브젝트에 두어 로우패스 필터를 같이 거치게 함
        barkSource = gameObject.AddComponent<AudioSource>();
        barkSource.playOnAwake = false;
        if (playOnStart) Begin();
    }

    public void Begin()
    {
        playing = true;
        round = 0;
        foreach (var b in disableWhilePlaying) if (b != null) b.enabled = false;
        if (humSource != null && humSource.clip != null && !humSource.isPlaying) humSource.Play();
        if (humFilter != null) humFilter.cutoffFrequency = mutedCutoff;
        barking = barkClip != null;
        barkTimer = 0.5f;   // 시작하고 잠깐 뒤 첫 짖음
        StartRound();
    }

    // 미니게임 2 등에서 짖는 소리를 멈출 때 호출
    public void StopBarking() => barking = false;

    void StartRound()
    {
        waiting = false;
        elapsed = 0f;
        gauge = 0f;
        hint.rectTransform.anchoredPosition = ScreenToLocal(TargetScreenPos());
    }

    void Update()
    {
        if (barking) TickBark();
        if (!playing) return;

        // 커서가 보이고 자유롭게 움직이게 (다른 스크립트가 잠가도 매 프레임 풀어줌)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (waiting) return;
        elapsed += Time.deltaTime;

        Vector2 mouse = Input.mousePosition;
        float dist = Vector2.Distance(mouse, TargetScreenPos()) / Screen.height;   // 화면 높이 기준 거리
        float closeness = 1f - Mathf.Clamp01((dist - targetRadius) / (maxRange - targetRadius)); // 0 멀다 ~ 1 가깝다
        bool holding = Input.GetMouseButton(0);
        bool onTarget = dist <= targetRadius;

        // 0. 누르기 안내
        UpdatePressHint(holding);

        // 1. 파동: 누르고 있는 동안, 가까울수록 짧은 간격으로 발산
        if (holding)
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f)
            {
                SpawnRipple(mouse, closeness);
                spawnTimer = Mathf.Lerp(farInterval, nearInterval, closeness * closeness);
            }
        }
        else spawnTimer = 0f;   // 누르는 순간 바로 하나 나오게

        // 2. 게이지: 목표 위에서 누르고 있으면 차고, 아니면 빠르게 줄어듦
        if (holding && onTarget) gauge += Time.deltaTime / holdTime;
        else gauge -= Time.deltaTime * drainSpeed;
        gauge = Mathf.Clamp01(gauge);
        holdGauge.fillAmount = gauge;
        holdGauge.rectTransform.anchoredPosition = ScreenToLocal(mouse) + new Vector2(0f, 70f);

        // 3. 소리: 누르고 가까울수록 크고, 회차가 갈수록 또렷하게
        UpdateSound(holding ? closeness : 0f);

        // 4. 힌트: 오래 못 찾으면 목표 쪽에 희미한 빛
        if (elapsed > hintDelay)
            SetAlpha(hint, 0.15f + 0.1f * Mathf.Sin(Time.time * 2f));

        if (gauge >= 1f) StartCoroutine(RoundCleared());
    }

    // 안 누르고 있으면 커서 옆에 마우스 아이콘이 스르륵 나타났다 사라짐. 처음 누르면 영영 숨김
    // 안 누르고 있으면 화면 중앙에 마우스 아이콘이 스르륵 나타났다 사라짐. 처음 누르면 영영 숨김
    void UpdatePressHint(bool holding)
    {
        if (pressHint == null || everPressed) return;
        if (holding) { everPressed = true; SetAlpha(pressHint, 0f); return; }

        float t = elapsed - pressHintDelay;
        if (t < 0f) return;
        float p = (t % pressHintCycle) / pressHintCycle;                 // 한 주기 안에서 0~1
        float a = p < 0.6f ? Mathf.Sin(p / 0.6f * Mathf.PI) : 0f;        // 앞 60%: 나타났다 사라짐, 뒤 40%: 쉼
        SetAlpha(pressHint, a * 0.8f);
    }

    // 일정하지 않은 간격으로 계속 짖음
    void TickBark()
    {
        barkTimer -= Time.deltaTime;
        if (barkTimer > 0f) return;
        barkSource.PlayOneShot(barkClip, barkVolume);
        barkTimer = Random.Range(barkIntervalMin, barkIntervalMax);
    }

    void UpdateSound(float closeness)
    {
        barkVolume = Mathf.Lerp(0.4f, 1f, closeness);
        // 이번 회차의 또렷함 범위 안에서, 가까울수록 더 또렷하게
        if (humFilter != null)
            humFilter.cutoffFrequency = Cutoff((round + closeness) / Rounds);

        if (humSource == null) return;
        float targetVol = Mathf.Lerp(0.25f, 1f, closeness);
        float targetPitch = Mathf.Lerp(0.9f, 1.2f, closeness) + gauge * 0.4f;
        humSource.volume = Mathf.MoveTowards(humSource.volume, targetVol, Time.deltaTime * 2f);
        humSource.pitch = Mathf.MoveTowards(humSource.pitch, targetPitch, Time.deltaTime * 2f);
    }

    // 0 = 가장 먹먹, 1 = 또렷. 귀가 느끼는 대로 고르게 바뀌도록 비율로 계산
    float Cutoff(float t) => mutedCutoff * Mathf.Pow(clearCutoff / mutedCutoff, Mathf.Clamp01(t));

    IEnumerator RoundCleared()
    {
        waiting = true;
        round++;
        gauge = 0f;
        holdGauge.fillAmount = 0f;
        SetAlpha(hint, 0f);
        float clarity = (float)round / Rounds;

        // 한 단계 또렷해진 소리로 바로 한 번 짖음
        if (humFilter != null) humFilter.cutoffFrequency = Cutoff(clarity);
        if (humSource != null) humSource.volume = 0.1f;
        barkVolume = 1f;
        if (barking) { barkSource.PlayOneShot(barkClip, barkVolume); barkTimer = barkIntervalMax; }

        if (round < Rounds)
        {
            yield return new WaitForSeconds(roundPause);
            StartRound();
            yield break;
        }

        // 마지막: 웅웅 소리가 사라지고 또렷한 짖는 소리만 남음
        playing = false;
        float startVol = humSource != null ? humSource.volume : 0f;
        float t = 0f;
        while (t < 1.5f)
        {
            t += Time.deltaTime;
            if (humSource != null) humSource.volume = Mathf.Lerp(startVol, 0f, t / 1.5f);
            yield return null;
        }
        if (humSource != null) humSource.Stop();
        if (!keepBarkingAfterClear) barking = false;

        if (OptionState.Instance != null) OptionState.Instance.Unlock(OptionType.SFX);   // 알림은 UnlockNotice가 자동 표시
        onCleared.Invoke();
        // 눈은 아직 감은 상태 → Blackout 유지 (미니게임 2에서 처리)
    }

    void SpawnRipple(Vector2 screenPos, float closeness)
    {
        Image r = Instantiate(ripplePrefab, rippleContainer);
        r.rectTransform.anchoredPosition = ScreenToLocal(screenPos);
        StartCoroutine(AnimateRipple(r, closeness));
    }

    // 파동 하나: 작게 시작해 커지며 사라짐. 가까울수록 크고 밝게
    IEnumerator AnimateRipple(Image r, float closeness)
    {
        float startAlpha = Mathf.Lerp(0.25f, 0.9f, closeness);
        float endScale = Mathf.Lerp(1f, 2.5f, closeness);
        float t = 0f;
        while (t < rippleLife)
        {
            t += Time.deltaTime;
            float p = t / rippleLife;
            r.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.2f, endScale, p);
            SetAlpha(r, Mathf.Lerp(startAlpha, 0f, p));
            yield return null;
        }
        Destroy(r.gameObject);
    }

    Vector2 TargetScreenPos()
    {
        Vector2 p = targetPositions[Mathf.Min(round, Rounds - 1)];
        return new Vector2(p.x * Screen.width, p.y * Screen.height);
    }

    // 화면 좌표(픽셀) → UI 좌표
    Vector2 ScreenToLocal(Vector2 screenPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rippleContainer, screenPos, null, out Vector2 local);
        return local;
    }

    static void SetAlpha(Image img, float a)
    {
        Color c = img.color; c.a = a; img.color = c;
    }
}