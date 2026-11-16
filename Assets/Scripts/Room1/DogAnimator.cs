using System.Collections.Generic;
using UnityEngine;

// 겨울이(강아지 모델) 애니메이션을 "움직임을 보고" 자동으로 바꿔 준다.
// 원리:
//   - 겨울이를 실제로 옮기는 건 HurdleTraining·MemorySequence. 이 스크립트는 매 프레임 마지막에
//     "얼마나 빨리 움직였는지 / 얼마나 빨리 돌았는지 / 땅에서 얼마나 떠 있는지"만 보고 애니메이션을 고른다.
//       멈춤 → Idle (제자리에서 돌고 있으면 TurnL / TurnR)
//       느리게 → Walk / 조금 빠르게 → Trot / 빠르게 → Run   (속도에 맞춰 재생 속도도 조절 → 발 미끄러짐 줄임)
//       허들 점프: 달리며 점프 애니메이션(RunJump = Jump_Run_IP) 한 덩어리를 HurdleTraining이 타이밍을 맞춰 재생
//       (예비: 그냥 떠 있으면 Air → 내려오면 Land)
//   - 한 번 하는 동작(짖기·앉기·먹기·멈춤 행동)은 다른 스크립트가 Bark() / Sit() / Eat() / PauseAction()으로 부른다.
//     여러 동작을 순서대로 이어 붙일 수 있다 (먹기 시작 → 먹기 → 먹기 끝 → 앉기).
//     짖기·멈춤 행동은 움직이기 시작하면 바로 끊긴다 (미끄러지지 않게).
// 애니메이터 컨트롤러는 Tools > 겨울이 애니메이터 만들기 로 만든 것(Winter_Animator)을 쓴다. 상태 이름이 아래 이름들과 같아야 함.
// 사용: HurdleTraining의 Dog 칸에 들어 있는 오브젝트(스크립트가 옮기는 오브젝트)에 붙이고, Animator 칸에 자식 강아지 모델의 Animator를 넣는다.
[DefaultExecutionOrder(2000)]   // 다른 스크립트들이 겨울이를 다 옮긴 "뒤에" 움직임을 읽기 위해
public class DogAnimator : MonoBehaviour
{
    public Animator animator;                // 자식 강아지 모델(Spitz)의 Animator
    public Transform body;                   // 스크립트가 옮기는 오브젝트 (비우면 이 오브젝트)
    public bool startSitting = false;        // 체크: 시작할 때 앉아 있음

    [Header("걷기·뛰기 기준 (초당 m)")]
    public float idleBelow = 0.08f;          // 이보다 느리면 멈춤
    public float trotAbove = 0.9f;           // 이보다 빠르면 종종걸음
    public float runAbove = 2.0f;            // 이보다 빠르면 달리기
    public float walkAnimSpeed = 0.6f;       // 걷기 애니메이션 1배속일 때 맞는 이동 속도 (발이 미끄러지면 조절)
    public float trotAnimSpeed = 1.4f;       // 종종걸음 1배속일 때 맞는 이동 속도
    public float runAnimSpeed = 3.0f;        // 달리기 1배속일 때 맞는 이동 속도
    public float turnAbove = 45f;            // 멈춘 채 초당 이 각도보다 빨리 돌면 "제자리 돌기" 동작

    [Header("점프")]
    public float airHeight = 0.15f;          // 땅에서 이만큼 뜨면 "공중"으로 봄 (m) — 달리며 점프(RunJump)가 아닐 때만 사용
    public float landTime = 0.25f;           // 착지 동작을 보여 주는 시간 (초)

    [Header("달리며 점프 (Jump_Run_IP, 미리보기 프레임 ÷ 24)")]
    public float runJumpLiftoff = 10f / 24f;     // 네 발이 땅에서 떨어지는 시간 (1배속 기준, Frame 10)
    public float runJumpTouchdown = 31f / 24f;   // 앞발이 다시 땅에 닿는 시간 (Frame 31)
    public float runJumpLength = 36f / 24f;      // 전체 길이 (Frame 36)

    [Header("한 번 하는 동작 (초)")]
    public float barkTime = 1.2f;            // 짖기를 보여 주는 시간 (원본은 4.6초라 앞부분만)
    public float sitStartTime = 1.25f;       // 앉는 동작 길이
    public float sitEndTime = 1.0f;          // 일어나는 동작 길이
    public float eatStartTime = 0.58f;       // 먹으려고 고개 숙이는 동작 길이
    public float eatEndTime = 0.58f;         // 먹고 고개 드는 동작 길이
    public float fade = 0.15f;               // 동작이 바뀔 때 섞이는 시간

    [Header("다 먹고 쓰다듬받을 때 자세 (Play 중 바꿔 보며 고르기)")]
    // 후보: Lie_belly_start / Lie_belly_loop_1 (배 깔고 엎드림), Crouch_Idle_start / Crouch_Idle_loop_1 (몸을 낮춤),
    //       Lie_start / Lie_loop_1 (옆으로 누움), SitStart / Sit (앉기). 시작 칸을 비우면 바로 반복 동작으로
    public string afterEatStart = "Lie_belly_start";
    public string afterEatLoop = "Lie_belly_loop_1";

