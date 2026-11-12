using System.Collections;
using UnityEngine;

// 미니게임5 회상 장면의 연출 순서 담당 (시작 장면 + 끝 장면)
// ── 시작 장면 ── (MemoryTrip의 On White → BeginIntro)
//   [화면이 하얀 동안] 조작 잠금, 겨울이를 시작 자리에 앉힘(HurdleTraining.Prepare), 손을 겨울이 등 위에 올리고 쓰다듬기 시작
//   [화면이 걷힌 뒤]
//     1) 쓰다듬는 채로 대사1 (petDialogue: 겨울이에게 운동을 시키는 이유)
//     2) 대사2 (lookDialogue: "…이 허들 정도는 뛰어넘을 수 있겠지?")가 뜨는 순간 손을 거두고, 일어서며 허들 쪽으로 (Stand View)
//     3) 대사3 (goDialogue: "좋아, 그럼 해 보자. 겨울아!")
//     4) 겨울이 "왈!" (짖는 동작) → 조작 풀림 → 허들 훈련 시작 (HurdleTraining.Begin)
// ── 끝 장면 ── (HurdleTraining의 On Cleared → BeginOutro)
//     1) 조작 잠금. "잘했어, 겨울아!"(praiseDialogue)와 동시에 겨울이 앞에 쪼그려 앉으며 겨울이를 내려다봄
//     2) 간식을 쥔 손을 겨울이 입가로 내밂 (자리 = Feed Height/Forward/Side 숫자, 방향 = Mouth Point)
//     3) 겨울이가 고개를 숙여 간식을 먹음 (먹는 애니메이션: 고개 숙이기 → 먹기 → 고개 들기 → 앉기) → 간식이 사라짐
//     4) 손이 입가에서 등 위(Pet Point)로 옮겨 가 다시 쓰다듬기 + 대사(endDialogue)
//     5) 잠시 쓰다듬다가 회상 끝 (MemoryTrip.Exit → 하얗게 → 방으로 → 독백 → 점프 해금)
// 손 움직임 원리: 미니게임3의 HandAnchor를 그대로 쓴다. HandAnchor가 손을 붙잡아 둘 자리(Grip Point)를
//   이 스크립트가 만든 "손 목표점"으로 바꿔 두고, 그 목표점을 매 프레임 옮긴다.
//   (어루만지기 = 손을 등에서 떼지 않고 머리 쪽 → 꼬리 쪽으로 천천히 쓸어 주기. 빠르기·좌우가 저절로 천천히 바뀜
//    / 간식 주기 = Mouth Point / 그 사이 = 부드럽게 옮겨 감)
//   손 높이는 Back Bone(등뼈)을 따라가고 Palm Height 숫자 하나로 맞춘다. Pet Point는 손의 "방향(각도)"만 정한다.
//   손이 HandAnchor에 붙잡혀 있는 동안은 HandSway(평소 손 흔들림)를 꺼 둔다.
// 겨울이 동작(짖기·먹기)은 HurdleTraining에 연결된 DogAnimator로 한다 (없으면 소리만).
// 앉은 자세: 몸이 땅보다 아래로 내려가므로 그동안은 CharacterController를 꺼 둔다.
// 사용: MG5_Sequence에 붙이고 칸을 채운다.
//       MemoryTrip: Release Player On Enter 해제, On White → BeginIntro, Restart Point → Stand View
//       HurdleTraining: On Cleared → BeginOutro
[DefaultExecutionOrder(1100)]   // HandSway(평소 손 흔들림)가 손 자리를 정한 뒤에 실행 (손을 부드럽게 되돌리는 데 필요)
public class MemorySequence : MonoBehaviour
{
    [Header("연결")]
    public MemoryTrip trip;
    public HurdleTraining training;
    public HandAnchor petHand;               // 손을 붙잡아 둘 HandAnchor (Grip Point = Pet Point)
    public Transform petPoint;               // 겨울이 등 위 쓰다듬는 자리 (Winter의 자식). 비우면 Pet Hand의 Grip Point
    public Transform mouthPoint;             // 간식을 내밀 때 손의 "방향(각도)" (Winter의 자식). 손 자리는 아래 Feed Height/Forward/Side 숫자로
    public Transform dog;                    // 겨울이 (Winter)
    public Transform standView;              // 일어서서 허들을 바라보는 눈 자리·방향 (빈 오브젝트)
    public float standTime = 1.5f;           // 일어서며 고개를 돌리는 시간 (초)
    public Behaviour[] disableWhileLocked;   // 연출 동안 끌 스크립트 (비워도 PlayerMotionFeel은 자동으로 끔. 걸음 흔들림 등이 남으면 여기에 추가)

