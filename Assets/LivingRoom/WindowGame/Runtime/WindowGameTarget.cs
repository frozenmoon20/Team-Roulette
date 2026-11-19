using UnityEngine;

public sealed class WindowGameTarget : MonoBehaviour, IInteractable, IHasName
{
    public WindowGameController game;
    public string DisplayName => "창문";
    public void Interact() { if (isActiveAndEnabled && game) game.Interact(); }
}
