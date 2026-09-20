using System;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using TMPro.EditorUtilities;

// 조건 충족 시 상호작용이 가능해지는 오브젝트의 기본 틀

// [사용법 - 새로운 상호작용 오브젝트 제작 시]

// 1. 해금 조건이 필요한 오브젝트는 이 클래스(ConditionalInteractable)를 상속받아 만든다.
//      - 한 번 클릭으로 끝나는 것(대화 시작, 아이템 획득 등) -> OnUnlocked()만 override

// 2. 그 이후 반복 상호작용이 필요하다면 OnRepeatInteract()도 override하기

// 3. 만일 반복 상호작용이 필요 없을 시 OnRepeatInteract()는 건들이지 않아도 됨

public class ConditionalInteractable : MonoBehaviour, IInteractable
{
    // 이 오브젝트가 요구하는 조건들(에디터의 inspector에서 리스트에 추가)
    public List<OptionType> requiredOptions;

    // 조건 충족 시 한 번이라도 해금됬는지의 여부가 체크되는 불리언
    protected bool hasUnlocked = false;

    // [Interact() 설명]
    // IInteractable 규칙 구현 -> 후에 마우스 클릭으로 PlayerInteractor.cs의 TryInteract()가 
    // 실행 될 때 각 오브젝트의 Interact()가 호출됨
    public void Interact()
    {
        if(!hasUnlocked)
        {
            if (ConditionsMet())
            {
                hasUnlocked = true;
                OnUnlocked();
            }
            else
            {
                Debug.Log("조건 충족 x");
            }
        }
        else
        {
            OnRepeatInteract();
        }  

    }

   
    // requiredOptions에 등록된 조건들이 전부 해금(true상태)됬는지 확인한다.
    // 리스트가 비어있다면 무조건 true 반환하게 됨
    bool ConditionsMet()
    {
        foreach(var opt in requiredOptions)
        {
            if(!OptionState.Instance.IsUnlocked(opt))
            {
                return false;
            }
            
        }
        // 모든 조건이 해금 된 상태면 true를 반환
        return true;
    }

    // 조건 처음 충족 시 딱 한 번 실행됨
    protected virtual void OnUnlocked()
    {
        Debug.Log("조건 충족 - 기본 동작");
    }

    // 해금된 이후 다시 클릭 할 때마다 실행된다(기본적으론 아무 동작 x)
    protected virtual void OnRepeatInteract()
    {

    }


}