    [Header("어루만지기 (손을 떼지 않고 천천히 쓸어 주기)")]
    public Transform backBone;               // 손이 따라갈 등뼈 (Spitz 안의 Spine_03 등). 비우면 Pet Point 자리 그대로
    public float palmHeight = 0.1f;          // 등뼈에서 손까지 높이 (m). Play 중 이 숫자만 바꿔 가며 털에 닿게 맞추기
    public float palmForward = 0f;           // 등뼈 기준 앞(+, 머리 쪽)뒤(-) 위치 (m)
    public float palmSide = 0f;              // 등뼈 기준 좌우 위치 (m, + = 겨울이의 오른쪽, - = 왼쪽)
    public float strokeLength = 0.08f;       // 쓸어 주는 폭 (m, 가운데에서 머리 쪽·꼬리 쪽 끝까지)
    public float strokeTime = 2.4f;          // 한 번 쓸어내렸다 돌아오는 시간 (초). 클수록 느긋하게
    [Range(0.5f, 0.8f)] public float downPortion = 0.62f;   // 한 번 중 "쓸어내리는" 쪽이 차지하는 비율 (내릴 땐 천천히, 돌아올 땐 조금 빨리)
    [Range(0f, 0.6f)] public float tempoVariation = 0.3f;  // 빠르기가 저절로 천천히 바뀌는 정도 (기계적인 반복 방지)
    public float pressDepth = 0.006f;        // 쓸어내릴 때 털을 살짝 누르는 정도 (m)
    public float returnLift = 0.006f;        // 돌아올 때 손이 아주 살짝 뜨는 정도 (m) — 거의 닿은 채로
    public float arcDrop = 0.015f;           // 둥근 등: 앞뒤 끝으로 갈수록 손이 내려가는 정도 (m)
    public float sideSway = 0.012f;          // 좌우로 천천히 흘러가는 정도 (m)
    public float strokeTilt = 8f;            // 등 곡선을 따라 손목이 앞뒤로 기우는 각도 (도)
    [Range(0f, 1f)] public float fingerCurl = 0.25f;   // 손가락 굽힘 (털을 감싸듯 살짝, 아주 천천히 꼼지락)

    [Header("끝 장면 어루만지기 (먹고 난 뒤 — 시작 장면과 따로 맞춤)")]
    public Transform endBone;                // 끝 장면에서 손이 따라갈 뼈. 비우면 머리 뼈(Head Bone) → 머리를 쓰다듬음
    public Transform endHandAngle;           // 끝 장면 손 각도 (빈 오브젝트, Winter의 자식). Play 중 이걸 돌리면 손이 바로 따라 돎. 비우면 Pet Point 각도
    public float endPalmHeight = 0.08f;      // 그 뼈에서 손까지 높이 (m)
    public float endPalmForward = -0.03f;    // 앞(+, 코 쪽)뒤(-, 목 쪽) (m)
    public float endPalmSide = 0f;           // 겨울이의 오른쪽(+) / 왼쪽(-) (m)
    public float endSphereRadius = 0.06f;    // 둥근 머리를 공이라 치고 그 반지름 (m). 손이 이 공 표면을 따라 미끄러짐
    public float endMoveAngle = 12f;         // 앞뒤로 미끄러지는 범위 (도, 공 꼭대기 기준). 작을수록 아주 조금만 움직임
    public float endSideAngle = 7f;          // 좌우로 미끄러지는 범위 (도)
    public float endSpeed = 0.15f;           // 움직이는 빠르기 (1초에 대략 몇 번 오가는지). 작을수록 느릿느릿
    public float handReturnTime = 0.8f;      // 쓰다듬기를 끝내고 손이 원래 자리로 돌아가는 시간 (초)

    [Header("시작 장면 대사")]
    public DialogueStep petDialogue;         // 대사1: 쓰다듬으며 (운동을 시키는 이유)
    public DialogueStep lookDialogue;        // 대사2: "…이 허들 정도는 뛰어넘을 수 있겠지?"
    public DialogueStep goDialogue;          // 대사3: "좋아, 그럼 해 보자. 겨울아!"
    public float startDelay = 0.8f;          // 화면이 걷히고 대사1까지 쓰다듬기만 보여 주는 시간 (초)

