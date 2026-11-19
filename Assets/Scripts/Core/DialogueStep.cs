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
//
// 대사 끊기 규칙: "새로운 오브젝트를 조사하면 이전 대사를 끊고 새 대사를 바로 보여 준다. 단 필수 대사는 끝까지."
//   - 조사 대사: 플레이어가 클릭한 그 순간에 시작된 대사 (물건 살펴보기, 잡기 전 "힘이 없다" 등)
//       → 새로 클릭해서 다른 대사가 시작되면 바로 끊긴다. 기다리던 옛날 조사 대사도 취소된다 (줄줄이 밀리지 않게).
//   - 필수 대사 (끊기지 않고 끝까지 나옴):
//       · 클릭 없이 스크립트가 띄운 대사 (오프닝, 미니게임, 회상, 전화 장면 같은 연출)
//       · 끝난 뒤 할 일(On Finished)이 연결된 대사
//       · Must Finish를 체크한 대사
//       · PlayRequired()로 띄운 대사 (해금·목표로 이어지는 처음 조사 대사 등, DialogueInteractable이 알아서 부름)
//     필수 대사가 떠 있는 동안 새로 클릭하면, 그 대사가 끝난 뒤 "마지막으로 클릭한 것" 하나만 나온다.
// 원리: 클릭은 PlayerInteractor가 좌클릭(GetMouseButtonDown(0))한 그 프레임에 Interact()를 부르고,
//       그 안에서 바로 Play()가 불린다. 그래서 Play()가 불린 프레임에 좌클릭이 있었으면 "조사 대사"로 본다.
//       끊을 때는 GameUI의 공개 함수 AdvanceDialogue()로 남은 줄을 한꺼번에 넘긴다 (GameUI 코드는 그대로).
// 사용: 빈 오브젝트에 붙이고 Dialogue에 독백 파일을 넣은 뒤, 다른 이벤트에서 Play()를 부른다.
//       씬 시작하자마자 나올 대사(오프닝)는 Play On Start를 체크한다.
public class DialogueStep : MonoBehaviour
{
    public DialogueSequence dialogue;        // Create > Team Roulette > UI > 독백 으로 만든 파일
    public bool playOnStart = false;         // 체크하면 씬 시작 때 자동 재생 (오프닝 대사용)
    [Tooltip("체크하면 다른 오브젝트를 클릭해도 끊기지 않고 항상 끝까지 나옴 (스토리상 꼭 봐야 하는 조사 대사)")]
    public bool mustFinish = false;
    public UnityEvent onFinished;            // 대사가 끝나고 입력까지 되돌려진 뒤 실행 (미니게임 시작 등)

    // 대사가 시작돼서 onFinished까지 끝나기 전이면 true (다른 스크립트가 "끝날 때까지 기다리기"에 씀)
    public bool IsPlaying { get; private set; }

    // 이 대사를 끝까지 한 번이라도 봤으면 true (예: 일기장·사진첩을 다 봐야 메신저를 보낼 수 있음)
    public bool HasPlayed { get; private set; }

    static DialogueStep showing;             // 지금 화면에 떠 있는 DialogueStep 대사
    static int clickSerial;                  // 조사 대사(클릭)가 요청될 때마다 1씩 올라감 (가장 최근 클릭인지 확인용)

    GameUI ui;
    bool cuttable;                           // 이번 재생이 끊겨도 되는 조사 대사인지
    bool cutNow;                             // 새 조사 때문에 끊겼음
    int mySerial;

    void Start()
    {
        if (playOnStart) Play();
    }

    // 보통은 이걸 부른다. 클릭한 순간이면 조사 대사, 아니면 필수 대사로 자동 구분
    public void Play() { Begin(false); }

    // 해금·목표로 이어지는 대사처럼, 클릭으로 시작해도 끝까지 보여 줘야 할 때
    public void PlayRequired() { Begin(true); }

    // (예전 이름 그대로 둠) Play()와 같음
    public void PlayCuttable() { Begin(false); }

    void Begin(bool required)
    {
        if (ui == null) ui = FindFirstObjectByType<GameUI>();

        // 씬에 UI가 없거나 대사 파일이 비어 있으면 대사 없이 바로 다음으로
        if (ui == null || dialogue == null) { HasPlayed = true; onFinished.Invoke(); return; }

        // 이미 이 대사가 진행 중이면 다시 시작하지 않음 (같은 물건을 연타한 경우)
        if (IsPlaying) return;

        bool clicked = Input.GetMouseButtonDown(0);
        cuttable = clicked && !required && !mustFinish && onFinished.GetPersistentEventCount() == 0;

        // 새로 조사(클릭)한 거면: 기다리던 옛날 조사 대사는 취소, 떠 있던 조사 대사는 바로 끝냄
        if (clicked)
        {
            clickSerial++;
            if (showing != null && showing != this && showing.cuttable) showing.Cut();
        }

        cutNow = false;
        mySerial = clickSerial;
        IsPlaying = true;
        StartCoroutine(Run(clicked));
    }

    IEnumerator Run(bool clicked)
    {
        // 0) 한 프레임 쉬기: 씬 시작 직후라면 다른 오브젝트들의 Start가 다 끝난 뒤에 대사를 띄우기 위해
        yield return null;

        // 1) 다른 대사가 나오는 중이거나 설정창이 열려 있으면 끝날 때까지 기다렸다가 시작
        //    클릭으로 시작한 대사는, 기다리는 동안 더 새로운 클릭이 오면 취소 (마지막 클릭만 남김)
        while (ui.IsDialogueOpen || ui.IsPaused)
        {
            if (clicked && mySerial != clickSerial) { IsPlaying = false; yield break; }
            yield return null;
        }
        if (clicked && mySerial != clickSerial) { IsPlaying = false; yield break; }

        ui.ShowDialogue(dialogue);
        showing = this;

        // 2) 대사가 끝난 뒤 "조용한 프레임"이 3번 연속될 때까지 기다림
        //    (GameUI가 다음 프레임 LateUpdate에서 입력을 되돌리므로, 그보다 확실히 뒤에 실행)
        //    중간에 설정창을 열면 다시 0부터 셈. 새 조사 때문에 끊기면 바로 끝냄
        int calm = 0;
        while (calm < 3 && !cutNow)
        {
            yield return null;
            if (showing == this && !ui.IsDialogueOpen) showing = null;   // 내 대사가 화면에서 내려감
            calm = (ui.IsDialogueOpen || ui.IsPaused) ? 0 : calm + 1;
        }
        if (showing == this) showing = null;

        IsPlaying = false;
        HasPlayed = true;
        onFinished.Invoke();
    }

    // 지금 떠 있는 조사 대사가 있으면 바로 끝냄 (대사 없이 바로 화면이 열리는 곳에서 씀. 예: 두 번째부터 컴퓨터 열기)
    public static void CutCurrent()
    {
        if (showing != null && showing.cuttable) showing.Cut();
    }

    // 화면에 떠 있는 이 대사를 바로 끝냄: 남은 줄을 한꺼번에 넘긴다
    void Cut()
    {
        cutNow = true;
        if (showing == this) showing = null;
        for (int i = 0; i < 100 && ui != null && ui.IsDialogueOpen; i++) ui.AdvanceDialogue();
    }
}