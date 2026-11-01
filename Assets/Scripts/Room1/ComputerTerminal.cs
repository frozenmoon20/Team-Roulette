using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// 컴퓨터: 손(Hand) 해금 전/후로 반응이 다르다.
//   - 손 해금 전 클릭: lockedDialogue ("컴퓨터다.. 손에 감각이 없다…")
//                      → 처음 한 번만 onFirstLocked (목표 "손의 감각을 찾기" 추가)
//   - 손 해금 후 클릭: openDialogue ("손의 감각이 돌아왔으니 이젠 볼 수 있어.")
//                      → onOpened (다음 단계에서 카메라 이동 + 컴퓨터 화면 + 선택창을 여기에 연결)
// 팀 공용 ConditionalInteractable을 상속만 한다 (원본은 건드리지 않음). Required Options는 비워 둔다.
// 사용: 컴퓨터 오브젝트(Collider 있어야 함)에 붙이고, 자식 오브젝트 두 개에 DialogueStep을 하나씩 붙여 연결한다.
public class ComputerTerminal : ConditionalInteractable
{
    [Header("손 해금 전")]
    public DialogueStep lockedDialogue;      // 손에 감각이 없을 때 대사
    public UnityEvent onFirstLocked;         // 처음 한 번만 실행 (목표 추가)

    [Header("손 해금 후")]
    public DialogueStep openDialogue;        // 손이 돌아온 뒤 대사
    public UnityEvent onOpened;              // 대사가 끝난 뒤 실행 (컴퓨터 보기 시작)

    bool busy;                               // 대사 진행 중 (그동안 다시 클릭 무시)
    bool toldLocked;                         // 손 없을 때 대사를 한 번이라도 봤는지

    // 원본 구조상 첫 클릭은 OnUnlocked, 그 뒤는 OnRepeatInteract로 온다 → 둘 다 같은 처리
    protected override void OnUnlocked() { Use(); }
    protected override void OnRepeatInteract() { Use(); }

    void Use()
    {
        if (busy) return;
        bool hand = OptionState.Instance != null && OptionState.Instance.IsUnlocked(OptionType.Hand);
        StartCoroutine(hand ? OpenRoutine() : LockedRoutine());
    }

    IEnumerator LockedRoutine()
    {
        busy = true;
        yield return PlayAndWait(lockedDialogue);
        if (!toldLocked)
        {
            toldLocked = true;
            onFirstLocked.Invoke();
        }
        busy = false;
    }

    IEnumerator OpenRoutine()
    {
        busy = true;
        yield return PlayAndWait(openDialogue);
        onOpened.Invoke();
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