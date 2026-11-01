using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using TeamRoulette.UI;

// 다이얼 숫자 자물쇠 (서랍 등)
// 원리:
//   - Open(): 선택창 상자(컴퓨터 선택창과 같은 PC_Menu)를 켜고 "질문 + 숫자 4개 + 그만 둔다"를 그린다.
//   - A/D(←/→): 자리 옮기기 (숫자 4자리 → (추가 선택지) → 맨 오른쪽 "그만 둔다"까지)
//   - W/S(↑/↓): 고른 자리의 숫자를 0~9로 돌리기 (9 다음은 0)
//   - Space(또는 Enter): 숫자 자리에서 누르면 정답 확인 / "그만 둔다"에서 누르면 닫기
//   - 추가 선택지(예: "포스트잇 보기", Extra Label이 비어 있으면 없음):
//       누르면 선택창을 잠시 숨기고 → 시점을 Extra Point로 더 가까이 들이밀고(ViewFocus.ShiftTo) → Extra Dialogue 재생
//       → 끝나면 원래 시점으로 돌아와(ShiftBack) 선택창으로 돌아옴. 3D 물체를 그대로 보여 주므로 하프톤 분위기가 유지된다.
//   - 맞으면: onSolved(서랍 열기) → 맞았을 때 대사 → 닫고 onClosed
//     틀리면: 틀렸을 때 대사 → 다시 돌리기. 돌려 둔 숫자는 그대로 남는다 (진짜 자물쇠처럼)
//   - 대사·설정창이 떠 있는 동안에는 입력을 받지 않는다.
// 사용: 빈 오브젝트에 붙이고 Menu Panel / Menu Text(/ Hint Text)를 컴퓨터 선택창 것으로 연결.
//       On Closed에 ViewFocus.Return, On Solved에 SlideOpen.Open을 연결한다.
public class CodeLock : MonoBehaviour
{
    [Header("UI (컴퓨터 선택창 상자를 같이 씀)")]
    public GameObject menuPanel;             // 선택창 상자 (PC_Menu)
    public TMP_Text menuText;                // 선택창 글자 (MenuText)
    public TMP_Text hintText;                // 아래 조작 안내 글자 (MenuHint, 비워도 됨)
    public string hint = "A / D 자리    W / S 돌리기    SPACE 확인";
    [TextArea] public string prompt = "숫자 자물쇠다. 네 자리…";
    public string exitLabel = "그만 둔다";
    public string selectedColor = "#FFFFFF"; // 고른 자리 색
    public string normalColor = "#808080";   // 나머지 색
    public string gap = "     ";             // 숫자 사이 띄어쓰기

    [Header("정답")]
    public string answer = "1203";           // 숫자만 (자릿수 = 다이얼 개수)

    [Header("자물쇠 모형에 붙은 숫자 글자 (비워도 됨)")]
    public TMP_Text worldText;               // 3D 자물쇠 위 TextMeshPro (돌리면 같이 바뀜)

    [Header("추가 선택지 (Extra Label을 비워 두면 없음)")]
    public string extraLabel = "";           // 예: "포스트잇 보기"
    public ViewFocus extraFocus;             // 지금 보고 있는 ViewFocus (Drawer_Focus)
    public Transform extraPoint;             // 더 가까이 볼 자리 (포스트잇 앞 빈 오브젝트, 파란 화살표가 포스트잇을 향하게)
    public DialogueStep extraDialogue;       // 들여다보는 동안 나올 대사

    [Header("대사 (비워도 됨)")]
    public DialogueStep wrongDialogue;       // 틀렸을 때 ("…아닌가 봐.")
    public DialogueStep solvedDialogue;      // 맞았을 때 ("열렸다.")

    [Header("이벤트")]
    public UnityEvent onSolved;              // 맞힌 순간 (SlideOpen.Open 연결)
    public UnityEvent onClosed;              // 닫힐 때 - 맞혔을 때도, 그만 둘 때도 (ViewFocus.Return 연결)

    public bool IsOpen { get; private set; }
    public bool IsSolved { get; private set; }

    int[] digits;
    int index;                               // 0~자릿수-1 = 숫자 자리, 그다음 (추가 선택지), 맨 끝 = "그만 둔다"
    bool busy;                               // 정답 확인·대사 중
    int openedFrame;                         // 열린 프레임 (같은 프레임의 Space 입력 무시)
    string savedHint;                        // 원래 안내 문구 (닫을 때 되돌림)
    GameUI ui;

    bool HasExtra => !string.IsNullOrEmpty(extraLabel);
    int ExtraIndex => digits.Length;                                    // 추가 선택지 자리 (있을 때만)
    int ExitIndex => digits.Length + (HasExtra ? 1 : 0);               // "그만 둔다" 자리

    void Awake()
    {
        digits = new int[answer.Length];
        UpdateWorld();
    }

