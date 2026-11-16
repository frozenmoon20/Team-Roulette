using System.Collections;
using UnityEngine;
using TMPro;

// 화면 위쪽에 잠깐 뜨는 조작 안내 자막 (미니게임 조작법 등)
// Show("문구"): 스르륵 나타남. 이미 다른 문구가 떠 있으면 먼저 스르륵 사라진 뒤 새 문구가 나타남 (문구 바꾸기)
//               autoHideTime이 0보다 크면 그 시간 뒤 저절로 사라짐 (0이면 Hide()할 때까지 유지)
// Hide(): 스르륵 사라짐
// 사용: 캔버스 안에 TextMeshPro 글자를 하나 만들고 이 스크립트를 같은 오브젝트에 붙인다.
//       이벤트에서 Show(string)에 문구를 직접 입력해서 연결한다.
[RequireComponent(typeof(TMP_Text))]
public class ControlHint : MonoBehaviour
{
    public float fadeTime = 1f;              // 나타나고 사라지는 데 걸리는 시간 (초). 클수록 천천히
    public float autoHideTime = 0f;          // 0보다 크면 이 시간(초) 뒤 저절로 사라짐

    TMP_Text text;
    Coroutine running;

    void Awake()
    {
        text = GetComponent<TMP_Text>();
        text.alpha = 0f;                     // 처음엔 안 보임
    }

    public void Show(string message)
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(ShowRoutine(message));
    }

    public void Hide()
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Fade(0f));
    }

    IEnumerator ShowRoutine(string message)
    {
        // 다른 문구가 떠 있으면 먼저 사라지게 한 뒤 바꿈 (글자가 확 바뀌지 않게)
        if (text.alpha > 0.01f && text.text != message) yield return Fade(0f);

        text.text = message;
        yield return Fade(1f);

        if (autoHideTime > 0f)
        {
            yield return new WaitForSeconds(autoHideTime);
            yield return Fade(0f);
        }
    }

    IEnumerator Fade(float target)
    {
        float start = text.alpha;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / fadeTime)
        {
            text.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        text.alpha = target;
    }
}