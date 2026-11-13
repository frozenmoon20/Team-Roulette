using System;
using System.Collections.Generic;
using UnityEngine;

// 집 지도 진행도 계산 (침실·복도·거실). 화면에 그리는 건 HouseMap이 한다.
// 원리:
//   - 침실·복도: "그 공간에서 되찾는 기능"이 해금됐는지 OptionState에서 세기만 한다 (따로 저장하지 않음).
//       침실 8개: 시점 회전, 이동, 손, 잡기, 효과음, 배경음, 점프, 달리기 / 복도 2개: 밀기, 당기기
//   - 거실: 해금이 아니라 "진행 단계" 7개 (커튼, 세수, 밥, 게시판, 장난감, 달력, TV).
//       각 단계가 끝날 때 ProgressStep.Complete()가 이름을 기록한다.
//   - static 이라 씬이 바뀌어도 기록이 남는다 (해금 상태처럼).
//   - 무언가 바뀌면 OnChanged 알림 → 지도·하프톤이 다시 그린다.
public static class HouseProgress
{
    public enum Area { Bedroom, Hallway, Living }

    public static readonly OptionType[] BedroomOptions =
        { OptionType.RotateCamera, OptionType.Move, OptionType.Hand, OptionType.Grab,
          OptionType.SFX, OptionType.BGM, OptionType.Jump, OptionType.Run };
    public static readonly OptionType[] HallwayOptions = { OptionType.Push, OptionType.Pull };
    public const int LivingTotal = 7;

    static readonly HashSet<string> livingDone = new HashSet<string>();

    public static event Action OnChanged;

    // 거실 진행 단계 하나 완료 (같은 이름은 한 번만 셈)
    public static void CompleteLivingStep(string stepName)
    {
        if (string.IsNullOrEmpty(stepName) || !livingDone.Add(stepName)) return;
        Debug.Log("HouseProgress: 거실 단계 완료 - " + stepName + " (" + livingDone.Count + "/" + LivingTotal + ")");
        OnChanged?.Invoke();
    }

    // 해금 상태가 바뀌었을 때 (HouseMap이 OptionState 알림을 받아 대신 불러 줌)
    public static void NotifyChanged() { OnChanged?.Invoke(); }

    public static int Done(Area area)
    {
        switch (area)
        {
            case Area.Bedroom: return CountUnlocked(BedroomOptions);
            case Area.Hallway: return CountUnlocked(HallwayOptions);
            default: return Mathf.Min(livingDone.Count, LivingTotal);
        }
    }

    public static int Total(Area area)
    {
        switch (area)
        {
            case Area.Bedroom: return BedroomOptions.Length;
            case Area.Hallway: return HallwayOptions.Length;
            default: return LivingTotal;
        }
    }

    public static bool IsComplete(Area area) => Done(area) >= Total(area);

    // 전체 진행 칸 수 (8 + 2 + 7 = 17)
    public static int AllDone => Done(Area.Bedroom) + Done(Area.Hallway) + Done(Area.Living);
    public static int AllTotal => Total(Area.Bedroom) + Total(Area.Hallway) + Total(Area.Living);

    static int CountUnlocked(OptionType[] list)
    {
        OptionState s = OptionState.Instance;
        if (s == null) return 0;
        int n = 0;
        foreach (OptionType t in list) if (s.IsUnlocked(t)) n++;
        return n;
    }
}