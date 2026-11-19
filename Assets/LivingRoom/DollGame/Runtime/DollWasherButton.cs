using UnityEngine;
namespace TeamRoulette.Room3.DollMemory {
 public sealed class DollWasherButton:MonoBehaviour,IInteractable,IHasName {
 public DollMemorySequence memory;
 public string DisplayName=>memory && memory.CanUseWasher ? "세탁기 버튼" : "";
 public void Interact(){if(memory)memory.PressWasherButton();}
 }
}
