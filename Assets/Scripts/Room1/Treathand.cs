using UnityEngine;

// 회상(미니게임5) 동안 손에 간식을 들고, 좌클릭을 누른 채 마우스를 좌우로 움직이면 손을 앞으로 내밀어 간식을 흔든다.
// 원리:
//   - Show(): 간식 오브젝트를 손의 쥐는 자리(Hold Point)에 붙이고 손가락을 쥔 모양으로 바꾼다.
//             그리고 손 자세를 "간식 보여 주는 자세(Treat Pose)"로 스르륵 바꾼다 (잡기로 물건을 들 때 손바닥이 뒤집히는 것과 같은 방식).
//   - 손 자세: PlayerHand가 매 프레임 손 뼈(J_Right)를 평소 자세로 정한다. 이 스크립트는 그 "다음에" 실행되어(DefaultExecutionOrder)
//             손 뼈를 Treat Pose 쪽으로 옮긴다. → Play 중에 Treat Pose를 움직이면 손이 바로 따라와서 각도 맞추기가 쉽다.
//   - 손(HandRoot)의 자리는 원래 HandSway가 매 프레임 정한다. 여기에 "앞으로 내밀기 + 좌우 흔들기"만큼을 더한다.
//             그래서 걷기·숨쉬기 흔들림은 그대로 남는다.
//   - 좌우 흔들기: 마우스를 움직인 만큼 손이 그쪽으로 밀리고, 손을 멈추면 가운데로 돌아온다. 손목도 같이 까딱인다.
//   - Hide(): 간식을 숨기고 손가락 모양·손 자세를 원래대로 (자세는 스르륵 돌아감).
// 이 스크립트는 "보이는 것"만 담당한다. 겨울이 관심도 계산은 HurdleTraining이 한다.
// 사용: MG5_Hurdle에 붙이고 Treat(간식), Hold Point(간식 쥘 자리), Treat Pose(손 자세)를 넣는다.
//       HurdleTraining의 Treat Hand 칸에 넣으면 훈련 시작 때 Show, 클리어 때 Hide가 자동으로 불린다.
[DefaultExecutionOrder(1000)]   // PlayerHand(손 자세)·HandSway(손 흔들림)가 정한 뒤에 실행
public class TreatHand : MonoBehaviour
{
    [Header("간식")]
    public GameObject treat;                 // 간식 오브젝트 (Collider 삭제, 처음엔 꺼 둠)
    public Transform holdPoint;              // 간식을 붙일 자리 (J_Right 아래 손가락 안쪽 빈 오브젝트. 비우면 HandRoot)
    [Range(0f, 1f)] public float gripAmount = 0.55f;   // 쥘 때 손가락 굽힘
    public Vector3 treatOffset;              // 손 위 간식 위치 미세 조정 (m)
    public Vector3 treatRotation;            // 손 위 간식 각도 미세 조정 (도)

    [Header("손 자세 (잡기 때 손바닥이 뒤집히는 것처럼)")]
    public Transform treatPose;              // 간식을 보여 주는 손 자세. J_Right와 같은 부모 아래 빈 오브젝트 (PalmUpPose를 복제해서 만들면 편함)
    public float poseTime = 0.3f;            // 자세가 바뀌는 데 걸리는 시간 (초)

    [Header("내밀기 (좌클릭을 누르는 동안, 카메라 기준 m)")]
    public Vector3 reach = new Vector3(-0.04f, 0.05f, 0.12f);   // x = 좌우, y = 위, z = 앞
    public float blendSpeed = 6f;            // 내밀고 거두는 빠르기

    [Header("좌우 흔들기")]
    public float waveFollow = 0.004f;        // 마우스 움직임 1당 손이 밀리는 거리 (m)
    public float waveRange = 0.08f;          // 좌우로 최대 밀리는 거리 (m)
    public float waveReturn = 5f;            // 가운데로 돌아오는 빠르기
    public float tiltAngle = 20f;            // 끝까지 밀렸을 때 손목이 까딱이는 각도 (도)

    [System.NonSerialized] public bool handLocked;   // true면 손 자리(내밀기·흔들기)는 건드리지 않음 (다른 연출이 손을 옮기는 중, 예: 간식 주기)

    [Header("확인용")]
    public bool waving;                      // 지금 흔드는 중인지
    public float waveX;                      // 지금 좌우로 밀린 거리

