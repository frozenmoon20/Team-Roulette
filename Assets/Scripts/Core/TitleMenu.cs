using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 타이틀 화면 (게임 이름: Restore). Title 씬의 빈 오브젝트 "TitleMenu"에 붙인다.
// 버튼은 Unity에서 직접 만들고(Canvas > Button), 각 버튼의 On Click()에 아래 함수를 연결한다.
//   - 이어하기 버튼 → TitleMenu.Continue   (저장이 없거나 엔딩을 봤으면 버튼이 흐려지고 눌리지 않음)
//   - 새 게임 버튼 → TitleMenu.NewGame     (이어할 저장이 있으면 확인창을 먼저 띄움 → 새로 시작하면 이전 저장은 지워짐)
//   - 확인창의 [새로 시작] → TitleMenu.ConfirmNewGame / [취소]·뒤 배경 → TitleMenu.CancelNewGame (Esc도 취소)
//   - 종료 버튼   → TitleMenu.Quit
// 하프톤: 타이틀 화면의 하프톤은 저장된 진행도를 따른다 (진행할수록 깨끗, 엔딩을 봤으면 완전히 깨끗).
//         저장이 없으면 No Save Halftone 값. 이 값을 매 프레임 넣으므로 Title 씬에 HalftoneController는 두지 않는다.
// 새 게임을 누르면:
//   1) 제목·버튼 같은 타이틀 UI가 서서히 사라짐 (Title UI의 CanvasGroup 투명도를 1 → 0)
//   2) 다 사라지면 화면이 까맣게 덮임 (SceneFader)
//   3) 까만 상태에서 첫 씬(침실)을 불러오고, 침실에서 까만 화면이 서서히 걷힘
// 원리: CanvasGroup은 그 아래 UI 전체의 투명도와 클릭 가능 여부를 한 번에 정하는 부품이다.
//       누르는 순간 클릭을 막고(연타 방지), 투명도를 천천히 내린 뒤 씬을 넘긴다.
// 준비: 타이틀의 제목·버튼을 빈 오브젝트 하나(예: TitleUI) 아래에 모으고, 그 오브젝트에 CanvasGroup을 붙여
//       Title UI 칸에 넣는다. (비워 두면 UI 사라지기 없이 바로 까맣게 덮임)
// 주의: File > Build Profiles > Scene List에서 Title 씬을 맨 위(0번)로 올려야 게임이 타이틀부터 시작한다.
public class TitleMenu : MonoBehaviour
{
    [Header("새 게임으로 시작할 씬")]
    public string firstScene = "Room1_Room";

    [Header("타이틀 UI (제목·버튼을 묶은 오브젝트의 CanvasGroup)")]
    public CanvasGroup titleUI;

    [Header("이어하기 버튼 오브젝트 (저장이 없으면 흐리게 + 못 누름)")]
    public GameObject continueButton;
    [Range(0f, 1f)] public float disabledAlpha = 0.45f;   // 못 누를 때 밝기 (MenuHover의 평소 밝기와 곱해짐)

    [Header("새 게임 확인창 (이어할 저장이 있을 때만 뜸, 비워 두면 묻지 않고 바로 시작)")]
    public CanvasGroup confirmPanel;         // 확인창 (TitleUI 바깥, Canvas 바로 아래에 둘 것)
    public float confirmFadeTime = 0.2f;

    [Header("타이틀 배경음 (선택)")]
    public AudioSource bgm;                  // 배경음 AudioSource (Clip에 음악. Loop·Play On Awake는 스크립트가 알아서 정함)
    [Range(0f, 1f)] public float bgmVolume = 0.6f;
    public float bgmFadeIn = 2f;             // 타이틀이 열릴 때 서서히 커지는 시간
    public float bgmFadeOut = 1.5f;          // 버튼을 누르면 서서히 작아지는 시간
    [Tooltip("체크: 저장에 배경음(BGM)이 해금돼 있을 때만 틈 (저장이 없거나 해금 전이면 무음)")]
    public bool needBgmUnlock = true;
    Coroutine bgmFade;

