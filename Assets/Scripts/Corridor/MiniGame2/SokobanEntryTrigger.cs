using UnityEngine;

public class SokobanEntryTrigger : MonoBehaviour
{
    public SokobanGameManager gameManager;

    bool activated;

    private void OnTriggerEnter(Collider other)
    {
        if (activated)
            return;

        FirstPersonPlayer player =
            other.GetComponentInParent<FirstPersonPlayer>();

        if (player == null)
            return;

        activated = true;

        if (OptionState.Instance != null)
            OptionState.Instance.Unlock(OptionType.Push);

        if (gameManager != null)
            gameManager.BeginGame();

        PlayerInteractor.ShowHint(
            "밀기 기능이 해금되었다."
        );
    }
}