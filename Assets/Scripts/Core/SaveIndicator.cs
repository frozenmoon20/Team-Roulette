using TMPro;
using UnityEngine;

// 자동저장될 때 화면 구석에 "저장 중..."을 잠깐 띄우는 표시.
// 씬에 붙이지 않는다. 게임이 켜지면 Resources 폴더의 "SaveIndicator" 프리팹을 저절로 하나 만들어서
// 씬이 바뀌어도 계속 유지한다 (그래서 침실·복도·거실 어느 씬에서 시작해도 동작).
// 원리:
//   - SaveSystem이 저장 내용이 실제로 바뀌었을 때 Saved 알림을 보낸다.
//   - 알림이 오면 "Show Time초 뒤까지 보이기"로 정해 둔다. 저장이 연달아 오면 그 시간만 늘어나서 한 번만 보인다.
//   - 매 프레임 투명도(CanvasGroup alpha)를 목표(보이기 1 / 숨기기 0)로 조금씩 옮겨서 부드럽게 나타나고 사라진다.
//   - 글자 뒤의 점(. .. ...)은 시간에 따라 바뀐다.
//   - 게임이 멈춰 있어도(Time.timeScale = 0) 동작하도록 unscaled 시간을 쓴다.
// 준비: Assets/Resources 폴더에 "SaveIndicator" 이름의 프리팹 (맨 위에 Canvas + CanvasGroup + 이 스크립트)
public class SaveIndicator : MonoBehaviour
{
    const string PrefabName = "SaveIndicator";

    [Tooltip("전체 투명도를 조절할 CanvasGroup (비워 두면 자동으로 찾음)")]
    public CanvasGroup group;
    [Tooltip("\"저장 중\" 글자 (비워 두면 점 애니메이션 없음)")]
    public TMP_Text label;
    [Tooltip("돌릴 아이콘 (선택, 비워 두면 안 돌림)")]
    public RectTransform spinner;

    [Header("표시")]
    public string text = "저장 중";
    public float showTime = 1.5f;       // 마지막 저장 뒤 보이는 시간 (초)
    public float fadeTime = 0.3f;       // 나타나고 사라지는 시간 (초)
    public float dotSpeed = 3f;         // 1초에 점이 바뀌는 횟수
    public float spinSpeed = 180f;      // 아이콘이 1초에 도는 각도

    static SaveIndicator instance;
    float showUntil;
    int lastDots = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (instance != null) return;
        GameObject prefab = Resources.Load<GameObject>(PrefabName);
        if (prefab == null)
        {
            Debug.LogWarning("SaveIndicator: Resources 폴더에 \"" + PrefabName + "\" 프리팹이 없어서 저장 표시를 안 띄워요.");
            return;
        }
        GameObject go = Instantiate(prefab);
        go.name = PrefabName;
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;

        if (group == null) group = GetComponentInChildren<CanvasGroup>(true);
        if (group != null)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;   // 표시가 마우스 클릭을 가로채지 않게
        }
        SaveSystem.Saved += OnSaved;
    }

    void OnDestroy()
    {
        if (instance != this) return;
        SaveSystem.Saved -= OnSaved;
        instance = null;
    }

    void OnSaved()
    {
        showUntil = Time.unscaledTime + showTime;
    }

    void Update()
    {
        if (group == null) return;

        float target = Time.unscaledTime < showUntil ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime));
        if (group.alpha <= 0f) { lastDots = -1; return; }   // 안 보일 때는 아무것도 안 함

        if (label != null)
        {
            int dots = (int)(Time.unscaledTime * dotSpeed) % 4;   // 0~3개
            if (dots != lastDots)
            {
                lastDots = dots;
                label.text = text + new string('.', dots);
            }
        }
        if (spinner != null) spinner.Rotate(0f, 0f, -spinSpeed * Time.unscaledDeltaTime);
    }
}