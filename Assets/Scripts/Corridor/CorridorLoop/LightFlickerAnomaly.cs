
using System.Collections;
using UnityEngine;

public class LightFlickerAnomaly : MonoBehaviour
{
    public Light targetLight;

    public float minInterval = 0.05f;
    public float maxInterval = 0.35f;

    public float minIntensity = 0f;
    public float maxIntensity = 2f;

    float originalIntensity;
    Coroutine flickerRoutine;

    void Awake()
    {
        if (targetLight != null)
            originalIntensity = targetLight.intensity;
    }

    void OnEnable()
    {
        if (targetLight == null)
            return;

        if (flickerRoutine != null)
            StopCoroutine(flickerRoutine);

        flickerRoutine = StartCoroutine(Flicker());
    }

    void OnDisable()
    {
        if (flickerRoutine != null)
        {
            StopCoroutine(flickerRoutine);
            flickerRoutine = null;
        }

        if (targetLight != null)
            targetLight.intensity = originalIntensity;
    }

    IEnumerator Flicker()
    {
        while (true)
        {
            targetLight.intensity =
                Random.Range(minIntensity, maxIntensity);

            yield return new WaitForSeconds(
                Random.Range(minInterval, maxInterval)
            );
        }
    }
}
