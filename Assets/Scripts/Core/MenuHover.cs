using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 타이틀 메뉴 버튼 효과. 각 버튼(NewGameButton, LoadGameButton, ExitButton)에 하나씩 붙인다. (글자 오브젝트 말고 버튼에)
// - 글자는 왼쪽 정렬, 글자 왼쪽에 얇은 막대(|)가 항상 있음 → 버튼들의 막대가 한 줄로 세로로 맞춰짐
// - 마우스를 올리면: 글자+막대가 같이 왼쪽으로 살짝 밀리고, 조금 커지고, 밝아짐
// - 누르고 있으면: 글자+막대가 오른쪽으로 살짝 이동 → 떼면 원래 자리로
// 원리: 마우스가 올라왔는지 / 누르고 있는지만 기억해 두고(OnPointerEnter/Exit/Down/Up),
//       매 프레임 "지금 값 → 목표 값"으로 조금씩 다가가게 해서 부드럽게 바뀐다.
//       막대는 글자의 자식으로 자동으로 만들어서, 글자와 같이 움직이고 커진다.
// 준비 (한 줄로 맞추려면): 버튼 3개의 Pos X와 Width를 같게, 글자 오브젝트는 버튼을 꽉 채우게(Rect Transform을 stretch로)
public class MenuHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Tooltip("움직일 글자 (비워 두면 버튼 안의 글자를 자동으로 찾음)")]
    public Graphic label;
    [Tooltip("글자를 왼쪽 정렬로 바꿈 (막대가 버튼끼리 세로 한 줄로 맞춰짐)")]
    public bool alignLeft = true;

    [Header("마우스를 올렸을 때")]
    public float moveX = -10f;                       // 밀리는 거리 (음수 = 왼쪽)
    public float scale = 1.08f;                      // 커지는 정도 (1 = 그대로)
    [Range(0f, 1f)] public float normalAlpha = 0.6f; // 평소 밝기 (1 = 원래 색 그대로)
    public float speed = 10f;                        // 바뀌는 빠르기

    [Header("누르고 있을 때")]
    public float pressX = 14f;                       // 누르면 오른쪽으로 이동하는 거리 (호버 위치 기준)
    public float pressSpeed = 25f;                   // 누를 때는 더 빠르게

    [Header("왼쪽 막대")]
    [Tooltip("해제하면 막대를 만들지 않음 (확인창 버튼 등)")]
    public bool showBar = true;
    public float barGap = 12f;                       // 글자 왼쪽 끝과 막대 사이 거리
    public float barWidth = 3f;
    [Range(0.1f, 1.5f)] public float barHeight = 0.7f;   // 글자 높이 대비 막대 높이

    [Header("소리 (선택)")]
    public AudioClip hoverClip;                      // 마우스를 올렸을 때
    [Range(0f, 1f)] public float hoverVolume = 0.5f;
    public AudioClip clickClip;                      // 눌렀을 때 (딸깍)
    [Range(0f, 1f)] public float clickVolume = 0.8f;
    [Tooltip("체크: 저장에 효과음(SFX)이 해금돼 있을 때만 소리가 남 (저장이 없거나 해금 전이면 무음)")]
    public bool needSfxUnlock = true;

    RectTransform rect;
    Vector2 basePos;
    Color baseColor;
    Image bar;
    AudioSource source;
    bool hovered, pressed;
    float t;                                         // 0 = 평소, 1 = 호버
    float p;                                         // 0 = 안 누름, 1 = 누름

    void Awake()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        if (label == null) label = GetComponentInChildren<Text>(true);
        if (label == null) { enabled = false; return; }

        if (alignLeft)
        {
            if (label is TMP_Text tmp) tmp.alignment = TextAlignmentOptions.MidlineLeft;
            else if (label is Text txt) txt.alignment = TextAnchor.MiddleLeft;
        }

        rect = label.rectTransform;
        basePos = rect.anchoredPosition;
        baseColor = label.color;
        if (showBar) MakeBar();
        Apply();
    }

    void OnDisable()
    {
        hovered = pressed = false;
        t = p = 0f;
        if (rect != null) Apply();
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (!Interactable()) return;
        hovered = true;
        PlaySound(hoverClip, hoverVolume);
    }

    // 효과음 (버튼에 AudioSource를 자동으로 붙여서 재생)
    void PlaySound(AudioClip clip, float volume)
    {
        if (clip == null) return;
        if (needSfxUnlock && !SaveSystem.SavedUnlocked(OptionType.SFX)) return;   // 아직 효과음을 되찾지 못한 저장이면 무음
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }
        source.PlayOneShot(clip, volume);
    }

    public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData e)
    {
        if (!Interactable()) return;
        pressed = true;
        PlaySound(clickClip, clickVolume);       // 누르는 순간 딸깍
    }
    public void OnPointerUp(PointerEventData e) { pressed = false; }

    void Update()
    {
        bool on = Interactable();
        float tt = hovered && on ? 1f : 0f;
        float pt = pressed && on ? 1f : 0f;
        if (Mathf.Approximately(t, tt) && Mathf.Approximately(p, pt)) return;
        t = Mathf.MoveTowards(t, tt, speed * Time.unscaledDeltaTime);       // 게임이 멈춰 있어도 동작
        p = Mathf.MoveTowards(p, pt, pressSpeed * Time.unscaledDeltaTime);
        Apply();
    }

    void Apply()
    {
        float k = Mathf.SmoothStep(0f, 1f, t);
        float q = Mathf.SmoothStep(0f, 1f, p);
        rect.anchoredPosition = basePos + new Vector2(moveX * k + pressX * q, 0f);
        rect.localScale = Vector3.one * Mathf.Lerp(1f, scale, k);

        Color c = baseColor;
        c.a = baseColor.a * Mathf.Lerp(normalAlpha, 1f, k);
        label.color = c;
        if (bar != null) bar.color = c;              // 막대도 글자와 같은 밝기
    }

    // 버튼이 눌린 뒤(타이틀 UI가 사라지는 중)에는 반응하지 않음
    bool Interactable()
    {
        Selectable s = GetComponent<Selectable>();
        return s == null || s.IsInteractable();
    }

    // 글자 왼쪽에 얇은 막대를 글자의 자식으로 만든다 (글자와 같이 움직이고 커짐)
    void MakeBar()
    {
        Canvas.ForceUpdateCanvases();
        float h = LayoutUtility.GetPreferredHeight(rect);
        if (h <= 0f) h = rect.rect.height;

        // 막대 x 위치 (글자 칸 기준): 왼쪽 정렬이면 칸의 왼쪽 끝, 아니면 글자 실제 폭의 왼쪽 끝
        float left;
        if (alignLeft) left = rect.rect.xMin;
        else
        {
            float w = LayoutUtility.GetPreferredWidth(rect);
            if (w <= 0f) w = rect.rect.width;
            left = rect.rect.center.x - w * 0.5f;
        }

        GameObject go = new GameObject("HoverBar", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(rect, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(1f, 0.5f);
        r.sizeDelta = new Vector2(barWidth, h * barHeight);
        r.localPosition = new Vector3(left - barGap, rect.rect.center.y, 0f);
        bar = go.GetComponent<Image>();
        bar.raycastTarget = false;                   // 막대가 마우스를 가로채지 않게
    }
}