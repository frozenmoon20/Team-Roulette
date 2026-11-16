
using System.Collections;
using UnityEngine;

public class MazeFogController : MonoBehaviour
{
    [SerializeField] private float fadeDuration = 2f;
    [SerializeField] private float clearDistance = 1000f;

    private Coroutine fadeCoroutine;

    public void ClearFog()
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeOutFog());
    }

    private IEnumerator FadeOutFog()
    {
        if (!RenderSettings.fog)
            yield break;

        float initialStart = RenderSettings.fogStartDistance;
        float initialEnd = RenderSettings.fogEndDistance;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            t = t * t * (3f - 2f * t);

            RenderSettings.fogEndDistance =
                Mathf.Lerp(initialEnd, clearDistance, t);

            yield return null;
        }

        RenderSettings.fog = false;
        RenderSettings.fogStartDistance = initialStart;
        RenderSettings.fogEndDistance = initialEnd;
        fadeCoroutine = null;
    }
}
