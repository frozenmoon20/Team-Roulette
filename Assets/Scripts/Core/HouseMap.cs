using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 해금 현황 (작은 집 지도). Tab을 누르면 화면 왼쪽 위에 펼쳐지고, 다시 누르면 접힌다.
// 계산은 HouseProgress가 하고, 이 스크립트는 그리기만 한다.
//   2층: [침실] [복도]   ↓ 계단   1층: [거실]
// 공간 하나의 모습:
//   - 아직 모름: 어두운 칸에 "?"
//   - 진행 중: 어두운 칸 + 진행한 만큼 아래에서부터 따뜻한 색 게이지가 차오름
//              + 아래에 작은 점(●)이 칸 수만큼 (켜진 점 = 흰색, 꺼진 점 = 어두운 회색)
//   - 완료: 칸 전체에 따뜻한 색이 돌아옴 (완료되는 순간 한 번 반짝임)
//   - 지금 있는 공간: 오른쪽 위에 흰 점
// 언제 쓸 수 있나: Show After 기능(기본 = 카메라 회전)이 해금된 뒤부터 Tab이 먹는다 (미니게임2가 끝나고 둘러볼 수 있게 된 뒤).
//   해금되면, 카메라 조작 안내 자막이 다 사라지고 Hint Delay초 뒤에 같은 자막 자리(ControlHint)에 "Tab : 해금 현황" 안내를 한 번 띄운다.
//   씬에 ControlHint가 여러 개(MG3_Hint, MG5_Hint, UnlockMessage …) 있어서, 이름(Hint Object Name)으로 정확히 그 하나를 찾는다.
//   처음으로 Tab을 눌러 펼치면 그 안내는 바로 사라진다 (안내를 보고 → 눌러 보고 → "아 이거구나").
// 원리: Play가 시작되면 Canvas와 칸들을 코드로 직접 만든다 (UI를 손으로 배치할 필요 없음).
//       해금이 바뀌거나(OptionState) 거실 단계가 끝나거나(HouseProgress) 씬이 바뀔 때마다 다시 칠한다.
//       펼치기/접기는 Canvas 전체의 투명도(CanvasGroup.alpha)를 목표값으로 천천히 바꾸는 것.
//       게이지는 "목표 높이"만 정해 두고, 지도가 펼쳐져 있을 때만 그 높이까지 천천히 따라 올라간다
//       (접혀 있는 동안 해금하면, 다음에 펼쳤을 때 차오르는 걸 볼 수 있다).
// 사용: _Systems 프리팹(씬이 바뀌어도 살아남음)을 열어 이 스크립트를 붙이고, Font 칸에 NotoSansKR-Regular SDF를 넣는다.
public class HouseMap : MonoBehaviour
{
    [Header("글꼴 (한글 SDF 폰트)")]
    public TMP_FontAsset font;

    [Header("펼치기 키")]
    public KeyCode toggleKey = KeyCode.Tab;
    public bool holdToShow = false;      // 체크하면 "누르고 있는 동안만" 펼침, 끄면 누를 때마다 펼침/접힘
    public float fadeTime = 0.2f;        // 펼쳐지고 접히는 시간 (초)

    [Header("이 기능이 해금된 뒤부터 Tab 사용 가능")]
    public OptionType showAfter = OptionType.RotateCamera;

    [Header("조작 안내 자막 (ControlHint, 한 번만)")]
    public string hintObjectName = "ControlHint";   // 카메라 조작 안내가 뜨는 ControlHint 오브젝트 이름 (Hierarchy 이름 그대로)
    public string hintText = "Tab : 해금 현황 보기";
    public float hintDelay = 5f;         // 카메라 조작 안내가 사라지고 몇 초 뒤에 띄울지

    [Header("위치·크기 (화면 왼쪽 위 기준, 1920x1080 화면 기준 픽셀)")]
    public Vector2 margin = new Vector2(30f, 30f);
    public float width = 300f;
    public float roomHeight = 86f;

