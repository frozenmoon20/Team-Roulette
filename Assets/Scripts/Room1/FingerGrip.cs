using UnityEngine;
using UnityEngine.Events;
using TMPro;

// 미니게임 3 - 손가락 쥐기 (컵, 나중에 문고리에도 같이 씀)
// F, D, S, A, Space = 검지, 중지, 약지, 새끼, 엄지 (키보드 위 왼손 손가락 자리 그대로). 순서는 자유.
// 한 번에 한 손가락만 굽힐 수 있다: 가장 마지막에 누른 키 하나만 인정.
// 다른 키를 누르거나 손을 떼면, 놓은 손가락은 랜덤한 시간(holdRange) 동안 버티다가 스르르 풀린다.
//   → 풀리기 직전에 그 손가락이 떨린다 (언제 풀릴지 눈으로 보고 다시 눌러 줄 수 있게)
// 다섯 손가락이 동시에 gripLevel 이상 굽어 있으면 "다 쥠" → onGripped
//   - Lift After Grip 해제(기본, 문고리 등): 다 쥐면 바로 끝난다.
//   - Lift After Grip 체크(컵): 다 쥐어도 끝나지 않고 "들어 올리기 모드"로 넘어간다.
//       이때부터는 lift 설정(빨리 굽고, 짧게 버티고)으로 계속 진행되고, 손가락이 계속 풀리므로 키를 계속 눌러야 한다.
//       들어 올리기 스크립트(CupLift)가 Strength(쥔 힘)를 읽고, 다 들어 올리면 End()로 끝낸다.
// 구제: 시작하고 rescueStart초가 지나면 점점 덜 풀려서(버티는 시간·풀리는 시간이 길어짐) 결국 누구나 성공한다.
// A, S, D, Space는 이동·점프 키와 겹치므로, 하는 동안 플레이어 조작은 꺼져 있어야 한다
//   (컵은 ViewFocus가 꺼 줌 → Disable While Playing은 비워 둔다).
// 빈 오브젝트에 붙인다. Begin()으로 시작.
public class FingerGrip : MonoBehaviour
{
    [Header("연결")]
    public HandPose pose;                    // Player/.../HandRoot 의 HandPose
    public TMP_Text keyText;                 // 키 안내 글자 (비워도 됨)

    [Header("쥐는 느낌")]
    public float curlTime = 1f;              // 펴진 손가락이 다 굽는 데 걸리는 시간 (초)
    public Vector2 holdRange = new Vector2(1f, 4f);   // 놓은 손가락이 버티는 시간 (초, 이 사이에서 매번 랜덤)
    public float slipTime = 1.5f;            // 버티기가 끝난 뒤 다 풀리는 데 걸리는 시간 (초)
    [Range(0.5f, 1f)] public float gripLevel = 0.9f;  // 이만큼 굽어 있으면 "쥔 손가락"으로 침

    [Header("들어 올리기 모드 (컵)")]
    public bool liftAfterGrip = false;       // 체크하면 다 쥔 뒤 끝내지 않고 들어 올리기 모드로
    public float liftCurlTime = 0.3f;        // 들어 올리는 동안 손가락이 다 굽는 데 걸리는 시간 (빨리 다시 쥘 수 있게)
    public Vector2 liftHoldRange = new Vector2(0.4f, 1f);   // 들어 올리는 동안 놓은 손가락이 버티는 시간 (짧게)
    public float liftSlipTime = 1.5f;        // 들어 올리는 동안 다 풀리는 데 걸리는 시간

    [Header("구제: 오래 걸리면 점점 덜 풀림")]
    public float rescueStart = 25f;          // 시작하고 이 시간(초)이 지나면 구제 시작
    public float rescueDuration = 20f;       // 이 시간(초)에 걸쳐 최대로
    public float rescueMultiplier = 5f;      // 최대일 때 버티는 시간·풀리는 시간이 몇 배로 길어질지

