using UnityEngine;

namespace TeamRoulette.Room3
{
    // Living-room toy that opens its minigame in the yard.
    public sealed class Room3FlowTarget : MonoBehaviour, IInteractable, IHasName
    {
        public string ObjectId { get; private set; }
        Room3FlowController flow;
        public string DisplayName => ObjectId switch {
            "로프" => "로프 장난감",
            "뼈다귀 장난감" => "뼈다귀 장난감",
            "인형" => "오리 인형", _ => ObjectId
        };

        internal void Bind(Room3FlowController owner, string id) { flow = owner; ObjectId = id; }
        public void Interact() { if (isActiveAndEnabled && flow) flow.Click(this); }
    }
}
