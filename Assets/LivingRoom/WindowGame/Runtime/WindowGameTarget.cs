using UnityEngine;

public sealed class WindowGameTarget : MonoBehaviour, IInteractable, IHasName
{
    public WindowGameController game;
    public string DisplayName => game && game.CanInteract ? "좌클릭 · 창문 열기" : "";
    public void Interact() { if (isActiveAndEnabled && game) game.Interact(); }
}
