using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TeamRoulette.UI;

// 대사 하나 + 그 대사가 끝난 뒤 할 일.
// 팀원의 GameUI(공용 UI)는 건드리지 않고, 씬에서 대사를 여러 개 쓰기 위한 창구.
//   - GameUI의 SceneUIContent에는 대사와 "끝났을 때" 이벤트가 하나씩만 있어서, 대사마다 이 컴포넌트를 하나씩 둔다.
//   - GameUI는 대사가 끝나고 한두 프레임 뒤에 플레이어 입력을 "대사 전 상태"로 되돌린다.
//     그 전에 미니게임을 시작하면 미니게임이 꺼 둔 플레이어가 다시 켜지므로,
//     입력이 다 되돌려진 뒤에 onFinished를 실행한다.
// 사용: 빈 오브젝트에 붙이고 Dialogue에 독백 파일을 넣은 뒤, 다른 이벤트에서 Play()를 부른다.
//       씬 시작하자마자 나올 대사(오프닝)는 Play On Start를 체크한다.
public class DialogueStep : MonoBehaviour
{
    public DialogueSequence dialogue;        // Create > Team Roulette > UI > 독백 으로 만든 파일
    public bool playOnStart = false;         // 체크하면 씬 시작 때 자동 재생 (오프닝 대사용)
    public UnityEvent onFinished;            // 대사가 끝나고 입력까지 되돌려진 뒤 실행 (미니게임 시작 등)

    // 대사가 시작돼서 onFinished까지 끝나기 전이면 true (다른 스크립트가 "끝날 때까지 기다리기"에 씀)
    public bool IsPlaying { get; private set; }

    // 이 대사를 끝까지 한 번이라도 봤으면 true (예: 일기장·사진첩을 다 봐야 메신저를 보낼 수 있음)
    public bool HasPlayed { get; private set; }

    GameUI ui;

    void Start()
    {
        if (playOnStart) Play();
    }

    // 다른 이벤트(오브젝트 클릭, 미니게임 끝 등)에서 부른다
    public void Play()
    {
        if (ui == null) ui = FindFirstObjectByType<GameUI>();

        // 씬에 UI가 없거나 대사 파일이 비어 있으면 대사 없이 바로 다음으로
        if (ui == null || dialogue == null) { HasPlayed = true; onFinished.Invoke(); return; }

        IsPlaying = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // 0) 한 프레임 쉬기: 씬 시작 직후라면 다른 오브젝트들의 Start가 다 끝난 뒤에 대사를 띄우기 위해
        yield return null;

        // 1) 다른 대사가 나오는 중이거나 설정창이 열려 있으면 끝날 때까지 기다렸다가 시작
        while (ui.IsDialogueOpen || ui.IsPaused) yield return null;
        ui.ShowDialogue(dialogue);

        // 2) 대사가 끝난 뒤 "조용한 프레임"이 3번 연속될 때까지 기다림
        //    (GameUI가 다음 프레임 LateUpdate에서 입력을 되돌리므로, 그보다 확실히 뒤에 실행)
        //    중간에 설정창을 열면 다시 0부터 셈
        int calm = 0;
        while (calm < 3)
        {
            yield return null;
            calm = (ui.IsDialogueOpen || ui.IsPaused) ? 0 : calm + 1;
        }

        IsPlaying = false;
        HasPlayed = true;
        onFinished.Invoke();
    }
}