using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TeamRoulette.UI;

// 침실 엔딩: 일자 복도 끝 전화기 앞 → 전화벨 → 고개를 돌림 → 털썩 주저앉음 → 서서히 두려움 → 눈을 감음 → 미로 복도 씬으로
// 흐름 (Begin()을 부르면 시작. 보통 전화기 앞 TriggerZone의 On Enter에 연결):
//   1) 전화벨이 울림 (ringBeforeLock초 동안은 아직 움직일 수 있음 → "어?" 하는 틈)
//   2) ViewFocus로 몸이 굳으며 전화기 쪽으로 고개를 돌림 (조작 잠김. 돌리는 빠르기는 ViewFocus의 Move Time)
//   3) 잠깐 멈칫(beforeFall) → 다리가 풀려 털썩 주저앉음 (시점이 바닥 쪽으로 떨어지며 살짝 기울고, 쿵 소리 + 화면이 한 번 덜컥)
//   4) 서서히 두려움 (fearTime초): 처음엔 거의 티가 안 나다가 갈수록 빨라지게 쌓임
//        - 심장 소리: 아주 작고 느리게 → 점점 크고 빠르게
//        - 숨소리: 늦게 시작해서 점점 거칠게
//        - 떨림: 처음엔 거의 없다가 점점 크게
//        - 하프톤: 가끔 확 옅어졌다가 → 곧바로 확 짙어지는 깜빡임. 두려울수록 더 자주·세게, 바탕도 점점 짙어짐
//      두려움 독백(fearDialogue)이 있으면 이 동안 같이 나옴 (비워도 됨)
//      다 쌓이면 peakHold초 동안 최고조로 버팀
//   5) 눈을 감음: 시야가 좁아지다 한 번 버티듯 살짝 넓어졌다가 → 끝까지 감음
//      마지막으로 끝까지 감는 동안: 심장 소리가 점점 커지고, 암전 직전에 심장 소리·전화벨이 일그러짐
//   6) 완전히 감기는 순간 전화벨이 뚝 끊김 → 일그러진 심장 소리만 잦아듦 → 하프톤을 원래대로 돌려놓고 → 다음 씬 (nextScene)
// 원리:
//   - 주저앉기·떨림: 매 프레임 마지막(LateUpdate)에 카메라만 움직인다 (기억해 둔 원래 자세 + 주저앉은 정도 + 떨림).
//     ViewFocus는 고개(PlayerCameraRoot)를 붙잡고, 이 스크립트는 그 아래 카메라만 움직여서 서로 안 싸움.
//     주저앉는 높이는 "세상 기준 아래"로 내려서, 고개가 어느 쪽을 보고 있든 바닥 쪽으로 떨어진다.
//   - 두려움 정도(fear)를 0 → 1로 천천히 올리고, 소리·떨림·하프톤이 모두 이 값 하나를 보고 움직인다.
//     fear를 제곱해서 쓰는 곳이 많아서, 처음엔 아주 조용하다가 뒤로 갈수록 확 커진다.
//   - 하프톤: HalftoneController.SetOverride(값)으로 잠깐 덮어씀 → 끝날 때 ClearOverride()로 원래대로.
//     (0 = 가장 짙음, 1 = 깨끗함)
//   - 눈 감기: ① 원형 시야(Iris) ② 눈꺼풀(Lid) 중 넣은 것만 동작
//   - 씬 전환: 화면이 완전히 검을 때 넘기므로 로딩 끊김이 안 보인다.
// 사용: 빈 오브젝트 "PhoneScare"에 붙이고 칸을 채운다. nextScene에 넣은 씬은 File > Build Profiles의 Scene List에 있어야 함.
[DefaultExecutionOrder(1000)]   // ViewFocus 등이 고개를 맞춘 "뒤에" 카메라를 움직이기 위해
public class PhoneScare : MonoBehaviour
{
    [Header("연결")]
    public ViewFocus focus;                  // 전화기를 바라보는 ViewFocus (View Point = 전화기를 보는 눈 자리)
    public AudioSource ring;                 // 전화벨 (전화기에 붙인 AudioSource, Loop 체크, Spatial Blend 1 = 3D)
    public AudioSource heartbeat;            // 심장 소리 (Loop 체크, Spatial Blend 0 = 2D, 비워도 됨)
    public AudioSource breath;               // 거친 숨소리 (Loop 체크, Spatial Blend 0 = 2D, 비워도 됨)
    public DialogueStep fearDialogue;        // 떨면서 나오는 독백 (비워도 됨)
    public RawImage iris;                    // ① 원형 시야: UI/Iris 머티리얼을 쓰는 RawImage (미니게임2와 같은 머티리얼, EyeOpen 스크립트는 붙이지 않음)
    public float irisMax = 1.15f;            //    다 떴을 때 구멍 크기 (1.15면 화면 전체가 보임)
    public RectTransform topLid;             // ② 위 눈꺼풀 (검은 Image, 화면 맨 위에 붙임. 비워도 됨)
    public RectTransform bottomLid;          // ② 아래 눈꺼풀 (검은 Image, 화면 맨 아래에 붙임. 비워도 됨)

