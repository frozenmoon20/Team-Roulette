using UnityEngine;

// 하프톤 세기를 "집 전체 진행도"에 맞춰 정하는 스크립트. _Systems 프리팹에 들어 있으므로 씬에 따로 붙이지 않는다.
// 진행도 (0 ~ 1) = 침실 몫 + 복도 몫 + 거실 몫   (기본: 40% + 20% + 40% = 100%)
//   - 침실: 해금 8개 중 몇 개 했는지 × 0.4   (시점 회전, 이동, 손, 잡기, 효과음, 배경음, 점프, 달리기)
//   - 복도: 해금 2개 중 몇 개 했는지 × 0.2   (밀기, 당기기)
//   - 거실: 진행 7단계 중 몇 단계 했는지 × 0.4 (커튼, 세수, 밥, 게시판, 장난감, 달력, TV)
//   세는 건 HouseProgress가 하고(집 지도와 같은 숫자), 이 스크립트는 그 숫자로 하프톤 값만 정한다.
// 하프톤 값 (0 = 가장 짙음, 1 = 깨끗함) = Start Blend(진행 0%)에서 End Blend(진행 100%)까지, Blend Curve 모양대로.
//   셰이더가 눈에 띄게 변하는 구간이 좁으면 Start Blend를 올리거나 Blend Curve를 휘어서 맞춘다.
// 조정: Play 중 "테스트" 칸의 Preview를 켜고 Preview Progress를 끌면, 진행도별 화면을 바로 볼 수 있다.
//   (0.4 = 침실 끝, 0.6 = 복도 끝, 1 = 거실 끝)
// (연출용) SetOverride(값)으로 잠깐 다른 값을 보여 줄 수 있다. ClearOverride()로 원래대로. (전화 장면, 미니게임2 깨어남 등)
public class HalftoneController : MonoBehaviour
{
    // 셰이더 프로퍼티 이름을 매번 문자열로 찾지 않도록 id로 캐싱
    static readonly int BlendId = Shader.PropertyToID("_HalftoneBlend");
    static HalftoneController active;

    // 연출용 덮어쓰기
    static bool overriding;
    static float overrideValue;

    [Tooltip("초당 블렌드 변화량 (진행할 때 화면이 바뀌는 빠르기)")]
    [SerializeField] float fadeSpeed = 0.08f;

    [Header("공간별 몫 (합이 1이 되게)")]
    [SerializeField] float bedroomShare = 0.4f;
    [SerializeField] float hallwayShare = 0.2f;
    [SerializeField] float livingShare = 0.4f;

    [Header("하프톤 값 (0 = 가장 짙음, 1 = 깨끗함)")]
    [SerializeField] float startBlend = 0.35f;   // 진행 0% (게임 시작)
    [SerializeField] float endBlend = 1f;        // 진행 100% (거실까지 다 끝)
    [Tooltip("가로 = 진행도(0~1), 세로 = Start→End 사이 어디쯤인지(0~1). 기본은 직선")]
    [SerializeField] AnimationCurve blendCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("확인용 (Play 중 값 보기)")]
    [SerializeField] float progressNow;          // 지금 진행도 (0~1)
    [SerializeField] float targetNow;            // 지금 목표 하프톤 값

    [Header("테스트 (Play 중에만)")]
    [SerializeField] bool preview;               // 켜면 실제 진행도 대신 아래 값으로 보여 줌
    [Range(0f, 1f)][SerializeField] float previewProgress;

    // 현재 화면에 적용 중인 값
    float current;

    // 지금 원래(진행도 기준) 하프톤 값. 연출이 끝나고 이 값으로 부드럽게 돌아가고 싶을 때 읽는다
    public static float CurrentValue => active != null ? active.current : 0f;

    // 연출용: 진행 상태와 상관없이 이 값(0~1)을 화면에 보여 준다
    public static void SetOverride(float value)
    {
        overriding = true;
        overrideValue = Mathf.Clamp01(value);
    }

    // 연출 끝: 다시 진행도 기준 값으로
    public static void ClearOverride()
    {
        overriding = false;
    }

    void Awake()
    {
        // 이미 동작 중인 컨트롤러가 있으면 이건 꺼 둠 (전역 값 건드리지 않음)
        if (active != null && active != this) { enabled = false; return; }
        active = this;
    }

    void OnDisable()
    {
        if (active == this)            // 진짜 컨트롤러가 꺼질 때만 초기화
        {
            active = null;
            overriding = false;
            Shader.SetGlobalFloat(BlendId, 0f);
        }
    }

    void Start()
    {
        current = Target();            // 시작할 때 현재 진행 상태에 바로 맞춤
        Shader.SetGlobalFloat(BlendId, current);
    }

    void Update()
    {
        // 목표값을 매 프레임 다시 계산 (숫자 몇 개 세는 것뿐이라 가벼움) → 해금·거실 단계가 바뀌면 자동 반영
        float target = Target();
        current = Mathf.MoveTowards(current, target, fadeSpeed * Time.deltaTime);

        // 연출 중이면 연출 값을, 아니면 진행도 기준 값을 매 프레임 적용 → 누가 값을 바꿔도 바로 복구
        Shader.SetGlobalFloat(BlendId, overriding ? overrideValue : current);
    }

    // 지금 진행도 → 목표 하프톤 값
    float Target()
    {
        float p = preview ? previewProgress : Progress();
        progressNow = p;
        targetNow = Mathf.Lerp(startBlend, endBlend, Mathf.Clamp01(blendCurve.Evaluate(p)));
        return targetNow;
    }

    // 집 전체 진행도 (0~1)
    float Progress()
    {
        float p = Part(HouseProgress.Area.Bedroom) * bedroomShare
                + Part(HouseProgress.Area.Hallway) * hallwayShare
                + Part(HouseProgress.Area.Living) * livingShare;
        return Mathf.Clamp01(p);
    }

    // 한 공간을 얼마나 했는지 (0~1)
    static float Part(HouseProgress.Area area)
    {
        int total = HouseProgress.Total(area);
        return total > 0 ? (float)HouseProgress.Done(area) / total : 0f;
    }
}