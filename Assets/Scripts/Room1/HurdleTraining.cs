using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TeamRoulette.UI;

// 미니게임5 - 회상 속 겨울이 허들 훈련 (클리어하면 기억에서 깨어나 점프 해금)
// 허들 하나씩 "내가 먼저 넘어서 시범 → 겨울이를 불러서 넘게 하기"를 반복한다.
//   1) 내가 허들을 넘는다 (시범).
//   2) 휘파람(Q)은 언제든 불 수 있지만, 겨울이가 callRange(m) 안에 있고 겨울이를 바라봐야 들린다 → 겨울이가 "왈!" 하고 집중.
//      (멀리 앞서 가면 안 들림 → 허들 하나씩 유도할 수밖에 없다)
//      집중이 풀려 있는 동안 겨울이는 혼자 근처를 어슬렁거린다 (곡선으로 걷다가 멈춰서 냄새 맡기·긁기 등. 다음 허들은 넘지 않음).
//   3) 집중한 동안 좌클릭을 누른 채 마우스를 좌우로 흔들면 (손이 간식을 내밀어 흔듦):
//        - 안 흔들거나 약하게 → 서서히 멈춰서 나를 보며 기다림 (집중이 서서히 풀림)
//        - 적당히 → 나를 향해 신나서 달려옴 (서서히 빨라짐, 흔드는 동안은 집중 유지)
//        - 너무 세게 → 흥분해서 제자리에서 빙글 (진행 없음, 실패 아님)
//   4) 내가 이미 넘은 허들은 달리던 속도 그대로 도약해서 넘는다 (멈칫하지 않음. 달리며 점프 애니메이션에 타이밍을 맞춤).
//      내가 아직 안 넘은 허들은 그 앞에서 미리 감속해 멈춘다.
//   5) 내가 허들을 넘은 뒤 patienceTime초 안에 겨울이가 그 허들을 못 넘으면 → 흥미를 잃고 → 처음부터 (MemoryTrip.Restart)
//   6) 마지막 허들을 넘으면 더 부르지 않아도 내 앞(stopDistance)까지 달려와서 onCleared.
// 겨울이의 "위치·방향"은 이 스크립트가 정하고, 다리 움직임(걷기·달리기·점프·앉기 등)은 DogAnimator가 그 움직임을 보고 맞춘다.
// 사용: MG5_Hurdle에 붙인다. MemorySequence가 첫 장면에서 Prepare(), 대사 뒤에 Begin()을 부른다.
//       허들은 파란 화살표(Z축)가 진행 방향(넘어가는 쪽)을 향하게, 모두 같은 방향으로 놓는다.
public class HurdleTraining : MonoBehaviour
{
    [Header("연결")]
    public MemoryTrip trip;                  // 실패 시 처음 자리로 되돌릴 때 사용
    public Transform dog;                    // 겨울이 (Winter). 땅 위에 올려 둔 높이를 기준으로 움직인다
    public DogAnimator dogAnim;              // 겨울이 애니메이션 (Winter의 DogAnimator). 비워 두면 Dog에서 자동으로 찾음
    public Transform[] hurdles;              // 허들들 (순서대로, 파란 화살표 = 넘어가는 방향)
    public Camera playerCamera;              // 비워 두면 Camera.main

