using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// LP 플레이어 연출: LP판을 턴테이블(LP_Spot)에 놓으면
//   0초        뚜껑이 열리기 시작 (턴테이블 애니메이션 Take 001 재생, 24초 정도)
//   Record Drop Time   뚜껑이 다 열리면 판이 위에서 내려와 자리에 놓임 (그 전까지는 판을 숨겨 둠 → 닫힌 뚜껑을 뚫고 보이지 않게)
//   Music Start Time (7초)  바늘이 판에 닿을 때쯤: 판이 돌기 시작 + 음악이 서서히 커짐
//   Music Stop Time (17초)  턴테이블이 멈출 때쯤: 판이 서서히 멈춤 + 음악이 서서히 작아짐
//   애니메이션 끝      On Finished 실행 (OptionUnlocker.Unlock → BGM 해금 알림)
// 무한 반복 (Loop Forever 체크): 7초부터는 애니메이션을 Loop Start~Loop End(7~14초) 구간에서 앞으로 → 뒤로 → 앞으로 계속 왕복하고,
//   음악·판 회전도 멈추지 않는다. 끝이 없으므로 해금은 Unlock Time(14초)에 한다. (Music Stop Time은 쓰지 않음)
//   원리: 그 구간에서는 애니메이션 자동 재생을 멈추고(speed 0), 매 프레임 "지금 보여 줄 시점"을 직접 정해 준다(Play의 시점 지정).
//         시점 = Loop Start + 왕복(Mathf.PingPong) → 7 → 14 → 7 → 14 … 로 부드럽게 오간다.
// 원리:
//   - Take 001은 게임 시작하자마자 재생되는 구조라서, 시작할 때 첫 프레임에 멈춰 둔다(speed 0). Play()에서 처음부터 재생.
//   - 시간 진행은 애니메이션에 이벤트를 넣지 않고(에셋 원본이라 수정 불가), Play()부터 지난 시간으로 판단한다.
//   - 판 회전은 애니메이션이 아니라 이 스크립트가 한다 (판 = LP_Record, 자기 Y축 = 원기둥의 축).
//   - 판을 놓는 일은 원래대로 PlaceSpot(LP_Spot)이 하고, 그 On All Placed에서 Play()를 부른다.
// 사용: LP_Player에 붙어 있는 그대로 쓰고 (Play()라는 이름이 같아서 LP_Spot의 연결도 그대로) 아래 칸을 채운다.
//   Deck: 씬에 놓은 RecordDeckAnimated의 Animator / Record: LP_Record / Music: 음악 AudioSource (비우면 같은 오브젝트)
//   On Finished: LP_Player의 OptionUnlocker.Unlock (BGM 해금) → LP_Spot의 On All Placed에서는 Unlock을 빼기
public class LPPlayer : MonoBehaviour
{
    [Header("연결")]
    public Animator deck;                    // 턴테이블 애니메이션 (RecordDeckAnimated의 Animator)
    public string stateName = "Take 001";    // 재생할 애니메이션 상태 이름
    public AudioSource music;                // 음악 (비워 두면 같은 오브젝트의 AudioSource)
    public Transform record;                 // LP판 (LP_Record)

    [Header("시간 (초, Play()부터)")]
    public float recordDropTime = 3f;        // 뚜껑이 다 열리는 시점 → 판이 내려와 놓임
    public float musicStartTime = 7f;        // 판이 돌고 음악이 시작되는 시점
    public float musicStopTime = 17f;        // 판이 멈추고 음악이 사라지는 시점
    public float endTime = 0f;               // 끝(해금) 시점. 0이면 애니메이션 길이를 자동으로 씀

    [Header("무한 반복 (체크: 7~14초 구간을 왕복하며 계속 재생, 음악도 계속)")]
    public bool loopForever = true;
    public float loopStart = 7f;             // 왕복 구간 시작 (초)
    public float loopEnd = 14f;              // 왕복 구간 끝 (초)
    public float unlockTime = 14f;           // 반복 모드일 때 해금(On Finished) 시점 (초, Play()부터)

