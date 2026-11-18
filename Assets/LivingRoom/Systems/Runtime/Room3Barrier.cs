using UnityEngine;

namespace TeamRoulette.Room3
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class Room3Barrier : MonoBehaviour
    {
        [SerializeField, TextArea] string remark;
        float lastContact = float.NegativeInfinity;

        public string Remark => remark;

        internal void OnPlayerContact(DialogueManager dialogue)
        {
            bool newContact = Time.time - lastContact > .35f;
            lastContact = Time.time;
            if (newContact && dialogue && !string.IsNullOrWhiteSpace(remark))
                dialogue.PlayLines(new[] { remark });
        }

        void OnDisable() { lastContact = float.NegativeInfinity; }
    }
}
