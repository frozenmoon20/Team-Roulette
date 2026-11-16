using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// 플레이어가 지정 자리(viewPoint)로 걸어가 그쪽을 바라보게 했다가, 다시 원래 자리·방향으로 돌려놓는다.
// 컴퓨터 화면 보기, 미니게임3(컵 쥐기), 문고리 등에서 같이 쓴다.
// 원리: 카메라만 따로 날려 보내면 몸과 시점이 따로 놀아 부자연스럽다. 그래서 플레이어 몸(좌우 방향 + 위치)과
//       고개(PlayerCameraRoot의 위아래 각도)를 직접 움직인다. 사람이 하듯 "먼저 고개를 돌리고 → 다가간다" 순서로 겹쳐 움직인다.
//       - viewPoint의 위치 = 눈이 갈 자리. (Keep Eye Height를 체크하면 높이는 무시하고 서 있는 눈높이 유지)
//       - viewPoint의 방향 = 바라볼 방향 (좌우는 몸, 위아래는 고개).
//       - 보는 동안 다른 스크립트가 고개 각도를 덮어써도 매 프레임 마지막(LateUpdate)에 다시 맞춘다.
// 사용: 빈 오브젝트에 붙이고 View Point에 바라볼 자리(빈 오브젝트)를 넣는다. 다른 이벤트에서 Focus() / Return()을 부른다.
// 더 가까이 보기: 보는 중에 ShiftTo(다른 자리)를 부르면 그 자리로 고개를 들이밀고, ShiftBack()으로 원래 보던 자리로 돌아온다.
//                 (예: 서랍 자물쇠를 보다가 그 아래 포스트잇을 가까이 들여다보기). 이때는 onArrived/onReturned가 불리지 않는다.
// 이름 지우기: Focus()하면 화면 아래 오브젝트 이름·조준점이 바로 사라진다 (PlayerInteractor를 Disable While Focused에 넣어 둔 경우).
// 즉시 되돌리기: 화면이 가려진 동안 연출 없이 바로 원래 자리로 돌려놓으려면 ReturnInstant()를 부른다 (예: 액자 → 회상 하얀 화면).
public class ViewFocus : MonoBehaviour
{
    public Transform viewPoint;              // 눈이 갈 자리와 바라볼 방향 (씬에서 맞춘 빈 오브젝트)
    public Transform body;                   // 플레이어 몸 (비워 두면 FirstPersonPlayer가 붙은 오브젝트)
    public Transform cameraRoot;             // 고개 (비워 두면 PlayerInteractor가 붙은 PlayerCameraRoot)

    [Header("움직임")]
    public float moveTime = 1.0f;            // 다 옮겨 가는 데 걸리는 시간 (초)
    [Range(0.2f, 1f)] public float turnPortion = 0.6f;    // 전체 시간 중 고개 돌리기가 끝나는 지점 (0.6 = 앞 60% 동안 돌림)
    [Range(0f, 0.8f)] public float stepDelay = 0.2f;      // 전체 시간 중 다가가기가 시작되는 지점 (0.2 = 고개를 조금 돌린 뒤 출발)
    public bool keepEyeHeight = false;       // 체크: 서 있는 눈높이 그대로 / 해제: viewPoint 높이까지 내려감 (의자에 앉는 느낌)

    [Header("보는 동안")]
    public Behaviour[] disableWhileFocused;  // 끌 스크립트 (FirstPersonPlayer, PlayerMotionFeel, PlayerInteractor 등)
    public GameObject[] hideWhileFocused;    // 숨길 오브젝트 (조준점 등, 비워도 됨)

    public UnityEvent onArrived;             // 다 옮겨 간 뒤 실행 (컴퓨터 화면 UI, 선택창 열기 등)
    public UnityEvent onReturned;            // 원래 자리로 돌아온 뒤 실행

    [Header("더 가까이 보기 (ShiftTo / ShiftBack)")]
    public float shiftTime = 0.6f;           // 가까이 들여다보거나 돌아오는 데 걸리는 시간 (초)

    public bool IsFocused { get; private set; }   // 지정 자리에 가 있는 동안 true
    public bool IsMoving => moving;               // 움직이는 중이면 true (다른 스크립트가 "다 갈 때까지 기다리기"에 씀)