    [Header("떨림")]
    public float trembleAmount = 0.05f;      // 손가락 떨림 폭 (0이면 끔)
    public float trembleSpeed = 12f;
    public float warnTime = 0.8f;            // 풀리기 이 시간(초) 전부터 떨리기 시작

    [Header("다 쥐었을 때 손가락 굽힘 (0~1, 순서: 검지 중지 약지 새끼 엄지)")]
    public float[] gripCurl = { 0.75f, 0.8f, 0.85f, 0.9f, 0.4f };

    [Header("하는 동안 꺼 둘 것 (Player, PlayerInteractor 등)")]
    public Behaviour[] disableWhilePlaying;

    [Header("다 쥐었을 때 할 일")]
    public UnityEvent onGripped;

    [Header("테스트용")]
    public bool beginOnStart = false;

    [Header("확인용 (Play 중 값 보기)")]
    [Range(0f, 1f)] public float[] progress = new float[5];   // 손가락마다 0 = 펴짐, 1 = 다 굽음
    public float elapsed;                                      // 시작하고 지난 시간 (구제 확인용)
    public bool lifting;                                       // 들어 올리기 모드인지

    // 다른 스크립트가 바꾸는 값: 1 = 보통, 2 = 두 배로 빨리 풀림 (CupLift가 너무 빨리 들어 올릴 때 올림)
    [System.NonSerialized] public float extraSlip = 1f;

    // 쥔 힘 = 다섯 손가락 굽힘의 평균 (0~1). CupLift가 읽는다.
    public float Strength
    {
        get
        {
            float sum = 0f;
            for (int i = 0; i < 5; i++) sum += progress[i];
            return sum / 5f;
        }
    }

    public bool IsPlaying => playing;

    // 누를 키와 그 키가 굽힐 손가락 (같은 순번끼리 짝)
    static readonly KeyCode[] keys = { KeyCode.F, KeyCode.D, KeyCode.S, KeyCode.A, KeyCode.Space };
    static readonly string[] keyNames = { "F", "D", "S", "A", "Space" };
    static readonly HandPose.Finger[] fingers =
        { HandPose.Finger.Index, HandPose.Finger.Middle, HandPose.Finger.Ring, HandPose.Finger.Pinky, HandPose.Finger.Thumb };

    float[] holdTimer = new float[5];        // 놓은 손가락이 앞으로 버틸 시간 (0이 되면 풀리기 시작)
    int active = -1;                         // 지금 굽히고 있는 손가락 순번 (-1 = 없음)
    bool playing;

    void Start()
    {
        if (beginOnStart) Begin();
    }

    // 쥐기 시작 (손이 컵 앞에 도착했을 때 부른다)
    public void Begin()
    {
        // 걷던 중이었으면 남은 속도(관성)부터 0으로. 안 하면 다 쥐고 다시 켜질 때 미끄러짐
        FirstPersonPlayer player = FindFirstObjectByType<FirstPersonPlayer>();
        if (player != null) player.StopMovement();

        foreach (Behaviour b in disableWhilePlaying) if (b != null) b.enabled = false;
        for (int i = 0; i < 5; i++) { progress[i] = 0f; holdTimer[i] = 0f; }
        active = -1;
        elapsed = 0f;
        lifting = false;
        extraSlip = 1f;
        playing = true;
        Apply();
    }