    [Header("겨울이 움직임")]
    public bool useScenePosition = true;     // 체크: Scene에 놓아 둔 Winter 자리에서 시작 / 해제: 첫 허들 앞 Start Distance m에서 시작
    public float startDistance = 3f;         // (Use Scene Position 해제 시) 첫 허들 앞 몇 m에 앉아 있을지
    public float waitDistance = 0.8f;        // 내가 아직 안 넘은 허들 앞 몇 m에서 멈춰 기다릴지
    public float takeoffDistance = 1.0f;     // 내가 넘은 허들 앞 몇 m에서 도약할지 (달리던 속도 그대로)
    public float landDistance = 0.8f;        // 허들 뒤 몇 m에 착지할지
    public float runSpeed = 2.8f;            // 간식을 보고 달려오는 빠르기 (m/초)
    public float acceleration = 5f;          // 빨라지는 정도 (m/초²). 클수록 금방 최고 속도
    public float braking = 6f;               // 느려지는 정도 (m/초²). 멈출 자리 앞에서 미리 감속
    public float minJumpSpeed = 2f;          // 도약할 때 최소 앞으로 나가는 빠르기 (천천히 오다 뛰어도 시원하게)
    public Vector2 jumpAnimSpeedRange = new Vector2(0.8f, 1.5f);   // 달리며 점프 애니메이션을 이 배속 안에서 맞춤 (최소, 최대)
    public float stopDistance = 1.2f;        // 나에게 이만큼 가까워지면 멈춤 (m)
    public float jumpHeight = 0.6f;          // 몸을 띄우는 높이 (m). 애니메이션 자체도 몸을 들어 올리므로, 너무 높게 뜨면 줄이기
    public float turnSpeed = 360f;           // 달릴 때 몸 돌리는 빠르기 (도/초)
    public float lookTurnSpeed = 180f;       // 서서 나를 바라볼 때 몸 돌리는 빠르기 (도/초)

    [Header("집중이 풀렸을 때 (혼자 어슬렁)")]
    public float wanderRadius = 1.5f;        // 집중이 풀린 자리에서 이만큼 안을 돌아다님 (m)
    public float wanderSpeed = 0.5f;         // 어슬렁거리는 빠르기 (m/초)
    public float wanderTurnSpeed = 100f;     // 걸으면서 방향 트는 빠르기 (도/초). 작을수록 크게 곡선을 그림
    public float wanderAngle = 100f;         // 다음 목적지를 "지금 보는 방향 기준 좌우 몇 도" 안에서 고를지 (뒤로 홱 돌지 않게)
    public Vector2 wanderPause = new Vector2(1f, 3f);   // 도착해서 멈춰 있는 시간 (DogAnimator의 멈춤 행동이 없을 때)
    public float laneHalfWidth = 1.3f;       // 코스 가운데에서 좌우로 이 이상 벗어나지 않음 (m, 옆 벽 안쪽)

    [Header("부르기")]
    public KeyCode whistleKey = KeyCode.Q;
    public float callRange = 7f;             // 이보다 멀면 휘파람이 안 들림 (m)
    public float lookAngle = 25f;            // 겨울이가 화면 가운데에서 이 각도 안에 있어야 "바라봄"
    public float attentionTime = 3f;         // 휘파람 후 집중하는 시간 (초). 적당히 흔드는 동안은 줄지 않음

    [Header("간식 흔들기 (빠르기 = 마우스를 좌우로 움직이는 양, 아래 '확인용 Wave Speed'를 보며 조정)")]
    public float slowWave = 20f;             // 이보다 느리면 "안 흔듦" → 기다림
    public float fastWave = 250f;            // 이보다 빠르면 "너무 세게" → 흥분해서 빙글
    public float waveSmoothing = 8f;         // 빠르기를 부드럽게 재는 정도 (클수록 바로바로 반응)

    [Header("인내심")]
    public float patienceTime = 10f;         // 내가 허들을 넘은 뒤 이 시간 안에 겨울이가 넘어야 함 (초)
    public float whineAt = 3f;               // 이만큼 남으면 낑낑

    [Header("소리 (비워도 됨)")]
    public AudioSource whistle;              // 휘파람 (플레이어 쪽, Spatial Blend 0)
    public AudioSource dogVoice;             // 겨울이에 붙은 AudioSource (Spatial Blend 1)
    public AudioClip barkClip;               // 왈!
    public AudioClip whineClip;              // 낑낑

    [Header("손 위 간식 / 안내 / 대사 (비워도 됨)")]
    public TreatHand treatHand;              // 손에 간식을 들고, 좌클릭+마우스 좌우로 흔드는 연출
    public ControlHint hint;                 // 화면 위 조작 안내 (MG3_Hint)
    [TextArea] public string guideText = "Q : 휘파람 (가까이에서)<br>좌클릭을 누른 채 마우스를 좌우로 : 간식 흔들기 (적당히)";
    public DialogueStep farDialogue;         // 처음으로 너무 멀리서 불렀을 때 한 번만 ("…너무 멀어서 못 듣나 봐.")
    public DialogueStep failDialogue;        // 처음 실패했을 때 한 번만 ("…겨울이가 심심해졌나 봐.")

