using UnityEngine;

public sealed class BoardTarget : MonoBehaviour, IInteractable, IHasName
{
    public BoardGameController game;
    public bool trash;
    public string DisplayName => trash ? "쓰레기통" : "게시판";
    public void Interact() { if (game && !trash) game.Begin(); }
}
