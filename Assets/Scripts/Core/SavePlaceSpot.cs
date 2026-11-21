using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// PlaceSpot(쓰레기통, 빨래통, LP 자리 등)에 "어떤 물건을 넣었는지" 자동으로 저장하고,
// 이어하기 때 그 물건들을 다시 넣은 상태로 되돌리는 부품. 물건마다 따로 연결할 필요 없음.
// 원리: - 물건을 하나 넣을 때마다(PlaceSpot의 onPlaced) Accept Items를 훑어서, 넣어진 물건의 번호를 저장한다.
//         (저장 이름 = Id/번호. 예: Bedroom_Trash/3 → Accept Items의 3번 물건을 넣었음)
//       - 이어하기 때는 씬이 열리고 한 프레임 뒤, 저장된 번호의 물건을 PlaceSpot.RestorePlaced로 다시 넣는다.
//         (소리·대사·이벤트 없이 모양만. 쓰레기는 사라지고, 옷은 빨래통 안으로, LP판은 턴테이블 위로)
//       - 그다음 On Restored 실행 (예: LP 자리면 Lpplayer.Play로 음악 다시 틀기)
// 사용: PlaceSpot이 붙은 오브젝트에 같이 붙이고 Id만 적는다. (Accept Items의 순서를 나중에 바꾸면 저장이 어긋남)
[RequireComponent(typeof(PlaceSpot))]
public class SavePlaceSpot : MonoBehaviour
{
    [Tooltip("겹치지 않는 이름 (예: Bedroom_Trash)")]
    public string id = "";

    [Tooltip("이어하기로 물건을 되살린 뒤 할 일 (하나라도 넣었을 때만)")]
    public UnityEvent onRestored;

    PlaceSpot spot;

    void Awake()
    {
        spot = GetComponent<PlaceSpot>();
        spot.onPlaced.AddListener(SavePlaced);
    }

    IEnumerator Start()
    {
        yield return null;                   // 물건들이 처음 자리를 기억한 뒤에
        if (string.IsNullOrEmpty(id) || spot.acceptItems == null) yield break;

        bool any = false;
        for (int i = 0; i < spot.acceptItems.Length; i++)
        {
            GrabbableItem item = spot.acceptItems[i];
            if (item == null || !SaveSystem.HasFlag(Key(i))) continue;
            spot.RestorePlaced(item);
            any = true;
        }
        if (any) onRestored.Invoke();
    }

    // 물건을 넣을 때마다: 넣어진 물건 번호를 모두 저장
    void SavePlaced()
    {
        if (string.IsNullOrEmpty(id)) { Debug.LogWarning("SavePlaceSpot: Id가 비어 있어서 저장하지 못했어요.", this); return; }
        for (int i = 0; i < spot.acceptItems.Length; i++)
            if (IsPlaced(spot.acceptItems[i])) SaveSystem.SetFlag(Key(i));
    }

    // 넣어진 물건 = 사라졌거나(쓰레기통) 이 PlaceSpot 아래로 들어간 물건
    bool IsPlaced(GrabbableItem item)
    {
        if (item == null) return false;
        if (!item.gameObject.activeSelf) return true;
        return item.transform.IsChildOf(transform)
            || (spot.placePoint != null && item.transform.IsChildOf(spot.placePoint))
            || (spot.placePoints != null && System.Array.Exists(spot.placePoints, p => p != null && item.transform.IsChildOf(p)));
    }

    string Key(int i) => id + "/" + i;
}