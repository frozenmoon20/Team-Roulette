using UnityEngine;

public enum WashAction { Water, Soap, Towel }

public sealed class WashTarget : MonoBehaviour, IInteractable, IHasName
{
    public WashGameController game;
    public WashAction action;
    public string displayName;
    public string DisplayName => action switch {
        WashAction.Water => "세면대",
        WashAction.Soap => "비누",
        WashAction.Towel => "수건", _ => displayName
    };
    public void Interact() { if (game) game.Click(action); }
}