    [Header("어슬렁 중 멈췄을 때 하는 행동 (Play 중 지워 가며 자연스러운 것만 남기세요)")]
    public string[] pauseActions = { "Idle_2", "Idle_3", "Idle_4", "Idle_6", "Idle_7", "Scratching", "Sitting_loop_2", "Digging_loop" };
    [Range(0f, 1f)] public float pauseActionChance = 0.6f;   // 멈췄을 때 위 행동 중 하나를 할 확률 (나머지는 그냥 서 있기)
    public float maxPauseTime = 6f;          // 행동이 길어도 이 시간까지만 (초)

    [Header("확인용")]
    public float speed;                      // 지금 이동 속도
    public float yawSpeed;                   // 지금 도는 빠르기 (초당 도, + = 오른쪽)
    public float height;                     // 지금 땅에서 떠 있는 높이
    public string current = "";              // 지금 재생 중인 상태 이름

    struct Step
    {
        public string state;
        public float time;
        public bool breakOnMove;             // 움직이기 시작하면 끊을지
        public Step(string s, float t, bool b) { state = s; time = t; breakOnMove = b; }
    }
    readonly List<Step> queue = new List<Step>();
    bool inStep;
    float stepEnd;
    bool stepBreakOnMove;

    bool sitting;                            // 쉬는 자세(앉기·엎드리기 등)로 있는지
    string restLoop = "Sit";                 // 쉬는 자세일 때 반복할 상태
    bool airborne;
    Vector3 lastPos;
    float lastYaw;
    float jumpUntil;                         // 달리며 점프 애니메이션이 끝나는 시간 (그동안은 자동 전환 안 함)

