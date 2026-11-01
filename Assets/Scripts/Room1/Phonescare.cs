using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TeamRoulette.UI;

// 침실 엔딩: 일자 복도 끝 전화기 앞 → 전화벨 → 몸이 굳고 떨림 + 심장·숨소리 → 눈을 감음 → 미로 복도 씬으로
// 흐름 (Begin()을 부르면 시작. 보통 전화기 앞 TriggerZone의 On Enter에 연결):
//   1) 전화벨이 울림 (ringBeforeLock초 동안은 아직 움직일 수 있음 → "어?" 하는 틈)
//   2) ViewFocus로 몸이 굳으며 전화기를 바라봄 (조작 잠김)
//   3) 화면 떨림이 점점 커지고, 심장 소리가 점점 빨라지고, 숨소리가 점점 커짐
//   4) 두려움 독백 (fearDialogue)
//   5) 눈을 감음: 시야가 좁아지다 한 번 버티듯 살짝 넓어졌다가 → 끝까지 감음
//   6) 완전히 감기는 순간 전화벨이 뚝 끊김 → 심장 소리만 잠깐 → 다음 씬 (nextScene)
// 원리:
//   - 떨림: 매 프레임 마지막(LateUpdate)에 카메라의 위치·각도를 작게 흔든다. 펄린 노이즈라 덜덜 떨리는 느낌.
//     ViewFocus는 고개(PlayerCameraRoot)를 붙잡고, 이 스크립트는 그 아래 카메라만 흔들어서 서로 안 싸움.
//   - 눈 감기: 두 방식 중 하나 (둘 다 넣으면 둘 다 동작)
//       ① 원형 시야(Iris): 미니게임2와 같은 UI/Iris 머티리얼. 둥근 구멍이 점점 작아지며 캄캄해짐 (게임 안에서 "눈"을 표현하는 방식이 통일됨)
//       ② 눈꺼풀(Lid): 화면 UI의 검은 이미지 2장(위·아래)의 높이를 0 → 화면 절반까지 늘림
//   - 씬 전환: 화면이 완전히 검을 때 넘기므로 로딩 끊김이 안 보인다.
// 사용: 빈 오브젝트 "PhoneScare"에 붙이고 칸을 채운다. nextScene에 넣은 씬은 File > Build Profiles의 Scene List에 있어야 함.
[DefaultExecutionOrder(1000)]   // ViewFocus 등이 고개를 맞춘 "뒤에" 카메라를 흔들기 위해
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
    public float shakeBuildTime = 4f;        // 떨림·심장 소리가 최대가 되기까지
    public float afterDialogue = 1f;         // 독백이 끝나고 눈을 감기 시작하기까지
    public float closeTime = 2.5f;           // 마지막에 끝까지 감는 시간
    public float darkHold = 2f;              // 완전히 감은 뒤 다음 씬까지 (심장 소리만 들림)

    [Header("떨림")]
    public float maxShakeAngle = 1.2f;       // 최대 흔들림 각도 (도)
    public float maxShakeMove = 0.008f;      // 최대 흔들림 거리 (m)
    public float shakeSpeed = 18f;           // 떨리는 빠르기

    [Header("소리")]
    public float heartPitchFrom = 0.9f;      // 심장 소리 처음 빠르기
    public float heartPitchTo = 1.5f;        // 심장 소리 최대 빠르기 (점점 빨라짐)
    public float soundVolume = 1f;           // 심장·숨소리 최대 크기

    [Header("다음")]
    public string nextScene = "";            // 넘어갈 씬 이름 (예: Hallway_Temp). 비워 두면 씬은 안 넘기고 onClosed만
    public UnityEvent onClosed;              // 눈을 완전히 감은 뒤 (비워도 됨)

    bool started;
    float shake;                             // 지금 떨림 세기 0~1
    Transform cam;
    Vector3 camPos;                          // 흔들기 전 카메라 위치·각도 (부모 기준)
    Quaternion camRot;
    Material irisMat;                        // 원형 시야 머티리얼 복사본 (원본 파일을 건드리지 않게)
    static readonly int RadiusId = Shader.PropertyToID("_Radius");
    static readonly int AspectId = Shader.PropertyToID("_Aspect");

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

        // 2) 몸이 굳으며 전화기를 바라봄 (ViewFocus가 조작을 끔)
        if (focus != null)
        {
            focus.Focus();
            yield return new WaitForSeconds(focus.moveTime);
        }

        // 3) 떨림 시작 (흔들기 전 카메라 자세 저장) + 심장·숨소리
        if (Camera.main != null)
        {
            cam = Camera.main.transform;
            camPos = cam.localPosition;
            camRot = cam.localRotation;
        }
        StartLoop(heartbeat);
        StartLoop(breath);
        StartCoroutine(BuildFear());

        // 4) 두려움 독백
        yield return Wait(fearDialogue);
        yield return new WaitForSeconds(afterDialogue);

        // 5) 눈을 감음: 내려오다 → 한 번 버티듯 살짝 뜨고 → 끝까지
        yield return Lids(0f, 0.55f, 0.8f);
        yield return Lids(0.55f, 0.3f, 0.5f);
        yield return Lids(0.3f, 1f, closeTime);

        // 6) 완전히 감은 순간 전화벨이 뚝 끊김 → 숨소리 잦아들고 심장 소리만
        if (ring != null) ring.Stop();
        shake = 0f;
        RestoreCamera();
        StartCoroutine(FadeOut(breath, 0.6f));
        yield return new WaitForSeconds(darkHold);

        onClosed.Invoke();
        if (!string.IsNullOrEmpty(nextScene))
        {
            if (Application.CanStreamedLevelBeLoaded(nextScene)) SceneManager.LoadScene(nextScene);
            else Debug.LogWarning("PhoneScare: '" + nextScene + "' 씬을 불러올 수 없어요. 이름이 맞는지, File > Build Profiles의 Scene List에 들어 있는지 확인하세요.", this);
        }
    }

    // 떨림·심장 빠르기·숨소리 크기를 shakeBuildTime 동안 점점 키움
    IEnumerator BuildFear()
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(shakeBuildTime, 0.01f))
        {
            shake = Mathf.SmoothStep(0f, 1f, t);
            if (heartbeat != null)
            {
                heartbeat.pitch = Mathf.Lerp(heartPitchFrom, heartPitchTo, shake);
                heartbeat.volume = Mathf.Lerp(0.3f, 1f, shake) * soundVolume;
            }
            if (breath != null) breath.volume = shake * soundVolume;
            yield return null;
        }
        shake = 1f;
    }

    // 매 프레임 마지막에 카메라를 작게 흔듦 (펄린 노이즈 = 덜덜 떨리는 느낌)
    void LateUpdate()
    {
        if (cam == null || shake <= 0f) return;
        float t = Time.time * shakeSpeed;
        float nx = Mathf.PerlinNoise(t, 0.1f) * 2f - 1f;
        float ny = Mathf.PerlinNoise(0.3f, t) * 2f - 1f;
        float nz = Mathf.PerlinNoise(t, 0.7f) * 2f - 1f;
        cam.localRotation = camRot * Quaternion.Euler(nx * maxShakeAngle * shake, ny * maxShakeAngle * shake, nz * maxShakeAngle * 0.5f * shake);
        cam.localPosition = camPos + new Vector3(ny, nx, 0f) * (maxShakeMove * shake);
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