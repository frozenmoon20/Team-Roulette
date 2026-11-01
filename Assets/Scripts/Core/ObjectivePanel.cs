using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TeamRoulette.UI;

// 목표창 보이기 / 숨기기 (팀원의 GameUI 코드는 건드리지 않음)
// 원리: 목표창 오브젝트에 CanvasGroup을 하나 얹고 그 투명도(alpha)만 0↔1로 바꾼다.
//       GameUI가 안쪽에서 자기 CanvasGroup을 조절해도, 겹친 CanvasGroup의 투명도는 곱해지므로
//       여기서 0이면 GameUI가 뭘 하든 항상 안 보인다.
// 사용: Hierarchy의 GameUI 안에 있는 목표창 오브젝트(Objectives)에 붙인다.
//       Show() / Hide()를 이벤트에 연결한다. HideObjective("id")로 다 끝난 목표를 목록에서 뺄 수 있다.
// 진행 횟수 표시: 목표 Text에 {n}을 넣어 두면(예: "소리의 근원을 찾자 ({n}/3)") 처음엔 0으로 보이고,
//       AddProgress("id")를 부를 때마다 숫자가 1씩 올라간다.
public class ObjectivePanel : MonoBehaviour
{
    public bool hideOnStart = true;          // 게임 시작 때 숨겨 두기
    public float fadeTime = 0.6f;            // 나타나고 사라지는 데 걸리는 시간 (초)

    CanvasGroup group;
    SceneUIContent content;
    Coroutine fading;
    readonly Dictionary<string, string> baseTexts = new Dictionary<string, string>();   // {n}이 들어 있는 원래 문구
    readonly Dictionary<string, int> counts = new Dictionary<string, int>();            // 목표별 지금 횟수

    void Awake()
    {
        content = GetComponentInParent<SceneUIContent>();

        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();

        if (hideOnStart) group.alpha = 0f;

        // {n}이 들어 있는 목표는 원래 문구를 기억해 두고, 처음엔 0으로 표시
        if (content != null)
        {
            foreach (var item in content.objectives)
            {
                if (item == null || string.IsNullOrEmpty(item.text) || !item.text.Contains("{n}")) continue;
                baseTexts[item.id] = item.text;
                counts[item.id] = 0;
                item.text = item.text.Replace("{n}", "0");
            }
            content.Refresh();
        }
    }

    // 목표 진행 횟수 +1 (예: 개를 찾을 때마다). Text에 {n}이 있는 목표만 바뀜
    public void AddProgress(string id)
    {
        if (content == null || !baseTexts.ContainsKey(id)) return;
        counts[id]++;
        var item = content.objectives.Find(x => x != null && x.id == id);
        if (item == null) return;
        item.text = baseTexts[id].Replace("{n}", counts[id].ToString());
        content.Refresh();
    }

    // 목표창 나타나기
    public void Show() { FadeTo(1f); }

    // 목표창 숨기기
    public void Hide() { FadeTo(0f); }

    // 목표 하나를 목록에서 빼기 (다 끝난 꿈속 목표를 침실에서 안 보이게 할 때)
    public void HideObjective(string id)
    {
        if (content == null) return;
        var item = content.objectives.Find(x => x != null && x.id == id);
        if (item == null) return;
        item.visible = false;
        content.Refresh();
    }

    void FadeTo(float target)
    {
        if (fading != null) StopCoroutine(fading);
        fading = StartCoroutine(Fade(target));
    }

    IEnumerator Fade(float target)
    {
        float start = group.alpha;
        // 설정창(ESC)으로 게임이 멈춰 있어도 진행되도록 unscaledDeltaTime 사용
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / fadeTime)
        {
            group.alpha = Mathf.Lerp(start, target, t);
            yield return null;
        }
        group.alpha = target;
        fading = null;
    }
}