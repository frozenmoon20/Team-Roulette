using UnityEngine;
using UnityEngine.Events;

// 손에 든 물건(GrabbableItem)을 넣거나 올려 두는 곳 (선반, 빨래통, 쓰레기통 등)
// 맞지 않는 물건을 넣으려 하면: wrongItemDialogue(대화창)가 나온다.
//   wrongItemDialogue를 비워 두면 예전처럼 화면 아래 짧은 글자(wrongItemMessage)가 뜬다 (기존 PlaceSpot은 그대로 동작).
// 빈손으로 클릭하면: emptyHandDialogue (예: LP 플레이어 "…판이 없네.") / 다 놓은 뒤 클릭하면: doneDialogue
//   둘 다 비워 두면 예전처럼 아무 일도 없다. → 상호작용 오브젝트를 따로 두지 않고 PlaceSpot 하나로 처리할 수 있다.
public class PlaceSpot : MonoBehaviour, IInteractable, IHasName
{
    public string displayName = "";

    public GrabbableItem[] acceptItems;

    public Transform placePoint;
    public Transform[] placePoints;

    public bool hideOnPlace;
    public string wrongItemMessage = "여기에 둘 건 아니다.";
    public DialogueStep wrongItemDialogue;   // 맞지 않는 물건일 때 대사 (넣으면 위 글자 대신 이게 나옴)
    public DialogueStep emptyHandDialogue;   // 빈손으로 클릭했을 때 대사 (비워 두면 아무 일 없음)
    public DialogueStep doneDialogue;        // 다 놓은 뒤 다시 클릭했을 때 대사 (비워 두면 아무 일 없음)
    public UnityEvent onAllPlaced;

    public string DisplayName => displayName;
    public int PlacedCount { get; private set; }

    public bool CanAccept(GrabbableItem item)
    {
        if (item == null || acceptItems == null)
            return false;

        return System.Array.IndexOf(acceptItems, item) >= 0;
    }

    public void Interact()
    {
        PlayerHand hand = PlayerHand.Instance;

        // 이미 다 놓았으면
        if (acceptItems != null && acceptItems.Length > 0 && PlacedCount >= acceptItems.Length)
        {
            PlayDialogue(doneDialogue);
            return;
        }

        // 빈손이면
        if (hand == null || hand.Held == null)
        {
            PlayDialogue(emptyHandDialogue);
            return;
        }

        if (!CanAccept(hand.Held))
        {
            if (wrongItemDialogue != null)
            {
                if (!wrongItemDialogue.IsPlaying) wrongItemDialogue.Play();   // 대화창으로
            }
            else PlayerInteractor.ShowHint(wrongItemMessage);                 // 대사가 없으면 예전처럼 글자로
            return;
        }

        Transform targetPoint = GetNextPlacePoint();

        hand.PlaceHeld(
            targetPoint,
            hideOnPlace
        );

        PlacedCount++;

        if (PlacedCount >= acceptItems.Length)
        {
            enabled = false;
            onAllPlaced.Invoke();
        }
    }

    static void PlayDialogue(DialogueStep step)
    {
        if (step != null && !step.IsPlaying) step.Play();
    }

    Transform GetNextPlacePoint()
    {
        if (placePoints != null &&
            PlacedCount < placePoints.Length &&
            placePoints[PlacedCount] != null)
        {
            return placePoints[PlacedCount];
        }

        if (placePoint != null)
            return placePoint;

        return transform;
    }
}