    [Header("이벤트")]
    public UnityEvent onAttentive;           // 휘파람에 겨울이가 집중했을 때
    public UnityEvent onBark;                // 휘파람에 반응해 "왈!" 할 때 (DogAnimator.Bark 연결)
    public UnityEvent onCleared;             // 마지막 허들을 넘고 내 앞까지 달려왔을 때 → MemorySequence.BeginOutro (끝 장면)

    [Header("확인용 (Play 중 값 보기)")]
    public string state;
    public int index;                        // 다음에 넘을 허들 (0부터)
    public float waveSpeed;                  // 지금 흔드는 빠르기 → Slow Wave / Fast Wave 정할 때 이 값을 보세요
    public float attention;                  // 남은 집중 시간
    public float patience;                   // 남은 인내심
    public float curSpeed;                   // 겨울이 지금 빠르기 (m/초)

    enum Mode { Off, Running, Busy, Done }
    enum Mood { LookAway, Wait, Walk, Excited }
    Mode mode = Mode.Off;
    Mood mood = Mood.LookAway;

    FirstPersonPlayer fpp;
    GameUI ui;
    Vector3 dogPos;                          // 겨울이의 "실제" 위치
    float groundY;
    bool groundSet;
    Vector3 scenePos;                        // Scene에 놓아 둔 Winter 자리 (처음 한 번 기억)
    bool wanderActive;                       // 지금 어슬렁 중인지
    bool wanderMoving;                       // 어슬렁 중 걷는 중인지 (멈춰 있으면 false)
    Vector3 wanderCenter, wanderTarget;
    float wanderWait;
    float wanderLegTime;                     // 한 목적지로 걷기 시작한 뒤 지난 시간 (너무 오래 걸리면 포기하고 다음 곳)
    bool whined, guideShown, failedOnce, farTold;

    // ===== 시작 =====

    // 겨울이를 시작 자리에 앉혀 두기만 함 (첫 장면 준비, MemorySequence가 부름)
    public void Prepare()
    {
        if (!Setup()) return;
        StopAllCoroutines();
        index = 0;
        curSpeed = 0f;
        dogPos = StartPoint();
        dog.position = dogPos;
        mode = Mode.Off;
        state = "준비";
    }

    // 훈련 시작 (MemorySequence가 첫 장면 대사 뒤에 부름)
    public void Begin()
    {
        if (!Setup()) return;
        StopAllCoroutines();
        ResetCourse();
        if (treatHand != null) treatHand.Show();
        if (hint != null && !guideShown) { hint.Show(guideText); guideShown = true; }
    }

    bool Setup()
    {
        if (dog == null || hurdles == null || hurdles.Length == 0) return false;
        if (fpp == null) fpp = FindFirstObjectByType<FirstPersonPlayer>();
        if (ui == null) ui = FindFirstObjectByType<GameUI>();
        if (playerCamera == null) playerCamera = Camera.main;
        if (dogAnim == null) dogAnim = dog.GetComponent<DogAnimator>();
        if (!groundSet) { groundY = dog.position.y; scenePos = dog.position; groundSet = true; }
        return true;
    }

    // 겨울이를 출발점으로, 처음 상태로
    void ResetCourse()
    {
        index = 0;
        attention = 0f;
        waveSpeed = 0f;
        patience = patienceTime;
        whined = false;
        curSpeed = 0f;
        dogPos = StartPoint();
        dog.position = dogPos;
        mood = Mood.Wait;
        wanderActive = false;
        wanderMoving = false;
        mode = Mode.Running;
        if (dogAnim != null) dogAnim.Interrupt();
        Face(PlayerPos() - dogPos, 0f);
        UpdateStateText();
    }

    // ===== 매 프레임 =====