    [Header("저장이 없을 때 타이틀 하프톤 (0 = 가장 어두움, 1 = 깨끗)")]
    [Range(0f, 1f)] public float noSaveHalftone = 0.35f;

    [Header("화면 전환 (초)")]
    public float uiFadeTime = 1.2f;          // 타이틀 UI가 서서히 사라지는 시간
    public float fadeOutTime = 0.6f;         // UI가 사라진 뒤 화면이 까맣게 덮이는 시간
    public float fadeInTime = 1.5f;          // 침실에서 까만 화면이 걷히는 시간

    bool starting;                           // 버튼 연타 방지
    bool canContinue;                        // 이어할 저장이 있는지 (있으면 새 게임 전에 확인)
    bool confirming;                         // 확인창이 열려 있는지
    Coroutine confirmFade;
    float halftone;                          // 타이틀 하프톤 값
    static readonly int HalftoneId = Shader.PropertyToID("_HalftoneBlend");

    void Start()
    {
        // 타이틀에서는 마우스 커서가 보이고 자유롭게 움직여야 함
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;
        if (titleUI != null) { titleUI.alpha = 1f; titleUI.interactable = true; titleUI.blocksRaycasts = true; }

        // 저장 확인 → 이어하기 버튼 보이기/숨기기, 하프톤 값 정하기
        SaveSystem.Data save = SaveSystem.Load();
        canContinue = save != null && !save.endingSeen && !string.IsNullOrEmpty(save.scene)
                           && Application.CanStreamedLevelBeLoaded(save.scene);
        if (continueButton != null)
        {
            // 숨기지 않고: 누를 수 없게 + 흐리게 → 이어할 게임이 없다는 걸 보여 줌
            continueButton.SetActive(true);
            Button button = continueButton.GetComponent<Button>();
            if (button != null) button.interactable = canContinue;     // MenuHover도 이걸 보고 호버를 안 함
            CanvasGroup group = continueButton.GetComponent<CanvasGroup>();
            if (group == null) group = continueButton.AddComponent<CanvasGroup>();
            group.alpha = canContinue ? 1f : disabledAlpha;
        }
        halftone = (save != null && save.halftone > 0f) ? save.halftone : noSaveHalftone;

        // 확인창은 처음엔 숨김
        if (confirmPanel != null) SetPanel(confirmPanel, 0f, false);

        // 배경음: 저장에 배경음이 해금돼 있으면 반복 재생, 서서히 커지게
        bool bgmAllowed = !needBgmUnlock || SaveSystem.SavedUnlocked(OptionType.BGM);
        if (bgm != null && !bgmAllowed) bgm.Stop();   // Play On Awake가 켜져 있어도 해금 전이면 끔
        if (bgm != null && bgm.clip != null && bgmAllowed)
        {
            bgm.loop = true;
            bgm.volume = 0f;
            bgm.Play();
            bgmFade = StartCoroutine(FadeBgm(bgmVolume, bgmFadeIn));
        }
    }