    void Update()
    {
        if (!playing) return;
        elapsed += Time.deltaTime;

        // 1) 가장 마지막에 누른 키 하나만 인정. 그 키를 떼면 아무 손가락도 안 굽힘
        for (int i = 0; i < 5; i++)
            if (Input.GetKeyDown(keys[i])) SetActive(i);
        if (active >= 0 && !Input.GetKey(keys[active])) SetActive(-1);

        // 2) 굽히는 손가락은 굽고, 놓은 손가락은 버티다가 풀림 (들어 올리는 동안은 lift 설정 사용)
        float slow = Slowness();
        float curl = lifting ? liftCurlTime : curlTime;
        float slip = lifting ? liftSlipTime : slipTime;
        for (int i = 0; i < 5; i++)
        {
            if (i == active) progress[i] += Time.deltaTime / curl;
            else if (holdTimer[i] > 0f) holdTimer[i] -= Time.deltaTime * extraSlip;
            else progress[i] -= Time.deltaTime * extraSlip / (slip * slow);
            progress[i] = Mathf.Clamp01(progress[i]);
        }

        Apply();

        // 3) 다섯 손가락이 동시에 충분히 굽어 있으면 "다 쥠" (들어 올리는 중에는 다시 판정하지 않음)
        if (lifting) return;
        bool all = true;
        for (int i = 0; i < 5; i++) if (progress[i] < gripLevel) all = false;
        if (!all) return;

        if (liftAfterGrip)
        {
            // 끝내지 않고 들어 올리기 모드로. 구제 시간도 다시 0부터
            lifting = true;
            elapsed = 0f;
            Debug.Log("미니게임 3: 다 쥠 → 들어 올리기");
            onGripped.Invoke();
        }
        else
        {
            End();
            onGripped.Invoke();
        }
    }

    // 굽힐 손가락 바꾸기. 지금까지 굽히던 손가락은 랜덤한 시간 동안 버티다가 풀리게 됨
    void SetActive(int next)
    {
        Vector2 range = lifting ? liftHoldRange : holdRange;
        if (active >= 0) holdTimer[active] = Random.Range(range.x, range.y) * Slowness();
        active = next;
    }

    // 구제 배율: rescueStart 전엔 1, 이후 rescueDuration에 걸쳐 rescueMultiplier까지 커짐
    float Slowness()
    {
        float k = Mathf.Clamp01((elapsed - rescueStart) / rescueDuration);
        return Mathf.Lerp(1f, rescueMultiplier, k);
    }

    // progress를 실제 손 모양에 반영 + 키 안내
    void Apply()
    {
        if (pose != null)
        {
            for (int i = 0; i < 5; i++)
            {
                // 떨림 세기: 굽히는 중이면 반쯤 굽었을 때 가장 크게 / 놓은 손가락은 풀리기 직전에 점점 크게
                float shake;
                if (i == active) shake = progress[i] * (1f - progress[i]) * 4f;
                else if (holdTimer[i] > 0f) shake = 1f - Mathf.Clamp01(holdTimer[i] / warnTime);
                else shake = 0f;

                float noise = (Mathf.PerlinNoise(Time.time * trembleSpeed, i * 10f) - 0.5f) * 2f;
                float curl = progress[i] * gripCurl[i] + noise * trembleAmount * shake;
                pose.SetFinger(fingers[i], curl);
            }
        }

        if (keyText != null)
        {
            // 쥔 손가락은 흐리게, 지금 굽히는 손가락은 밝게
            string s = "";
            for (int i = 0; i < 5; i++)
            {
                string color = i == active ? "#FFFFFF" : progress[i] >= gripLevel ? "#555555" : "#AAAAAA";
                s += "<color=" + color + ">" + keyNames[i] + "</color>   ";
            }
            keyText.text = playing ? s : "";
        }
    }

    // 끝내기 (다 쥐었을 때 자동으로, 들어 올리기 모드에서는 CupLift가 부름). 손가락은 지금 모양 그대로 멈춤
    public void End()
    {
        if (!playing) return;
        playing = false;
        lifting = false;
        active = -1;
        extraSlip = 1f;
        Apply();
        foreach (Behaviour b in disableWhilePlaying) if (b != null) b.enabled = true;
        Debug.Log("미니게임 3: 쥐기 끝 (" + elapsed.ToString("0.0") + "초)");
    }

    public void OpenAll()
    {
        End();
        for (int i = 0; i < 5; i++)
        {
            progress[i] = 0f;
            holdTimer[i] = 0f;
            if (pose != null) pose.SetFinger(fingers[i], 0f);
        }
    }
}