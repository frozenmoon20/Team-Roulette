using UnityEngine;
using UnityEngine.SocialPlatforms.GameCenter;

// 들 수 있는 물건에 붙인다. Collider 필요.

public class GrabbableItem : MonoBehaviour, IInteractable, IHasName
{
    public string displayName = "";
    public string DisplayName => displayName;     // IHasName 약속: 이름

    [Header("고급: 손 안에서 위치·각도 미세 조정 (손이 보이는 사람만)")]
    public Vector3 positionOffset;
    public Vector3 rotationOffset;

    public string lockedMessage = "손에 힘이 들어가지 않는다.";   // Grab 해금 전 클릭했을 때

    // 원래 자리 (우클릭으로 되돌릴 때 사용)
    Transform homeParent;
    Vector3 homePos;
    Quaternion homeRot;


    // 지금 잡을 수 있는가: Grab 해금 + 손이 보임
    public bool CanGrab
    {
        get
        {
            bool grabUnlocked = OptionState.Instance != null && OptionState.Instance.IsUnlocked(OptionType.Grab);
            PlayerHand hand = PlayerHand.Instance;
            return grabUnlocked && hand != null && hand.IsVisible && hand.Held == null;
        }
    }

    Vector3 localBottom;

    void Awake()
    {
        homeParent = transform.parent;
        homePos = transform.position;
        homeRot = transform.rotation;

        if (GetComponentInChildren<Collider>() != null)
        {
            Debug.LogWarning("Collider가 없어서 클릭이 안 됩니다 → " + name, this);
        }

        // 실제 모양의 가운데를 한 번에 계산해두기
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            Vector3 bottom = new Vector3(b.center.x, b.min.y, b.center.z);
            localBottom = transform.InverseTransformVector(bottom - transform.position);
        }

    }


    // IInteractable 약속: 클릭하면 실행
    public void Interact()
    {
        PlayerHand hand = PlayerHand.Instance;

        // 1) 손 자체가 아직 없음
        if (hand == null || !hand.IsVisible) { PlayerInteractor.ShowHint(LookMessage()); return; }

        // 2) 손은 있지만 아직 쥘 수 없음
        if (!CanGrab) { PlayerInteractor.ShowHint(lockedMessage); return; }

        hand.Hold(this);
    }

    // 원래 자리로 되돌리기 (PlayerHand가 호출)
    public void ReturnHome()
    {
        transform.SetParent(homeParent, true);
        transform.SetPositionAndRotation(homePos, homeRot);
    }

    // 특정 자리에 똑바로 세워 두기 (PlaceSpot이 호출). 바닥을 point에 맞춤
    public void PlaceAt(Transform point)
    {
        transform.SetParent(point, true);
        transform.rotation = point.rotation;
        transform.position = point.position - transform.TransformVector(localBottom);

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;    // 놓인 자리에 고정
    }

    // 손바닥 위에 올리기 (PlayerHand가 호출) 물건의 바닥이 point에 닿고, point의 윗쪽 방향으로 서게 됨
    public void AttachTo(Transform point)
    {
        transform.SetParent(point, true); 
        transform.rotation = point.rotation * Quaternion.Euler(rotationOffset);
        transform.position = point.TransformPoint(positionOffset) - transform.TransformVector(localBottom);   
    }

    // 이름 + "다/이다" (마지막 글자에 받침이 있으면 "이다")
    string LookMessage()
    {
        if (string.IsNullOrEmpty(displayName)) return "";
        char last = displayName[displayName.Length - 1];
        bool hasBatchim = last >= '가' && last <= '힣' && (last - '가') % 28 != 0;
        return displayName + (hasBatchim ? "이다." : "다.");
    }

}