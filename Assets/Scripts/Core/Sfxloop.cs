using UnityEngine;

// 계속 반복해서 나는 효과음 (컴퓨터 웅웅 소리, 냉장고 소리 등)을 물건에 붙인다.
// 원리:
//   - 같은 오브젝트의 AudioSource를 반복 재생하고, 매 프레임 볼륨 = 기본 볼륨 × 효과음 크기(AudioManager.Volume).
//   - 그래서 효과음이 해금되기 전엔 안 들리다가, 해금되는 순간부터 들리고, 설정창 효과음 슬라이더도 따른다.
//   - AudioSource의 Spatial Blend를 1(3D)로 두면 물건에 가까이 갈수록 크게 들린다.
// 사용: 소리를 낼 물건(예: 컴퓨터 본체)에 Audio Source를 추가해 Clip을 넣고(Loop 체크, Play On Awake 끔), 이 스크립트를 붙인다.
[RequireComponent(typeof(AudioSource))]
public class SfxLoop : MonoBehaviour
{
    [Range(0f, 1f)] public float volume = 0.5f;   // 기본 볼륨 (효과음 슬라이더가 최대일 때 크기)
    public bool playOnStart = true;               // 체크: 시작하자마자 반복 재생 (해금 전엔 소리만 0)

    AudioSource source;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.volume = 0f;
    }

    void Start()
    {
        if (playOnStart) Play();
    }

    // UnityEvent에서 켜고 끌 수 있게 (예: 컴퓨터를 켤 때 Play)
    public void Play()
    {
        if (source.clip != null && !source.isPlaying) source.Play();
    }

    public void Stop()
    {
        source.Stop();
    }

    void Update()
    {
        source.volume = volume * AudioManager.Volume;
    }
}