using UnityEngine;
public sealed class CurtainClickTarget : MonoBehaviour, IInteractable, IHasName
{
    public CurtainGameController game;
    public string DisplayName => game && !game.Started && !game.Completed ? "커튼" : "";
    public void Interact() { if (game) game.TryOpen(); }
}
