using UnityEngine;
using UnityEngine.UI;

// 미니게임 2 - 눈 (원형 시야)
// 평소에는 눈을 감고 있다 (캄캄함).
// Peek(): 빠르게 눈을 떴다가(openTime) → 잠깐 보고(holdTime) → 다시 감는다(closeTime). DreamTurn이 클릭할 때 부른다.
// ForceOpen(): 끝까지 뜬 채로 유지하고, 좁던 시야가 천천히 화면 전체로 트인다 (마지막으로 개를 찾았을 때).
// 평소 잠깐 뜰 때는 좁은 구멍(peekRadius)만 보인다 → 소리로 방향을 먼저 맞춰야 개가 보인다.
// OpenSlow(시간): 처음 꿈에 들어왔을 때 천천히 눈을 떠서 공간을 보여 준다 (조금 넓게, introRadius). CloseEye() 할 때까지 유지.
// CloseEye(): 뜬 눈을 감는다.
// ResetEye(): 캄캄한 상태로 처음부터 (Minigame2.Begin이 부른다).
//   ※ OnEnable에서 초기화하지 않는 이유: 공용 UI(GameUI)가 대사·설정창 동안 이 스크립트를 껐다 켜는데,
//     켜질 때마다 초기화하면 ESC를 눌렀다 닫기만 해도 눈이 감겨 버린다.
// 다른 스크립트는 Open(0~1)과 IsBusy(눈을 뜨거나 감는 중인지)를 읽는다.
public class EyeOpen : MonoBehaviour
{
    public RawImage iris;                    // UI/Iris 머티리얼을 쓰는 RawImage

    [Header("눈 뜨고 감기 (초)")]
    public float openTime = 0.35f;           // 감은 상태 → 다 뜰 때까지
    public float holdTime = 1.5f;            // 다 뜬 채로 보는 시간
    public float closeTime = 0.5f;           // 다 뜬 상태 → 다시 감을 때까지

    [Header("떨림: 눈을 뜬 동안 시야가 미세하게 떨림")]
    public float trembleAmount = 0.03f;      // 떨리는 폭 (0이면 끔)
    public float trembleSpeed = 9f;          // 떨리는 빠르기

    [Header("구멍 크기 (화면 높이 기준, 0.5 = 위아래 끝에 닿음)")]
    public float introRadius = 0.45f;        // 처음 천천히 눈 뜰 때 구멍 크기 (공간을 보여 주려고 조금 넓게)
    public float peekRadius = 0.25f;         // 잠깐 뜰 때 구멍 크기. 좁을수록 소리로 정확히 맞춰야 보임
    public float maxRadius = 1.15f;          // 마지막에 다 트였을 때 (1.15면 화면 전체가 보임)
    public float widenTime = 2f;             // 마지막에 좁은 시야 → 화면 전체로 트이는 데 걸리는 시간 (초)

    [Header("확인용 (Play 중 값 보기)")]
    [Range(0f, 1f)] public float open;       // 0 = 캄캄, 1 = 다 뜸

    public float Open => open;
    public bool IsBusy => phase != Phase.Closed;   // 눈을 뜨는 중·보는 중·감는 중이면 true

    enum Phase { Closed, Opening, Holding, HoldOpen, Closing }   // HoldOpen = 감으라고 할 때까지 뜬 채로 유지
    Phase phase;
    float holdTimer;
    float currentOpenTime;                   // 이번에 눈 뜨는 데 걸리는 시간 (Peek은 openTime, 처음 연출은 더 느리게)
    bool intro;                              // 처음 천천히 뜨는 연출 중 (구멍 크기 introRadius)
    bool forced;
    float wide;                              // 0 = 좁은 시야(peekRadius), 1 = 화면 전체(maxRadius)
    Material mat;
    static readonly int RadiusId = Shader.PropertyToID("_Radius");
    static readonly int AspectId = Shader.PropertyToID("_Aspect");

    void Awake()
    {
        // 머티리얼 원본(파일)을 건드리지 않도록 복사본을 만들어 쓴다
        mat = new Material(iris.material);
        iris.material = mat;
        ResetEye();
    }

    // 캄캄한 상태로 처음부터
    public void ResetEye()
    {
        open = 0f;
        phase = Phase.Closed;
        intro = false;
        forced = false;
        wide = 0f;
        Apply();
    }

    // 눈을 잠깐 뜨기 (감고 있을 때만)
    public void Peek()
    {
        if (phase != Phase.Closed || forced) return;
        currentOpenTime = openTime;
        phase = Phase.Opening;
    }

    // 처음 연출: time초에 걸쳐 천천히 눈을 뜨고, CloseEye()까지 뜬 채로 유지
    public void OpenSlow(float time)
    {
        if (phase != Phase.Closed || forced) return;
        intro = true;
        currentOpenTime = Mathf.Max(0.01f, time);
        phase = Phase.Opening;
    }

    // 뜬 눈을 감기
    public void CloseEye()
    {
        if (phase != Phase.Closed && !forced) phase = Phase.Closing;
    }

    // 끝까지 뜬 채로 유지
    public void ForceOpen()
    {
        forced = true;
        currentOpenTime = openTime;
        phase = Phase.Opening;
    }

    void Update()
    {
        switch (phase)
        {
            case Phase.Opening:
                open = Mathf.MoveTowards(open, 1f, Time.deltaTime / currentOpenTime);
                if (open >= 1f)
                {
                    // 마지막 연출·처음 연출은 뜬 채로 유지, 평소(Peek)는 잠깐 보고 감음
                    if (forced || intro) phase = Phase.HoldOpen;
                    else { phase = Phase.Holding; holdTimer = holdTime; }
                }
                break;

            case Phase.Holding:
                holdTimer -= Time.deltaTime;
                if (holdTimer <= 0f) phase = Phase.Closing;
                break;

            case Phase.Closing:
                open = Mathf.MoveTowards(open, 0f, Time.deltaTime / closeTime);
                if (open <= 0f) { phase = Phase.Closed; intro = false; }
                break;
        }

        // 마지막(ForceOpen): 좁은 시야가 천천히 화면 전체로 트임
        if (forced) wide = Mathf.MoveTowards(wide, 1f, Time.deltaTime / widenTime);

        Apply();
    }

    void Apply()
    {
        if (mat == null) return;

        // 떨림: 눈을 뜬 만큼 미세하게 (끝까지 뜬 마지막 연출에서는 떨지 않음)
        float noise = (Mathf.PerlinNoise(Time.time * trembleSpeed, 0f) - 0.5f) * 2f;
        float shake = forced ? 0f : noise * trembleAmount * open;

        // SmoothStep: 천천히 열리기 시작해서 빠르게 열리고, 끝에서 다시 천천히 (눈꺼풀 느낌)
        float baseSize = intro ? introRadius : peekRadius;
        float size = Mathf.Lerp(baseSize, maxRadius, Mathf.SmoothStep(0f, 1f, wide));
        float radius = Mathf.SmoothStep(0f, 1f, open) * size;
        mat.SetFloat(RadiusId, Mathf.Max(0f, radius + shake));
        mat.SetFloat(AspectId, (float)Screen.width / Screen.height);
    }
}