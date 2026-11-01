using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TeamRoulette.UI;

// 미니게임 4 - 문고리 돌려 열기 (손가락으로 다 쥔 뒤). 미니게임3의 CupLift와 같은 구조.
// 원리 (매 프레임 위에서부터 차례로 판단):
//   1) 손가락이 풀림 (엄지가 풀렸거나, looseCount개 이상 풀림) → 손잡이가 빠르게 제자리로 되돌아감 (slipBack)
//   2) 마우스 휠을 굴림 → 굴린 만큼 손잡이가 돌아감 (turnPerScroll)
//   3) 그 밖 (휠을 안 굴림) → 진짜 문고리처럼 스프링 힘으로 천천히 되돌아감 (springBack)
//   - 손잡이가 targetAngle까지 돌면 성공 → 쥐기 끝 → onUnlatched → 문이 openAngle만큼 스르륵 열림 → onOpened
//   - 실패는 없다. 손가락이 풀리면 되돌아갈 뿐이고, 오래 걸리면 FingerGrip의 구제가 점점 쉽게 만든다.
// 손이 손잡이와 같이 돌아가는 원리: 손이 갈 자리(HandAnchor의 Grip Point)를 Knob_Pivot의 자식으로 두면,
//   손잡이가 돌 때 그 자리도 같이 돌고, HandAnchor가 매 프레임 손을 그 자리에 붙잡아 두므로 손도 같이 돈다.
// 사용: 빈 오브젝트에 붙이고 Grip / Knob / Hinge를 연결한다. FingerGrip(Lift After Grip 체크)의 On Gripped에 Begin을 연결.
public class KnobTurn : MonoBehaviour
{
    [Header("연결")]
    public FingerGrip grip;                  // 손가락 (Lift After Grip 체크 → 돌리는 동안에도 손가락이 계속 풀림)
    public Transform knob;                   // 돌아갈 손잡이 (Knob_Pivot, 로컬 Z축으로 돎)
    public Transform hinge;                  // 열릴 문 (Door_Hinge, 로컬 Y축으로 돎)

    [Header("돌리기")]
    public float targetAngle = 60f;          // 이만큼(도) 돌리면 성공
    public float turnPerScroll = 6f;         // 휠 한 칸에 도는 각도 (도)
    public bool scrollDown = true;           // 체크: 휠을 아래로(몸 쪽으로) 굴려야 돎 / 해제: 위로
    public float turnSign = 1f;              // 손잡이 도는 방향 (반대로 돌면 -1)

    [Header("되돌아가기")]
    public float springBack = 20f;           // 휠을 안 굴리면 되돌아가는 빠르기 (도/초)
    [Range(0f, 1f)] public float looseLevel = 0.5f;   // 손가락 굽힘이 이보다 작으면 "풀린 손가락"
    public int looseCount = 3;               // 이 개수 이상 풀리면 미끄러짐
    public bool thumbMatters = true;         // 체크: 엄지 하나만 풀려도 미끄러짐
    public float slipBack = 120f;            // 손가락이 풀렸을 때 되돌아가는 빠르기 (도/초)

    [Header("문 열기")]
    public float openAngle = 25f;            // 문이 열리는 각도 (도, 반대로 열리면 -로)
    public float openTime = 1.2f;            // 열리는 데 걸리는 시간 (초)

    [Header("이벤트")]
    public UnityEvent onUnlatched;           // 손잡이를 다 돌린 순간 (철컥 소리, 안내 자막 Hide 등)
    public UnityEvent onOpened;              // 문이 다 열린 뒤 (대사 → 손 되돌리기 → 다음 방 전환)

    [Header("확인용 (Play 중 값 보기)")]
    public string state;                     // 지금 상태 (미끄러짐 / 돌아감 / 되돌아감)
    public float angle;                      // 지금 돌린 각도 (도)

    bool turning;
    bool homeSet;
    Quaternion knobHome;                     // 손잡이 처음 회전
    Quaternion hingeHome;                    // 문 처음 회전
    GameUI ui;

    // 돌리기 시작 (FingerGrip의 On Gripped에 연결)
    public void Begin()
    {
        if (turning || knob == null || grip == null) return;
        if (ui == null) ui = FindFirstObjectByType<GameUI>();
        if (!homeSet)
        {
            knobHome = knob.localRotation;
            if (hinge != null) hingeHome = hinge.localRotation;
            homeSet = true;
        }
        angle = 0f;
        turning = true;
        Apply();
    }

    void Update()
    {
        if (!turning) return;
        if (ui != null && (ui.IsDialogueOpen || ui.IsPaused)) return;   // 대사·설정창 중엔 멈춤

        float dt = Time.deltaTime;
        float wheel = Input.mouseScrollDelta.y;                         // 위로 + / 아래로 -
        float turn = Mathf.Max(0f, scrollDown ? -wheel : wheel);        // 맞는 방향으로 굴린 칸 수

        // 풀린 손가락 세기
        int loose = 0;
        for (int i = 0; i < 5; i++) if (grip.progress[i] < looseLevel) loose++;
        bool thumbLoose = grip.progress[4] < looseLevel;               // 4번 = 엄지

        if ((thumbMatters && thumbLoose) || loose >= looseCount)
        {
            state = "미끄러짐";
            angle -= slipBack * dt;                                     // 1) 손가락이 풀림 → 빠르게 되돌아감
        }
        else if (turn > 0f)
        {
            state = "돌아감";
            angle += turn * turnPerScroll;                              // 2) 휠 → 돌아감
        }
        else
        {
            state = "되돌아감";
            angle -= springBack * dt;                                   // 3) 가만히 → 스프링처럼 천천히 되돌아감
        }

        angle = Mathf.Clamp(angle, 0f, targetAngle);
        Apply();

        if (angle >= targetAngle)
        {
            turning = false;
            grip.End();                                                 // 손가락은 쥔 모양 그대로 멈춤
            Debug.Log("미니게임 4: 문고리 다 돌림");
            onUnlatched.Invoke();
            StartCoroutine(OpenRoutine());
        }
    }

    // 손잡이를 지금 각도만큼 (로컬 Z축)
    void Apply()
    {
        knob.localRotation = knobHome * Quaternion.Euler(0f, 0f, angle * turnSign);
    }

    // 문을 openAngle만큼 스르륵 열기 (로컬 Y축)
    IEnumerator OpenRoutine()
    {
        if (hinge != null)
        {
            for (float k = 0f; k < 1f; k += Time.deltaTime / openTime)
            {
                hinge.localRotation = hingeHome * Quaternion.Euler(0f, openAngle * Mathf.SmoothStep(0f, 1f, k), 0f);
                yield return null;
            }
            hinge.localRotation = hingeHome * Quaternion.Euler(0f, openAngle, 0f);
        }
        onOpened.Invoke();
    }

    // 처음 상태로 되돌리기 (테스트용: 손잡이·문 원위치)
    [ContextMenu("테스트: 원위치 (ResetDoor)")]
    public void ResetDoor()
    {
        if (!homeSet) return;
        StopAllCoroutines();
        turning = false;
        angle = 0f;
        knob.localRotation = knobHome;
        if (hinge != null) hinge.localRotation = hingeHome;
    }
}