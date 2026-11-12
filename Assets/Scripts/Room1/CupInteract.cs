using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// 2층 선반의 컵: 클릭하면 (처음 한 번만 대사) → 컵 앞으로 가기(onUse) → 미니게임3
//   - 처음 클릭: firstDialogue ("컵이다… 쥘 수 있을까") → onUse
//   - 다시 클릭(아직 못 쥐었을 때): 대사 없이 바로 onUse
//   - 잡기(Grab)를 이미 얻었으면: doneDialogue 대사만 나옴
//   - 높이 조건: 플레이어 발이 minFeetY보다 낮으면(바닥에 서 있으면) 손이 닿지 않는다 → 대사만 나옴
//       점프 해금 전: tooLowDialogue ("…다리에 힘이 없어 저기까지 못 올라가")
//       점프 해금 후: tooLowJumpDialogue ("…손이 안 닿아. 저 상자를 밟고 올라가면 닿을지도") — 비워 두면 tooLowDialogue
//       (점프로 1층 선반 밑 잡동사니 위에 올라서야 컵에 닿게 하기 위함. 클릭 거리(레이캐스트)는 그대로 둔다)
//       minFeetY를 0으로 두면 높이 조건 없음
// 대사는 전부 대화창(DialogueStep)으로 나온다. 화면 아래 짧은 글자(ShowHint)는 쓰지 않는다.
// onUse에는 ViewFocus.Focus를 연결하고, ViewFocus의 On Arrived에 HandAnchor.Begin을 연결한다.
// 팀 공용 ConditionalInteractable을 상속만 한다 (원본은 건드리지 않음). Required Options는 비워 둔다.
// 사용: 컵(Collider 있어야 함)에 붙인다.
public class CupInteract : ConditionalInteractable
{
    [Header("대사 (비워도 됨)")]
    public DialogueStep firstDialogue;       // 처음 클릭했을 때 ("컵이다… 쥘 수 있을까")
    public DialogueStep tooLowDialogue;      // 점프 해금 전, 바닥에서 클릭했을 때 ("…다리에 힘이 없어.")
    public DialogueStep tooLowJumpDialogue;  // 점프 해금 후, 바닥에서 클릭했을 때 ("…상자를 밟고 올라가면 닿을지도.")
    public DialogueStep doneDialogue;        // 잡기를 얻은 뒤 클릭했을 때 ("…이제 쥘 수 있어.")

    [Header("이벤트")]
    public UnityEvent onUse;                 // 대사 뒤 실행 (ViewFocus.Focus 연결)

    [Header("높이 조건 (0이면 없음)")]
    public float minFeetY = 0f;              // 플레이어 발 높이(월드 Y)가 이보다 낮으면 손이 안 닿음
    public float lastFeetY;                  // 확인용: 마지막으로 클릭했을 때 플레이어 발 높이 (이 값을 보고 minFeetY를 정함)

    bool busy;                               // 대사 진행 중 (그동안 다시 클릭 무시)
    bool told;                               // 처음 대사를 봤는지
    FirstPersonPlayer player;

    // 원본 구조상 첫 클릭은 OnUnlocked, 그 뒤는 OnRepeatInteract로 온다 → 둘 다 같은 처리
    protected override void OnUnlocked()       { Use(); }
    protected override void OnRepeatInteract() { Use(); }

    void Use()
    {
        if (busy) return;

        // 이미 잡기를 얻었으면 대사만
        bool grab = OptionState.Instance != null && OptionState.Instance.IsUnlocked(OptionType.Grab);
        if (grab) { StartCoroutine(PlayOnly(doneDialogue)); return; }

        // 높이 확인: 바닥에 서 있으면 손이 안 닿음 → 대사만
        if (player == null) player = FindFirstObjectByType<FirstPersonPlayer>();
        if (player != null) lastFeetY = player.transform.position.y;
        if (minFeetY != 0f && player != null && lastFeetY < minFeetY)
        {
            bool jump = OptionState.Instance != null && OptionState.Instance.IsUnlocked(OptionType.Jump);
            DialogueStep step = (jump && tooLowJumpDialogue != null) ? tooLowJumpDialogue : tooLowDialogue;
            StartCoroutine(PlayOnly(step));
            return;
        }

        StartCoroutine(UseRoutine());
    }

    // 대사만 재생 (끝날 때까지 다시 클릭 무시)
    IEnumerator PlayOnly(DialogueStep step)
    {
        if (step == null) yield break;
        busy = true;
        step.Play();
        while (step.IsPlaying) yield return null;
        busy = false;
    }

    IEnumerator UseRoutine()
    {
        busy = true;

        if (!told && firstDialogue != null)
        {
            told = true;
            firstDialogue.Play();
            while (firstDialogue.IsPlaying) yield return null;
        }

        onUse.Invoke();
        busy = false;
    }
}