    // 배경음 크기를 지금 값 → to로 서서히
    IEnumerator FadeBgm(float to, float time)
    {
        if (bgm == null) yield break;
        float from = bgm.volume;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(time, 0.01f))
        {
            bgm.volume = Mathf.Lerp(from, to, t);
            yield return null;
        }
        bgm.volume = to;
    }

    void Update()
    {
        Shader.SetGlobalFloat(HalftoneId, halftone);
        if (confirming && Input.GetKeyDown(KeyCode.Escape)) CancelNewGame();
    }

    // 이어하기 버튼
    public void Continue()
    {
        if (starting) return;
        string scene = SaveSystem.PrepareContinue();
        if (string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogWarning("TitleMenu: 이어할 저장이 없거나 씬을 불러올 수 없어요.", this);
            return;
        }
        starting = true;
        StartCoroutine(StartGame(scene));
    }

    // 새 게임 버튼: 이어할 저장이 있으면 확인창부터
    public void NewGame()
    {
        if (starting || confirming) return;
        if (canContinue && confirmPanel != null)
        {
            confirming = true;
            if (titleUI != null) { titleUI.interactable = false; titleUI.blocksRaycasts = false; }   // 뒤 버튼은 못 누르게
            FadePanel(1f, true);
            return;
        }
        StartNewGame();
    }

    // 확인창 [새로 시작]
    public void ConfirmNewGame()
    {
        if (!confirming || starting) return;
        confirming = false;
        FadePanel(0f, false);
        StartNewGame();
    }

    // 확인창 [취소] / 뒤 배경 클릭 / Esc
    public void CancelNewGame()
    {
        if (!confirming) return;
        confirming = false;
        FadePanel(0f, false);
        if (titleUI != null) { titleUI.interactable = true; titleUI.blocksRaycasts = true; }
    }

    void FadePanel(float to, bool clickable)
    {
        if (confirmPanel == null) return;
        if (confirmFade != null) StopCoroutine(confirmFade);
        confirmFade = StartCoroutine(FadePanelRoutine(to, clickable));
    }

    IEnumerator FadePanelRoutine(float to, bool clickable)
    {
        confirmPanel.interactable = clickable;
        confirmPanel.blocksRaycasts = clickable;
        float from = confirmPanel.alpha;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(confirmFadeTime, 0.01f))
        {
            confirmPanel.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
        confirmPanel.alpha = to;
    }

    static void SetPanel(CanvasGroup g, float alpha, bool clickable)
    {
        g.alpha = alpha;
        g.interactable = clickable;
        g.blocksRaycasts = clickable;
    }

    // 실제로 새 게임 시작 (이전 저장 지움)
    void StartNewGame()
    {
        if (starting) return;
        if (!Application.CanStreamedLevelBeLoaded(firstScene))
        {
            Debug.LogWarning("TitleMenu: '" + firstScene + "' 씬을 불러올 수 없어요. 이름과 Build Profiles의 Scene List를 확인하세요.", this);
            return;
        }
        starting = true;
        SaveSystem.PrepareNewGame();
        StartCoroutine(StartGame(firstScene));
    }

    IEnumerator StartGame(string scene)
    {
        // 0) 배경음 서서히 줄이기 (UI가 사라지는 동안 같이)
        if (bgm != null && bgm.isPlaying)
        {
            if (bgmFade != null) StopCoroutine(bgmFade);     // 커지는 중이었으면 멈추고
            bgmFade = StartCoroutine(FadeBgm(0f, bgmFadeOut));
        }

        // 1) 타이틀 UI 서서히 사라지기 (누르는 순간 클릭은 막음)
        if (titleUI != null)
        {
            titleUI.interactable = false;
            titleUI.blocksRaycasts = false;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(uiFadeTime, 0.01f))
            {
                titleUI.alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
                yield return null;
            }
            titleUI.alpha = 0f;
        }

        // 2) 이전 플레이(또는 엔딩)에서 넘어온 공용 시스템(_Systems: 해금 상태 등)을 지움
        //    → 다음 씬의 _Systems가 새로 깨어나면서 해금 상태를 처음부터(또는 저장에서) 다시 만든다
        PersistentSystems old = FindFirstObjectByType<PersistentSystems>();
        if (old != null) Destroy(old.gameObject);
        if (OptionState.Instance != null) Destroy(OptionState.Instance.gameObject);
        if (TeamRoulette.Room3.GameManager.Instance != null)                  // 거실 진행 매니저도 (엔딩 뒤 새 게임이 거실 끝 상태로 시작하지 않게)
            Destroy(TeamRoulette.Room3.GameManager.Instance.gameObject);

        // 3) 화면을 까맣게 덮고 → 씬 넘기기 → 다음 씬에서 서서히 밝아짐
        SceneFader.Go(scene, fadeOutTime, fadeInTime);
    }

    // 종료 버튼
    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;   // 에디터에서는 Play 종료
#else
        Application.Quit();
#endif
    }
}