    CharacterController cc;
    Vector3 homePos;                         // 원래 몸 위치
    float homeYaw;                           // 원래 몸 좌우 방향
    Quaternion homeHead;                     // 원래 고개 각도
    Vector3 eyeOffset;                       // 몸 기준 눈(고개) 위치 (몸이 돌아도 변하지 않는 값)
    bool moving;
    bool holdHead;                           // 도착 후 고개 각도를 계속 붙잡아 두기
    Quaternion heldHead;
    Vector3 viewPos;                         // viewPoint에 도착했을 때 몸 위치 (ShiftBack으로 돌아올 자리)
    float viewYaw;
    Quaternion viewHead;
    bool shifted;                            // ShiftTo로 다른 자리에 가 있는지

    // 지정 자리로 가기
    [ContextMenu("테스트: 가기 (Focus)")]
    public void Focus()
    {
        if (IsFocused || moving || viewPoint == null) return;
        if (!FindRig()) return;

        // 걷던 중이면 미끄러지지 않게 멈추고, 조작을 끈다
        FirstPersonPlayer player = body.GetComponent<FirstPersonPlayer>();
        if (player != null) player.StopMovement();
        foreach (Behaviour b in disableWhileFocused) if (b != null) b.enabled = false;
        foreach (GameObject g in hideWhileFocused) if (g != null) g.SetActive(false);
        if (cc != null) cc.enabled = false;   // 켜져 있으면 위치를 직접 바꿔도 무시됨

        homePos = body.position;
        homeYaw = body.eulerAngles.y;
        homeHead = cameraRoot.localRotation;
        eyeOffset = body.InverseTransformPoint(cameraRoot.position);

        IsFocused = true;
        StartCoroutine(Move(true));
        StartCoroutine(ClearAimNextFrame());
    }

    // 클릭하자마자 화면 아래 오브젝트 이름·조준점·손 아이콘을 지운다 (화면이 지저분하지 않게)
    //   한 프레임 뒤에 지우는 이유: 클릭은 PlayerInteractor 안에서 불려서, 같은 프레임에 PlayerInteractor가
    //   이름을 한 번 더 그리고 끝난다. 그 뒤로는 꺼져 있으니(Disable While Focused) 지운 상태가 유지된다.
    //   돌아오면 PlayerInteractor가 다시 켜지면서 알아서 새로 그린다.
    IEnumerator ClearAimNextFrame()
    {
        yield return null;
        PlayerInteractor pi = FindFirstObjectByType<PlayerInteractor>();
        if (pi == null || pi.enabled) yield break;   // 켜져 있으면 어차피 다시 그리므로 건드리지 않음
        if (pi.nameText != null) pi.nameText.text = "";
        if (pi.handIcon != null) pi.handIcon.SetActive(false);
        if (pi.crosshairDot != null) pi.crosshairDot.SetActive(false);
    }

    // 원래 자리로 돌아오기
    [ContextMenu("테스트: 돌아오기 (Return)")]
    public void Return()
    {
        if (!IsFocused || moving) return;
        shifted = false;
        StartCoroutine(Move(false));
    }

    // 보는 중에 다른 자리로 더 가까이 들여다보기 (눈이 target 위치로, target 방향을 바라봄. 높이도 target 그대로)
    public void ShiftTo(Transform target)
    {
        if (!IsFocused || moving || target == null) return;
        Vector3 e = target.eulerAngles;
        float yaw = e.y;
        Quaternion head = Quaternion.Euler(e.x, 0f, 0f);
        Vector3 pos = target.position - Quaternion.Euler(0f, yaw, 0f) * eyeOffset;
        shifted = true;
        StartCoroutine(Shift(pos, yaw, head));
    }

    // 더 가까이 보던 것을 끝내고 원래 보던 자리(viewPoint)로
    public void ShiftBack()
    {
        if (!IsFocused || moving || !shifted) return;
        shifted = false;
        StartCoroutine(Shift(viewPos, viewYaw, viewHead));
    }

    IEnumerator Shift(Vector3 toPos, float toYaw, Quaternion toHead)
    {
        moving = true;
        holdHead = false;
        Vector3 fromPos = body.position;
        float fromYaw = body.eulerAngles.y;
        Quaternion fromHead = cameraRoot.localRotation;

        for (float t = 0f; t < 1f; t += Time.deltaTime / shiftTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            body.position = Vector3.Lerp(fromPos, toPos, k);
            body.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(fromYaw, toYaw, k), 0f);
            cameraRoot.localRotation = Quaternion.Slerp(fromHead, toHead, k);
            yield return null;
        }

