using UnityEngine;
namespace TeamRoulette.Room3.DollMemory {
 public sealed class DollWasherButton:MonoBehaviour,IInteractable,IHasName {
 public DollMemorySequence memory;
 public string DisplayName=>"세탁기 시작";
 public void Interact(){if(memory)memory.PressWasherButton();}
 }
}