    void Update()
    {
        if (mode != Mode.Running) return;
        if (ui != null && (ui.IsDialogueOpen || ui.IsPaused)) return;   // 대사·설정창 중엔 멈춤
        float dt = Time.deltaTime;

        // 간식 흔들기: 좌클릭을 누르는 동안 시점 고정, 마우스 좌우 움직임 양으로 빠르기를 잼
        bool waving = Input.GetMouseButton(0);
        if (fpp != null) fpp.canLook = !waving;
        float raw = waving ? Mathf.Abs(Input.GetAxis("Mouse X")) / Mathf.Max(dt, 0.0001f) : 0f;
        waveSpeed = Mathf.Lerp(waveSpeed, raw, 1f - Mathf.Exp(-waveSmoothing * dt));

        // 휘파람: 가까이 있고 바라봐야 들림
        if (Input.GetKeyDown(whistleKey)) Whistle();

        // 집중: 적당히 흔드는 동안은 유지, 그 밖엔 줄어듦
        bool goodWave = waveSpeed >= slowWave && waveSpeed <= fastWave;
        if (attention > 0f)
        {
            if (goodWave) attention = attentionTime;
            else attention -= dt;
        }

        // 기분 정하기
        if (attention <= 0f) mood = Mood.LookAway;
        else if (waveSpeed > fastWave) mood = Mood.Excited;
        else if (goodWave) mood = Mood.Walk;
        else mood = Mood.Wait;

        // 어슬렁 중이었다가 집중하면 하던 행동(냄새 맡기 등)을 멈춤
        if (mood != Mood.LookAway)
        {
            if (wanderActive && dogAnim != null) dogAnim.Interrupt();
            wanderActive = false;
            wanderMoving = false;
        }

        // 움직이기
        if (mood == Mood.Walk) RunTowardPlayer(dt);
        else if (mood == Mood.Excited) { Coast(dt); dog.Rotate(0f, 360f * dt, 0f); }   // 흥분해서 빙글
        else if (mood == Mood.Wait) { Coast(dt); if (curSpeed <= 0.01f) Face(PlayerPos() - dogPos, lookTurnSpeed); }
        else Wander(dt);                                                                  // 집중이 풀림 → 혼자 어슬렁

        // 인내심: 내가 다음 허들을 넘어가 있는 동안만 줄어듦
        if (mode == Mode.Running && index < hurdles.Length && PlayerPassed(index))
        {
            patience -= dt;
            if (!whined && patience <= whineAt) { whined = true; PlayDog(whineClip); }
            if (patience <= 0f) { StartCoroutine(FailRoutine()); return; }
        }

        UpdateStateText();
    }

    void Whistle()
    {
        if (whistle != null) whistle.Play();
        float dist = FlatDistance(PlayerPos(), dogPos);
        if (dist > callRange)
        {
            // 너무 멀어서 안 들림 (처음 한 번만 혼잣말)
            if (!farTold && farDialogue != null) { farTold = true; farDialogue.Play(); }
            return;
        }
        if (!LookingAtDog()) return;

        bool newlyHeard = attention <= 0f;
        attention = attentionTime;
        onAttentive.Invoke();
        if (newlyHeard)
        {
            // 딴짓하다 휘파람을 들으면 "왈!" (이미 집중 중이면 또 짖지 않음)
            PlayDog(barkClip);
            onBark.Invoke();
        }
    }

    // 간식을 보고 나를 향해 달려옴: 서서히 빨라지고, 멈출 자리(나 / 내가 안 넘은 허들 앞) 앞에서는 미리 감속
    void RunTowardPlayer(float dt)
    {
        Vector3 to = PlayerPos() - dogPos;
        to.y = 0f;
        float room = to.magnitude - stopDistance;                 // 내 앞에서 멈출 때까지 남은 거리
        if (room <= 0.01f) { curSpeed = 0f; Face(to, lookTurnSpeed); return; }

        float limit = Mathf.Min(room, RoomToHurdleLine());
        float target = Mathf.Min(runSpeed, Mathf.Sqrt(2f * braking * Mathf.Max(0f, limit)));   // 이 거리 안에서 멈출 수 있는 속도까지만
        curSpeed = Mathf.MoveTowards(curSpeed, target, (target > curSpeed ? acceleration : braking) * dt);

        // 먼저 몸을 돌리고, 나를 향한 만큼만 앞으로 (뒤를 보고 있으면 제자리에서 돌다가 출발)
        Face(to, turnSpeed);
        Vector3 dir = to.normalized;
        float align = Mathf.Clamp01(Vector3.Dot(Flat(dog.forward), dir));
        StepDog(dir, Mathf.Min(curSpeed * align * dt, room), true);
    }

