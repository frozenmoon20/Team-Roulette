using UnityEngine;
namespace TeamRoulette.Room3.DollMemory
{
    public sealed class DollMemoryStart : MonoBehaviour,IInteractable,IHasName
    {
        public DollMemorySequence memory;
        public string DisplayName => "오리 인형의 기억";
        public void Interact() { if(memory && !memory.Running) memory.Begin(); }
    }
}
