using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// 책상 서랍 자물쇠: 클릭하면 (처음 한 번만 대사) → 서랍 앞에 앉기(onUse) → 다이얼(CodeLock)
//   - 손(Hand) 해금 전 클릭: noHandDialogue만 나오고 끝 ("손에 감각이 없어서 돌릴 수가 없어")
//   - 손 해금 후 처음 클릭: firstDialogue ("잠겨 있다. 숫자 자물쇠…") → onUse
//   - 다시 클릭(아직 못 열었을 때): 대사 없이 바로 onUse
//   - 이미 열었으면: solvedHint 한 줄만 화면 아래에 잠깐 표시
// onUse에는 ViewFocus.Focus를 연결하고, ViewFocus의 On Arrived에 CodeLock.Open,
// CodeLock의 On Closed에 ViewFocus.Return을 연결한다.
// 팀 공용 ConditionalInteractable을 상속만 한다 (원본은 건드리지 않음). Required Options는 비워 둔다.
//   (Required Options에 Hand를 넣으면 손 전엔 클릭해도 아무 반응이 없어서, 대신 여기서 대사로 막는다)
// 사용: 서랍(desk_drawer, Collider 있어야 함)에 붙인다.
public class DrawerLock : ConditionalInteractable
{
    public CodeLock codeLock;                // 다이얼 (열렸는지 확인용)

    [Header("손 해금 전")]
    public DialogueStep noHandDialogue;      // 손에 감각이 없을 때 대사 (비워도 됨)

    [Header("손 해금 후")]
    public DialogueStep firstDialogue;       // 처음 클릭했을 때 대사 (비워도 됨)
    public UnityEvent onUse;                 // 대사 뒤 실행 (ViewFocus.Focus 연결)
    public string solvedHint = "이미 열었어.";   // 다 연 뒤 클릭하면 뜨는 한 줄

    bool busy;                               // 대사 진행 중 (그동안 다시 클릭 무시)
    bool told;                               // 처음 대사를 봤는지

    // 원본 구조상 첫 클릭은 OnUnlocked, 그 뒤는 OnRepeatInteract로 온다 → 둘 다 같은 처리
    protected override void OnUnlocked() { Use(); }
    protected override void OnRepeatInteract() { Use(); }

    void Use()
    {
        if (busy) return;
        if (codeLock != null && codeLock.IsOpen) return;   // 이미 다이얼을 돌리는 중

        if (codeLock != null && codeLock.IsSolved)
        {
            if (!string.IsNullOrEmpty(solvedHint)) PlayerInteractor.ShowHint(solvedHint);
            return;
        }

        bool hand = OptionState.Instance != null && OptionState.Instance.IsUnlocked(OptionType.Hand);
        StartCoroutine(hand ? UseRoutine() : NoHandRoutine());
    }

    // 손 해금 전: 대사만 보고 끝 (서랍 앞으로 가지 않음)
    IEnumerator NoHandRoutine()
    {
        busy = true;
        yield return PlayAndWait(noHandDialogue);
        busy = false;
    }

    // 손 해금 후: (처음 한 번만 대사) → 서랍 앞으로
    IEnumerator UseRoutine()
    {
        busy = true;

        if (!told)
        {
            told = true;
            yield return PlayAndWait(firstDialogue);
        }

        onUse.Invoke();
        busy = false;
    }

    // 대사를 틀고, 끝나서 입력까지 돌아올 때까지 기다림
    IEnumerator PlayAndWait(DialogueStep step)
    {
        if (step == null) yield break;
        step.Play();
        while (step.IsPlaying) yield return null;
    }
}