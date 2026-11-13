using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    private AudioSource audioSource;
    readonly Dictionary<AudioSource, Coroutine> sceneFades = new Dictionary<AudioSource, Coroutine>();

    // Scene-owned looping sources retain the shared SFX mixer route without making music loop.
    public void PlaySceneLoop(AudioSource source, AudioClip clip, float volume, float fadeSeconds)
    {
        if (!source || !clip || !source.gameObject.activeInHierarchy) return;
        StopSceneLoop(source);
        source.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
        source.playOnAwake = false;
        source.clip = clip;
        source.loop = true;
        source.volume = 0;
        source.Play();
        sceneFades[source] = StartCoroutine(FadeSceneLoop(source, Mathf.Clamp01(volume), fadeSeconds));
    }

    IEnumerator FadeSceneLoop(AudioSource source, float volume, float seconds)
    {
        float elapsed = 0;
        while (source && source.gameObject.activeInHierarchy && elapsed < Mathf.Max(.01f, seconds))
        {
            elapsed += Time.deltaTime;
            source.volume = volume * Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / Mathf.Max(.01f, seconds)));
            yield return null;
        }
        if (source && source.gameObject.activeInHierarchy) source.volume = volume;
        if (source) sceneFades.Remove(source);
    }

    public void StopSceneLoop(AudioSource source)
    {
        if (!source) return;
        if (sceneFades.TryGetValue(source, out var fade)) { StopCoroutine(fade); sceneFades.Remove(source); }
        source.Stop();
    }

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

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        audioSource.PlayOneShot(clip);
    }
}
    