    [Header("색")]
    public Color panelColor = new Color(0.04f, 0.04f, 0.05f, 0.78f);
    public Color warm = new Color(0.91f, 0.76f, 0.42f, 1f);          // 완료된 칸
    public Color gaugeColor = new Color(0.91f, 0.76f, 0.42f, 0.85f); // 차오르는 게이지
    public Color roomDark = new Color(0.11f, 0.11f, 0.12f, 1f);      // 진행 중인 칸 바탕
    public Color unknownColor = new Color(0.07f, 0.07f, 0.08f, 1f);  // 모르는 칸
    public Color pipOn = Color.white;                                // 켜진 점
    public Color pipOff = new Color(0.32f, 0.32f, 0.35f, 1f);        // 꺼진 점

    [Header("게이지가 차오르는 속도 (1 = 1초에 칸 전체)")]
    public float gaugeSpeed = 0.8f;

    [Header("지금 있는 공간 판단 (씬 이름)")]
    public string bedroomScene = "Test_NightRoom";   // 이 씬에서 달리기가 해금됐으면(문 밖 일자 복도) 복도로 봄
    public string hallwayScene = "Corridor";
    public string livingScenePrefix = "Room3";

    class Room
    {
        public HouseProgress.Area area;
        public RectTransform rt;
        public Image bg, gauge, labelBack, pipBack, here;
        public RectTransform gaugeRt;
        public TextMeshProUGUI label;
        public Image[] pips;
        public float shown, target;   // 지금 보이는 게이지 높이 / 목표 높이 (0~1)
        public bool wasComplete;
    }

    Room bedroom, hallway, living;
    TextMeshProUGUI headerLeft, headerRight;
    CanvasGroup group;
    Sprite circle;
    bool built, open;
    ControlHint hintShownOn;             // 안내를 띄운 ControlHint (처음 펼칠 때 지우려고)
    static bool hintShown;               // 게임 한 판에 한 번만 (씬이 바뀌어도 유지)

    void Start()
    {
        Build();
        Refresh(false);
    }

    void OnEnable()
    {
        OptionState.OnOptionChanged += OnOption;
        HouseProgress.OnChanged += OnProgress;
        SceneManager.sceneLoaded += OnScene;
    }

    void OnDisable()
    {
        OptionState.OnOptionChanged -= OnOption;
        HouseProgress.OnChanged -= OnProgress;
        SceneManager.sceneLoaded -= OnScene;
    }

    void OnOption(OptionType t, bool on)
    {
        Refresh(true);
        if (t == showAfter && on && !hintShown)
        {
            hintShown = true;
            StartCoroutine(ShowHint());
        }
    }

    void OnProgress() { Refresh(true); }
    void OnScene(Scene s, LoadSceneMode m) { Refresh(false); }

    void Update()
    {
        if (!built) return;

        // 1) Tab 입력 (해금 전엔 무시)
        bool usable = OptionState.Instance != null && OptionState.Instance.IsUnlocked(showAfter);
        if (!usable) open = false;
        else if (holdToShow) open = Input.GetKey(toggleKey);
        else if (Input.GetKeyDown(toggleKey)) open = !open;

        // 처음 펼치면 "Tab : 해금 현황" 안내는 할 일을 다 했으니 지움
        if (open && hintShownOn != null)
        {
            hintShownOn.Hide();
            hintShownOn = null;
        }

        // 2) 펼치기/접기 (투명도를 목표값으로 천천히)
        group.alpha = Mathf.MoveTowards(group.alpha, open ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(fadeTime, 0.01f));

        // 3) 게이지는 펼쳐져 있을 때만 차오름
        if (group.alpha > 0.5f)
        {
            StepGauge(bedroom);
            StepGauge(hallway);
            StepGauge(living);
        }
    }

