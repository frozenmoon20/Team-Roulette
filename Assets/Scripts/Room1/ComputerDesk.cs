using UnityEngine;

public class ComputerDesk : ConditionalInteractable

{
    protected override void OnUnlocked()
    {
        OptionState.Instance.Unlock(OptionType.RotateCamera);
        Debug.Log("시점 조작 해금완료");
    }
   
}