    [Header("흐름 (초)")]
    public float ringBeforeLock = 1.5f;      // 벨이 울리고 몸이 굳기까지
    public float beforeFall = 0.8f;          // 고개를 돌린 뒤 멈칫하는 시간 (그다음 주저앉음)
    public float fearTime = 9f;              // 주저앉은 뒤 두려움이 최고조까지 쌓이는 시간
    public float peakHold = 1.5f;            // 최고조에서 버티는 시간 (그다음 눈을 감기 시작)
    public float closeTime = 2.5f;           // 마지막에 끝까지 감는 시간
    public float darkHold = 2f;              // 완전히 감은 뒤 다음 씬까지 (심장 소리만 들림)

    [Header("주저앉기")]
    public float fallDepth = 0.7f;           // 시점이 떨어지는 높이 (m). 0.7 ≈ 무릎 꿇은 높이
    public float fallTime = 0.45f;           // 떨어지는 시간 (초). 짧을수록 털썩
    public float fallPitch = -6f;            // 주저앉은 뒤 고개 각도 (- = 위로 올려다봄, + = 아래로)
    public float fallRoll = 5f;              // 주저앉은 뒤 옆으로 기운 각도 (도)
    public float fallJolt = 2.5f;            // 바닥에 닿는 순간 화면이 덜컥하는 세기 (0이면 없음)
    public AudioClip fallClip;               // 바닥에 닿는 소리 (쿵. 비워 두면 소리 없음)
    [Range(0f, 1f)] public float fallVolume = 0.8f;

    [Header("떨림")]
    public float maxShakeAngle = 1.2f;       // 최대 흔들림 각도 (도)
    public float maxShakeMove = 0.008f;      // 최대 흔들림 거리 (m)
    public float shakeSpeed = 18f;           // 떨리는 빠르기

    [Header("소리")]
    public float heartPitchFrom = 0.85f;     // 심장 소리 처음 빠르기
    public float heartPitchTo = 1.5f;        // 심장 소리 최대 빠르기 (점점 빨라짐)
    [Range(0f, 1f)] public float heartStartVolume = 0.05f;   // 심장 소리 처음 크기 (Sound Volume에 곱함. 작을수록 조용히 시작)
    public float soundVolume = 1f;           // 심장·숨소리 최대 크기

    [Header("하프톤 깜빡임 (0 = 가장 짙음, 1 = 깨끗함)")]
    public bool halftoneFlicker = true;      // 체크 해제하면 하프톤은 건드리지 않음
    public float flickerLight = 0.35f;       // 한 번 깜빡일 때 확 옅어지는 양
    public float flickerDark = 0.45f;        // 곧바로 확 짙어지는 양
    public float flickerSlow = 2.5f;         // 두려움이 작을 때 깜빡이는 간격 (초)
    public float flickerFast = 0.3f;         // 두려움이 최고조일 때 깜빡이는 간격 (초)
    public float flickerRecover = 5f;        // 깜빡인 뒤 바탕으로 돌아오는 빠르기 (클수록 순간적)
    [Range(0f, 1f)] public float darkDrift = 0.6f;   // 두려움이 최고조일 때 바탕이 짙어지는 정도 (0 = 그대로, 1 = 가장 짙게)

