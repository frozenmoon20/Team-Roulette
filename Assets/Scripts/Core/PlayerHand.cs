using System.Collections;
using UnityEngine;

// 플레이어 손의 창구. 팀원은 이 스크립트만 쓰면 된다.
// HandRoot에 붙인다. 칸이 비어 있어도(손 에셋이 없는 PC) 에러 없이 동작한다.
public class PlayerHand : MonoBehaviour
{
    public static PlayerHand Instance { get; private set; }

    public GameObject handModel;             // Free Pack - VR Hands (Rigged)

    [Header("받쳐 들기")]
    public Transform handBone;               // J_Right (손 전체를 움직이는 뼈)
    public Transform palmUpPose;             // 손바닥이 위를 향한 자세 (Root 아래 빈 오브젝트)
    public Transform palmPoint;              // 물건이 놓일 손바닥 위 지점 (초록 화살표가 위)
    public float poseTime = 0.3f;            // 손이 뒤집히는 데 걸리는 시간 (초)
    public float palmCurl = 0.25f;           // 받칠 때 손가락을 살짝 오므림

    [Header("쥐기 (미니게임 3, 문고리용)")]
    public float gripAmount = 0.8f;

    public bool IsVisible { get; private set; }
    public GrabbableItem Held { get; private set; }   // 지금 들고 있는 물건 (없으면 null)

    HandPose pose;
    Vector3 restPos;                         // 평소 자세 (시작할 때 J_Right에서 기억)
    Quaternion restRot;
    float poseBlend;                         // 0 = 평소 자세, 1 = 손바닥 위
    float poseTarget;
    Rigidbody heldRb;
    bool heldWasKinematic;
    Collider[] heldColliders;

    float[] savedPose = new float[5];        // 들기 전 손가락 모양
    Coroutine placeRoutine;

    void Awake()
    {
        Instance = this;
        pose = GetComponent<HandPose>();
        if (handBone != null) { restPos = handBone.localPosition; restRot = handBone.localRotation; }
    }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void OnEnable() { OptionState.OnOptionChanged += HandleOptionChanged; }
    void OnDisable() { OptionState.OnOptionChanged -= HandleOptionChanged; }

    void Start() { Refresh(); }

    void HandleOptionChanged(OptionType type, bool isUnlocked)
    {
        if (type == OptionType.Hand) Refresh();
    }

    void Refresh()
    {
        bool show = OptionState.Instance != null && OptionState.Instance.IsUnlocked(OptionType.Hand);
        IsVisible = show;
        if (handModel != null) handModel.SetActive(show);
    }

    // 매 프레임: 손 자세를 평소 ↔ 손바닥 위 사이에서 부드럽게 옮김
    void LateUpdate()
    {
        if (handBone == null || palmUpPose == null) { poseBlend = poseTarget; return; }

        poseBlend = Mathf.MoveTowards(poseBlend, poseTarget, Time.deltaTime / poseTime);
        float t = Mathf.SmoothStep(0f, 1f, poseBlend);
        handBone.localPosition = Vector3.Lerp(restPos, palmUpPose.localPosition, t);
        handBone.localRotation = Quaternion.Slerp(restRot, palmUpPose.localRotation, t);
    }

    // ===== 다른 스크립트에서 쓰는 함수 =====

    // 손 쥐기 정도 (0 = 편 손, 1 = 꽉 쥠). 미니게임 3, 문고리 연출용
    public void SetGrip(float amount)
    {
        if (pose == null) return;
        pose.SetFinger(HandPose.Finger.Thumb, amount * 0.5f);
        pose.SetFinger(HandPose.Finger.Index, amount * 0.94f);
        pose.SetFinger(HandPose.Finger.Middle, amount);
        pose.SetFinger(HandPose.Finger.Ring, amount * 1.06f);
        pose.SetFinger(HandPose.Finger.Pinky, amount * 1.12f);
    }

    // 물건 들기 (보통은 GrabbableItem을 클릭하면 자동으로 호출됨)
    public bool Hold(GrabbableItem item)
    {
        if (item == null || Held != null || !IsVisible) return false;
        Held = item;

        // 물리 끄기 (떨어지거나 부딪히지 않게, 조준을 가리지 않게)
        heldRb = item.GetComponent<Rigidbody>();
        if (heldRb != null) { heldWasKinematic = heldRb.isKinematic; heldRb.isKinematic = true; }
        heldColliders = item.GetComponentsInChildren<Collider>();
        foreach (Collider c in heldColliders) c.enabled = false;

        // 들기 전 손가락 모양을 기억해 두고 살짝 오므림
        if (pose != null)
        {
            for (int f = 0; f < 5; f++) savedPose[f] = pose.GetFinger((HandPose.Finger)f);
            pose.SetAll(palmCurl);
        }
        poseTarget = 1f;

        // 손이 다 뒤집히면 물건을 손바닥 위에 올림
        placeRoutine = StartCoroutine(PlaceOnPalmWhenReady(item));
        return true;
    }

    IEnumerator PlaceOnPalmWhenReady(GrabbableItem item)
    {
        while (poseBlend < 1f) yield return null;    // 한 프레임씩 쉬면서 다 뒤집힐 때까지 기다림
        if (Held == item) item.AttachTo(palmPoint != null ? palmPoint : transform);
    }

    // 들고 있는 물건을 원래 자리로 (우클릭)
    public void ReturnHeld()
    {
        if (Held == null) return;
        GrabbableItem item = Held;
        LetGo();
        item.ReturnHome();
    }

    // 들고 있는 물건을 놓을 자리에 놓기 (PlaceSpot이 호출). 놓인 물건은 다시 집을 수 없음
    public void PlaceHeld(Transform point, bool hide)
    {
        if (Held == null) return;
        GrabbableItem item = Held;
        LetGo();

        if (hide) item.gameObject.SetActive(false);   // 쓰레기통: 사라짐
        else item.PlaceAt(point);                     // 선반: 그 자리에 세움

        item.enabled = false;                         // 다시 못 집게
    }

    // 손에서 물건을 떼고 손을 원래대로 (5단계 PlaceSpot도 같이 씀)
    void LetGo()
    {
        if (placeRoutine != null) { StopCoroutine(placeRoutine); placeRoutine = null; }  // 아직 뒤집히는 중이면 취소

        foreach (Collider c in heldColliders) c.enabled = true;
        if (heldRb != null) heldRb.isKinematic = heldWasKinematic;

        if (pose != null)
            for (int f = 0; f < 5; f++) pose.SetFinger((HandPose.Finger)f, savedPose[f]);
        poseTarget = 0f;                     // 손을 평소 자세로 되돌림

        Held = null;
    }

}