    public float RunJumpAir => runJumpTouchdown - runJumpLiftoff;   // 공중에 떠 있는 시간 (1배속 기준)

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (body == null) body = transform;
        if (animator != null) animator.applyRootMotion = false;   // 몸은 스크립트가 옮김
        lastPos = body.position;
        lastYaw = body.eulerAngles.y;
    }

    void Start()
    {
        if (startSitting) { sitting = true; restLoop = "Sit"; Go("Sit"); }
        else Go("Idle");
    }

    void LateUpdate()
    {
        if (animator == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // 1) 이동 속도 (좌우 평면만), 도는 빠르기 — 살짝 부드럽게
        Vector3 delta = body.position - lastPos;
        lastPos = body.position;
        delta.y = 0f;
        speed = Mathf.Lerp(speed, delta.magnitude / dt, 1f - Mathf.Exp(-10f * dt));
        float yaw = body.eulerAngles.y;
        yawSpeed = Mathf.Lerp(yawSpeed, Mathf.DeltaAngle(lastYaw, yaw) / dt, 1f - Mathf.Exp(-10f * dt));
        lastYaw = yaw;

        // 달리며 점프 애니메이션이 재생되는 동안은 그대로 둠 (끝나면 달리기로 자연스럽게 이어짐)
        if (Time.time < jumpUntil) { airborne = false; return; }

        // 2) 공중 / 착지
        height = HeightAboveGround();
        bool air = height > airHeight;
        if (air && !airborne)
        {
            airborne = true;
            sitting = false;
            ClearSteps();
            Go("Air");
            return;
        }
        if (!air && airborne)
        {
            airborne = false;
            Play(new Step("Land", landTime, false));
            return;
        }
        if (airborne) return;

        // 3) 한 번 하는 동작 진행 (끝나면 다음 동작으로)
        if (inStep)
        {
            if (stepBreakOnMove && speed > idleBelow * 3f) ClearSteps();    // 움직이기 시작하면 끊음
            else if (Time.time < stepEnd) return;
            else { NextStep(); if (inStep) return; }
        }

        // 4) 앉아 있음 (움직이기 시작하면 일어남)
        if (sitting)
        {
            if (speed > idleBelow * 3f) Stand();
            else Go(restLoop);
            return;
        }

        // 5) 걷기·뛰기 / 멈춰서 돌기
        if (speed < idleBelow)
        {
            if (Mathf.Abs(yawSpeed) > turnAbove) Go(yawSpeed > 0f ? "TurnR" : "TurnL");
            else Go("Idle");
        }
        else if (speed < trotAbove) Move("Walk", walkAnimSpeed);
        else if (speed < runAbove) Move("Trot", trotAnimSpeed);
        else Move("Run", runAnimSpeed);
    }

    // ── 다른 스크립트에서 부르는 동작들 ──

    // 앉기 (앉는 동작 → 앉아 있기. 움직이기 시작하면 저절로 일어남)
    public void Sit()
    {
        if (sitting || airborne) return;
        sitting = true;
        restLoop = "Sit";
        Play(new Step("SitStart", sitStartTime, true));
    }

    public void Stand()
    {
        if (!sitting) return;
        sitting = false;
        Play(new Step("SitEnd", sitEndTime, true));
    }

    // 짖기 (움직이기 시작하면 끊김)
    public void Bark()
    {
        if (airborne) return;
        sitting = false;
        Play(new Step("Bark", barkTime, true));
    }

    // 먹기: 고개 숙이기 → seconds초 먹기 → 고개 들기 → 쓰다듬받는 자세(afterEatStart → afterEatLoop 반복)
    public void Eat(float seconds)
    {
        if (airborne) return;
        sitting = true;   // 다 먹으면 쉬는 자세로 있음
        restLoop = string.IsNullOrEmpty(afterEatLoop) ? "Sit" : afterEatLoop;
        List<Step> steps = new List<Step>
        {
            new Step("EatStart", eatStartTime, false),
            new Step("Eat", seconds, false),
            new Step("EatEnd", eatEndTime, false),
        };
        if (!string.IsNullOrEmpty(afterEatStart))
        {
            float t = afterEatStart == "SitStart" ? sitStartTime : ClipLength(afterEatStart);
            if (t > 0f) steps.Add(new Step(afterEatStart, t, false));
        }
        Play(steps.ToArray());
    }

    // 먹기 전체 길이 (고개 숙이기 + 먹기 + 고개 들기)
    public float EatTotal(float seconds) { return eatStartTime + seconds + eatEndTime; }

    // 어슬렁 중 멈췄을 때 행동 하나 (냄새 맡기·긁기 등). 실제로 한 시간을 돌려줌 (안 하면 0)
    public float PauseAction()
    {
        if (airborne || pauseActions == null || pauseActions.Length == 0) return 0f;
        if (Random.value > pauseActionChance) return 0f;
        string name = pauseActions[Random.Range(0, pauseActions.Length)];
        float len = ClipLength(name);
        if (len <= 0f) return 0f;
        float t = Mathf.Min(len, maxPauseTime);
        sitting = false;
        Play(new Step(name, t, true));
        return t;
    }

    // 하던 한 번 동작을 멈춤 (휘파람을 들었을 때 등)
    public void Interrupt()
    {
        ClearSteps();
    }

    // 달리며 점프 (Jump_Run_IP) 한 번을 animSpeed 배속으로 처음부터 재생. HurdleTraining이 도약 준비 시작 때 부름
    //   HurdleTraining은 이 애니메이션의 "발이 떨어지는 순간~닿는 순간"에 맞춰 몸을 띄운다
    public void PlayRunJump(float animSpeed)
    {
        ClearSteps();
        sitting = false;
        airborne = false;
        animator.CrossFadeInFixedTime("RunJump", 0.08f, 0, 0f);   // 같은 상태여도 처음부터
        current = "RunJump";
        animator.speed = animSpeed;
        jumpUntil = Time.time + runJumpLength / Mathf.Max(animSpeed, 0.01f);
    }

    // (예비) 점프 출발 → 떠오르면 Air로. 달리며 점프 애니메이션이 없을 때 사용
    public void JumpTakeoff()
    {
        ClearSteps();
        sitting = false;
        Go("JumpStart");
    }

    // ── 내부 ──

    void Play(params Step[] steps)
    {
        queue.Clear();
        queue.AddRange(steps);
        NextStep();
    }

    void NextStep()
    {
        if (queue.Count == 0) { inStep = false; return; }
        Step s = queue[0];
        queue.RemoveAt(0);
        Go(s.state);
        stepEnd = Time.time + s.time;
        stepBreakOnMove = s.breakOnMove;
        inStep = true;
    }

    void ClearSteps()
    {
        queue.Clear();
        inStep = false;
    }

    // 걷기·뛰기: 이동 속도에 맞춰 재생 속도를 바꿔 발 미끄러짐을 줄임
    void Move(string state, float animSpeed)
    {
        Go(state);
        animator.speed = Mathf.Clamp(speed / Mathf.Max(animSpeed, 0.01f), 0.5f, 2f);
    }

    void Go(string state)
    {
        if (state != "Walk" && state != "Trot" && state != "Run") animator.speed = 1f;
        if (current == state) return;
        animator.CrossFadeInFixedTime(state, fade);
        current = state;
    }

    // 상태 이름과 같은 애니메이션의 길이 (멈춤 행동용. 예: "Idle_2" → Arm_Spitz|Idle_2)
    float ClipLength(string state)
    {
        RuntimeAnimatorController rac = animator.runtimeAnimatorController;
        if (rac == null) return 0f;
        foreach (AnimationClip c in rac.animationClips)
            if (c != null && (c.name == state || c.name.EndsWith("|" + state))) return c.length;
        Debug.LogWarning("DogAnimator: '" + state + "' 애니메이션이 컨트롤러에 없어요. Tools > 겨울이 애니메이터 만들기를 다시 실행하세요.", this);
        return 0f;
    }

    // 몸 아래로 광선을 쏴서 땅(또는 허들 윗면)까지의 높이를 잼. 자기 몸·트리거는 무시
    float HeightAboveGround()
    {
        Vector3 from = body.position + Vector3.up * 0.5f;
        RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        foreach (RaycastHit h in hits)
        {
            if (h.transform.IsChildOf(body)) continue;
            if (h.distance < best) best = h.distance;
        }
        return best == float.MaxValue ? 0f : best - 0.5f;
    }
}