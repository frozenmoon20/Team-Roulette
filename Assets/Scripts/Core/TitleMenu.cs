using System.Collections;
using UnityEngine;

// 타이틀 화면 (게임 이름: Restore). Title 씬의 빈 오브젝트 "TitleMenu"에 붙인다.
// 버튼은 Unity에서 직접 만들고(Canvas > Button), 각 버튼의 On Click()에 아래 함수를 연결한다.
//   - 이어하기 버튼 → TitleMenu.Continue   (저장이 없거나 엔딩을 봤으면 버튼이 저절로 숨음)
//   - 새 게임 버튼 → TitleMenu.NewGame     (이전 저장은 지워짐)
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

    [Header("이어하기 버튼 오브젝트 (저장이 없으면 숨김)")]
    public GameObject continueButton;

    [Header("저장이 없을 때 타이틀 하프톤 (0 = 가장 어두움, 1 = 깨끗)")]
    [Range(0f, 1f)] public float noSaveHalftone = 0.35f;

    [Header("화면 전환 (초)")]
    public float uiFadeTime = 1.2f;          // 타이틀 UI가 서서히 사라지는 시간
    public float fadeOutTime = 0.6f;         // UI가 사라진 뒤 화면이 까맣게 덮이는 시간
    public float fadeInTime = 1.5f;          // 침실에서 까만 화면이 걷히는 시간

    bool starting;                           // 버튼 연타 방지
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
        bool canContinue = save != null && !save.endingSeen && !string.IsNullOrEmpty(save.scene)
                           && Application.CanStreamedLevelBeLoaded(save.scene);
        if (continueButton != null) continueButton.SetActive(canContinue);
        halftone = (save != null && save.halftone > 0f) ? save.halftone : noSaveHalftone;
    }

    void Update()
    {
        Shader.SetGlobalFloat(HalftoneId, halftone);
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

    // 새 게임 버튼
    public void NewGame()
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