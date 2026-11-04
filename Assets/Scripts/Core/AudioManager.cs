using UnityEngine;

// 효과음(한 번 나는 소리)을 대신 틀어 주는 공용 관리자. _Systems/AudioManager에 붙어 있고 씬이 바뀌어도 살아남는다.
// 원리:
//   - 효과음(SFX) 기능이 아직 해금 안 됐으면 소리를 내지 않는다 (게임 설정: 효과음도 되찾아야 하는 감각).
//   - 설정창의 효과음 볼륨 슬라이더가 이 오브젝트의 AudioSource 볼륨을 바꾼다 (GameUI가 처리).
//     PlayOneShot은 AudioSource 볼륨과 곱해지므로, 여기로 보낸 소리는 자동으로 슬라이더를 따른다.
// 사용 (어느 스크립트에서나):
//   AudioManager.Play(clip);          // 기본 크기
//   AudioManager.Play(clip, 0.5f);    // 절반 크기
//   AudioManager.Volume               // 지금 효과음이 나야 할 크기 (해금 전 0, 해금 후 슬라이더 값). 반복 소리(웅웅 등)에 곱해서 씀
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
}