    [Header("끝 장면")]
    public DialogueStep praiseDialogue;      // "잘했어, 겨울아!" (쪼그려 앉으며) 비워도 됨
    public DialogueStep endDialogue;         // 다시 쓰다듬으며 (예: "…착하다, 우리 겨울이.") 비워도 됨
    public Transform crouchView;             // 먹이 줄 때 눈 자리·방향 (Winter의 자식 빈 오브젝트, Align With View로 맞춤). 비우면 아래 숫자로 자동
    public Transform petView;                // 다 먹고 쓰다듬을 때 눈 자리·방향 (Winter의 자식). 손이 머리로 옮겨 가는 동안 이 시점으로 천천히 옮겨 감. 비우면 그대로
    public float petViewTime = 1.5f;         // 먹이 시점 → 쓰다듬기 시점으로 옮겨 가는 시간 (초)
    public float crouchHeight = 0.7f;        // (Crouch View가 비었을 때) 쪼그려 앉은 눈 높이 (땅에서 m)
    public float crouchDistance = 0.6f;      // (Crouch View가 비었을 때) 겨울이 머리에서 눈까지 떨어진 거리 (m)
    public float crouchTime = 1f;            // 쪼그려 앉는 시간 (초)
    public float feedTime = 0.6f;            // 간식을 입가로 내미는 시간 (초)
    public float feedHeight = 0.15f;         // 간식 든 손 높이: 겨울이 발밑에서 위로 (m). Play 중 숫자만 바꿔 가며 맞추기
    public float feedForward = 0.25f;        // 간식 든 손 앞뒤: 겨울이 몸 중심에서 머리 쪽(+)으로 (m)
    public float feedSide = 0f;              // 간식 든 손 좌우: 겨울이의 오른쪽(+) / 왼쪽(-) (m)
    public float feedHold = 0.3f;            // 손을 내밀고 겨울이가 먹기 시작하기까지 (초). 손 위치 맞출 땐 크게(60) 해 두고, 끝나면 되돌리기
    public float eatTime = 2.5f;             // 냠냠 먹는 시간 (초, 고개 숙이기·들기 제외)
    public float toPetTime = 1.2f;           // 입가에서 등 위로 손을 옮기는 시간 (초)
    public float toPetArc = 0.15f;           // 입가 → 등 위로 옮길 때 겨울이 머리 꼭대기보다 이만큼 위로 넘어감 (m, 손 크기 포함). 손이 겨울이를 뚫으면 키우기
    public Transform headBone;               // 겨울이 머리 뼈 (Spitz 안의 Head 등). 비우면 이름에 "Head"가 들어간 뼈를 자동으로 찾음
    public float petBeforeExit = 2.5f;       // 대사가 끝나고 회상이 끝나기 전까지 쓰다듬는 시간 (초)

    [Header("소리")]
    public AudioSource dogVoice;             // 겨울이의 Audio Source
    public AudioClip barkClip;               // 왈!
    public AudioClip eatClip;                // 냠냠 (비워도 됨)

    enum HandMode { None, Pet, Feed, FeedToPet }
    HandMode handMode = HandMode.None;

    FirstPersonPlayer fpp;
    CharacterController cc;
    PlayerMotionFeel motionFeel;             // 걸음에 따른 화면 흔들림
    HandSway sway;
    Transform handTarget;                    // HandAnchor가 손을 붙잡아 둘 목표점 (이 스크립트가 매 프레임 옮김)
    Quaternion feedRot;                      // 간식을 내밀 때 손 방향 (끝 장면 시작할 때의 손 방향 그대로)
    float feedToPetBlend;

    Transform handT;                         // 손 (HandRoot)
    Vector3 fromPos, homePos;                // 손 되돌리기: 쓰다듬던 자리 → 원래 자리 (카메라 기준)
    Quaternion fromRot, homeRot;
    float returnBlend = 1f;                  // 0 = 쓰다듬던 자리, 1 = 원래 자리

    DogAnimator DogAnim => training != null ? training.dogAnim : null;

    // 어루만지기 진행 상태
    float caressPhase;                       // 0~1을 계속 돌며 쓸어내리기·돌아오기를 반복
    bool endPetting;                         // 끝 장면 어루만지기 중 (End 값들을 씀)
    HandPose handPose;

    void Awake()
    {
        // HandAnchor가 붙잡을 자리를 "손 목표점"으로 바꿔 둔다 (원래 자리는 Pet Point로 기억)
        handTarget = new GameObject("MG5_HandTarget").transform;
        if (petHand != null)
        {
            if (petPoint == null) petPoint = petHand.gripPoint;
            petHand.gripPoint = handTarget;
        }
        if (trip != null) trip.onExitWhite.AddListener(OnExitWhite);
    }

    // ===================== 시작 장면 =====================

