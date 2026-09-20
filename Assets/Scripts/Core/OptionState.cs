using UnityEngine;
using System.Collections.Generic;

// 게임 내 각종 옵션(조작 기능)의 해금 상태를 관리한다.

// OptionType에 새로운 해금 될 기능들을 추가하면 자동으로 이곳에 반영됨 
public enum OptionType { Move, RotateCamera, Hand, Grab, Push, Pull }

public class OptionState : MonoBehaviour
{
    // 편리하게 OptionState의 Instance로 접근하기 위한 싱글턴
    public static OptionState Instance;

    // 각 옵션의 해금 여부 저장
    private Dictionary<OptionType, bool> unlocked = new Dictionary<OptionType, bool>();


    void Awake()
    {
        if(Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 모든 옵션을 잠긴 상태로 초기화

        foreach(OptionType type in System.Enum.GetValues(typeof(OptionType)))
        {
            unlocked[type] = false;
        }


    }

    // 해당 옵션이 해금됬는지 확인하기 (ConditionalInteractable에서 사용)
    public bool IsUnlocked(OptionType type)
    {
        return unlocked.ContainsKey(type) && unlocked[type];
    }

    // 해당 옵션을 해금
    public void Unlock(OptionType type)
    {
        unlocked[type] = true;
    }

    // 해당 옵션을 잠금
    public void Lock(OptionType type)
    {
        unlocked[type] = false;
    }
   
}
