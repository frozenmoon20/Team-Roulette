using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 씬 전환: 화면이 서서히 까매짐 → 다음 씬 불러오기 → 새 씬에서 까만 화면이 서서히 걷힘.
// 원리: 전환할 때만 화면 맨 앞에 검은 판(Canvas + Image)을 스스로 만들고, 씬이 바뀌어도 지워지지 않게(DontDestroyOnLoad) 해서
//       새 씬이 다 준비된 뒤에 걷어 낸다. 끝나면 스스로 사라진다. → 씬에 따로 준비해 둘 것이 없다.
// 사용 (어느 스크립트에서나):
//   SceneFader.Go("Corridor");                 // 까매졌다가 넘어가서 밝아짐
//   SceneFader.Go("Corridor", startBlack: true); // 이미 화면이 까만 상태(눈 감기 연출 등)에서 바로 넘어가서 밝아짐
//   SceneFader.Go("Room3_LivingRoom", color: Color.white); // 하얗게 넘어가기
//   yield return SceneFader.CoverIn(); ... SceneFader.CoverOut();  // 로딩은 직접 하고 화면만 덮었다 걷기
// 넘어갈 씬은 File > Build Profiles의 Scene List에 들어 있어야 한다.
public class SceneFader : MonoBehaviour
{
    static SceneFader running;               // 지금 전환 중이면 또 부르지 않게

    Image black;
    string sceneName;
    float fadeOut, fadeIn, hold;
    bool startBlack;
    Color color = Color.black;

    public static bool IsBusy => running != null;

    // ===== 씬 이름 없이 화면만 덮었다 걷기 (로딩을 직접 하는 쪽에서 씀, 예: GameUI의 타이틀로 나가기) =====
    // 사용: yield return SceneFader.CoverIn();   → 화면이 까매질 때까지 기다림
    //       (직접 씬 불러오기)
    //       SceneFader.CoverOut();               → 잠깐 뒤 스스로 밝아지고 사라짐 (부른 쪽이 사라져도 괜찮음)
    public static IEnumerator CoverIn(float time = 1f)
    {
        if (running != null) yield break;
        GameObject go = new GameObject("SceneFader");
        DontDestroyOnLoad(go);
        SceneFader f = go.AddComponent<SceneFader>();
        f.color = Color.black;
        f.startBlack = false;
        f.MakeBlack();
        running = f;
        yield return f.Fade(0f, 1f, time);
    }

    public static void CoverOut(float time = 1.2f, float hold = 0.3f)
    {
        if (running == null) return;
        running.StartCoroutine(running.Uncover(time, hold));
    }

    IEnumerator Uncover(float time, float hold)
    {
        yield return new WaitForSecondsRealtime(hold);
        yield return null;                   // 새 씬의 Start들이 한 번 돌고 나서
        yield return null;
        yield return Fade(1f, 0f, time);
        running = null;
        Destroy(gameObject);
    }

    public static void Go(string sceneName, float fadeOutTime = 1f, float fadeInTime = 1.2f, float holdBlack = 0.3f, bool startBlack = false, Color? color = null)
    {
        if (running != null || string.IsNullOrEmpty(sceneName)) return;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning("SceneFader: '" + sceneName + "' 씬을 불러올 수 없어요. 이름이 맞는지, File > Build Profiles의 Scene List에 들어 있는지 확인하세요.");
            return;
        }

        GameObject go = new GameObject("SceneFader");
        DontDestroyOnLoad(go);
        SceneFader f = go.AddComponent<SceneFader>();
        f.sceneName = sceneName;
        f.fadeOut = fadeOutTime;
        f.fadeIn = fadeInTime;
        f.hold = holdBlack;
        f.startBlack = startBlack;
        f.color = color ?? Color.black;
        f.MakeBlack();
        running = f;
        f.StartCoroutine(f.Run());
    }

    // 화면 맨 앞에 검은 판 만들기
    void MakeBlack()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;         // 다른 UI보다 항상 앞에
        GameObject img = new GameObject("Black");
        img.transform.SetParent(transform, false);
        black = img.AddComponent<Image>();
        black.color = new Color(color.r, color.g, color.b, startBlack ? 1f : 0f);
        black.raycastTarget = false;
        RectTransform rt = black.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    IEnumerator Run()
    {
        // 1) 까매짐
        if (!startBlack) yield return Fade(0f, 1f, fadeOut);
        SetAlpha(1f);
        yield return new WaitForSecondsRealtime(hold);

        // 2) 다음 씬 불러오기 (다 불러올 때까지 기다림)
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone) yield return null;
        yield return null;                   // 새 씬의 Start들이 한 번 돌고 나서
        yield return null;

        // 3) 밝아짐 → 스스로 사라짐
        yield return Fade(1f, 0f, fadeIn);
        running = null;
        Destroy(gameObject);
    }

    IEnumerator Fade(float from, float to, float time)
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(time, 0.01f))
        {
            SetAlpha(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)));
            yield return null;
        }
        SetAlpha(to);
    }

    void SetAlpha(float a)
    {
        Color c = black.color;
        c.a = a;
        black.color = c;
    }

    void OnDestroy()
    {
        if (running == this) running = null;
    }
}