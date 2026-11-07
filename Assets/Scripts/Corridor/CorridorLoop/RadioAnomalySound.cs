
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class RadioAnomalySound : MonoBehaviour
{
    AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 1f;
    }

    void OnEnable()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource.clip != null)
            audioSource.Play();
    }

    void OnDisable()
    {
        if (audioSource != null)
            audioSource.Stop();
    }
}