        body.position = toPos;
        body.rotation = Quaternion.Euler(0f, toYaw, 0f);
        cameraRoot.localRotation = toHead;
        heldHead = toHead;
        holdHead = true;
        moving = false;
    }

    bool FindRig()
    {
        if (body == null)
        {
            FirstPersonPlayer p = FindFirstObjectByType<FirstPersonPlayer>();
            if (p != null) body = p.transform;
        }
        if (cameraRoot == null)
        {
            PlayerInteractor pi = FindFirstObjectByType<PlayerInteractor>();
            if (pi != null) cameraRoot = pi.transform;
        }
        if (body == null || cameraRoot == null)
        {
            Debug.LogWarning("ViewFocus: Body 또는 Camera Root를 찾지 못했어요. 칸에 직접 넣어 주세요.", this);
            return false;
        }
        cc = body.GetComponent<CharacterController>();
        return true;
    }

    IEnumerator Move(bool toView)
    {
        moving = true;
        holdHead = false;

        // 출발 상태
        Vector3 fromPos = body.position;
        float fromYaw = body.eulerAngles.y;
        Quaternion fromHead = cameraRoot.localRotation;

        // 도착 상태
        Vector3 toPos;
        float toYaw;
        Quaternion toHead;
        if (toView)
        {
            Vector3 e = viewPoint.eulerAngles;
            toYaw = e.y;
            toHead = Quaternion.Euler(e.x, 0f, 0f);
            // 눈이 viewPoint에 오도록 몸 위치를 거꾸로 계산 (Keep Eye Height면 높이는 그대로)
            toPos = viewPoint.position - Quaternion.Euler(0f, toYaw, 0f) * eyeOffset;
            if (keepEyeHeight) toPos.y = homePos.y;
        }
        else
        {
            toPos = homePos;
            toYaw = homeYaw;
            toHead = homeHead;
        }

        for (float t = 0f; t < 1f; t += Time.deltaTime / moveTime)
        {
            // 고개(방향)는 앞부분에 먼저, 위치는 조금 늦게 출발해서 끝까지
            float kTurn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / turnPortion));
            float kStep = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - stepDelay) / (1f - stepDelay)));

            body.position = Vector3.Lerp(fromPos, toPos, kStep);
            body.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(fromYaw, toYaw, kTurn), 0f);
            cameraRoot.localRotation = Quaternion.Slerp(fromHead, toHead, kTurn);
            yield return null;
        }

        body.position = toPos;
        body.rotation = Quaternion.Euler(0f, toYaw, 0f);
        cameraRoot.localRotation = toHead;
        moving = false;

        if (toView)
        {
            heldHead = toHead;
            holdHead = true;
            viewPos = toPos;                 // 더 가까이 보기에서 돌아올 자리로 기억
            viewYaw = toYaw;
            viewHead = toHead;
            onArrived.Invoke();
        }
        else
        {
            FinishReturn();
        }
    }

    // 원래 자리·방향으로 "즉시" 돌려놓기 (움직이는 연출 없이)
    // 화면이 하얗게/까맣게 가려져 있을 때 몰래 되돌려 놓는 용도 (예: 액자 → 회상으로 넘어가는 하얀 화면)
    public void ReturnInstant()
    {
        if (!IsFocused) return;
        StopAllCoroutines();                 // 가는 중/돌아오는 중/들여다보는 중이었으면 멈춤
        moving = false;
        holdHead = false;
        shifted = false;
        body.position = homePos;
        body.rotation = Quaternion.Euler(0f, homeYaw, 0f);
        cameraRoot.localRotation = homeHead;
        FinishReturn();
    }

    // 돌아온 뒤 정리: 조작·숨긴 오브젝트를 원래대로
    void FinishReturn()
    {
        IsFocused = false;
        if (cc != null) cc.enabled = true;
        foreach (Behaviour b in disableWhileFocused) if (b != null) b.enabled = true;
        foreach (GameObject g in hideWhileFocused) if (g != null) g.SetActive(true);
        onReturned.Invoke();
    }

    // 보는 동안 다른 스크립트가 고개 각도를 바꿔도 마지막에 다시 맞춘다
    void LateUpdate()
    {
        if (holdHead && cameraRoot != null) cameraRoot.localRotation = heldHead;
    }
}