    [Header("암전 직전 소리 일그러짐")]
    [Range(0f, 1f)] public float warpStart = 0.4f;   // 마지막 눈 감기의 몇 % 지점부터 일그러지기 시작할지 (0 = 처음부터, 0.4 = 40%부터)
    public float ringPitchEnd = 0.5f;        // 다 감기 직전 전화벨 높낮이 (낮을수록 늘어지고 괴상함)
    public float heartPitchEnd = 0.55f;      // 다 감기 직전 심장 소리 높낮이 (낮을수록 무겁고 느리게 쿵…쿵…)
    public float wobble = 0.08f;             // 높낮이가 출렁이는 폭 (0이면 출렁임 없음)
    public float wobbleSpeed = 3f;           // 출렁이는 빠르기
    public float muffleCutoff = 700f;        // 다 감기 직전 먹먹함 (Hz, 낮을수록 물속처럼 먹먹. 22000이면 안 먹먹함)
    [Range(0f, 1f)] public float heartEndVolume = 1f;   // 다 감기 직전 심장 소리 크기 (Sound Volume에 곱함)
    [Range(0f, 1f)] public float ringEndVolume = 1f;    // 다 감기 직전 전화벨 크기 (원래 크기에 곱함)
    public bool fadeHeartInDark = true;      // 체크: 암전 뒤 심장 소리가 Dark Hold 동안 서서히 사라짐 / 해제: 씬이 넘어갈 때까지 그대로

    [Header("다음")]
    public string nextScene = "";            // 넘어갈 씬 이름 (예: Corridor). 비워 두면 씬은 안 넘기고 onClosed만
    public float arriveFadeTime = 2f;        // 다음 씬에서 까만 화면이 걷히는 시간 (초)
    public UnityEvent onClosed;              // 눈을 완전히 감은 뒤 (비워도 됨)

    [Header("확인용 (Play 중 값 보기)")]
    [Range(0f, 1f)] public float fear;       // 지금 두려움 정도 (0~1)

    bool started;
    Coroutine fearRoutine;                   // 두려움을 키우는 중 (마지막 눈 감기 때 멈춤)
    bool fearDone;
    float shake;                             // 지금 떨림 세기 0~1
    float jolt;                              // 바닥에 닿는 순간의 덜컥 (점점 사라짐)
    float fallDrop, fallTilt;                // 주저앉은 정도 (0 = 서 있음, 1 = 다 앉음)
    Transform cam;
    Vector3 camPos;                          // 움직이기 전 카메라 위치·각도 (부모 기준)
    Quaternion camRot;
    Material irisMat;                        // 원형 시야 머티리얼 복사본 (원본 파일을 건드리지 않게)
    static readonly int RadiusId = Shader.PropertyToID("_Radius");
    static readonly int AspectId = Shader.PropertyToID("_Aspect");

    // 하프톤 깜빡임
    bool flickering;
    bool usedOverride;                       // 하프톤을 덮어쓴 적 있음 (끝날 때 꼭 되돌리기)
    float normalBlend;                       // 연출 시작 전 원래 하프톤 값
    float kick;                              // 지금 깜빡임 (+ 옅어짐 / - 짙어짐, 점점 0으로)

    void Awake()
    {
        if (iris != null)
        {
            irisMat = new Material(iris.material);
            iris.material = irisMat;
            iris.raycastTarget = false;      // 클릭을 가로막지 않게
        }
        SetLids(0f);
    }

