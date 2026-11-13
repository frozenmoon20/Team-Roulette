using UnityEngine;
namespace TeamRoulette.Room3.Nosework
{
    public sealed class NoseworkStartPoint : MonoBehaviour, IInteractable, IHasName
    {
        public NoseworkGame game;
        public string DisplayName => "겨울이와 노즈워크 놀이";
        public void Interact() { if (game) game.Begin(); }
    }
}