    // 화면이 하얀 동안 첫 장면 준비 (MemoryTrip의 On White에 연결)
    public void BeginIntro()
    {
        if (!FindPlayer()) return;
        endPetting = false;
        LockPlayer();
        if (training != null) training.Prepare();   // 겨울이를 시작 자리에 앉힘
        HoldHand(HandMode.Pet, 0.2f);               // 화면이 걷히기 전에 손이 도착하도록 빠르게
        StartCoroutine(IntroRoutine());
    }

    IEnumerator IntroRoutine()
    {
        while (trip.Busy) yield return null;         // 하얀 화면이 다 걷힐 때까지
        yield return new WaitForSeconds(startDelay);

        // 1) 쓰다듬으며 대사1
        yield return Play(petDialogue);

        // 2) 대사2가 뜨는 순간 손을 거두고 일어서며 허들 쪽으로
        if (lookDialogue != null) lookDialogue.Play();
        ReleaseHandSmooth();
        if (standView != null)
        {
            Vector3 eyeOffset = EyeOffset();
            float yaw = standView.eulerAngles.y;
            Vector3 bodyPos = standView.position - Quaternion.Euler(0f, yaw, 0f) * eyeOffset;
            bodyPos.y = GroundY(bodyPos, bodyPos.y);   // 발을 땅 위에 (Stand View 높이는 상관없음)
            yield return MoveBody(bodyPos, yaw, Mathf.DeltaAngle(0f, standView.eulerAngles.x), standTime);
        }
        if (lookDialogue != null) while (lookDialogue.IsPlaying) yield return null;

        // 3) 대사3
        yield return Play(goDialogue);

        // 4) 왈! (짖는 동작) → 조작 풀고 훈련 시작
        PlayDog(barkClip);
        if (DogAnim != null) DogAnim.Bark();
        yield return new WaitForSeconds(0.5f);
        UnlockPlayer();
        if (training != null) training.Begin();
    }

    // ===================== 끝 장면 =====================

    // 허들을 다 넘고 겨울이가 내 앞까지 왔을 때 (HurdleTraining의 On Cleared에 연결)
    public void BeginOutro()
    {
        if (!FindPlayer()) return;
        StartCoroutine(OutroRoutine());
    }

    IEnumerator OutroRoutine()
    {
        LockPlayer();

        // 1) "잘했어, 겨울아!" + 겨울이 앞에 쪼그려 앉아 내려다봄
        if (praiseDialogue != null) praiseDialogue.Play();
        yield return CrouchToDog();

        // 2) 간식 쥔 손을 겨울이 입가로 (손 방향은 지금 그대로)
        TreatHand treatHand = training != null ? training.treatHand : null;
        if (treatHand != null) treatHand.handLocked = true;   // 간식 손의 내밀기·흔들기는 멈추고 (손 자세·간식은 그대로)
        // 손 방향: Mouth Point가 있으면 그 방향 (Play 중 돌려 가며 맞출 수 있음), 없으면 지금 손 방향 그대로
        if (mouthPoint != null) feedRot = mouthPoint.rotation;
        else if (PlayerHand.Instance != null) feedRot = PlayerHand.Instance.transform.rotation;
        HoldHand(HandMode.Feed, feedTime);
        while (petHand != null && !petHand.IsHolding) yield return null;
        yield return new WaitForSeconds(feedHold);

        // 3) 냠냠: 겨울이가 고개를 숙여 간식을 물고(간식 사라짐) → 먹고 → 고개를 들고 → 앉음
        DogAnimator anim = DogAnim;
        if (anim != null)
        {
            anim.Eat(eatTime);
            yield return new WaitForSeconds(anim.eatStartTime);   // 고개가 손에 닿을 때쯤
        }
        // 간식만 사라지게 (손 자세는 그대로 두었다가, 손을 등으로 옮길 때 함께 풂 → 먹는 중에 손이 갑자기 돌아가지 않게)
        if (treatHand != null && treatHand.treat != null) treatHand.treat.SetActive(false);
        PlayDog(eatClip);
        yield return new WaitForSeconds(eatTime + (anim != null ? anim.eatEndTime : 0f));

        // 4) 손을 입가에서 등 위로 옮겨 다시 쓰다듬기 (겨울이 머리 위로 둥글게 넘어가며, 간식 쥔 손 자세도 이때 함께 풀림)
        if (treatHand != null)
        {
            treatHand.Hide();                       // 손 자세가 poseTime 동안 스르륵 평소로 (옮겨 가는 중이라 자연스러움)
            treatHand.handLocked = true;            // Hide가 풀어 버린 "손 내밀기·흔들기 멈춤"을 다시 걸어 둠 (손이 엉뚱하게 밀리지 않게)
        }
        if (petView != null) StartCoroutine(MoveToView(petView, petViewTime));   // 쓰다듬기 시점으로 천천히 (손 옮기기와 동시에)
        endPetting = true;                          // 이제부터는 끝 장면 값(머리 쓰다듬기)으로
        StartCaress();                              // 쓰다듬기 시작 자세를 미리 정해 두고 그쪽으로 옮김 (도착 후 튀지 않게)
        feedToPetBlend = 0f;
        handMode = HandMode.FeedToPet;
        while (handMode == HandMode.FeedToPet) yield return null;

        if (praiseDialogue != null) while (praiseDialogue.IsPlaying) yield return null;
        yield return Play(endDialogue);

        // 5) 잠시 쓰다듬다가 회상 끝 (화면이 하얘지는 순간 손은 OnExitWhite에서 원래대로)
        yield return new WaitForSeconds(petBeforeExit);
        if (trip != null) trip.Exit();
    }

