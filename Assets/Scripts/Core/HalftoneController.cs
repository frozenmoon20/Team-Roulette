using UnityEngine;


// 해금된 옵션 개수에 비례하여 하프톤 셰이더의 _HalftoneBlend 값을 올리는 스크립트
// _Systems 프리팹에 들어 있으므로 씬에 따로 붙이지 않는다

public class HalftoneController : MonoBehaviour
{
    // 셰이더 프로퍼티 이름을 매번 문자열로 찾지 않도록 id로 캐싱작업
    static readonly int BlendId = Shader.PropertyToID("_HalftoneBlend");
    static HalftoneController active;

    [Tooltip("초당 블렌드 변화량")]
    [SerializeField] float fadeSpeed = 0.08f;

    [Tooltip("하프톤 계산에서 뺄 항목 (진행 기록처럼 기능이 아닌 것)")]
    [SerializeField] OptionType[] excludedOptions = { OptionType.MessageSent };

    // 현재 화면에 적용 중인 값
    float current;

    // 도달해야 할 값
    float target;

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

        target = total > 0 ? (float)unlockedCount / total : 0f;
    }




    void Update()
    {
        current = Mathf.MoveTowards(current, target, fadeSpeed * Time.deltaTime);
        Shader.SetGlobalFloat(BlendId, current);   // 매 프레임 적용 → 누가 값을 바꿔도 바로 복구
    }





}
