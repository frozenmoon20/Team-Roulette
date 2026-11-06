
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    private AudioSource audioSource;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    public static bool SfxUnlocked =>
        OptionState.Instance == null ||
        OptionState.Instance.IsUnlocked(OptionType.SFX);

    public static float Volume
    {
        get
        {
            if (!SfxUnlocked)
                return 0f;

            if (Instance != null && Instance.audioSource != null)
                return Instance.audioSource.volume;

            return 1f;
        }
    }

    public static void Play(AudioClip clip, float volume = 1f)
    {
        if (Instance != null)
            Instance.PlaySFX(clip, volume);
    }

    public void PlaySFX(AudioClip clip)
    {
        PlaySFX(clip, 1f);
    }

    public void PlaySFX(AudioClip clip, float volume)
    {
        if (clip == null || !SfxUnlocked)
            return;

        audioSource.PlayOneShot(clip, volume);
    }
}