    // 쪼그려 앉으며 겨울이를 내려다봄 (겨울이와 나 사이, 겨울이 머리 앞 crouchDistance m, 땅에서 crouchHeight m)
    IEnumerator CrouchToDog()
    {
        if (dog == null) yield break;
        if (crouchView != null)
        {
            // 직접 맞춰 둔 눈 자리·방향으로 (Winter의 자식이라 겨울이가 어디 있든 같은 구도)
            yield return MoveToView(crouchView, crouchTime);
            yield break;
        }
        Vector3 head = dog.position + Vector3.up * 0.2f;
        Vector3 toMe = fpp.transform.position - dog.position;
        toMe.y = 0f;
        toMe = toMe.sqrMagnitude > 0.0001f ? toMe.normalized : -dog.forward;

        Vector3 eye = dog.position + toMe * crouchDistance;
        eye.y = dog.position.y + crouchHeight;
        Quaternion look = Quaternion.LookRotation(head - eye);
        float yaw = look.eulerAngles.y;
        float pitch = Mathf.DeltaAngle(0f, look.eulerAngles.x);
        Vector3 bodyPos = eye - Quaternion.Euler(0f, yaw, 0f) * EyeOffset();
        yield return MoveBody(bodyPos, yaw, pitch, crouchTime);
    }

    // 빈 오브젝트(view)의 위치·방향을 눈 자리로 삼아 몸과 고개를 천천히 옮김
    IEnumerator MoveToView(Transform view, float time)
    {
        float yaw = view.eulerAngles.y;
        float pitch = Mathf.DeltaAngle(0f, view.eulerAngles.x);
        Vector3 body = view.position - Quaternion.Euler(0f, yaw, 0f) * EyeOffset();
        yield return MoveBody(body, yaw, pitch, time);
    }

    // 나올 때 화면이 완전히 하얀 순간: 손을 원래대로 (MemoryTrip이 알아서 부름)
    void OnExitWhite()
    {
        handMode = HandMode.None;
        endPetting = false;
        if (training != null && training.treatHand != null) training.treatHand.handLocked = false;
        returnBlend = 1f;
        if (petHand != null) petHand.Release();
        if (sway != null) sway.enabled = true;
        EnableExtras();                             // 걸음 흔들림 등 다시 켜기
        // 몸 위치·충돌·조작은 MemoryTrip이 원래 자리로 돌려놓으며 다시 켬
    }

    // ===================== 손 =====================

    // 손을 HandAnchor로 붙잡기 시작 (mode에 따라 목표점이 바뀜)
    void HoldHand(HandMode mode, float moveTime)
    {
        if (PlayerHand.Instance != null)
        {
            sway = PlayerHand.Instance.GetComponent<HandSway>();
            if (sway != null) sway.enabled = false;   // 평소 손 흔들림 끄기
        }
        handMode = mode;
        returnBlend = 1f;
        if (mode == HandMode.Pet) StartCaress();
        UpdateHandTarget(0f);                       // 목표점을 먼저 정해 두고
        if (petHand != null && !petHand.IsHolding)
        {
            petHand.moveTime = moveTime;
            petHand.Begin();                        // 지금 손 자리에서 목표점까지 스르륵
        }
    }

