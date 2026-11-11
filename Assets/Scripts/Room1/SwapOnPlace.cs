using UnityEngine;

// 잡을 수 있는 물건(GrabbableItem)을 PlaceSpot(빨래통 등)에 넣었을 때 모양을 다른 모델로 바꾼다.
// 예: 의자에 걸쳐 있던 옷(SM_Clothhanging_02) → 빨래통 안에서는 뭉친 옷 모델로
// 원리:
//   - PlaceSpot이 물건을 넣으면 물건이 놓일 자리(Place Point)의 자식이 된다 → 부모가 바뀌었다는 알림(OnTransformParentChanged)이 온다.
//     새 부모 위쪽에 PlaceSpot이 있으면 "넣어진 것"으로 판단한다. (손에 들 때도 부모가 바뀌지만 그땐 위쪽에 PlaceSpot이 없음)
//   - 그 프레임 마지막(LateUpdate)에 (PlaceSpot이 자리·방향을 다 정한 뒤):
//       1) 원래 모양(Hide Renderers)을 숨기고
//       2) 넣은 뒤 모양(Placed Model)을 켜서, Place Point 방향으로 놓고, 밑면 가운데가 Place Point에 딱 닿게 맞춘다
//       3) 이 물건의 Collider를 모두 끈다 (다시 집을 일이 없고, 다음 옷을 넣으려고 빨래통을 클릭할 때 방해되지 않게)
// 사용: 옷(GrabbableItem이 붙은 오브젝트)에 붙인다.
//   - Placed Model: 뭉친 옷 모델을 이 옷의 "자식"으로 넣고 체크를 꺼(비활성) 둔다. 위치·각도는 아무렇게나 둬도 자동으로 맞춤
//   - Hide Renderers: 원래 모양의 Mesh Renderer (이 오브젝트 자신에 붙어 있으면 이 오브젝트를 끌어 넣기)
public class SwapOnPlace : MonoBehaviour
{
    public GameObject placedModel;           // 넣은 뒤 보일 모델 (이 오브젝트의 자식, 처음엔 꺼 둠)
    public Renderer[] hideRenderers;         // 넣은 뒤 숨길 원래 모양

    bool pending;                            // 이번 프레임에 넣어졌는지
    bool done;                               // 이미 바꿨는지

    void OnTransformParentChanged()
    {
        if (done || transform.parent == null) return;
        if (transform.parent.GetComponentInParent<PlaceSpot>() != null) pending = true;
    }

    void LateUpdate()
    {
        if (!pending) return;
        pending = false;
        done = true;
        Swap();
    }

    void Swap()
    {
        foreach (Renderer r in hideRenderers) if (r != null) r.enabled = false;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        if (placedModel == null) return;

        Transform point = transform.parent;  // 놓인 자리 (Place Point)
        placedModel.SetActive(true);
        placedModel.transform.rotation = point.rotation;

        // 넣은 뒤 모델의 실제 크기로 밑면 가운데를 구해서 Place Point에 맞춤
        Renderer[] rs = placedModel.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        Vector3 bottom = new Vector3(b.center.x, b.min.y, b.center.z);
        placedModel.transform.position += point.position - bottom;
    }
}