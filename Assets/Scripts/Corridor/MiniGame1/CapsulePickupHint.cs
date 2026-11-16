
using UnityEngine;

public class CapsulePickupHint : MonoBehaviour
{
    [Header("캡슐")]
    public GrabbableItem capsule;

    [Header("집었을 때 출력할 대사")]
    public string pickupMessage =
        "안에 뭐가 들어 있는 것 같은데…… 흔들어 볼까?";

    [Header("선택: 기존 대사 시스템 사용")]
    public DialogueStep pickupDialogue;

    private bool shown;

    private void Awake()
    {
        if (capsule == null)
            capsule = GetComponent<GrabbableItem>();
    }

    private void Update()
    {
        if (shown || capsule == null)
            return;

        PlayerHand hand = PlayerHand.Instance;

        if (hand == null || hand.Held != capsule)
            return;

        shown = true;

        // 기존 DialogueStep이 연결되어 있다면 대사 재생
        if (pickupDialogue != null)
        {
            pickupDialogue.Play();
        }
        else
        {
            // 대사가 연결되지 않았다면 일반 힌트 출력
            PlayerInteractor.ShowHint(pickupMessage);
        }
    }
}