    Transform hand;                          // HandRoot
    Transform handBone;                      // J_Right (PlayerHand의 Hand Bone)
    PlayerHand playerHand;
    HandPose pose;
    HandSway sway;
    Vector3 basePos;                         // HandSway가 없을 때 쓸 원래 자리
    Quaternion baseRot;
    float[] savedFingers = new float[5];
    float blend;                             // 내밀기 정도 (0~1)
    float poseBlend;                         // 손 자세 (0 = 평소, 1 = Treat Pose)
    bool showing;

    // 간식을 손에 들기 (HurdleTraining.Begin이 부름)
    public void Show()
    {
        playerHand = PlayerHand.Instance;
        if (playerHand == null) { Debug.LogWarning("TreatHand: 손(PlayerHand)을 찾지 못했어요.", this); return; }
        hand = playerHand.transform;
        handBone = playerHand.handBone;
        pose = hand.GetComponent<HandPose>();
        sway = hand.GetComponent<HandSway>();
        if (showing) return;

        basePos = hand.localPosition;
        baseRot = hand.localRotation;

        // 손가락 모양 기억 → 간식 쥐는 모양으로
        if (pose != null)
        {
            for (int f = 0; f < 5; f++) savedFingers[f] = pose.GetFinger((HandPose.Finger)f);
            playerHand.SetGrip(gripAmount);
        }

        // 간식을 쥐는 자리에 붙이기
        if (treat != null)
        {
            Transform point = holdPoint != null ? holdPoint : hand;
            treat.transform.SetParent(point, false);
            treat.transform.localPosition = treatOffset;
            treat.transform.localRotation = Quaternion.Euler(treatRotation);
            treat.SetActive(true);
        }

        blend = 0f;
        waveX = 0f;
        showing = true;
    }

    // 간식 숨기고 손 원래대로 (HurdleTraining 클리어 때 부름)
    public void Hide()
    {
        if (!showing) return;
        showing = false;
        waving = false;
        handLocked = false;
        if (treat != null) treat.SetActive(false);

        if (pose != null)
            for (int f = 0; f < 5; f++) pose.SetFinger((HandPose.Finger)f, savedFingers[f]);
        if (hand != null && !SwayOn()) hand.SetLocalPositionAndRotation(basePos, baseRot);
        // 손 자세(poseBlend)는 LateUpdate에서 스르륵 0으로 돌아감
    }

    void LateUpdate()
    {
        if (hand == null) return;
        float dt = Time.deltaTime;

        // 1) 손 자세: 보여 주는 동안 Treat Pose로, 끝나면 평소 자세로 (PlayerHand가 정한 평소 자세에서 섞음)
        poseBlend = Mathf.MoveTowards(poseBlend, showing ? 1f : 0f, dt / Mathf.Max(poseTime, 0.01f));
        if (handBone != null && treatPose != null && poseBlend > 0f)
        {
            float p = Mathf.SmoothStep(0f, 1f, poseBlend);
            handBone.localPosition = Vector3.Lerp(handBone.localPosition, treatPose.localPosition, p);
            handBone.localRotation = Quaternion.Slerp(handBone.localRotation, treatPose.localRotation, p);
        }

        if (!showing || handLocked) return;

        // 2) 내밀기: 좌클릭을 누르는 동안 앞으로, 떼면 거둠
        waving = Input.GetMouseButton(0) && Cursor.lockState == CursorLockMode.Locked;
        blend = Mathf.MoveTowards(blend, waving ? 1f : 0f, blendSpeed * dt);
        float k = Mathf.SmoothStep(0f, 1f, blend);

        // 3) 좌우 흔들기: 마우스를 움직인 만큼 밀리고, 가만히 있으면 가운데로
        if (waving) waveX += Input.GetAxis("Mouse X") * waveFollow;
        waveX = Mathf.Clamp(waveX, -waveRange, waveRange);
        waveX = Mathf.Lerp(waveX, 0f, 1f - Mathf.Exp(-waveReturn * dt));

        Vector3 offset = reach * k + Vector3.right * waveX * k;
        Quaternion tilt = Quaternion.Euler(0f, 0f, -(waveX / Mathf.Max(waveRange, 0.0001f)) * tiltAngle * k);

        if (SwayOn())
        {
            // HandSway가 이번 프레임에 정한 자리에 더하기
            hand.localPosition += offset;
            hand.localRotation *= tilt;
        }
        else
        {
            // HandSway가 꺼져 있으면 원래 자리 기준으로
            hand.localPosition = basePos + offset;
            hand.localRotation = baseRot * tilt;
        }
    }

    bool SwayOn()
    {
        return sway != null && sway.isActiveAndEnabled;
    }
}