    // 기다림·흥분 중: 달리던 힘으로 몇 걸음 더 가다가 서서히 멈춤 (뚝 멈추지 않게)
    void Coast(float dt)
    {
        if (curSpeed <= 0f) return;
        curSpeed = Mathf.MoveTowards(curSpeed, 0f, braking * dt);
        StepDog(Flat(dog.forward), curSpeed * dt, false);
    }

    // 겨울이를 dir 쪽으로 dist m 옮김.
    //   내가 이미 넘은 허들 → 도약 거리에 들어오면 점프 시작 (canJump일 때)
    //   내가 아직 안 넘은 허들 (또는 점프 불가) → 허들 앞 멈춤선에서 멈춤
    void StepDog(Vector3 dir, float dist, bool canJump)
    {
        Vector3 next = dogPos + dir * dist;
        if (index < hurdles.Length)
        {
            Transform h = hurdles[index];
            Vector3 fwd = Flat(h.forward);
            float along = Vector3.Dot(next - h.position, fwd);     // 허들 기준 앞뒤 위치 (앞이면 -)
            if (canJump && PlayerPassed(index))
            {
                // 도약 준비 동작 동안 달려갈 거리만큼 미리 점프를 시작 (발이 떨어지는 순간 = 도약 지점)
                PlanJump(Mathf.Max(curSpeed, minJumpSpeed), out _, out float preTime, out _);
                float runUp = Mathf.Max(curSpeed, minJumpSpeed) * preTime;
                if (along > -(takeoffDistance + runUp)) { StartCoroutine(JumpRoutine()); return; }
            }
            else if (along > -waitDistance)
            {
                next -= fwd * (along + waitDistance);
                curSpeed = Mathf.Min(curSpeed, 0.5f);
            }
        }
        dogPos = new Vector3(next.x, groundY, next.z);
    }

    // 내가 아직 안 넘은 허들의 멈춤선까지 남은 거리 (넘었으면 아주 큰 값)
    float RoomToHurdleLine()
    {
        if (index >= hurdles.Length || PlayerPassed(index)) return float.MaxValue;
        Transform h = hurdles[index];
        float along = Vector3.Dot(dogPos - h.position, Flat(h.forward));
        return Mathf.Max(0f, -waitDistance - along);
    }

    // 집중이 풀리면 혼자 어슬렁:
    //   주로 앞쪽에서 다음 목적지를 고르고 → 걸으면서 천천히 방향을 틀어 곡선으로 걸어감
    //   (많이 틀어야 하면 거의 멈춘 채로 돌다가 출발) → 도착하면 멈춰서 냄새 맡기·긁기 등 (DogAnimator)
    //   다음 허들은 넘지 않고, 지나온 허들 뒤로도 가지 않고, 옆 벽 밖으로도 나가지 않는다
    void Wander(float dt)
    {
        if (!wanderActive)
        {
            wanderActive = true;
            wanderCenter = dogPos;
            wanderTarget = dogPos;
            wanderWait = Random.Range(0.3f, 1f);      // 집중이 풀리고 잠깐 멍하니 있다가 출발
        }

        if (wanderWait > 0f)
        {
            curSpeed = Mathf.MoveTowards(curSpeed, 0f, braking * dt);
            if (curSpeed > 0f) StepDog(Flat(dog.forward), curSpeed * dt, false);
            wanderMoving = false;
            wanderWait -= dt;
            if (wanderWait <= 0f) { wanderTarget = PickWanderPoint(); wanderLegTime = 0f; }
            return;
        }

        Vector3 to = wanderTarget - dogPos;
        to.y = 0f;
        wanderLegTime += dt;
        if (to.magnitude < 0.15f || wanderLegTime > 8f)
        {
            // 도착 → 멈춰서 뭔가 하기 (DogAnimator가 행동을 골라 주면 그 길이만큼, 없으면 그냥 잠깐)
            curSpeed = 0f;
            wanderMoving = false;
            float act = dogAnim != null ? dogAnim.PauseAction() : 0f;
            wanderWait = act > 0f ? act : Random.Range(wanderPause.x, wanderPause.y);
            return;
        }

        // 걸으면서 방향 틀기: 조금 틀면 곡선으로 걷고, 90도 넘게 틀어야 하면 거의 제자리에서 돈 뒤 출발
        float angle = Vector3.SignedAngle(Flat(dog.forward), to, Vector3.up);
        float turn = Mathf.Clamp(angle, -wanderTurnSpeed * dt, wanderTurnSpeed * dt);
        dog.rotation = Quaternion.Euler(0f, turn, 0f) * dog.rotation;
        float go = Mathf.Clamp01(1f - Mathf.Abs(angle) / 90f);
        curSpeed = Mathf.MoveTowards(curSpeed, wanderSpeed * go, 1.5f * dt);
        wanderMoving = curSpeed > 0.05f;

        Vector3 next = ClampToSegment(dogPos + Flat(dog.forward) * (curSpeed * dt));
        if (go > 0.5f && curSpeed > 0.05f && (next - dogPos).sqrMagnitude < 0.0000001f)
        {
            wanderTarget = PickWanderPoint();          // 벽·허들에 막혀 못 가면 다른 곳으로
            wanderLegTime = 0f;
        }
        dogPos = next;
    }

