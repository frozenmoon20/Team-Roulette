using UnityEngine;

public enum WashAction { Water, Soap, Towel }

public sealed class WashTarget : MonoBehaviour, IInteractable, IHasName
{
    public WashGameController game;
    public WashAction action;
    public string displayName;
    public string DisplayName => displayName;
    public void Interact() { if (game) game.Click(action); }
}
