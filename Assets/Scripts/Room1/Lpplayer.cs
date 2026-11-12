using UnityEngine;

// LP 플레이어: Play()를 부르면 음악을 틀고 레코드판을 돌린다.
// 원리: 대사·해금은 DialogueInteractable이 맡고(처음 클릭 → 대사 → BGM 해금),
//       그 On First Finished에서 이 스크립트의 Play()를 불러 음악과 회전만 담당한다.
//   - 음악: 같은 오브젝트의 AudioSource를 재생 (Loop 체크해 두면 계속 반복)
//   - 레코드판: record의 로컬 Y축(위쪽 축)을 기준으로 rpm 속도로 돌린다. 처음엔 천천히 빨라짐
// 사용: LP 플레이어 오브젝트에 AudioSource(음악 클립, Play On Awake 해제, Loop 체크)와 이 스크립트를 붙이고
//       Record 칸에 돌아갈 판(원기둥)을 넣는다.
public class LPPlayer : MonoBehaviour
{
    public AudioSource music;                // 음악 (비워 두면 같은 오브젝트의 AudioSource)
    public Transform record;                 // 돌아갈 레코드판
    public float rpm = 33f;                  // 분당 회전수 (LP는 33)
    public float spinUpTime = 1.5f;          // 멈춰 있다가 제 속도가 되는 데 걸리는 시간 (초)

    public bool IsPlaying { get; private set; }

    float speed;                             // 지금 회전 속도 (도/초)

    void Awake()
    {
        if (music == null) music = GetComponent<AudioSource>();
    }

    // 음악 틀기 + 판 돌리기 (DialogueInteractable의 On First Finished에 연결)
    public void Play()
    {
        if (IsPlaying) return;
        IsPlaying = true;
        if (music != null) music.Play();
    }

    // 멈추기 (지금은 안 쓰지만 나중에 끌 일이 있을 때)
    public void Stop()
    {
        IsPlaying = false;
        if (music != null) music.Stop();
    }

    void Update()
    {
        float target = IsPlaying ? rpm * 6f : 0f;                     // rpm → 도/초 (1회전 = 360도, 1분 = 60초)
        speed = Mathf.MoveTowards(speed, target, rpm * 6f / Mathf.Max(spinUpTime, 0.01f) * Time.deltaTime);
        if (record != null && speed > 0f) record.Rotate(0f, speed * Time.deltaTime, 0f, Space.Self);
    }
}