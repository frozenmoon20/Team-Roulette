using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using TeamRoulette.UI;

// 컴퓨터 화면 + 선택창 (메신저 / 일기장 / 사진첩 / 그만 둔다)
// 팀원 대화창(GameUI)에는 선택지 기능이 없어서 따로 만든다. 팀원 코드는 건드리지 않음.
// 원리:
//   - Open(): 컴퓨터 화면(screenPanel)과 선택창(menuPanel)을 켠다. (ViewFocus의 On Arrived에 연결)
//   - A/D(←/→) 또는 W/S(↑/↓)로 고르고 Space(또는 Enter)로 결정한다. 마우스·커서는 건드리지 않는다.
//   - 항목을 고르면 선택창만 숨기고 그 항목의 대사(DialogueStep)를 튼다. 대사가 끝나면 선택창이 다시 나온다.
//   - 조건(Need Played): 지정한 대사들을 다 본 뒤에만 그 항목이 제대로 실행된다.
//     아직이면 Locked Dialogue(예: "…아직은 못 하겠어")만 나오고 선택창으로 돌아온다.
//   - 하위 선택지(Sub Menu): 대사가 끝난 뒤 다른 ComputerMenu(예: "답장을 보낼까?")를 열고, 닫힐 때까지 기다린다.
//     하위 선택지는 같은 선택창 UI를 같이 쓰고, Screen Panel은 비워 둔다.
//   - Close After: 그 항목이 끝나면 이 선택창을 닫는다 (하위 선택지에서 "보낸다" 후 원래 메뉴로 돌아갈 때).
//   - "그만 둔다"(Is Exit 체크) → 화면과 선택창을 끄고 onClosed (ViewFocus의 Return에 연결).
// 사용: 빈 오브젝트에 붙이고, Screen Panel / Menu Panel / Menu Text를 연결한 뒤 Items에 항목을 채운다.
public class ComputerMenu : MonoBehaviour
{
    [System.Serializable]
    public class Item
    {
        public string label = "";            // 선택창에 보일 이름
        public DialogueStep dialogue;        // 고르면 나올 대사 (비워도 됨)
        public GameObject showOnScreen;      // 대사 동안 컴퓨터 화면에 띄울 것 (일기장 화면, 사진 등, 비워도 됨)
        public bool isExit = false;          // 체크하면 "그만 둔다" (선택창을 닫음)

        [Header("조건 (비워 두면 항상 실행)")]
        public DialogueStep[] needPlayed;    // 이 대사들을 다 봐야 실행됨 (예: 일기장, 사진첩)
        public DialogueStep lockedDialogue;  // 조건이 안 됐을 때 나올 대사 (예: "…아직은 못 하겠어")

        [Header("이어서")]
        public ComputerMenu subMenu;         // 대사가 끝난 뒤 열 하위 선택지 (비워도 됨)
        public bool closeAfter = false;      // 이 항목이 끝나면 이 선택창을 닫음
        public UnityEvent onChosen;          // 조건을 통과해 실행될 때, 대사 전에 실행 (비워도 됨)
        public UnityEvent onDone;            // 조건을 통과해 대사까지 끝난 뒤 실행 (해금 등, 비워도 됨)
    }

    [Header("UI")]
    public GameObject screenPanel;           // 컴퓨터 화면 (하위 선택지는 비워 둠)
    public GameObject menuPanel;             // 선택창 (대화창 자리)
    public TMP_Text menuText;                // 선택창 글자 (질문 + 항목들을 한 글자 오브젝트에 그림)
    [TextArea] public string prompt = "컴퓨터에서 볼 수 있는 건…";
    public string selectedColor = "#FFFFFF"; // 고른 항목 색
    public string normalColor = "#808080";   // 나머지 항목 색
    public bool horizontal = true;           // 체크: 항목을 한 줄에 가로로 / 해제: 세로 목록
    public string gap = "        ";           // 가로일 때 항목 사이 띄어쓰기

    [Header("항목 (위에서부터 순서대로)")]
    public Item[] items;

    [Header("닫혔을 때 (메인 메뉴: ViewFocus.Return 연결 / 하위 선택지: 비워 둠)")]
    public UnityEvent onClosed;