    // 다음 목적지: 지금 보는 방향 기준 좌우 wanderAngle도 안, 0.5m ~ wanderRadius 거리 (뒤로 홱 도는 일이 드물게)
    Vector3 PickWanderPoint()
    {
        Vector3 fwd = Flat(dog.forward);
        for (int i = 0; i < 8; i++)
        {
            float ang = Random.Range(-wanderAngle, wanderAngle);
            float d = Random.Range(0.5f, wanderRadius);
            Vector3 p = ClampToSegment(dogPos + Quaternion.Euler(0f, ang, 0f) * fwd * d);
            if (FlatDistance(p, wanderCenter) > wanderRadius) continue;   // 처음 자리에서 너무 멀어지지 않게
            if (FlatDistance(p, dogPos) > 0.4f) return p;
        }
        // 마땅한 곳이 없으면 처음 자리 근처로 (그쪽으로 돌아서 감)
        Vector2 r = Random.insideUnitCircle * (wanderRadius * 0.5f);
        return ClampToSegment(wanderCenter + new Vector3(r.x, 0f, r.y));
    }

    // 지금 있어야 할 구간(지나온 허들 ~ 다음 허들 사이, 옆 벽 안쪽)으로 위치를 제한
    Vector3 ClampToSegment(Vector3 p)
    {
        Transform h = hurdles[Mathf.Min(index, hurdles.Length - 1)];
        Vector3 fwd = Flat(h.forward);
        Vector3 right = Vector3.Cross(Vector3.up, fwd);

        float lat = Vector3.Dot(p - h.position, right);
        p -= right * (lat - Mathf.Clamp(lat, -laneHalfWidth, laneHalfWidth));

        if (index < hurdles.Length)
        {
            float along = Vector3.Dot(p - h.position, fwd);
            float maxAlong = -(waitDistance + 0.2f);   // 다음 허들에 너무 붙지 않게
            if (along > maxAlong) p -= fwd * (along - maxAlong);
        }
        if (index > 0)
        {
            Transform prev = hurdles[index - 1];
            float along = Vector3.Dot(p - prev.position, fwd);
            float minAlong = landDistance * 0.5f;      // 지나온 허들 뒤로 가지 않게
            if (along < minAlong) p += fwd * (minAlong - along);
        }

        p.y = groundY;
        return p;
    }

    // 이번 점프 계획: 애니메이션 배속, 도약 준비 시간(발이 떨어지기 전), 공중 시간
    //   공중 시간이 "도약 지점 → 착지 지점 거리 ÷ 달리던 속도"와 맞도록 애니메이션 배속을 정한다 (너무 빠르거나 느려지지 않게 범위 제한)
    void PlanJump(float v, out float animSpeed, out float preTime, out float airTime)
    {
        float flight = takeoffDistance + landDistance;
        if (dogAnim == null)
        {
            animSpeed = 1f;
            preTime = 0f;
            airTime = flight / v;
            return;
        }
        animSpeed = Mathf.Clamp(dogAnim.RunJumpAir / (flight / v), jumpAnimSpeedRange.x, jumpAnimSpeedRange.y);
        preTime = dogAnim.runJumpLiftoff / animSpeed;
        airTime = dogAnim.RunJumpAir / animSpeed;
    }

