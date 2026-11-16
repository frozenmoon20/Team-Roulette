using UnityEngine;

public class CorridorDebugUnlock : MonoBehaviour
{
    private void Start()
    {
        OptionState.Instance.Unlock(OptionType.Move);
        OptionState.Instance.Unlock(OptionType.RotateCamera);
    }
}