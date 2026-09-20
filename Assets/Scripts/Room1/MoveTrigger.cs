using UnityEngine;

public class MoveTrigger : ConditionalInteractable
{
    protected override void OnUnlocked()
    {
        OptionState.Instance.Unlock(OptionType.Move);
        Debug.Log("이동 해금완료");
    }


}
