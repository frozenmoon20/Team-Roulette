using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;


// 해금된 옵션 개수에 비례하여 하프톤 셰이더의 _HalftoneBlend 값을 올리는 스크립트
// 각 씬에 빈 오브젝트를 만들어 이 스크랩트를 붙여주면 적용됩니다.

public class HalftoneController : MonoBehaviour
{
    // 셰이더 프로퍼티 이름을 매번 문자열로 찾지 않도록 id로 캐싱작업
    static readonly int BlendId = Shader.PropertyToID("_HalftoneBlend");

    [Tooltip("초당 블렌드 변화량")]
    [SerializeField] float fadeSpeed = 0.08f;

    // OptionType 전체 목록
    OptionType[] allTypes;

    // 현재 화면에 적용 중인 값
    float current;

    // 도달해야 할 값
    float target;

    void Awake()
    {
        allTypes = (OptionType[])System.Enum.GetValues(typeof(OptionType));
        current = 0f;
        target = 0f;
        Shader.SetGlobalFloat(BlendId, current);
    }

    void OnEnable()
    {
        OptionState.OnOptionChanged += HandleOptionChanged;
    }

    void OnDisable()
    {
        OptionState.OnOptionChanged -= HandleOptionChanged;

        // 플레이 모드를 벗어날 때 에디터 화면이 하프톤인 채로 남지 않게 초기화
        Shader.SetGlobalFloat(BlendId, 0f);
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

        int unlockedCount = 0;
        for (int i = 0; i < allTypes.Length; i++)
        {
            if (OptionState.Instance.IsUnlocked(allTypes[i])) unlockedCount++;
        }

        target = allTypes.Length > 0 ? (float)unlockedCount / allTypes.Length : 0f;
        target = target * target;
    }

    void Update()
    {
        // 목표값에 도달했으면 아무것도 하지 않음
        if (Mathf.Approximately(current, target)) return;

        // 목표값을 향해 조금씩 이동시켜서 부드럽게 전환
        current = Mathf.MoveTowards(current, target, fadeSpeed * Time.deltaTime);
        Shader.SetGlobalFloat(BlendId, current);
    }





}
