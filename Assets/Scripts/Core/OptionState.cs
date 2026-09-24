using UnityEngine;
using System.Collections.Generic;

// 게임 내 각종 옵션(조작 기능)의 해금 상태를 관리한다.

// OptionType에 새로운 해금 될 기능들을 추가하면 자동으로 이곳에 반영됨 
public enum OptionType 
{ 
    // 조작 관련
    Move, 
    RotateCamera, 
    Grab, 
    Push, 
    Pull,
        
    // 그래픽, 비디오
    Hand,
    Saturation,
    Blur,

    // 사운드 관련

}

public class OptionState : MonoBehaviour
{
    // 편리하게 OptionState의 Instance로 접근하기 위한 싱글턴
    public static OptionState Instance;



    // 옵션 해금 시 호출되는 이벤트
    // 전달 값: (어떤 옵션인지, 해금 여부)
    // 게임 플레이 중에선 해금(true)만 발생하고 false는 에디터에서 테스트로 되돌릴 때만
    
    // [사용 방법]

    // void OnEnable() { OptionState.OnOptionChanged += 내함수; }
    // void OnDisable() { OptionState.OnOptionChanged -= 내함수; }
    // void 내함수(OptionType type, bool isUnlocked) { ... }
    public static event System.Action<OptionType, bool> OnOptionChanged;


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
        DontDestroyOnLoad(gameObject);
        // 모든 옵션을 잠긴 상태로 초기화

        foreach (OptionType type in System.Enum.GetValues(typeof(OptionType)))
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
        OnOptionChanged?.Invoke(type, true);
    }

    // 해당 옵션을 잠금
    public void Lock(OptionType type)
    {
        unlocked[type] = false;
        OnOptionChanged?.Invoke(type, false);
    }
   
}
