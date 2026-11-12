using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// 미니게임 3: 손을 컵 뒤의 "쥘 자리"(gripPoint)로 옮겨, 손가락을 쫙 편 채로 붙잡아 둔다.
// 원리:
//   - 손(HandRoot)은 원래 카메라에 붙어 있고, HandSway가 매 프레임 자리를 덮어쓴다.
//     → 미니게임 동안은 ViewFocus의 Disable 목록에 HandSway를 넣어 끄고, 이 스크립트가 손 자리를 정한다.
//   - Begin(): 지금 손 자리 → gripPoint 위치·방향으로 moveTime 동안 스르륵 이동, 손가락은 openCurl(0 = 쫙 폄)로.
//   - 도착 후에는 매 프레임 마지막(LateUpdate)에 gripPoint 자리 + 위로 lift(m)만큼에 손을 붙잡아 둔다.
//     (들어 올리기 스크립트가 lift 값을 올리면 손이 그만큼 올라가고, shake 값만큼 떨린다)
//   - Play 중에 gripPoint를 Scene에서 옮기면 손이 바로 따라온다 → 손 위치·각도 맞추기가 쉬움.
//   - Release(): 손 자리·손가락 모양을 미니게임 전으로 되돌린다.
// 사용: 빈 오브젝트에 붙이고 Grip Point(컵 뒤 빈 오브젝트)를 넣는다.
//       ViewFocus의 On Arrived에 Begin, 미니게임이 끝나면 Release를 연결한다.
public class HandAnchor : MonoBehaviour
{
    public Transform gripPoint;              // 손이 갈 자리와 방향 (컵 뒤 빈 오브젝트)
    public Transform hand;                   // 손 (비워 두면 PlayerHand가 붙은 HandRoot)
    public float moveTime = 0.6f;            // 손이 쥘 자리까지 가는 시간 (초)
    [Range(0f, 1f)] public float openCurl = 0f;   // 시작할 때 손가락 굽힘 (0 = 쫙 폄)

    [Header("들어 올리기 (다른 스크립트가 바꿈, Play 중 확인용)")]
    public float lift = 0f;                  // 쥘 자리에서 위로 얼마나 올라갔는지 (m)
    public Vector3 shake = Vector3.zero;     // 부들부들 떨림 (m, CupLift가 매 프레임 바꿈)

    public UnityEvent onArrived;             // 손이 쥘 자리에 도착한 뒤 실행 (FingerGrip.Begin 연결)

    public bool IsHolding { get; private set; }   // 쥘 자리에 붙잡혀 있는 동안 true

    HandPose pose;
    Vector3 homeLocalPos;                    // 미니게임 전 손 자리 (카메라 기준)
    Quaternion homeLocalRot;
    float[] savedFingers = new float[5];
    bool moving;

    // 손을 쥘 자리로
    [ContextMenu("테스트: 손을 쥘 자리로 (Begin)")]
    public void Begin()
    {
        if (IsHolding || moving || gripPoint == null) return;
        if (hand == null && PlayerHand.Instance != null) hand = PlayerHand.Instance.transform;
        if (hand == null) { Debug.LogWarning("HandAnchor: 손(HandRoot)을 찾지 못했어요. Hand 칸에 직접 넣어 주세요.", this); return; }
        if (pose == null) pose = hand.GetComponent<HandPose>();

        // 끝나고 되돌릴 수 있게 지금 상태 기억
        homeLocalPos = hand.localPosition;
        homeLocalRot = hand.localRotation;
        if (pose != null)
            for (int f = 0; f < 5; f++) savedFingers[f] = pose.GetFinger((HandPose.Finger)f);

        lift = 0f;
        shake = Vector3.zero;
        StartCoroutine(MoveIn());
    }

    IEnumerator MoveIn()
    {
        moving = true;
        if (pose != null) pose.SetAll(openCurl);       // 손가락 쫙 펴기 (HandPose가 부드럽게 바꿔 줌)

        Vector3 fromPos = hand.position;
        Quaternion fromRot = hand.rotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / moveTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            hand.SetPositionAndRotation(Vector3.Lerp(fromPos, Target(), k), Quaternion.Slerp(fromRot, gripPoint.rotation, k));
            yield return null;
        }

        moving = false;
        IsHolding = true;
        onArrived.Invoke();
    }

    // 손을 미니게임 전으로 되돌리기
    [ContextMenu("테스트: 손 되돌리기 (Release)")]
    public void Release()
    {
        if (!IsHolding && !moving) return;
        StopAllCoroutines();
        moving = false;
        IsHolding = false;
        lift = 0f;
        shake = Vector3.zero;
        hand.localPosition = homeLocalPos;
        hand.localRotation = homeLocalRot;
        if (pose != null)
            for (int f = 0; f < 5; f++) pose.SetFinger((HandPose.Finger)f, savedFingers[f]);
    }

    Vector3 Target()
    {
        return gripPoint.position + Vector3.up * lift + shake;
    }

    // 붙잡혀 있는 동안 매 프레임 마지막에 손 자리를 정함 (다른 스크립트보다 나중에)
    void LateUpdate()
    {
        if (IsHolding && hand != null && gripPoint != null)
            hand.SetPositionAndRotation(Target(), gripPoint.rotation);
    }
}