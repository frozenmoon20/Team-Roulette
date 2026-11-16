using UnityEngine;

// 이벤트 칸에서 옵션을 해금하기 위한 작은 도우미.
// 이벤트(UnityEvent) 칸에서는 OptionType 같은 선택지를 직접 넘길 수 없어서, 여기 드롭다운에 미리 골라 두고 Unlock()만 부른다.
// 사용: 빈 오브젝트에 붙이고 Option을 고른 뒤, 다른 이벤트에서 OptionUnlocker.Unlock()을 연결한다.
public class OptionUnlocker : MonoBehaviour
{
    public OptionType option;                // 해금할 옵션 (예: MessageSent)

    public void Unlock()
    {
        if (OptionState.Instance != null) OptionState.Instance.Unlock(option);
    }
}