    [Header("판")]
    public float dropHeight = 0.12f;         // 판이 내려오기 시작하는 높이 (자리에서 위로 m)
    public float dropTime = 0.8f;            // 내려오는 데 걸리는 시간
    public float rpm = 33f;                  // 분당 회전수 (LP는 33)
    public float spinUpTime = 1.5f;          // 멈춰 있다가 제 속도가 되는 시간 (초)
    public float spinDownTime = 3f;          // 제 속도에서 멈출 때까지 (초)

    [Header("음악")]
    [Range(0f, 1f)] public float musicVolume = 0.8f;
    public float fadeInTime = 2f;            // 음악이 서서히 커지는 시간
    public float fadeOutTime = 3f;           // 음악이 서서히 작아지는 시간

    public UnityEvent onFinished;            // 애니메이션이 다 끝난 뒤 (OptionUnlocker.Unlock = BGM 해금)

    public bool IsPlaying { get; private set; }

    float speed;                             // 지금 판 회전 속도 (도/초)
    bool spinning;                           // 판이 돌아야 하는지 (속도는 서서히 따라감)
    float clipLength = 1f;                   // 애니메이션 길이 (초)
    float pingStart = -1f;                   // 왕복을 시작한 시각 (-1 = 아직 아님)

    void Awake()
    {
        if (music == null) music = GetComponent<AudioSource>();
        if (music != null) { music.playOnAwake = false; music.Stop(); }
    }

    void Start()
    {
        // 애니메이션을 첫 프레임에 멈춰 둠 (뚜껑 닫힌 상태)
        if (deck != null)
        {
            deck.Play(stateName, 0, 0f);
            deck.speed = 0f;
        }
    }

    // 연출 시작 (LP_Spot의 On All Placed에 연결)
    public void Play()
    {
        if (IsPlaying) return;
        IsPlaying = true;
        StartCoroutine(Sequence());
    }

