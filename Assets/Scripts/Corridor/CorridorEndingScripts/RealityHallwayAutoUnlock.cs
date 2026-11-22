
using System;
using UnityEngine;

[DefaultExecutionOrder(-500)]
public class RealityHallwayAutoUnlock : MonoBehaviour
{
    private void Start()
    {
        OptionState state = OptionState.Instance;

        if (state == null)
        {
            Debug.LogWarning(
                "RealityHallwayAutoUnlock: OptionState를 찾지 못했습니다.",
                this
            );
            return;
        }

        // 자동 해금 중 알림과 효과음이 연속으로
        // 출력되지 않도록 UnlockNotice를 잠시 중지
        UnlockNotice[] notices =
            FindObjectsByType<UnlockNotice>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        bool[] enabledStates = new bool[notices.Length];

        for (int i = 0; i < notices.Length; i++)
        {
            enabledStates[i] = notices[i].enabled;
            notices[i].enabled = false;
        }

        // 모든 조작 및 감각 기능 해금
        foreach (OptionType option in
                 Enum.GetValues(typeof(OptionType)))
        {
            // MessageSent는 조작 기능이 아닌
            // 스토리 진행 기록이므로 제외
            if (option == OptionType.MessageSent)
                continue;

            if (!state.IsUnlocked(option))
                state.Unlock(option);
        }

        // 해금 알림 원래 상태 복구
        for (int i = 0; i < notices.Length; i++)
        {
            if (notices[i] != null)
                notices[i].enabled = enabledStates[i];
        }

        Debug.Log(
            "현실 복도: 모든 조작 기능 자동 해금 완료!"
        );
    }
}