    // 손을 거두고 천천히 원래 자리로 (팔을 거두는 느낌)
    void ReleaseHandSmooth()
    {
        handMode = HandMode.None;
        handT = PlayerHand.Instance != null ? PlayerHand.Instance.transform : null;
        if (handT != null) { fromPos = handT.localPosition; fromRot = handT.localRotation; }
        if (petHand != null) petHand.Release();    // 원래 자리로 순간이동 → 아래 LateUpdate가 부드럽게 바꿈
        if (handT != null)
        {
            homePos = handT.localPosition;
            homeRot = handT.localRotation;
            handT.SetLocalPositionAndRotation(fromPos, fromRot);
            returnBlend = 0f;
        }
        if (sway != null) sway.enabled = true;
    }

    void Update()
    {
        UpdateHandTarget(Time.deltaTime);
    }

    // 손 목표점 옮기기 (HandAnchor가 매 프레임 손을 여기에 붙잡아 둠)
    void UpdateHandTarget(float dt)
    {
        if (handMode == HandMode.None || handTarget == null) return;
        if (petHand != null) petHand.shake = Vector3.zero;

        if (handMode == HandMode.Pet)
        {
            AdvanceCaress(dt);
            PetPose(out Vector3 p, out Quaternion r);
            handTarget.SetPositionAndRotation(p, r);
        }
        else if (handMode == HandMode.Feed)
        {
            if (mouthPoint != null) feedRot = mouthPoint.rotation;   // Play 중 Mouth Point를 돌리면 바로 따라옴
            handTarget.SetPositionAndRotation(MouthPos(), feedRot);
        }
        else // FeedToPet: 입가 → 등 위로 부드럽게
        {
            feedToPetBlend = Mathf.MoveTowards(feedToPetBlend, 1f, dt / Mathf.Max(toPetTime, 0.01f));
            float k = Mathf.SmoothStep(0f, 1f, feedToPetBlend);
            PetPose(out Vector3 p, out Quaternion r);
            // 입가 → 등 위: "위로 들어 올리고 → 머리 위로 넘어가서 → 등으로 내려놓기"
            //   (곡선의 가운데가 겨울이 머리 꼭대기 + toPetArc 높이를 지나도록 계산 → 머리를 뚫지 않음)
            Vector3 a = MouthPos();
            float top = Mathf.Max(a.y, p.y);
            Transform head = HeadBone();
            if (head != null) top = Mathf.Max(top, head.position.y);
            top += toPetArc;
            float ctrlY = (top - (a.y + p.y) / 8f) / 0.75f;   // 곡선 가운데가 정확히 top을 지나게 하는 조절점 높이
            Vector3 c1 = new Vector3(a.x, ctrlY, a.z);          // 입가 바로 위
            Vector3 c2 = new Vector3(p.x, ctrlY, p.z);          // 등 바로 위
            float u = 1f - k;
            Vector3 pos = u * u * u * a + 3f * u * u * k * c1 + 3f * u * k * k * c2 + k * k * k * p;
            handTarget.SetPositionAndRotation(pos, Quaternion.Slerp(feedRot, r, k));
            if (feedToPetBlend >= 1f) handMode = HandMode.Pet;
        }
    }

    // ===================== 어루만지기 =====================
    // 손을 등에서 떼지 않고, 머리 쪽 → 꼬리 쪽으로 천천히 쓸어내렸다가 거의 닿은 채로 돌아오기를 반복한다.
    // 모든 움직임이 시간에 따라 "끊김 없이 이어지는 곡선"이라 위치가 튀지 않는다.
    //   - 쓸어내릴 땐 천천히(downPortion), 돌아올 땐 조금 빨리
    //   - 빠르기·좌우 위치가 저절로 아주 천천히 바뀜 (매번 똑같이 반복되지 않게)
    //   - 손 높이는 등뼈(backBone)를 따라가서, 겨울이가 숨 쉬거나 자세를 바꿔도 손이 등에 붙어 있음

    void StartCaress()
    {
        handPose = PlayerHand.Instance != null ? PlayerHand.Instance.GetComponentInChildren<HandPose>() : null;
        caressPhase = 0f;
    }

    // 진행 + 손가락
    void AdvanceCaress(float dt)
    {
        float tempo = 1f + tempoVariation * (Mathf.PerlinNoise(Time.time * 0.15f, 1.3f) - 0.5f) * 2f;
        caressPhase = Mathf.Repeat(caressPhase + dt / Mathf.Max(strokeTime, 0.1f) * tempo, 1f);

        if (handPose == null) return;
        float curl = fingerCurl + 0.05f * Mathf.Sin(Time.time * 0.7f);
        for (int f = 1; f < 5; f++) handPose.SetFinger((HandPose.Finger)f, curl);
        handPose.SetFinger(HandPose.Finger.Thumb, curl * 0.5f);   // 엄지는 덜 굽힘
    }

