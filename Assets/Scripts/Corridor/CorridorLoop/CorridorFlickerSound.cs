
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class CorridorFlickerSound : MonoBehaviour
{
    [Header("전등 이상현상 효과음")]
    public AudioClip flickerSound;

    [Range(0f, 1f)]
    public float volume = 0.6f;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 1f;
    }

    private void OnEnable()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            return;

        audioSource.loop = true;
        audioSource.playOnAwake = false;
    }

    private void Update()
    {
        if (audioSource == null)
            return;

        bool canPlay =
            AudioManager.SfxUnlocked &&
            flickerSound != null;

        if (!canPlay)
        {
            if (audioSource.isPlaying)
                audioSource.Stop();

            return;
        }

        audioSource.volume =
            volume * AudioManager.Volume;

        if (audioSource.clip != flickerSound)
        {
            audioSource.Stop();
            audioSource.clip = flickerSound;
        }

        if (!audioSource.isPlaying)
            audioSource.Play();
    }

    private void OnDisable()
    {
        if (audioSource != null)
            audioSource.Stop();
    }
}
