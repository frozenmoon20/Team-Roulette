using UnityEngine;

// 들 수 있는 물건에 붙인다. Collider 필요.
// 클릭하면 손에 들고, 맞는 PlaceSpot을 클릭하면 그 자리에 놓인다. 우클릭은 원래 자리로.
// Grab이 해금되어 있어야 잡힌다. 해금 전에 클릭하면 lockedDialogue(대화창)가 나온다.
//   lockedDialogue를 비워 두면 예전처럼 화면 아래 짧은 글자(lockedMessage)가 뜬다 (기존 물건은 그대로 동작).
// 손 위 위치 맞추기: Play 중 물건을 손에 든 채로 Inspector의 Position/Rotation Offset 숫자를 바꾸면 바로 손 위에서 움직인다.
//   (숫자가 바뀔 때 Unity가 부르는 OnValidate에서 다시 손에 붙임) 맞춘 뒤 Play를 끄면 값이 돌아가므로,
//   끄기 전에 컴포넌트 ⋮ → Copy Component, 끈 뒤 ⋮ → Paste Component Values.
public class GrabbableItem : MonoBehaviour, IInteractable, IHasName
{
    public enum HoldStyle
    {
        [InspectorName("받쳐 들기 (손바닥 위)")] Palm,
        [InspectorName("감싸 쥐기 (컵, 손잡이)")] Grip
    }

    public string displayName = "";
    public HoldStyle holdStyle = HoldStyle.Palm;
    public string lockedMessage = "손에 힘이 들어가지 않는다.";
    public DialogueStep lockedDialogue;      // 잡기 해금 전 클릭했을 때 대사 (넣으면 위 글자 대신 이게 나옴)

    [Header("고급: 손 위 위치 미세 조정 (손이 보이는 사람만)")]
    public Vector3 positionOffset;
    public Vector3 rotationOffset;

    public string DisplayName => displayName;

    // 지금 잡을 수 있는가 (Grab 해금 + 손 보임 + 빈손)
    public bool CanGrab
    {
        get
        {
            PlayerHand hand = PlayerHand.Instance;
            return isActiveAndEnabled && GrabUnlocked() && hand != null && hand.IsVisible && hand.Held == null;
        }
    }

    Transform homeParent;
    Vector3 homePos;
    Quaternion homeRot;
    Vector3 localBottom;    // 물건 기준점 → 바닥 가운데 (물건 자기 좌표)
    Vector3 localCenter;    // 물건 기준점 → 몸통 가운데
    Transform heldPoint;    // 지금 붙어 있는 손 자리 (손에 들고 있을 때만)

    void Awake()
    {
        // 원래 자리 기억 (우클릭으로 돌려놓을 때 사용)
        homeParent = transform.parent;
        homePos = transform.position;
        homeRot = transform.rotation;

        if (GetComponentInChildren<Collider>() == null)
            Debug.LogWarning("GrabbableItem: Collider가 없어서 클릭이 안 됩니다 → " + name, this);

        // 모델 기준점이 엉뚱한 곳에 있어도 맞게, 실제 크기(Renderer)로 바닥/가운데 계산
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            localCenter = transform.InverseTransformVector(b.center - transform.position);
            localBottom = transform.InverseTransformVector(new Vector3(b.center.x, b.min.y, b.center.z) - transform.position);
        }
    }

    public void Interact()
    {
        if (!GrabUnlocked())
        {
            if (lockedDialogue != null) lockedDialogue.Play();     // 대화창으로
            else PlayerInteractor.ShowHint(lockedMessage);         // 대사가 없으면 예전처럼 글자로
            return;
        }
        if (PlayerHand.Instance != null) PlayerHand.Instance.Hold(this);
    }

    // 손에 붙이기 (PlayerHand가 호출). 받쳐 들기는 바닥을, 감싸 쥐기는 몸통 가운데를 기준점에 맞춤
    public void AttachTo(Transform point)
    {
        heldPoint = point;
        transform.SetParent(point, true);
        ApplyHold();
    }

    // 손 자리 + Offset으로 위치·방향 정하기
    void ApplyHold()
    {
        transform.rotation = heldPoint.rotation * Quaternion.Euler(rotationOffset);
        Vector3 anchor = holdStyle == HoldStyle.Palm ? localBottom : localCenter;
        transform.position = heldPoint.TransformPoint(positionOffset) - transform.TransformVector(anchor);
    }

    // 특정 자리에 똑바로 세워 두기 (PlaceSpot이 호출)
    public void PlaceAt(Transform point)
    {
        heldPoint = null;
        transform.SetParent(point, true);
        transform.rotation = point.rotation;
        transform.position = point.position - transform.TransformVector(localBottom);
    }

    // 원래 자리로 (우클릭)
    public void ReturnHome()
    {
        heldPoint = null;
        transform.SetParent(homeParent, true);
        transform.SetPositionAndRotation(homePos, homeRot);
    }

    // Play 중 Inspector에서 숫자를 바꾸면 Unity가 부름 → 손에 들고 있으면 바뀐 Offset으로 다시 붙임
    void OnValidate()
    {
        if (Application.isPlaying && heldPoint != null && transform.parent == heldPoint) ApplyHold();
    }

    static bool GrabUnlocked()
    {
        return OptionState.Instance != null && OptionState.Instance.IsUnlocked(OptionType.Grab);
    }
}