    [ContextMenu("테스트: 시작 (Begin)")]
    public void Begin()
    {
        if (started) return;
        started = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // 1) 전화벨
        if (ring != null) ring.Play();
        yield return new WaitForSeconds(ringBeforeLock);

        // 2) 몸이 굳으며 전화기 쪽으로 고개를 돌림 (ViewFocus가 조작을 끔)
        if (focus != null)
        {
            focus.Focus();
            yield return new WaitForSeconds(focus.moveTime);
        }

        // 카메라 원래 자세 기억 (이 자세를 기준으로 주저앉기·떨림을 더함)
        if (Camera.main != null)
        {
            cam = Camera.main.transform;
            camPos = cam.localPosition;
            camRot = cam.localRotation;
        }

        // 3) 멈칫 → 털썩 주저앉음
        yield return new WaitForSeconds(beforeFall);
        yield return Fall();

        // 4) 서서히 두려움 (+ 독백이 있으면 같이)
        StartLoop(heartbeat);
        StartLoop(breath);
        fearDone = false;
        fearRoutine = StartCoroutine(BuildFear());
        if (halftoneFlicker) StartFlicker();

        yield return Wait(fearDialogue);
        while (!fearDone) yield return null;                 // 두려움이 다 쌓일 때까지
        yield return new WaitForSeconds(peakHold);            // 최고조에서 버팀

        // 5) 눈을 감음: 내려오다 → 한 번 버티듯 살짝 뜨고 → 끝까지
        yield return Lids(0f, 0.55f, 0.8f);
        yield return Lids(0.55f, 0.3f, 0.5f);
        yield return CloseWithWarp();

        // 6) 완전히 감은 순간 전화벨이 뚝 끊김 → 숨소리 잦아들고 (일그러진) 심장 소리만
        if (ring != null) ring.Stop();
        shake = 0f;
        flickering = false;                                   // 화면이 캄캄하니 깜빡임도 멈춤
        StartCoroutine(FadeOut(breath, 0.6f));
        if (fadeHeartInDark) StartCoroutine(FadeOut(heartbeat, darkHold));
        yield return new WaitForSeconds(darkHold);

        // 하프톤을 원래(해금 기준)대로 → 다음 씬에 깜빡이던 값이 남지 않게
        if (usedOverride) { HalftoneController.ClearOverride(); usedOverride = false; }

        onClosed.Invoke();
        if (!string.IsNullOrEmpty(nextScene))
        {
            // 이미 눈을 다 감아 캄캄하므로, 까만 채로 넘어가서 새 씬에서 서서히 밝아짐 (눈을 뜨는 느낌)
            if (Application.CanStreamedLevelBeLoaded(nextScene)) SceneFader.Go(nextScene, 0f, arriveFadeTime, 0f, startBlack: true);
            else Debug.LogWarning("PhoneScare: '" + nextScene + "' 씬을 불러올 수 없어요. 이름이 맞는지, File > Build Profiles의 Scene List에 들어 있는지 확인하세요.", this);
        }
    }

