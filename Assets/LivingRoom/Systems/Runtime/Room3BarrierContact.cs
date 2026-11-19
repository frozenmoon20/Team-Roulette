using UnityEngine;

namespace TeamRoulette.Room3
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class Room3BarrierContact : MonoBehaviour
    {
        [SerializeField] DialogueManager dialogue;

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (Time.timeScale <= 0 || Mathf.Abs(hit.normal.y) > .7f) return;
            if (hit.collider.TryGetComponent<Room3Barrier>(out var barrier) && barrier.isActiveAndEnabled)
                barrier.OnPlayerContact(dialogue);
        }
    }
}
