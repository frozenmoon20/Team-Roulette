using System.Collections;
using UnityEngine;

namespace TeamRoulette.CorridorEnding
{
    public class EndingPhoneRing : MonoBehaviour
    {
        public static EndingPhoneRing Instance { get; private set; }

        private AudioSource source;
        private AudioLowPassFilter filter;
        private Coroutine fadeRoutine;

        public static void Begin(AudioClip clip, float initialVolume, float finalVolume, float duration, float initialCutoff)
        {
            if (clip == null)
            {
                Debug.LogError("엔딩 전화벨 AudioClip이 연결되지 않았습니다.");
                return;
            }

            if (Instance != null)
                return;

            GameObject root = new GameObject("EndingPhoneRing_Persistent");
            DontDestroyOnLoad(root);

            EndingPhoneRing bridge = root.AddComponent<EndingPhoneRing>();
            bridge.source = root.AddComponent<AudioSource>();
            bridge.source.playOnAwake = false;
            bridge.source.spatialBlend = 0f;
            bridge.source.loop = true;
            bridge.source.ignoreListenerPause = true;
            bridge.source.clip = clip;
            bridge.source.volume = Mathf.Clamp01(initialVolume);

            bridge.filter = root.AddComponent<AudioLowPassFilter>();
            bridge.filter.cutoffFrequency = Mathf.Clamp(initialCutoff, 10f, 22000f);

            bridge.source.Play();
            bridge.fadeRoutine = bridge.StartCoroutine(
                bridge.FadeIn(Mathf.Clamp01(finalVolume), duration)
            );
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private IEnumerator FadeIn(float targetVolume, float duration)
        {
            float startVolume = source.volume;
            float startCutoff = filter.cutoffFrequency;
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                source.volume = Mathf.Lerp(startVolume, targetVolume, t);
                filter.cutoffFrequency = Mathf.Lerp(startCutoff, 22000f, t);
                yield return null;
            }

            source.volume = targetVolume;
            filter.cutoffFrequency = 22000f;
            fadeRoutine = null;
        }

        public static void StopRinging(float duration)
        {
            if (Instance == null)
                return;

            if (Instance.fadeRoutine != null)
                Instance.StopCoroutine(Instance.fadeRoutine);

            Instance.fadeRoutine = Instance.StartCoroutine(Instance.FadeOut(duration));
        }

        private IEnumerator FadeOut(float duration)
        {
            float start = source.volume;
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(start, 0f, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            source.Stop();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