    IEnumerator Sequence()
    {
        // 판은 PlaceSpot이 이미 자리(Place Point)에 놓았음.
        // 단, PlaceSpot은 "물건의 실제 크기 바닥"을 기준으로 놓아서, 처음에 비스듬히 세워 둔 판은 옆으로 밀려 놓인다
        // → 판을 Place Point 한가운데에 똑바로 다시 맞춘다 (판의 기준점 = 밑면 가운데)
        // 또 Place Point의 부모(턴테이블 모델 등)에 찌그러진 크기(Scale)나 회전이 있으면, 그 자식이 된 판이 늘어나 버린다
        // → 판을 부모에서 떼어 내고(맨 위로) 크기 1로 되돌린 뒤 자리에 놓는다. 이후 판을 다시 집을 일은 없다.
        if (record != null && record.parent != null)
        {
            Transform point = record.parent;
            record.SetParent(null, false);
            record.localScale = Vector3.one;
            record.SetPositionAndRotation(point.position, point.rotation);
        }

        // 뚜껑이 열릴 때까지 숨겨 둠
        // 숨길 때 "지금 켜져 있는 것"만 기억 → 다시 보일 때 그것만 켬
        //   (원래부터 꺼 둔 모양, 예: LP판 만들기로 꺼진 원래 원기둥은 계속 꺼진 채로)
        System.Collections.Generic.List<Renderer> shown = new System.Collections.Generic.List<Renderer>();
        if (record != null)
            foreach (Renderer r in record.GetComponentsInChildren<Renderer>())
                if (r.enabled) shown.Add(r);
        Renderer[] recordRenderers = shown.ToArray();
        Vector3 recordPos = record != null ? record.position : Vector3.zero;
        SetVisible(recordRenderers, false);

        // 0초: 뚜껑 열기 (애니메이션 처음부터)
        if (deck != null)
        {
            deck.Play(stateName, 0, 0f);
            deck.speed = 1f;
        }
        clipLength = Mathf.Max(ClipLength(), 0.01f);
        float total = endTime > 0f ? endTime : clipLength;
        float t0 = Time.time;
        if (loopForever) StartCoroutine(StartLoop(t0));

        // 뚜껑이 다 열리면 판이 위에서 내려옴
        yield return new WaitUntil(() => Time.time - t0 >= recordDropTime);
        if (record != null)
        {
            SetVisible(recordRenderers, true);
            Vector3 from = recordPos + Vector3.up * dropHeight;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(dropTime, 0.01f))
            {
                record.position = Vector3.Lerp(from, recordPos, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            record.position = recordPos;
        }

        // 7초: 판이 돌고 음악이 서서히 커짐
        yield return new WaitUntil(() => Time.time - t0 >= musicStartTime);
        spinning = true;
        if (music != null)
        {
            music.volume = 0f;
            if (loopForever) music.loop = true;       // 반복 모드: 음악도 계속 반복
            music.Play();
            yield return Fade(0f, musicVolume, fadeInTime);
        }

        // 반복 모드: 멈추지 않음. Unlock Time에 해금만 하고 끝 (왕복·음악·회전은 계속)
        if (loopForever)
        {
            yield return new WaitUntil(() => Time.time - t0 >= unlockTime);
            onFinished.Invoke();
            Debug.Log("LP 플레이어: 해금 (반복 재생 계속)");
            yield break;
        }

        // 17초: 판이 서서히 멈추고 음악이 서서히 작아짐
        yield return new WaitUntil(() => Time.time - t0 >= musicStopTime);
        spinning = false;
        if (music != null)
        {
            yield return Fade(music.volume / Mathf.Max(AudioManager.Volume, 0.0001f), 0f, fadeOutTime);
            music.Stop();
        }

        // 애니메이션 끝: 해금
        yield return new WaitUntil(() => Time.time - t0 >= total);
        onFinished.Invoke();
        Debug.Log("LP 플레이어 연출 끝");
    }

    // Loop Start 시점이 되면 왕복 시작
    IEnumerator StartLoop(float t0)
    {
        yield return new WaitUntil(() => Time.time - t0 >= loopStart);
        pingStart = Time.time;
    }

    // 음악 크기를 from → to로 (설정의 소리 크기 슬라이더도 반영)
    IEnumerator Fade(float from, float to, float time)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(time, 0.01f))
        {
            music.volume = Mathf.Lerp(from, to, t) * AudioManager.Volume;
            yield return null;
        }
        music.volume = to * AudioManager.Volume;
    }

    // 판 회전 (자기 Y축). 돌기 시작·멈출 때 속도가 서서히 바뀜 + 반복 구간 애니메이션
    void Update()
    {
        // 왕복 구간: 애니메이션이 보여 줄 시점을 직접 정함 (7 → 14 → 7 → …)
        if (pingStart >= 0f && deck != null)
        {
            float len = Mathf.Max(loopEnd - loopStart, 0.01f);
            float at = loopStart + Mathf.PingPong(Time.time - pingStart, len);
            deck.speed = 0f;
            deck.Play(stateName, 0, at / clipLength);
        }

        float full = rpm * 6f;                                         // rpm → 도/초
        float target = spinning ? full : 0f;
        float rate = full / Mathf.Max(spinning ? spinUpTime : spinDownTime, 0.01f);
        speed = Mathf.MoveTowards(speed, target, rate * Time.deltaTime);
        if (record != null && speed > 0f) record.Rotate(0f, speed * Time.deltaTime, 0f, Space.Self);
    }

    float ClipLength()
    {
        if (deck != null && deck.runtimeAnimatorController != null)
            foreach (AnimationClip clip in deck.runtimeAnimatorController.animationClips)
                if (clip != null) return clip.length;
        return musicStopTime + fadeOutTime;
    }

    static void SetVisible(Renderer[] renderers, bool on)
    {
        foreach (Renderer r in renderers) if (r != null) r.enabled = on;
    }
}