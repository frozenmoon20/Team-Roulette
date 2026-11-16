using UnityEngine;

namespace TeamRoulette.CorridorEnding
{
    public class EndingDoorTarget : MonoBehaviour, IInteractable, IHasName
    {
        public CorridorFinalEnding ending;
        public string interactionName = "문 열기";

        public string DisplayName => ending != null && ending.CanOpenDoor ? interactionName : "";

        public void Interact()
        {
            if (ending != null && ending.CanOpenDoor)
                ending.OpenEndingDoor();
        }
    }
}
