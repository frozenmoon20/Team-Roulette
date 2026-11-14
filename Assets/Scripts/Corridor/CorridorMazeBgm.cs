
using System.Collections;
using UnityEngine;

public class CorridorMazeBgm : MonoBehaviour
{
    [Header("미로 배경음악")]
    public AudioSource audioSource;
    public AudioClip bgmClip;

    [Range(0f, 1f)]
    public float volume = 0.35f;

    [Min(0f)]
    public float fadeOutDuration = 2f;

    [Header("재생 설정")]
    [Tooltip("씬 시작부터 미로 BGM을 재생합니다.")]
    public bool playOnStart = true;

    private Coroutine fadeRoutine;
    private bool exitRequested;
    private bool started;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        OptionState.OnOptionChanged += OnOptionChanged;
    }

    private void OnDisable()
    {
        OptionState.OnOptionChanged -= OnOptionChanged;
    }

    private void Start()
    {
        if (playOnStart)
            PlayMazeBgm();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (playOnStart || started || exitRequested)
            return;

        FirstPersonPlayer player =
            other.GetComponentInParent<FirstPersonPlayer>();

        if (player != null)
            PlayMazeBgm();
    }

    private bool BgmUnlocked()
    {
        return OptionState.Instance == null ||
               OptionState.Instance.IsUnlocked(OptionType.BGM);
    }

    private void OnOptionChanged(
        OptionType type, bool unlocked)
    {
        if (type != OptionType.BGM || audioSource == null)
            return;

        if (!unlocked)
        {
            audioSource.Stop();
            return;
        }

        if (started && !exitRequested)
            PlayConfiguredAudio();
    }

    public void PlayMazeBgm()
    {
        if (exitRequested || started)
            return;

        started = true;
        PlayConfiguredAudio();
    }

    private void PlayConfiguredAudio()
    {
        if (audioSource == null ||
            bgmClip == null ||
            !BgmUnlocked() ||
            exitRequested)
            return;

        audioSource.clip = bgmClip;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = volume;

        if (!audioSource.isPlaying)
            audioSource.Play();
    }

    public void FadeOutAndStop()
    {
        if (exitRequested)
            return;

        exitRequested = true;

        if (audioSource == null)
            return;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeOutRoutine()
    {
        float startVolume = audioSource.volume;
        float duration = Mathf.Max(0.01f, fadeOutDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            audioSource.volume =
                Mathf.Lerp(startVolume, 0f, t);

            yield return null;
        }

        audioSource.volume = 0f;
        audioSource.Stop();
        fadeRoutine = null;
    }
}