    // 해금 직후 한 번: 카메라 조작 안내가 끝난 뒤 같은 자막 자리에 "Tab : 해금 현황 보기"
    IEnumerator ShowHint()
    {
        ControlHint hint = FindHint();
        if (hint == null)
        {
            Debug.LogWarning("HouseMap: 이름이 '" + hintObjectName + "'인 ControlHint를 못 찾아서 Tab 안내를 띄우지 않음");
            yield break;
        }
        TMP_Text text = hint.GetComponent<TMP_Text>();

        // 1) 카메라 조작 안내가 나타나기를 잠깐 기다림 (같은 순간에 시작되므로 최대 2초)
        for (float t = 0f; t < 2f && text.alpha < 0.01f; t += Time.unscaledDeltaTime) yield return null;
        // 2) 그 안내가 완전히 사라질 때까지 기다림
        while (text.alpha > 0.01f) yield return null;
        // 3) 그리고 몇 초 더 쉼
        yield return new WaitForSecondsRealtime(hintDelay);

        if (open) yield break;                                       // 이미 눌러 봤으면 안내할 필요 없음
        hint.Show(hintText);
        hintShownOn = hint;
    }

    ControlHint FindHint()
    {
        foreach (ControlHint h in FindObjectsByType<ControlHint>(FindObjectsSortMode.None))
            if (h.gameObject.name == hintObjectName) return h;
        return null;
    }

    // ===================== 만들기 =====================