    // 자물쇠 열기 (ViewFocus의 On Arrived에 연결)
    [ContextMenu("테스트: 열기 (Open)")]
    public void Open()
    {
        if (IsOpen || IsSolved) return;
        if (ui == null) ui = FindFirstObjectByType<GameUI>();
        IsOpen = true;
        busy = false;
        index = 0;
        openedFrame = Time.frameCount;
        ShowPanel(true);
        Redraw();
    }

    // 닫기 ("그만 둔다" 또는 정답 후)
    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        ShowPanel(false);
        onClosed.Invoke();
    }

    void Update()
    {
        if (!IsOpen || busy) return;
        if (Time.frameCount == openedFrame) return;                     // 열린 순간의 키 입력은 무시
        if (ui != null && (ui.IsDialogueOpen || ui.IsPaused)) return;   // 대사·설정창 중엔 입력 안 받음

        int slots = ExitIndex + 1;                                      // 숫자 자리들 + (추가 선택지) + "그만 둔다"
        bool onDigit = index < digits.Length;

        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            index = (index - 1 + slots) % slots;
            Redraw();
        }
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            index = (index + 1) % slots;
            Redraw();
        }
        else if (onDigit && (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)))
        {
            digits[index] = (digits[index] + 1) % 10;
            Redraw();
            UpdateWorld();
        }
        else if (onDigit && (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)))
        {
            digits[index] = (digits[index] + 9) % 10;                   // -1과 같음 (0 아래는 9)
            Redraw();
            UpdateWorld();
        }
        else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
        {
            if (onDigit) StartCoroutine(Check());
            else if (HasExtra && index == ExtraIndex) StartCoroutine(ShowExtra());
            else Close();
        }
    }

    IEnumerator Check()
    {
        busy = true;
        ShowPanel(false);

        if (Current() == answer)
        {
            // 정답: 서랍 열기 → 대사 → 닫기
            IsSolved = true;
            onSolved.Invoke();
            yield return PlayAndWait(solvedDialogue);
            busy = false;
            Close();
            yield break;
        }

        // 오답: 대사 → 다시 돌리기
        yield return PlayAndWait(wrongDialogue);
        ShowPanel(true);
        Redraw();
        busy = false;
    }

    // 추가 선택지: 선택창 숨기고 → 더 가까이 들여다보기 → 대사 → 원래 시점으로 → 선택창
    IEnumerator ShowExtra()
    {
        busy = true;
        ShowPanel(false);
        bool zoom = extraFocus != null && extraPoint != null;
        if (zoom)
        {
            extraFocus.ShiftTo(extraPoint);
            while (extraFocus.IsMoving) yield return null;
        }
        yield return PlayAndWait(extraDialogue);
        if (zoom)
        {
            extraFocus.ShiftBack();
            while (extraFocus.IsMoving) yield return null;
        }
        ShowPanel(true);
        Redraw();
        busy = false;
    }

    string Current()
    {
        string s = "";
        foreach (int d in digits) s += d;
        return s;
    }

    // 선택창 상자 켜기/끄기 + 아래 안내 문구 바꿔 두기/되돌리기
    void ShowPanel(bool on)
    {
        if (menuPanel != null) menuPanel.SetActive(on);
        if (hintText == null) return;
        if (on)
        {
            savedHint = hintText.text;
            hintText.text = hint;
        }
        else if (savedHint != null)
        {
            hintText.text = savedHint;
        }
    }

    // 질문 + [숫자] + 그만 둔다 를 글자 하나에 그림
    // 고르지 않은 자리의 [ ]는 투명하게 그려서, 골라도 글자 위치가 흔들리지 않게 함
    void Redraw()
    {
        if (menuText == null) return;
        string s = prompt + "\n\n";
        for (int i = 0; i < digits.Length; i++)
        {
            bool sel = i == index;
            string bracket = sel ? selectedColor : "#00000000";
            string color = sel ? selectedColor : normalColor;
            s += (i > 0 ? gap : "")
               + "<color=" + bracket + ">[ </color>"
               + "<color=" + color + ">" + digits[i] + "</color>"
               + "<color=" + bracket + "> ]</color>";
        }
        if (HasExtra)
        {
            bool extraSel = index == ExtraIndex;
            s += gap + gap + "<color=" + (extraSel ? selectedColor : normalColor) + ">" + (extraSel ? "▶ " : "   ") + extraLabel + "</color>";
        }
        bool exitSel = index == ExitIndex;
        s += gap + gap + "<color=" + (exitSel ? selectedColor : normalColor) + ">" + (exitSel ? "▶ " : "   ") + exitLabel + "</color>";
        menuText.text = s;
    }

    // 3D 자물쇠 위 숫자도 같이 돌림
    void UpdateWorld()
    {
        if (worldText == null || digits == null) return;
        string s = "";
        for (int i = 0; i < digits.Length; i++) s += (i > 0 ? " " : "") + digits[i];
        worldText.text = s;
    }

    IEnumerator PlayAndWait(DialogueStep step)
    {
        if (step == null) yield break;
        step.Play();
        while (step.IsPlaying) yield return null;
    }
}