    // 달리며 허들 넘기 (달리며 점프 애니메이션에 타이밍을 맞춤)
    //   1) 도약 준비: 애니메이션이 발을 구르는 동안 달리던 대로 도약 지점까지
    //   2) 공중: 애니메이션에서 발이 떨어지는 순간부터 닿는 순간까지 몸을 포물선으로 띄움
    //   3) 착지 후: 애니메이션이 끝날 때까지 계속 달림 → 그대로 달리기로 이어짐 (멈칫하지 않게)
    IEnumerator JumpRoutine()
    {
        mode = Mode.Busy;
        state = "점프";
        if (index == 0 && hint != null) hint.Hide();   // 첫 성공에서 조작 안내 숨김

        float v = Mathf.Max(curSpeed, minJumpSpeed);
        PlanJump(v, out float animSpeed, out float preTime, out float airTime);

        Transform h = hurdles[index];
        Vector3 fwd = Flat(h.forward);
        float along = Vector3.Dot(dogPos - h.position, fwd);
        Vector3 basePos = dogPos - fwd * along;                        // 허들 선 위 (옆 위치는 그대로)
        float liftAlong = Mathf.Max(along, -takeoffDistance);          // 이미 도약선 안쪽이면 그 자리에서 도약
        Vector3 liftPos = basePos + fwd * liftAlong;
        Vector3 landPos = basePos + fwd * landDistance;
        liftPos.y = groundY;
        landPos.y = groundY;
        Quaternion faceFwd = Quaternion.LookRotation(fwd);

        if (dogAnim != null) dogAnim.PlayRunJump(animSpeed);

        // 1) 도약 준비
        Vector3 start = dogPos;
        for (float t = 0f; t < preTime; t += Time.deltaTime)
        {
            dogPos = Vector3.Lerp(start, liftPos, t / preTime);
            dog.rotation = Quaternion.RotateTowards(dog.rotation, faceFwd, 720f * Time.deltaTime);
            yield return null;
        }

        // 2) 공중
        if (dogAnim == null) dog.rotation = faceFwd;
        for (float t = 0f; t < 1f; t += Time.deltaTime / airTime)
        {
            dogPos = Vector3.Lerp(liftPos, landPos, t) + Vector3.up * (4f * jumpHeight * t * (1f - t));
            dog.rotation = Quaternion.RotateTowards(dog.rotation, faceFwd, 720f * Time.deltaTime);
            yield return null;
        }
        dogPos = landPos;

        // 3) 착지 후 남은 동작 동안 계속 달림
        float post = dogAnim != null ? (dogAnim.runJumpLength - dogAnim.runJumpTouchdown) / animSpeed : 0f;
        for (float t = 0f; t < post; t += Time.deltaTime)
        {
            dogPos += fwd * (v * Time.deltaTime);
            yield return null;
        }
        curSpeed = v;                                 // 착지하고도 달리던 속도 그대로

        index++;
        if (index >= hurdles.Length)
        {
            // 마지막 허들까지 넘음 → 더 부르지 않아도 신나서 나에게 달려옴 → 클리어
            StartCoroutine(FinishRoutine());
            yield break;
        }
        patience = patienceTime;                      // 다음 허들은 인내심 새로
        whined = false;
        mode = Mode.Running;
    }

