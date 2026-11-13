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
    public bool disablePlacedColliders = true;   // 놓은 물건의 충돌체를 끔 (통 안 물건이 클릭을 가로채거나 다시 집히지 않게)
    [Header("효과음 (비워 두면 소리 없음)")]
    public AudioClip placeClip;              // 물건을 하나 넣을 때마다 나는 소리 (쓰레기통: 툭, 빨래통: 부스럭)
    [Range(0f, 1f)] public float placeVolume = 0.8f;

    public UnityEvent onPlaced;              // 물건을 하나 넣을 때마다 (필요할 때만 연결)
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
        GrabbableItem placedItem = hand.Held;

        hand.PlaceHeld(
            targetPoint,
            hideOnPlace
        );

        // 놓은 물건의 충돌체 끄기: 켜져 있으면 다음에 이 통을 클릭할 때 안에 든 물건이 먼저 맞아서
        //   통 이름·대사가 안 나오고, 놓은 물건을 다시 집을 수도 있음
        if (disablePlacedColliders && placedItem != null)
            foreach (Collider c in placedItem.GetComponentsInChildren<Collider>()) c.enabled = false;

        PlacedCount++;
        AudioManager.Play(placeClip, placeVolume);   // 효과음 (효과음 해금 전엔 안 남, 설정 볼륨 따름)
        onPlaced.Invoke();

        // 다 놓아도 이 컴포넌트는 끄지 않음 (끄면 마우스를 대도 이름이 안 뜨고 Done Dialogue도 안 나옴)
        //   다 놓은 뒤 클릭은 위쪽의 "이미 다 놓았으면" 검사에서 Done Dialogue로 처리됨
        if (PlacedCount >= acceptItems.Length)
            onAllPlaced.Invoke();
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