using UnityEngine;

namespace TeamRoulette.Room3
{
    public sealed class TVGameTarget : MonoBehaviour, IInteractable, IHasName
    {
        public TVGameController controller;
        public string DisplayName => controller && controller.Ready ? "영상 재생" : "TV 켜기";
        public void Interact() { if (isActiveAndEnabled && controller) controller.Interact(); }
    }
}