    // 손이 쓰다듬는 기준 자리 (등뼈를 따라감)
    Vector3 PetBase(Vector3 fwd)
    {
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        if (endPetting)
        {
            Transform bone = endBone != null ? endBone : HeadBone();
            if (bone != null) return bone.position + Vector3.up * endPalmHeight + fwd * endPalmForward + right * endPalmSide;
        }
        if (backBone != null) return backBone.position + Vector3.up * palmHeight + fwd * palmForward + right * palmSide;
        return petPoint != null ? petPoint.position : handTarget.position;
    }

    // 지금 손 자리·방향
    void PetPose(out Vector3 pos, out Quaternion rot)
    {
        Vector3 fwd = Vector3.ProjectOnPlane(dog != null ? dog.forward : Vector3.forward, Vector3.up).normalized;   // 머리 쪽
        Vector3 right = Vector3.Cross(Vector3.up, fwd);

        if (endPetting)
        {
            EndSpherePose(fwd, right, out pos, out rot);
            return;
        }

        // 한 바퀴(0~1)를 각도 w(0~2π)로: 앞 downPortion 동안 머리→꼬리(w 0~π), 나머지 동안 꼬리→머리(w π~2π)
        float p = caressPhase;
        float w = p < downPortion
            ? Mathf.PI * (p / downPortion)
            : Mathf.PI + Mathf.PI * ((p - downPortion) / (1f - downPortion));
        float s = Mathf.Cos(w);                      // +1 = 머리 쪽 끝, -1 = 꼬리 쪽 끝 (끝에서 속도가 0이라 부드럽게 방향이 바뀜)
        float sw = Mathf.Sin(w);                     // + = 쓸어내리는 중, - = 돌아오는 중

        float along = s * strokeLength;
        float up = -arcDrop * s * s - pressDepth * Mathf.Max(0f, sw) + returnLift * Mathf.Max(0f, -sw);
        float lat = sideSway * (Mathf.PerlinNoise(Time.time * 0.2f, 4.1f) - 0.5f) * 2f;
        float tilt = -s * strokeTilt;                // 둥근 등을 따라 손목이 앞뒤로 기움
        float roll = sideSway > 0f ? lat / sideSway * 3f : 0f;   // 좌우로 흘러갈 때 손목도 살짝

        Quaternion baseRot = endPetting && endHandAngle != null ? endHandAngle.rotation
                           : petPoint != null ? petPoint.rotation : handTarget.rotation;
        pos = PetBase(fwd) + fwd * along + right * lat + Vector3.up * up;
        rot = baseRot * Quaternion.Euler(tilt, 0f, roll);
    }

    // 끝 장면: 둥근 머리(공) 표면을 따라 손이 아주 조금, 느릿느릿 미끄러지며 어루만짐
    //   - 앞뒤·좌우 기울기를 "서로 맞아떨어지지 않는 빠르기의 물결 두 개"를 더해 만든다 → 끊김 없이 부드럽고, 같은 길을 반복하지 않음
    //   - 손바닥도 공 표면을 따라 같이 기울어서, 둥근 것을 감싸 쓰다듬는 느낌
    void EndSpherePose(Vector3 fwd, Vector3 right, out Vector3 pos, out Quaternion rot)
    {
        float t = Time.time * endSpeed * Mathf.PI * 2f;
        float pitch = endMoveAngle * (0.65f * Mathf.Sin(t) + 0.35f * Mathf.Sin(t * 1.618f + 1.3f));           // 앞뒤
        float roll = endSideAngle * (0.6f * Mathf.Sin(t * 0.73f + 0.7f) + 0.4f * Mathf.Sin(t * 1.27f + 2.1f)); // 좌우

        // 공 꼭대기(PetBase)에서 시작해, 공 중심을 기준으로 앞뒤·좌우로 살짝 굴린 자리
        Vector3 top = PetBase(fwd);
        Vector3 center = top - Vector3.up * endSphereRadius;
        Quaternion slide = Quaternion.AngleAxis(pitch, right) * Quaternion.AngleAxis(-roll, fwd);
        pos = center + slide * (Vector3.up * endSphereRadius);

        Quaternion baseRot = endHandAngle != null ? endHandAngle.rotation
                           : petPoint != null ? petPoint.rotation : handTarget.rotation;
        rot = slide * baseRot;   // 손바닥도 공 표면을 따라 같이 기움
    }

