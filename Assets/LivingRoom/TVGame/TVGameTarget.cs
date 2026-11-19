using UnityEngine;

namespace TeamRoulette.Room3
{
    public sealed class TVGameTarget : MonoBehaviour, IInteractable, IHasName
    {
        public TVGameController controller;
        public string DisplayName => "TV";
        public void Interact() { if (isActiveAndEnabled && controller) controller.Interact(); }
    }
}