    // 털썩 주저앉기: 점점 빨라지며 떨어짐(중력 느낌) → 바닥에 닿는 순간 쿵 + 덜컥 → 살짝 튕겼다가 자리 잡음
    IEnumerator Fall()
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(fallTime, 0.01f))
        {
            fallDrop = t * t;                                 // 처음엔 천천히, 갈수록 빠르게
            fallTilt = Mathf.SmoothStep(0f, 1f, t);
            yield return null;
        }
        fallDrop = 1f;
        fallTilt = 1f;

        AudioManager.Play(fallClip, fallVolume);               // 쿵
        jolt = 1f;                                             // 덜컥

        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
        {
            fallDrop = 1f - Mathf.Sin(t * Mathf.PI) * 0.06f;   // 아주 살짝 튕겼다가 자리 잡음
            yield return null;
        }
        fallDrop = 1f;
    }

    // 두려움을 fearTime 동안 0 → 1로. 소리·떨림은 이 값을 보고 움직임
    IEnumerator BuildFear()
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(fearTime, 0.01f))
        {
            fear = t;
            shake = fear * fear;                               // 떨림: 처음엔 거의 없다가 뒤에서 커짐
            if (heartbeat != null)
            {
                heartbeat.pitch = Mathf.Lerp(heartPitchFrom, heartPitchTo, fear * fear);
                heartbeat.volume = Mathf.Lerp(heartStartVolume, 1f, fear) * soundVolume;
            }
            if (breath != null) breath.volume = Mathf.Pow(fear, 1.5f) * soundVolume;   // 숨소리는 늦게 올라옴
            yield return null;
        }
        fear = 1f;
        shake = 1f;
        fearDone = true;
    }

    // ===== 하프톤 깜빡임 =====

    void StartFlicker()
    {
        normalBlend = HalftoneController.CurrentValue;         // 지금 해금 기준 하프톤 값
        kick = 0f;
        flickering = true;
        usedOverride = true;
        StartCoroutine(FlickerRoutine());
    }

    // 가끔 "확 옅어졌다가 → 곧바로 확 짙어짐". 두려울수록 간격이 짧고 세기가 셈
    IEnumerator FlickerRoutine()
    {
        while (flickering)
        {
            float wait = Mathf.Lerp(flickerSlow, flickerFast, fear) * Random.Range(0.6f, 1.4f);   // 불규칙하게
            yield return new WaitForSeconds(wait);
            if (!flickering) yield break;

            float power = Mathf.Lerp(0.3f, 1f, fear);
            kick = flickerLight * power;                        // 확 옅어짐
            yield return new WaitForSeconds(Random.Range(0.06f, 0.14f));
            kick = -flickerDark * power;                        // 곧바로 확 짙어짐
        }
    }

    void Update()
    {
        if (!flickering) return;
        kick = Mathf.Lerp(kick, 0f, 1f - Mathf.Exp(-flickerRecover * Time.deltaTime));   // 바탕으로 돌아옴
        float baseValue = Mathf.Lerp(normalBlend, 0f, darkDrift * fear);                  // 두려울수록 바탕이 짙어짐
        HalftoneController.SetOverride(baseValue + kick);
    }

    // ===== 눈 감기 + 소리 일그러짐 =====

    // 마지막으로 끝까지 눈을 감는 동안 (closeTime):
    //   - 심장 소리는 처음부터 점점 커짐
    //   - warpStart 지점부터 전화벨·심장 소리가 일그러짐: 높낮이가 낮아지고(늘어짐) + 출렁이고 + 먹먹해짐
    //   먹먹함은 각 소리에 Audio Low Pass Filter를 붙여서(없으면 자동으로 붙임) 높은 소리를 깎아 낸다.
    IEnumerator CloseWithWarp()
    {
        if (fearRoutine != null) StopCoroutine(fearRoutine);     // 심장 빠르기를 키우던 것과 섞이지 않게
        fear = 1f;
        shake = 1f;
        AudioLowPassFilter ringFilter = Muffler(ring);
        AudioLowPassFilter heartFilter = Muffler(heartbeat);

        float ringPitch0 = ring != null ? ring.pitch : 1f;
        float ringVol0 = ring != null ? ring.volume : 1f;
        float heartPitch0 = heartbeat != null ? heartbeat.pitch : 1f;
        float heartVol0 = heartbeat != null ? heartbeat.volume : 1f;

        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(closeTime, 0.01f))
        {
            SetLids(Mathf.Lerp(0.3f, 1f, Mathf.SmoothStep(0f, 1f, t)));

            float w = Mathf.InverseLerp(warpStart, 1f, t);       // 일그러짐 정도 0~1
            w *= w;                                             // 처음엔 살짝, 끝으로 갈수록 확
            float wob = (Mathf.PerlinNoise(Time.time * wobbleSpeed, 0.5f) * 2f - 1f) * wobble * w;
            float cutoff = 22000f * Mathf.Pow(Mathf.Max(muffleCutoff, 10f) / 22000f, w);   // 귀가 느끼는 대로 고르게 줄어들도록 비율로

            if (ring != null)
            {
                ring.pitch = Mathf.Lerp(ringPitch0, ringPitchEnd, w) + wob;
                ring.volume = Mathf.Lerp(ringVol0, ringVol0 * ringEndVolume, w);
            }
            if (heartbeat != null)
            {
                heartbeat.pitch = Mathf.Lerp(heartPitch0, heartPitchEnd, w) + wob;
                heartbeat.volume = Mathf.Lerp(heartVol0, heartEndVolume * soundVolume, t);   // 심장은 처음부터 점점 커짐
            }
            if (ringFilter != null) ringFilter.cutoffFrequency = cutoff;
            if (heartFilter != null) heartFilter.cutoffFrequency = cutoff;
            yield return null;
        }
        SetLids(1f);
    }

    // 소리에 먹먹함 필터를 붙임 (이미 있으면 그걸 씀)
    AudioLowPassFilter Muffler(AudioSource source)
    {
        if (source == null) return null;
        AudioLowPassFilter f = source.GetComponent<AudioLowPassFilter>();
        if (f == null) f = source.gameObject.AddComponent<AudioLowPassFilter>();
        f.cutoffFrequency = 22000f;
        return f;
    }

    // 매 프레임 마지막에 카메라 자세를 다시 계산: 원래 자세 + 주저앉은 정도 + 떨림(펄린 노이즈)
    void LateUpdate()
    {
        if (cam == null) return;

        jolt = Mathf.MoveTowards(jolt, 0f, Time.deltaTime / 0.35f);
        float s = shake + jolt * fallJolt;                     // 떨림 + 바닥에 닿는 순간의 덜컥

        float t = Time.time * shakeSpeed;
        float nx = Mathf.PerlinNoise(t, 0.1f) * 2f - 1f;
        float ny = Mathf.PerlinNoise(0.3f, t) * 2f - 1f;
        float nz = Mathf.PerlinNoise(t, 0.7f) * 2f - 1f;

        cam.localRotation = camRot * Quaternion.Euler(
            fallPitch * fallTilt + nx * maxShakeAngle * s,
            ny * maxShakeAngle * s,
            fallRoll * fallTilt + nz * maxShakeAngle * 0.5f * s);
        cam.localPosition = camPos + new Vector3(ny, nx, 0f) * (maxShakeMove * s);
        cam.position += Vector3.down * (fallDepth * fallDrop);  // 세상 기준 아래로 (고개 방향과 상관없이 바닥 쪽)
    }

    void RestoreCamera()
    {
        if (cam == null) return;
        cam.localPosition = camPos;
        cam.localRotation = camRot;
    }

    void OnDisable()
    {
        RestoreCamera();
        if (usedOverride) { HalftoneController.ClearOverride(); usedOverride = false; }
    }

    // 눈 감기: 0 = 다 뜸, 1 = 다 감음
    IEnumerator Lids(float from, float to, float time)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(time, 0.01f))
        {
            SetLids(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)));
            yield return null;
        }
        SetLids(to);
    }

    // 눈 감은 정도 k (0 = 다 뜸, 1 = 다 감음)를 원형 시야·눈꺼풀에 반영
    void SetLids(float k)
    {
        if (irisMat != null)
        {
            irisMat.SetFloat(RadiusId, (1f - k) * irisMax);   // 구멍이 작아질수록 캄캄
            irisMat.SetFloat(AspectId, (float)Screen.width / Screen.height);
        }
        SetLid(topLid, k);
        SetLid(bottomLid, k);
    }

    void SetLid(RectTransform lid, float k)
    {
        if (lid == null) return;
        RectTransform parent = lid.parent as RectTransform;
        float half = parent != null ? parent.rect.height * 0.5f : Screen.height * 0.5f;
        Vector2 size = lid.sizeDelta;
        size.y = half * k * 1.02f;
        lid.sizeDelta = size;
    }

    void StartLoop(AudioSource source)
    {
        if (source == null) return;
        source.loop = true;
        source.volume = 0f;
        source.Play();
    }

    IEnumerator FadeOut(AudioSource source, float time)
    {
        if (source == null) yield break;
        float from = source.volume;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(time, 0.01f))
        {
            source.volume = Mathf.Lerp(from, 0f, t);
            yield return null;
        }
        source.Stop();
    }

    // 대사가 실제로 뜬 뒤 → 완전히 닫힐 때까지 기다림 (DustyFrame과 같은 방식)
    IEnumerator Wait(DialogueStep step)
    {
        if (step == null) yield break;
        GameUI ui = FindFirstObjectByType<GameUI>();
        step.Play();
        for (float t = 0f; t < 2f; t += Time.unscaledDeltaTime)
        {
            if (step.IsPlaying || (ui != null && ui.IsDialogueOpen)) break;
            yield return null;
        }
        while (step.IsPlaying || (ui != null && (ui.IsDialogueOpen || ui.IsPaused))) yield return null;
    }
}