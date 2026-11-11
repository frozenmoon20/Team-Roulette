using UnityEngine;


// 해금된 옵션 개수에 비례하여 하프톤 셰이더의 _HalftoneBlend 값을 올리는 스크립트
// _Systems 프리팹에 들어 있으므로 씬에 따로 붙이지 않는다
// (연출용) SetOverride(값)으로 잠깐 다른 값을 보여 줄 수 있다. ClearOverride()로 원래대로.
// 해금 개수별 값 정하기 (Blend By Count): 칸 번호 = 해금된 개수, 칸의 값 = 그때의 하프톤 값 (0 = 가장 짙음, 1 = 깨끗함).
//   예) 침실을 다 끝냈을 때(대시 빼고) 30%만 밝아지게 → 그 개수 칸에 0.3. 이후 복도·거실 해금으로 1까지.
//   비워 두면 예전처럼 "해금 개수 ÷ 전체 개수"로 자동 계산. 지금 몇 개가 해금됐는지는 Play 중 "확인용" 칸에 보인다.

public class HalftoneController : MonoBehaviour
{
    // 셰이더 프로퍼티 이름을 매번 문자열로 찾지 않도록 id로 캐싱작업
    static readonly int BlendId = Shader.PropertyToID("_HalftoneBlend");
    static HalftoneController active;

    // 연출용 덮어쓰기 (미니게임 2 깨어남 등)
    static bool overriding;
    static float overrideValue;

    [Tooltip("초당 블렌드 변화량")]
    [SerializeField] float fadeSpeed = 0.08f;

    [Tooltip("하프톤 계산에서 뺄 항목 (진행 기록처럼 기능이 아닌 것)")]
    [SerializeField] OptionType[] excludedOptions = { OptionType.MessageSent };

    [Header("해금 개수별 하프톤 값 (칸 번호 = 해금 개수, 0 = 가장 짙음 / 1 = 깨끗함)")]
    [Tooltip("비워 두면 해금 개수에 비례. 칸이 모자라면 마지막 칸 값을 씀")]
    [SerializeField] float[] blendByCount;

    [Header("확인용 (Play 중 값 보기)")]
    [SerializeField] int unlockedNow;        // 지금 해금된 개수 (제외 항목 빼고)
    [SerializeField] int totalNow;           // 전체 개수 (제외 항목 빼고)
    [SerializeField] float targetNow;        // 지금 목표 하프톤 값

    // 현재 화면에 적용 중인 값
    float current;

    // 도달해야 할 값
    float target;

    // 지금 원래(해금 기준) 하프톤 값. 연출이 끝나고 이 값으로 부드럽게 돌아가고 싶을 때 읽는다
    public static float CurrentValue => active != null ? active.current : 0f;

    // 연출용: 해금 상태와 상관없이 이 값(0~1)을 화면에 보여 준다
    public static void SetOverride(float value)
    {
        overriding = true;
        overrideValue = Mathf.Clamp01(value);
    }

    // 연출 끝: 다시 해금 상태 기준 값으로
    public static void ClearOverride()
    {
        overriding = false;
    }

    void Awake()
    {
        // 이미 동작 중인 컨트롤러가 있으면 이건 꺼둠 (전역 값 건드리지 않음)
        if (active != null && active != this) { enabled = false; return; }
        active = this;
    }

    void OnEnable()
    {
        OptionState.OnOptionChanged += HandleOptionChanged;
    }

    void OnDisable()
    {
        OptionState.OnOptionChanged -= HandleOptionChanged;

        if (active == this)            // 진짜 컨트롤러가 꺼질 때만 초기화
        {
            active = null;
            overriding = false;
            Shader.SetGlobalFloat(BlendId, 0f);
        }
    }

    void Start()
    {
        RecalculateTarget();
        current = target;              // 시작할 때 현재 해금 상태에 바로 맞춤
        Shader.SetGlobalFloat(BlendId, current);
    }

    // 옵션이 켜지거나 꺼질 때마다 호출된다
    void HandleOptionChanged(OptionType type, bool isUnlocked)
    {
        RecalculateTarget();
    }



    // 지금 몇 개가 해금됐는지 세어서 목표값을 다시 구한다
    void RecalculateTarget()
    {
        if (OptionState.Instance == null) return;

        int total = 0, unlockedCount = 0;
        foreach (OptionType t in System.Enum.GetValues(typeof(OptionType)))
        {
            if (System.Array.IndexOf(excludedOptions, t) >= 0) continue;
            total++;
            if (OptionState.Instance.IsUnlocked(t)) unlockedCount++;
        }

        if (blendByCount != null && blendByCount.Length > 0)
            target = Mathf.Clamp01(blendByCount[Mathf.Clamp(unlockedCount, 0, blendByCount.Length - 1)]);
        else
            target = total > 0 ? (float)unlockedCount / total : 0f;

        unlockedNow = unlockedCount;
        totalNow = total;
        targetNow = target;
    }




    void Update()
    {
        current = Mathf.MoveTowards(current, target, fadeSpeed * Time.deltaTime);

        // 연출 중이면 연출 값을, 아니면 해금 기준 값을 매 프레임 적용 → 누가 값을 바꿔도 바로 복구
        Shader.SetGlobalFloat(BlendId, overriding ? overrideValue : current);
    }





}