    // 겨울이 머리 뼈 (비어 있으면 이름에 "Head"가 들어간 뼈를 한 번 찾아 둠)
    Transform HeadBone()
    {
        if (headBone != null || dog == null) return headBone;
        foreach (Transform t in dog.GetComponentsInChildren<Transform>())
            if (t.name.ToLower().Contains("head")) { headBone = t; break; }
        return headBone;
    }

    // 간식 든 손 자리: 겨울이(Winter) 기준 숫자로 (겨울이가 고개를 숙여 손으로 다가오므로 손은 이 자리에 가만히)
    Vector3 MouthPos()
    {
        if (dog == null) return mouthPoint != null ? mouthPoint.position : handTarget.position;
        Vector3 fwd = Vector3.ProjectOnPlane(dog.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        return dog.position + Vector3.up * feedHeight + fwd * feedForward + right * feedSide;
    }

    // 쓰다듬던 자리 → 원래 자리로 손을 천천히
    void LateUpdate()
    {
        if (returnBlend >= 1f || handT == null) return;
        returnBlend = Mathf.MoveTowards(returnBlend, 1f, Time.deltaTime / Mathf.Max(handReturnTime, 0.01f));
        float k = Mathf.SmoothStep(0f, 1f, returnBlend);
        bool swayOn = sway != null && sway.isActiveAndEnabled;
        Vector3 toPos = swayOn ? handT.localPosition : homePos;
        Quaternion toRot = swayOn ? handT.localRotation : homeRot;
        handT.localPosition = Vector3.Lerp(fromPos, toPos, k);
        handT.localRotation = Quaternion.Slerp(fromRot, toRot, k);
    }

    // ===================== 몸 =====================

    bool FindPlayer()
    {
        if (fpp == null) fpp = FindFirstObjectByType<FirstPersonPlayer>();
        if (fpp == null || trip == null) return false;
        cc = fpp.GetComponent<CharacterController>();
        return true;
    }

    // 조작 잠금 + 충돌 끄기 (앉은 자세는 몸이 땅 아래로 내려가므로)
    // 걸음 흔들림(PlayerMotionFeel)도 끈다: 연출이 몸을 직접 옮기면 걷는 중으로 착각해 화면이 계속 흔들리기 때문
    void LockPlayer()
    {
        fpp.enabled = false;
        fpp.StopMovement();
        fpp.canLook = true;
        if (cc != null) cc.enabled = false;
        if (motionFeel == null) motionFeel = fpp.GetComponentInChildren<PlayerMotionFeel>();
        if (motionFeel != null) motionFeel.enabled = false;
        foreach (Behaviour b in disableWhileLocked) if (b != null) b.enabled = false;
    }

    void UnlockPlayer()
    {
        if (cc != null) cc.enabled = true;
        fpp.enabled = true;
        EnableExtras();
    }

    void EnableExtras()
    {
        if (motionFeel != null) motionFeel.enabled = true;
        foreach (Behaviour b in disableWhileLocked) if (b != null) b.enabled = true;
    }

    Vector3 EyeOffset()
    {
        return fpp.transform.InverseTransformPoint(fpp.cameraRoot.position);   // 몸 기준 눈 위치
    }

    // 몸을 toPos로, 좌우 방향 yaw, 고개 pitch로 천천히
    IEnumerator MoveBody(Vector3 toPos, float yaw, float pitch, float time)
    {
        Transform body = fpp.transform;
        Vector3 fromPos = body.position;
        float fromYaw = body.eulerAngles.y;
        float fromPitch = fpp.Pitch;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(time, 0.01f))
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            body.position = Vector3.Lerp(fromPos, toPos, k);
            body.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(fromYaw, yaw, k), 0f);
            fpp.SetPitch(Mathf.Lerp(fromPitch, pitch, k));
            yield return null;
        }
        body.position = toPos;
        body.rotation = Quaternion.Euler(0f, yaw, 0f);
        fpp.SetPitch(pitch);
    }

    // pos 바로 아래 땅을 찾아, 발이 땅 위에 딱 닿는 몸 높이를 돌려준다 (못 찾으면 fallback)
    float GroundY(Vector3 pos, float fallback)
    {
        Vector3 from = new Vector3(pos.x, pos.y + 3f, pos.z);
        if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            return fallback;
        float feet = cc != null ? cc.center.y - cc.height * 0.5f : 0f;   // 몸 기준점에서 발바닥까지
        return hit.point.y - feet + 0.02f;
    }

    IEnumerator Play(DialogueStep step)
    {
        if (step == null) yield break;
        step.Play();
        while (step.IsPlaying) yield return null;
    }

    void PlayDog(AudioClip clip)
    {
        if (dogVoice != null && clip != null) dogVoice.PlayOneShot(clip);
    }
}