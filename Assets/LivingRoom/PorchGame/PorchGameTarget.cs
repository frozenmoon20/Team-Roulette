using UnityEngine;

namespace TeamRoulette.Room3
{
    public sealed class PorchGameTarget : MonoBehaviour, IInteractable, IHasName
    {
        public PorchGameController controller;
        public string DisplayName => "현관문";
        public void Interact() { if (isActiveAndEnabled && controller) controller.Interact(); }
    }
}