    public bool IsOpen { get; private set; }

    int index;
    bool busy;                               // 항목 대사 진행 중
    int openedFrame;                         // 열린 프레임 (같은 프레임의 Space 입력을 무시하기 위해)
    GameUI ui;

    void Awake()
    {
        if (screenPanel != null) screenPanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(false);
    }

    // 선택창 열기 (메인 메뉴는 컴퓨터 화면도 같이 켬)
    public void Open()
    {
        if (IsOpen) return;
        if (ui == null) ui = FindFirstObjectByType<GameUI>();
        IsOpen = true;
        busy = false;
        index = 0;
        openedFrame = Time.frameCount;
        if (screenPanel != null) screenPanel.SetActive(true);
        if (menuPanel != null) menuPanel.SetActive(true);
        Redraw();
    }

    // 닫기
    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (screenPanel != null) screenPanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(false);
        onClosed.Invoke();
    }

    void Update()
    {
        if (!IsOpen || busy || items == null || items.Length == 0) return;
        if (Time.frameCount == openedFrame) return;                     // 열린 순간의 키 입력은 무시
        if (ui != null && (ui.IsDialogueOpen || ui.IsPaused)) return;   // 대사·설정창 중엔 입력 안 받음

        bool prev = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)
                 || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
        bool next = Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)
                 || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);

        if (prev)
        {
            index = (index - 1 + items.Length) % items.Length;
            Redraw();
        }
        else if (next)
        {
            index = (index + 1) % items.Length;
            Redraw();
        }
        else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
        {
            Choose(items[index]);
        }
    }

    void Choose(Item item)
    {
        if (item.isExit) { Close(); return; }
        StartCoroutine(PlayItem(item));
    }

    // 조건에 적힌 대사들을 다 봤는지
    bool Ready(Item item)
    {
        if (item.needPlayed == null) return true;
        foreach (DialogueStep d in item.needPlayed)
            if (d != null && !d.HasPlayed) return false;
        return true;
    }

    IEnumerator PlayItem(Item item)
    {
        busy = true;
        if (menuPanel != null) menuPanel.SetActive(false);

        if (!Ready(item))
        {
            // 조건이 안 됨 → 막힌 대사만 보고 선택창으로
            yield return PlayAndWait(item.lockedDialogue);
        }
        else
        {
            item.onChosen.Invoke();

            if (item.showOnScreen != null) item.showOnScreen.SetActive(true);
            yield return PlayAndWait(item.dialogue);

            // 하위 선택지가 있으면 열고, 닫힐 때까지 기다림 (꺼져 있는 하위 선택지는 건너뜀)
            if (item.subMenu != null && item.subMenu.isActiveAndEnabled)
            {
                item.subMenu.Open();
                while (item.subMenu.IsOpen) yield return null;
            }

            if (item.showOnScreen != null) item.showOnScreen.SetActive(false);

            if (item.closeAfter)
            {
                // 먼저 닫고 나서 onDone 실행 (onDone에서 이 선택창 오브젝트를 꺼도 안전하도록)
                busy = false;
                Close();
                item.onDone.Invoke();
                yield break;
            }

            item.onDone.Invoke();
        }

        if (IsOpen && menuPanel != null) menuPanel.SetActive(true);
        Redraw();
        busy = false;
    }

    IEnumerator PlayAndWait(DialogueStep step)
    {
        if (step == null) yield break;
        step.Play();
        while (step.IsPlaying) yield return null;
    }

    // 질문 + 항목 목록을 글자 하나에 그림 (고른 항목은 밝게, ▶ 표시)
    void Redraw()
    {
        if (menuText == null || items == null) return;
        string s = prompt + "\n\n";
        for (int i = 0; i < items.Length; i++)
        {
            bool sel = i == index;
            string one = "<color=" + (sel ? selectedColor : normalColor) + ">" + (sel ? "▶ " : "   ") + items[i].label + "</color>";
            if (horizontal) s += (i > 0 ? gap : "") + one;
            else s += (i > 0 ? "\n" : "") + one;
        }
        menuText.text = s;
    }
}