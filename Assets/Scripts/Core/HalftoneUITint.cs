using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 테이프(목표창·대화창 배경)처럼 밝은 종이 UI를 하프톤에 맞춰 어둡게 → 밝게 바꾸는 부품.
// 왜: 게임 초반 화면은 거의 검은데 밝은 테이프만 튀어 보임. 해금 지도(메모지)와 같은 방식으로 맞춤.
// 원리:
//   - GameUI(지호 코드)는 시작할 때 UI_Tape 그림으로 테이프 Image를 만들고 색을 흰색으로 한 번 정한다.
//   - 이 부품은 지호 코드를 고치지 않고, 씬에서 "UI_Tape 그림을 쓰는 Image"를 찾아 색(RGB)만 바꾼다. (투명도는 그대로)
//   - 하프톤 값(0 = 가장 짙음, 1 = 다 걷힘)에 따라 Start Tint → End Tint 사이에서 섞는다.
//   - 테이프 위 글자가 흰색이라, End Tint를 완전 흰색으로 두면 후반에 글자가 안 보임 → 기본값은 살짝 어둡게 끝남.
// 사용: _Systems 프리팹에 붙이고, Textures 칸에 Assets/UI/UI_Tape를 넣는다.
public class HalftoneUITint : MonoBehaviour
{
    [Tooltip("이 그림을 쓰는 Image만 색을 바꿈 (예: UI_Tape)")]
    public Texture2D[] textures;
    public bool followHalftone = true;                              // 해제하면 늘 End Tint
    public Color startTint = new Color(0.36f, 0.35f, 0.34f, 1f);    // 하프톤 가장 짙을 때
    public Color endTint = new Color(0.62f, 0.61f, 0.59f, 1f);      // 하프톤 다 걷혔을 때 (흰 글자가 읽히게 너무 밝지 않게)
    public float rescanInterval = 1f;                               // 새로 생긴 테이프를 찾는 간격 (초)

    readonly List<Image> targets = new List<Image>();
    float nextScan;

    void OnEnable() { SceneManager.sceneLoaded += OnScene; }
    void OnDisable() { SceneManager.sceneLoaded -= OnScene; }
    void OnScene(Scene s, LoadSceneMode m) { nextScan = 0f; }

    void Update()
    {
        if (textures == null || textures.Length == 0) return;

        // 가끔 다시 찾기 (GameUI가 시작할 때 테이프를 만들고, 씬이 바뀌면 새 GameUI가 생김)
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + rescanInterval;
            Scan();
        }

        float t = followHalftone ? Mathf.Clamp01(HalftoneController.CurrentValue) : 1f;
        Color tint = Color.Lerp(startTint, endTint, t);
        for (int i = targets.Count - 1; i >= 0; i--)
        {
            Image img = targets[i];
            if (img == null) { targets.RemoveAt(i); continue; }
            Color c = img.color;
            if (Mathf.Approximately(c.r, tint.r) && Mathf.Approximately(c.g, tint.g) && Mathf.Approximately(c.b, tint.b)) continue;
            img.color = new Color(tint.r, tint.g, tint.b, c.a);     // 투명도는 원래 값 유지
        }
    }

    void Scan()
    {
        targets.Clear();
        foreach (Image img in FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (img.sprite == null) continue;
            Texture tex = img.sprite.texture;
            foreach (Texture2D want in textures)
                if (want != null && tex == want) { targets.Add(img); break; }
        }
    }
}