    // 마지막 허들을 넘은 뒤: 내가 어디 있든 내 앞까지 달려와서(앞에서 감속) → 클리어 (끝 장면 시작)
    IEnumerator FinishRoutine()
    {
        mode = Mode.Busy;
        state = "다 넘음 → 달려옴";
        if (fpp != null) fpp.canLook = true;
        if (hint != null) hint.Hide();

        while (true)
        {
            float dt = Time.deltaTime;
            Vector3 to = PlayerPos() - dogPos;
            to.y = 0f;
            float room = to.magnitude - stopDistance;
            if (room <= 0.02f) break;
            float target = Mathf.Min(runSpeed * 1.1f, Mathf.Sqrt(2f * braking * room));
            curSpeed = Mathf.MoveTowards(curSpeed, target, (target > curSpeed ? acceleration : braking) * dt);
            curSpeed = Mathf.Max(curSpeed, 0.3f);    // 끝까지 다가오도록
            Face(to, turnSpeed);
            float align = Mathf.Clamp01(Vector3.Dot(Flat(dog.forward), to.normalized));
            dogPos += to.normalized * Mathf.Min(curSpeed * align * dt, room);
            dogPos.y = groundY;
            yield return null;
        }

        curSpeed = 0f;
        mode = Mode.Done;
        state = "클리어";
        Debug.Log("미니게임 5: 허들 훈련 클리어");
        onCleared.Invoke();
    }

    // 인내심이 다 됨 → 고개를 돌리고 → 처음부터
    IEnumerator FailRoutine()
    {
        mode = Mode.Busy;
        state = "흥미 잃음";
        if (fpp != null) fpp.canLook = true;
        attention = 0f;
        curSpeed = 0f;

        Quaternion away = dog.rotation * Quaternion.Euler(0f, 150f, 0f);
        Quaternion start = dog.rotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.2f)
        {
            dog.rotation = Quaternion.Slerp(start, away, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        yield return new WaitForSeconds(0.8f);

        if (!failedOnce && failDialogue != null)
        {
            failedOnce = true;
            failDialogue.Play();
            while (failDialogue.IsPlaying) yield return null;
        }

        if (trip != null)
        {
            bool done = false;
            trip.Restart(() => { ResetCourse(); done = true; });
            while (!done) yield return null;
        }
        else ResetCourse();
    }

    void LateUpdate()
    {
        if (mode == Mode.Off || dog == null) return;
        dog.position = dogPos;                       // 통통 튀기 효과는 없앰 (다리 움직임은 DogAnimator가)
    }

    // ===== 도우미 =====

    // 겨울이 시작 자리: Scene에 놓아 둔 자리, 또는 첫 허들 앞 Start Distance m
    Vector3 StartPoint()
    {
        if (useScenePosition) return scenePos;
        Transform h = hurdles[0];
        Vector3 p = h.position - Flat(h.forward) * Mathf.Max(startDistance, waitDistance + 0.2f);
        p.y = groundY;
        return p;
    }

    Vector3 PlayerPos()
    {
        return fpp != null ? fpp.transform.position : dogPos;
    }

    // 플레이어가 i번 허들을 넘어갔는지
    bool PlayerPassed(int i)
    {
        if (fpp == null) return false;
        Transform h = hurdles[i];
        return Vector3.Dot(fpp.transform.position - h.position, Flat(h.forward)) > 0.5f;
    }

    bool LookingAtDog()
    {
        if (playerCamera == null) return false;
        Vector3 to = (dog.position + Vector3.up * 0.2f) - playerCamera.transform.position;
        return Vector3.Angle(playerCamera.transform.forward, to) < lookAngle;
    }

    // d 방향으로 몸 돌리기 (speed: 초당 몇 도, 0 이하면 바로)
    void Face(Vector3 d, float speed)
    {
        d = Flat(d);
        if (d.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(d);
        dog.rotation = speed <= 0f ? target : Quaternion.RotateTowards(dog.rotation, target, speed * Time.deltaTime);
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.normalized;
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    void PlayDog(AudioClip clip)
    {
        if (dogVoice != null && clip != null) dogVoice.PlayOneShot(clip);
    }

    void UpdateStateText()
    {
        state = mood switch
        {
            Mood.Walk => "달려옴 (적당히 흔드는 중)",
            Mood.Excited => "흥분 (너무 세게)",
            Mood.Wait => "집중 (흔들어 주세요)",
            _ => wanderMoving ? "어슬렁 (휘파람)" : "멈춰서 딴짓 (휘파람)"
        };
    }

    void OnDisable()
    {
        if (fpp != null) fpp.canLook = true;   // 시점 고정이 남지 않게
    }
}