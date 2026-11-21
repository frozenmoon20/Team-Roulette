using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// 침실 문: 클릭하면 지금 상태에 따라 세 가지 중 하나
//   1) 메신저를 아직 안 보냄(MessageSent 없음) → notReadyDialogue ("…아직 나가고 싶지 않아.")
//   2) 보냈지만 잡기(Grab)가 없음              → noGrabDialogue ("문고리를 쥘 수가 없어…")
//        처음 한 번만 그 뒤에 onFirstNoGrab (목표: door 완료 → strength 보이기)
//   3) 둘 다 있음                              → onOpen (문 앞으로 가기 → 미니게임4)
//   4) 이미 열었음 (Knob Turn이 열림)           → openedDialogue만 (비워 두면 아무 일 없음) → 미니게임을 다시 하지 않음
// 원리: 클릭할 때마다 OptionState에서 해금 여부를 직접 확인한다. 그래서 순서가 어긋나도 항상 맞는 대사가 나온다.
// 팀 공용 ConditionalInteractable을 상속만 한다 (원본은 건드리지 않음). Required Options는 비워 둔다.
// 사용: 문(Collider 있어야 함)에 붙인다.
public class DoorInteract : ConditionalInteractable
{
    [Header("대사 (비워도 됨)")]
    public DialogueStep notReadyDialogue;    // 메신저 보내기 전
    public DialogueStep noGrabDialogue;      // 보냈지만 잡기 전

    [Header("이벤트")]
    public UnityEvent onFirstNoGrab;         // 잡기 전 대사가 처음 끝났을 때 한 번 (CompleteObjective door, RevealObjective strength)
    public UnityEvent onOpen;                // 둘 다 있을 때 (ViewFocus.Focus → 미니게임4)

    [Header("이미 열었을 때")]
    public KnobTurn knobTurn;                // 문 미니게임 (Door_Turn). 열렸는지 확인용
    public DialogueStep openedDialogue;      // 연 뒤 다시 클릭했을 때 대사 (비워도 됨)

    bool busy;                               // 대사 진행 중 (그동안 다시 클릭 무시)
    bool toldNoGrab;                         // 잡기 전 대사를 본 적 있는지

    // 원본 구조상 첫 클릭은 OnUnlocked, 그 뒤는 OnRepeatInteract로 온다 → 둘 다 같은 처리
    protected override void OnUnlocked() { Use(); }
    protected override void OnRepeatInteract() { Use(); }

    void Use()
    {
        if (busy) return;

        if (knobTurn != null && knobTurn.IsOpened)
        {
            if (openedDialogue != null) StartCoroutine(Play(openedDialogue, null));
            return;
        }

        OptionState state = OptionState.Instance;
        bool sent = state != null && state.IsUnlocked(OptionType.MessageSent);
        bool grab = state != null && state.IsUnlocked(OptionType.Grab);

        if (!sent) StartCoroutine(Play(notReadyDialogue, null));
        else if (!grab) StartCoroutine(Play(noGrabDialogue, toldNoGrab ? null : onFirstNoGrab));
        else onOpen.Invoke();

        if (sent && !grab) toldNoGrab = true;
    }

    // 대사 재생 → 끝나면 after 실행
    IEnumerator Play(DialogueStep step, UnityEvent after)
    {
        busy = true;
        if (step != null)
        {
            step.Play();
            while (step.IsPlaying) yield return null;
        }
        if (after != null) after.Invoke();
        busy = false;
    }
}