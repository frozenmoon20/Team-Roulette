using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// 효과음(한 번 나는 소리)을 대신 틀어 주는 공용 관리자. _Systems/AudioManager에 붙어 있고 씬이 바뀌어도 살아남는다.
// 원리:
//   - 효과음(SFX) 기능이 아직 해금 안 됐으면 소리를 내지 않는다 (게임 설정: 효과음도 되찾아야 하는 감각).
//   - 설정창의 효과음 볼륨 슬라이더가 이 오브젝트의 AudioSource 볼륨을 바꾼다 (GameUI가 처리).
//     PlayOneShot은 AudioSource 볼륨과 곱해지므로, 여기로 보낸 소리는 자동으로 슬라이더를 따른다.
// 사용 (어느 스크립트에서나):
//   AudioManager.Play(clip);          // 기본 크기
//   AudioManager.Play(clip, 0.5f);    // 절반 크기
//   AudioManager.Volume               // 지금 효과음이 나야 할 크기 (해금 전 0, 해금 후 슬라이더 값). 반복 소리(웅웅 등)에 곱해서 씀
//   AudioManager.Instance.PlaySceneLoop(source, clip, volume, fadeSeconds)   // (거실) 씬의 반복 소리를 서서히 틀기
//   AudioManager.Instance.StopSceneLoop(source)                              // (거실) 그 반복 소리 멈추기
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    private AudioSource audioSource;
    readonly Dictionary<AudioSource, Coroutine> sceneFades = new Dictionary<AudioSource, Coroutine>();

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

    // 효과음이 해금됐는지 (OptionState가 없는 테스트 씬에서는 그냥 소리 남)
    public static bool SfxUnlocked => OptionState.Instance == null || OptionState.Instance.IsUnlocked(OptionType.SFX);

    // 지금 효과음 크기: 해금 전 0, 해금 후 설정창 효과음 슬라이더 값
    public static float Volume
    {
        get
        {
            if (!SfxUnlocked) return 0f;
            if (Instance != null && Instance.audioSource != null) return Instance.audioSource.volume;
            return 1f;
        }
    }

    // 어디서든 한 줄로 효과음 틀기: AudioManager.Play(clip);
    public static void Play(AudioClip clip, float volume = 1f)
    {
        if (Instance != null) Instance.PlaySFX(clip, volume);
    }

    // (예전 방식 그대로 유지) AudioManager.Instance.PlaySFX(clip);
    public void PlaySFX(AudioClip clip)
    {
        PlaySFX(clip, 1f);
    }

    public void PlaySFX(AudioClip clip, float volume)
    {
        if (clip == null || !SfxUnlocked) return;
        audioSource.PlayOneShot(clip, volume);
    }

    // ===== (거실 팀원 추가) 씬에 있는 반복 소리를 같은 효과음 경로로 서서히 틀고 멈추기 =====

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
}