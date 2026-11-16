using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// 클릭하면 대사가 나오고, 대사가 끝나면 해금 / 목표 추가 등을 하는 오브젝트 (강아지 계단, 컴퓨터, 핸드크림 …)
// 팀 공용 ConditionalInteractable을 "상속"만 한다 → 원본 코드는 건드리지 않음 (원본 주석의 사용법 그대로)
//   - 처음 클릭: firstDialogue 재생 → 끝나면 (체크했으면) 옵션 해금 → onFirstFinished 실행
//   - 대사 → 미니게임 → 해금: Unlock Option 체크 해제, onFirstFinished에 미니게임 시작,
//     미니게임 클리어 이벤트에 UnlockNow() 연결
//   - 다시 클릭: repeatDialogue가 있으면 그 대사, 없으면 repeatHint 한 줄을 화면 아래에 잠깐 표시
//   - requiredOptions(원본 칸)에 옵션을 넣으면 그 옵션이 해금되기 전엔 클릭해도 반응 없음
// 사용: 오브젝트(Collider 있어야 함)에 이 스크립트 + InteractName + DialogueStep을 붙이고 칸을 채운다.
public class DialogueInteractable : ConditionalInteractable
{
    [Header("처음 클릭했을 때")]
    public DialogueStep firstDialogue;       // 처음 나올 대사 (같은 오브젝트의 DialogueStep을 끌어다 놓기)
    public bool unlockOption = false;        // 대사가 끝나면 옵션을 해금할지
    public OptionType unlockType;            // 해금할 옵션 (해금 알림은 UnlockNotice가 자동으로 띄움)
    public UnityEvent onFirstFinished;       // 대사(+해금)가 끝난 뒤 할 일 (목표 추가 등)

    [Header("다시 클릭했을 때 (둘 중 하나만 채워도 됨)")]
    public DialogueStep repeatDialogue;      // 다시 볼 때 대사 (비워 두면 아래 한 줄 문구 사용)
    public string repeatHint = "";           // 다시 볼 때 화면 아래에 잠깐 뜨는 한 줄

    bool running;                            // 처음 대사가 진행 중 (그동안 다시 클릭 무시)

    protected override void OnUnlocked()
    {
        StartCoroutine(FirstRoutine());
    }

    IEnumerator FirstRoutine()
    {
        running = true;

        // 1) 대사 재생 → 끝나고 입력까지 돌아올 때까지 기다림
        if (firstDialogue != null)
        {
            firstDialogue.Play();
            while (firstDialogue.IsPlaying) yield return null;
        }

        // 2) 옵션 해금 (대사가 다 끝난 뒤라서 해금 알림이 대사와 겹치지 않음)
        if (unlockOption) UnlockNow();

        // 3) 그 밖에 할 일 (목표 추가, 미니게임 시작 등)
        onFirstFinished.Invoke();

        running = false;
    }

    // Unlock Type에 고른 옵션을 지금 해금.
    // 대사 → 미니게임 → 해금 구조일 때: Unlock Option은 체크 해제하고,
    // 미니게임의 "클리어" 이벤트에 이 함수를 연결한다.
    public void UnlockNow()
    {
        if (OptionState.Instance != null) OptionState.Instance.Unlock(unlockType);
    }

    protected override void OnRepeatInteract()
    {
        if (running) return;

        if (repeatDialogue != null)
        {
            if (!repeatDialogue.IsPlaying) repeatDialogue.Play();
        }
        else if (!string.IsNullOrEmpty(repeatHint))
        {
            PlayerInteractor.ShowHint(repeatHint);
        }
    }
}