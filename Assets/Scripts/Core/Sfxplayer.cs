using UnityEngine;

// 이벤트 칸(On Solved, On Lifted, On Wiped 등)에서 효과음을 바로 틀기 위한 연결용 스크립트.
// 왜 필요한가: AudioManager는 씬이 바뀌어도 살아남는 오브젝트(DontDestroyOnLoad)라서 씬의 이벤트 칸에 끌어다 놓을 수 없다.
//              그래서 씬 안에 이 스크립트를 하나 두고, 이벤트에서 이걸 부르면 AudioManager로 소리를 넘겨준다.
// 사용:
//   1) 씬에 빈 오브젝트 "SFX"를 만들고 이 스크립트를 붙인다 (씬에 하나면 충분).
//   2) 아무 이벤트 칸에서 [+] → SFX 오브젝트를 끌어다 놓기 → SfxPlayer > Play (AudioClip) 선택 → 아래 칸에 소리 파일을 넣는다.
//   소리는 AudioManager로 가므로 효과음(SFX) 해금 전에는 안 나고, 설정창 효과음 볼륨을 따른다.
public class SfxPlayer : MonoBehaviour
{
    [Range(0f, 1f)] public float volume = 0.8f;   // 이 SfxPlayer로 트는 소리 전체 크기

    // 이벤트 칸에서 소리 파일을 골라 틀기
    public void Play(AudioClip clip)
    {
        AudioManager.Play(clip, volume);
    }
}