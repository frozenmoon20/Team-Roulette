using UnityEngine;

public sealed class CalGameTarget : MonoBehaviour, IInteractable, IHasName
{
    public CalGameController game;
    public bool springPage;
    public string DisplayName => springPage ? "달력 · 5월" : "달력 · 12월";
    public void Interact() { if (game) game.Click(springPage); }
}
