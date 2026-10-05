using UnityEngine;
using UnityEngine.Events;

// 물건을 놓을 자리에 붙인다 (쓰레기통, 선반 등). Collider 필요.
// Accept Items 목록의 물건을 들고 클릭하면 받는다. 목록을 다 받으면 On All Placed 실행.
public class PlaceSpot : MonoBehaviour, IInteractable, IHasName
{
    public string displayName = "";
    public GrabbableItem[] acceptItems;          // 받을 물건 (드래그로 추가)
    public Transform placePoint;                 // 놓일 위치 (비우면 이 오브젝트 위치). 초록 화살표가 위
    public bool hideOnPlace;                     // 체크하면 넣는 순간 사라짐 (쓰레기통)
    public string wrongItemMessage = "여기에 둘 건 아니다.";
    public UnityEvent onAllPlaced;               // 목록을 다 받았을 때 할 일

    public string DisplayName => displayName;    // IHasName 약속: 이름
    public int PlacedCount { get; private set; } // 지금까지 받은 개수

    // 이 물건을 받을 수 있는가 (목록에 있는가)
    public bool CanAccept(GrabbableItem item)
    {
        if (item == null || acceptItems == null) return false;
        return System.Array.IndexOf(acceptItems, item) >= 0;
    }

    // IInteractable 약속: 클릭하면 실행
    public void Interact()
    {
        PlayerHand hand = PlayerHand.Instance;
        if (hand == null || hand.Held == null) return;                       // 빈손이면 아무 일 없음
        if (!CanAccept(hand.Held)) { PlayerInteractor.ShowHint(wrongItemMessage); return; }

        hand.PlaceHeld(placePoint != null ? placePoint : transform, hideOnPlace);
        PlacedCount++;

        if (PlacedCount >= acceptItems.Length)
        {
            enabled = false;          // 다 받았으면 더 이상 조준·클릭 대상 아님
            onAllPlaced.Invoke();
        }
    }
}