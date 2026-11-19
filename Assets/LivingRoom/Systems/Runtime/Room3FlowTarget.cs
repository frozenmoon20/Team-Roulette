using UnityEngine;

namespace TeamRoulette.Room3
{
    // Temporary scene interactions; replace these targets as the remaining games are built.
    public sealed class Room3FlowTarget : MonoBehaviour, IInteractable, IHasName
    {
        public string ObjectId { get; private set; }
        Room3FlowController flow;
        public string DisplayName => ObjectId switch {
            "WindowClose" => "창문", "TV" => "TV",
            "로프" => "로프 장난감",
            "뼈다귀 장난감" => "뼈다귀 장난감",
            "인형" => "오리 인형", _ => ObjectId
        };

        internal void Bind(Room3FlowController owner, string id) { flow = owner; ObjectId = id; }
        public void Interact() { if (isActiveAndEnabled && flow) flow.Click(this); }
    }
}
