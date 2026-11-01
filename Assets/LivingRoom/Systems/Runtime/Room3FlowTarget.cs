using UnityEngine;

namespace TeamRoulette.Room3
{
    // Temporary scene interactions; replace these targets as the remaining games are built.
    public sealed class Room3FlowTarget : MonoBehaviour, IInteractable, IHasName
    {
        public string ObjectId { get; private set; }
        Room3FlowController flow;
        public string DisplayName => flow && flow.IsToyComplete(ObjectId) ? ObjectId + " (완료)" : ObjectId;

        internal void Bind(Room3FlowController owner, string id) { flow = owner; ObjectId = id; }
        public void Interact() { if (isActiveAndEnabled && flow) flow.Click(this); }
    }
}
