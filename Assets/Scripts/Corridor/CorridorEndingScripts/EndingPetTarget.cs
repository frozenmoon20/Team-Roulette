using UnityEngine;

namespace TeamRoulette.CorridorEnding
{
    public class EndingPetTarget : MonoBehaviour, IInteractable, IHasName
    {
        public CorridorFinalEnding ending;
        public string interactionName = "겨울이를 쓰다듬기";

        public string DisplayName => ending != null && ending.CanPet ? interactionName : "";

        public void Interact()
        {
            if (ending != null && ending.CanPet)
                ending.StartPetting();
        }
    }
}