    void Build()
    {
        if (built) return;
        built = true;
        circle = MakeCircleSprite();

        GameObject canvasGo = new GameObject("HouseMapCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;                                    // 일반 UI 위, 장면 전환(SceneFader) 아래
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        group = canvasGo.GetComponent<CanvasGroup>();
        group.alpha = 0f;                                             // 처음엔 접힘
        group.interactable = group.blocksRaycasts = false;

        // 바탕 판
        float pad = 14f, gap = 6f, header = 22f, floorLabel = 16f, stair = 20f;
        float h = pad + header + floorLabel + roomHeight + stair + floorLabel + roomHeight + pad;
        RectTransform panel = MakeRect("Panel", canvasGo.transform, new Vector2(margin.x, -margin.y), new Vector2(width, h));
        MakeImage(panel, panelColor);

        float inner = width - pad * 2f;
        float y = -pad;
        headerLeft = MakeText("Title", panel, new Vector2(pad, y), new Vector2(inner, header), 15f, TextAlignmentOptions.Left, Color.white);
        headerRight = MakeText("Count", panel, new Vector2(pad, y), new Vector2(inner, header), 13f, TextAlignmentOptions.Right, new Color(0.75f, 0.73f, 0.68f));
        y -= header;
        MakeText("Floor2", panel, new Vector2(pad, y), new Vector2(inner, floorLabel), 10f, TextAlignmentOptions.Left, new Color(0.5f, 0.5f, 0.53f)).text = "2층";
        y -= floorLabel;

        float bedW = (inner - gap) * 0.56f;
        float hallW = inner - gap - bedW;
        bedroom = MakeRoom(HouseProgress.Area.Bedroom, "침실", panel, new Vector2(pad, y), new Vector2(bedW, roomHeight));
        hallway = MakeRoom(HouseProgress.Area.Hallway, "복도", panel, new Vector2(pad + bedW + gap, y), new Vector2(hallW, roomHeight));
        y -= roomHeight;
        MakeText("Stair", panel, new Vector2(pad, y), new Vector2(inner - 10f, stair), 11f, TextAlignmentOptions.Right, new Color(0.5f, 0.5f, 0.53f)).text = "↓ 계단";
        y -= stair;
        MakeText("Floor1", panel, new Vector2(pad, y), new Vector2(inner, floorLabel), 10f, TextAlignmentOptions.Left, new Color(0.5f, 0.5f, 0.53f)).text = "1층";
        y -= floorLabel;
        living = MakeRoom(HouseProgress.Area.Living, "거실", panel, new Vector2(pad, y), new Vector2(inner, roomHeight));
    }

    Room MakeRoom(HouseProgress.Area area, string name, RectTransform parent, Vector2 pos, Vector2 size)
    {
        Room r = new Room { area = area };
        r.rt = MakeRect(name, parent, pos, size);
        r.bg = MakeImage(r.rt, roomDark);

        // 아래에서부터 차오르는 게이지 (아래 끝에 붙어 있고, 위쪽 끝(anchorMax.y)만 0~1로 바뀜)
        RectTransform g = MakeRect("Gauge", r.rt, Vector2.zero, Vector2.zero);
        g.anchorMin = new Vector2(0f, 0f);
        g.anchorMax = new Vector2(1f, 0f);
        g.offsetMin = g.offsetMax = Vector2.zero;
        r.gaugeRt = g;
        r.gauge = MakeImage(g, gaugeColor);

        // 이름 (게이지 위에서도 읽히게 어두운 받침)
        RectTransform lb = MakeRect("LabelBack", r.rt, new Vector2(size.x * 0.5f - 28f, -size.y * 0.5f + 20f), new Vector2(56f, 24f));
        r.labelBack = MakeImage(lb, new Color(0.05f, 0.05f, 0.06f, 0.85f));
        r.label = MakeText("Label", r.rt, new Vector2(0f, -size.y * 0.5f + 20f), new Vector2(size.x, 24f), 15f, TextAlignmentOptions.Center, Color.white);
        r.label.text = name;

        // 아래쪽 점 (칸 수만큼) + 어두운 받침
        int total = HouseProgress.Total(area);
        r.pips = new Image[total];
        float pip = 8f, pipGap = 5f;
        float rowW = total * pip + (total - 1) * pipGap;
        float pipY = -size.y + 9f + pip;
        RectTransform pb = MakeRect("PipBack", r.rt, new Vector2(size.x * 0.5f - rowW * 0.5f - 6f, pipY + 4f), new Vector2(rowW + 12f, pip + 8f));
        r.pipBack = MakeImage(pb, new Color(0.05f, 0.05f, 0.06f, 0.85f));
        for (int i = 0; i < total; i++)
        {
            RectTransform p = MakeRect("Pip" + i, r.rt, new Vector2(size.x * 0.5f - rowW * 0.5f + i * (pip + pipGap), pipY), new Vector2(pip, pip));
            r.pips[i] = MakeImage(p, pipOff);
            r.pips[i].sprite = circle;
        }

        // 지금 있는 곳 (흰 점)
        RectTransform hr = MakeRect("Here", r.rt, new Vector2(size.x - 16f, -6f), new Vector2(10f, 10f));
        r.here = MakeImage(hr, Color.white);
        r.here.sprite = circle;
        return r;
    }

    // ===================== 칠하기 =====================

    void Refresh(bool animate)
    {
        if (!built) return;
        HouseProgress.Area here = CurrentArea();
        bool showHall = HouseProgress.IsComplete(HouseProgress.Area.Bedroom) || here != HouseProgress.Area.Bedroom || HouseProgress.Done(HouseProgress.Area.Hallway) > 0;
        bool showLiving = HouseProgress.IsComplete(HouseProgress.Area.Hallway) || here == HouseProgress.Area.Living || HouseProgress.Done(HouseProgress.Area.Living) > 0;

        Paint(bedroom, true, here, animate);
        Paint(hallway, showHall, here, animate);
        Paint(living, showLiving, here, animate);

        bool all = HouseProgress.AllDone >= HouseProgress.AllTotal;
        headerLeft.text = all ? "집" : AreaName(here);
        headerRight.text = all ? "모두 되찾음" : HouseProgress.Done(here) + " / " + HouseProgress.Total(here);
    }

    void Paint(Room r, bool known, HouseProgress.Area here, bool animate)
    {
        int done = HouseProgress.Done(r.area);
        int total = HouseProgress.Total(r.area);
        bool complete = done >= total;

        r.here.enabled = known && r.area == here;
        r.label.text = known ? AreaName(r.area) : "?";

        if (!known)
        {
            r.bg.color = unknownColor;
            r.gauge.enabled = false;
            r.labelBack.enabled = r.pipBack.enabled = false;
            r.label.color = new Color(0.32f, 0.32f, 0.35f);
            foreach (Image p in r.pips) p.enabled = false;
            r.shown = r.target = 0f;
            return;
        }

        foreach (Image p in r.pips) p.enabled = true;
        r.pipBack.enabled = true;
        r.target = total > 0 ? (float)done / total : 0f;
        if (!animate) r.shown = r.target;     // 씬 시작·이동 때는 바로 그 높이로

        if (complete)
        {
            r.bg.color = warm;
            r.gauge.enabled = false;
            r.labelBack.enabled = false;
            r.label.color = new Color(0.16f, 0.14f, 0.09f);
            r.pipBack.color = new Color(0.16f, 0.14f, 0.09f, 0.25f);
            foreach (Image p in r.pips) p.color = new Color(0.16f, 0.14f, 0.09f);
            if (animate && !r.wasComplete) StartCoroutine(Flash(r));
        }
        else
        {
            r.bg.color = roomDark;
            r.gauge.enabled = true;
            r.labelBack.enabled = true;
            r.label.color = Color.white;
            r.pipBack.color = new Color(0.05f, 0.05f, 0.06f, 0.85f);
            for (int i = 0; i < r.pips.Length; i++)
                r.pips[i].color = i < done ? pipOn : pipOff;
            SetGauge(r, r.shown);
        }
        r.wasComplete = complete;
    }

    // 게이지를 목표 높이까지 천천히 따라 올림
    void StepGauge(Room r)
    {
        if (!r.gauge.enabled || Mathf.Approximately(r.shown, r.target)) return;
        r.shown = Mathf.MoveTowards(r.shown, r.target, gaugeSpeed * Time.unscaledDeltaTime);
        SetGauge(r, r.shown);
    }

    void SetGauge(Room r, float t)
    {
        r.gaugeRt.anchorMax = new Vector2(1f, Mathf.Clamp01(t));
        r.gaugeRt.offsetMax = Vector2.zero;
    }

    // 공간을 다 되찾는 순간: 칸이 잠깐 하얗게 빛났다가 따뜻한 색으로
    IEnumerator Flash(Room r)
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 1.2f)
        {
            r.bg.color = Color.Lerp(Color.white, warm, t);
            r.rt.localScale = Vector3.one * Mathf.Lerp(1.08f, 1f, t);
            yield return null;
        }
        r.bg.color = warm;
        r.rt.localScale = Vector3.one;
    }

    HouseProgress.Area CurrentArea()
    {
        string s = SceneManager.GetActiveScene().name;
        if (s.StartsWith(livingScenePrefix)) return HouseProgress.Area.Living;
        if (s == hallwayScene) return HouseProgress.Area.Hallway;
        if (s == bedroomScene && OptionState.Instance != null && OptionState.Instance.IsUnlocked(OptionType.Run))
            return HouseProgress.Area.Hallway;                       // 침실 문 밖 일자 복도
        return HouseProgress.Area.Bedroom;
    }

    static string AreaName(HouseProgress.Area a)
    {
        switch (a)
        {
            case HouseProgress.Area.Bedroom: return "침실";
            case HouseProgress.Area.Hallway: return "복도";
            default: return "거실";
        }
    }

    // ===================== 도우미 =====================

    // 왼쪽 위 기준 위치(pos.x 오른쪽, pos.y 아래로 음수)와 크기로 사각형 만들기
    RectTransform MakeRect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        GameObject g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        RectTransform rt = g.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    Image MakeImage(RectTransform rt, Color color)
    {
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    TextMeshProUGUI MakeText(string name, RectTransform parent, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions align, Color color)
    {
        RectTransform rt = MakeRect(name, parent, pos, size);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    static Sprite MakeCircleSprite()
    {
        const int n = 32;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2((n - 1) * 0.5f, (n - 1) * 0.5f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float a = Mathf.Clamp01(n * 0.5f - Vector2.Distance(new Vector2(x, y), c));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }
}