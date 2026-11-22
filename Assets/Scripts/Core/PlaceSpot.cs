
using UnityEngine;
using UnityEngine.Events;

// 손에 든 물건(GrabbableItem)을 넣거나 올려 두는 곳
// 선반, 빨래통, 장난감 상자 등에 사용
public class PlaceSpot : MonoBehaviour, IInteractable, IHasName
{
    public string displayName = "";

    public GrabbableItem[] acceptItems;

    public Transform placePoint;
    public Transform[] placePoints;

    public bool hideOnPlace;

    public string wrongItemMessage = "여기에 둘 건 아니다.";

    public DialogueStep wrongItemDialogue;
    public DialogueStep emptyHandDialogue;
    public DialogueStep doneDialogue;

    // 놓은 물건의 충돌체를 끌지 여부
    public bool disablePlacedColliders = true;

    [Header("효과음 (비워 두면 소리 없음)")]
    public AudioClip placeClip;

    [Range(0f, 1f)]
    public float placeVolume = 0.8f;

    public UnityEvent onPlaced;
    public UnityEvent onAllPlaced;

    public string DisplayName => displayName;

    public int PlacedCount { get; private set; }

    public bool useExactPlacement;

    // 해당 물건을 놓을 수 있는지 확인
    public bool CanAccept(GrabbableItem item)
    {
        if (item == null || acceptItems == null)
            return false;

        return System.Array.IndexOf(acceptItems, item) >= 0;
    }

    public void Interact()
    {
        PlayerHand hand = PlayerHand.Instance;

        // 이미 모든 물건을 놓았을 때
        if (acceptItems != null &&
            acceptItems.Length > 0 &&
            PlacedCount >= acceptItems.Length)
        {
            PlayDialogue(doneDialogue);
            return;
        }

        // 손에 아무것도 없을 때
        if (hand == null || hand.Held == null)
        {
            PlayDialogue(emptyHandDialogue);
            return;
        }

        // 놓을 수 없는 물건일 때
        if (!CanAccept(hand.Held))
        {
            if (wrongItemDialogue != null)
            {
                if (!wrongItemDialogue.IsPlaying)
                    wrongItemDialogue.Play();
            }
            else
            {
                PlayerInteractor.ShowHint(wrongItemMessage);
            }

            return;
        }

        // 다음 배치 위치 가져오기
        Transform targetPoint = GetNextPlacePoint();

        // 현재 들고 있는 물건 저장
        GrabbableItem placedItem = hand.Held;

        // 물건 배치
        if (useExactPlacement && !hideOnPlace)
        {
            GrabbableItem item = hand.Held;

            hand.PlaceHeld(targetPoint, false);

            item.PlaceExactlyAt(targetPoint);
        }
        else
        {
            hand.PlaceHeld(targetPoint, hideOnPlace);
        }

        // ============================================
        // [수정] 놓은 장난감의 Rigidbody 고정
        // ============================================
        //
        // 집기 전:
        // Rigidbody 중력 정상 작동
        //
        // 손에 들었을 때:
        // PlayerHand에서 물리 일시 정지
        //
        // 박스에 놓은 후:
        // Collider가 꺼지는 물건은 위치 고정
        //
        // 이를 통해 장난감이 박스 바닥을
        // 통과하여 떨어지는 현상 방지
        // ============================================

        if (!hideOnPlace &&
            disablePlacedColliders &&
            placedItem != null)
        {
            Rigidbody rb =
                placedItem.GetComponent<Rigidbody>();

            if (rb != null)
            {
                // 기존 이동 속도 제거
                rb.linearVelocity = Vector3.zero;

                // 기존 회전 속도 제거
                rb.angularVelocity = Vector3.zero;

                // 물리 시뮬레이션으로 움직이지 않도록 고정
                rb.isKinematic = true;
            }
        }

        // ============================================
        // 기존 기능: 놓은 물건의 Collider 비활성화
        // ============================================

        if (disablePlacedColliders && placedItem != null)
        {
            foreach (Collider c in
                placedItem.GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }
        }

        // 배치한 물건 수 증가
        PlacedCount++;

        // 배치 효과음
        AudioManager.Play(placeClip, placeVolume);

        // 물건 하나 배치 시 이벤트
        onPlaced.Invoke();

        // 모든 물건 배치 완료
        if (PlacedCount >= acceptItems.Length)
        {
            onAllPlaced.Invoke();
        }
    }

    // 대사 출력
    static void PlayDialogue(DialogueStep step)
    {
        if (step != null && !step.IsPlaying)
        {
            step.Play();
        }
    }

    // 다음 물건을 놓을 위치 결정
    Transform GetNextPlacePoint()
    {
        if (placePoints != null &&
            PlacedCount < placePoints.Length &&
            placePoints[PlacedCount] != null)
        {
            return placePoints[PlacedCount];
        }

        if (placePoint != null)
        {
            return placePoint;
        }

        return transform;
    }
}
