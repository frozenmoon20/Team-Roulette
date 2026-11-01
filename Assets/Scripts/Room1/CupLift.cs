using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TeamRoulette.UI;

// 미니게임 3 - 컵 들어 올리기 (손가락으로 다 쥔 뒤). "적당한 힘으로, 정밀하게" 드는 게임.
// 원리 (매 프레임 위에서부터 차례로 판단):
//   1) 손가락이 풀림 → 컵이 빠르게 미끄러져 내려간다 (마우스와 상관없이)
//        - 엄지가 풀렸거나 (엄지 굽힘 < looseLevel)
//        - 다섯 손가락 중 looseCount개(3개) 이상이 풀렸을 때
//   2) 좌클릭 + 위로 "적당한 빠르기"(weakSpeed ~ strongSpeed) → 움직인 만큼 올라간다
//   3) 좌클릭 + 위로 "너무 셈"(strongSpeed 초과) → 손이 부들부들 떨리며 더 안 올라가고 "무리"(strain)가 쌓인다
//        무리가 1이 되면 실패 → onDropped(안내 자막 끄기) → 손가락은 그 모양 그대로 멈추고,
//        손과 컵이 같이 점점 빨라지며 제자리 높이로 미끄러져 내려간다 → 잠깐 뒤 onFailed (대사 → 상호작용 종료)
//   4) 그 밖 (멈춤, 너무 느림, 좌클릭 뗌, 손가락 누르느라 마우스가 쉼) → 그 높이에서 아주 천천히 내려간다 (holdDrift)
//      → 손가락 키를 누르는 동안 컵이 확 내려가지 않는다.
//   - 무리는 3) 아닐 때 서서히 풀린다. 무리만큼 손가락도 빨리 풀린다(strainSlip).
//   - targetHeight까지 올리면 성공 → 쥐기 끝 → onLifted. 이후 PutDown()으로 천천히 내려놓고 → onPutDown.
// 마우스 입력은 프레임마다 들쭉날쭉해서, 빠르기는 speedSmoothTime(초) 동안 부드럽게 평균 낸 값으로 판단한다.
//   (값이 클수록 순간적인 손떨림에 덜 민감 → 빠르기 유지가 쉬워짐)
// 높이는 HandAnchor.lift, 떨림은 HandAnchor.shake로 손을 움직이고, 컵도 같은 만큼 직접 움직인다.
// 사용: 빈 오브젝트에 붙이고 Grip / Anchor / Cup을 연결한다. FingerGrip의 On Gripped에 Begin을 연결.
public class CupLift : MonoBehaviour
{
    [Header("연결")]
    public FingerGrip grip;                  // 손가락 (Lift After Grip 체크)
    public HandAnchor anchor;                // 손 자리 (lift, shake 값으로 손을 움직임)
    public Transform cup;                    // 들어 올릴 컵

    [Header("들어 올리기")]
    public float targetHeight = 0.08f;       // 이만큼(m) 올리면 성공
    public float liftPerMouse = 0.002f;      // 마우스를 위로 1만큼 움직일 때 올라가는 높이 (m)

    [Header("드는 빠르기 (m/초)")]
    public float weakSpeed = 0.008f;         // 이보다 느리면 안 올라감 (천천히 내려감)
    public float strongSpeed = 0.03f;        // 이보다 빠르면 "너무 셈" → 떨림 + 무리, 더 안 올라감
    public float speedSmoothTime = 0.3f;     // 빠르기를 이 시간(초) 동안 평균 냄. 클수록 유지가 쉬움
    public float holdDrift = 0.003f;         // 멈춰 있을 때 아주 천천히 내려가는 빠르기 (m/초)

    [Header("손가락이 풀리면 미끄러짐")]
    [Range(0f, 1f)] public float looseLevel = 0.5f;   // 손가락 굽힘이 이보다 작으면 "풀린 손가락"
    public int looseCount = 3;               // 이 개수 이상 풀리면 미끄러짐
    public bool thumbMatters = true;         // 체크: 엄지 하나만 풀려도 미끄러짐
    public float slipSpeed = 0.12f;          // 미끄러져 내려가는 빠르기 (m/초)

    [Header("너무 셀 때: 떨림과 무리")]
    public float strainRise = 1f;            // 아주 셀 때 무리가 1초에 차는 양 (1이 되면 실패)
    public float strainFall = 0.4f;          // 너무 세지 않으면 1초에 풀리는 양
    public float shakeAmount = 0.015f;       // 무리가 1일 때 손이 떨리는 폭 (m)
    public float shakeSpeed = 25f;           // 떨림 빠르기
    public float strainSlip = 1.5f;          // 무리가 1일 때 손가락이 몇 배로 빨리 풀릴지

    [Header("내려놓기 / 놓치기")]
    public float putDownTime = 0.6f;         // 성공 후 천천히 내려놓는 시간 (초)
    public float dropTime = 0.3f;            // 실패해서 손과 컵이 같이 내려가는 시간 (초)
    public float failPause = 0.6f;           // 내려간 뒤 실패 대사까지 기다리는 시간 (초)

    [Header("이벤트")]
    public UnityEvent onLifted;              // 다 들어 올렸을 때 (성공 대사 재생)
    public UnityEvent onPutDown;             // 성공 후 내려놓은 뒤 (Grab 해금, 손 되돌리기, 원래 자리로)
    public UnityEvent onDropped;             // 놓치는 순간 바로 (안내 자막 Hide 등)
    public UnityEvent onFailed;              // 내려가고 잠깐 뒤 (실패 대사 재생 → 손 되돌리기, 원래 자리로)

    [Header("확인용 (Play 중 값 보기)")]
    public string state;                     // 지금 상태 (미끄러짐 / 올라감 / 너무 셈 / 멈춤)
    public float height;                     // 지금 높이 (m)
    public float speed;                      // 지금 드는 빠르기 (m/초, 평균 낸 값)
    [Range(0f, 1f)] public float strain;     // 지금 무리 (1이 되면 실패)

    bool lifting;
    bool homeSet;
    Vector3 cupHome;                         // 컵 처음 자리 (실패 후 다시 해도 같은 자리)
    Vector3 shake;
    GameUI ui;

    // 들어 올리기 시작 (FingerGrip의 On Gripped에 연결)
    public void Begin()
    {
        if (lifting || cup == null || grip == null) return;
        if (ui == null) ui = FindFirstObjectByType<GameUI>();
        if (!homeSet) { cupHome = cup.position; homeSet = true; }
        height = 0f;
        speed = 0f;
        strain = 0f;
        shake = Vector3.zero;
        lifting = true;
        Apply();
    }

    void Update()
    {
        if (!lifting) return;
        if (ui != null && (ui.IsDialogueOpen || ui.IsPaused)) return;   // 대사·설정창 중엔 멈춤

        float dt = Time.deltaTime;
        bool holding = Input.GetMouseButton(0);

        // 드는 빠르기: 좌클릭 중 위로 움직인 양 → m/초, speedSmoothTime 동안 평균
        float up = holding ? Mathf.Max(0f, Input.GetAxis("Mouse Y")) * liftPerMouse : 0f;
        float rawSpeed = up / Mathf.Max(dt, 0.0001f);
        speed = Mathf.Lerp(speed, rawSpeed, 1f - Mathf.Exp(-dt / Mathf.Max(speedSmoothTime, 0.01f)));

        // 풀린 손가락 세기
        int loose = 0;
        for (int i = 0; i < 5; i++) if (grip.progress[i] < looseLevel) loose++;
        bool thumbLoose = grip.progress[4] < looseLevel;               // 4번 = 엄지

        float over = 0f;                                                // 너무 센 정도 (0~1)
        if ((thumbMatters && thumbLoose) || loose >= looseCount)
        {
            state = "미끄러짐";
            height -= slipSpeed * dt;                                   // 1) 손가락이 풀림 → 빠르게 미끄러짐
        }
        else if (holding && speed >= weakSpeed && speed <= strongSpeed)
        {
            state = "올라감";
            height += up;                                               // 2) 적당함 → 올라감
        }
        else if (holding && speed > strongSpeed)
        {
            state = "너무 셈";
            over = Mathf.Clamp01((speed - strongSpeed) / strongSpeed);  // 3) 너무 셈 → 안 올라가고 무리
        }
        else
        {
            state = "멈춤";
            height -= holdDrift * dt;                                   // 4) 그 밖 → 아주 천천히 내려감
        }

        // 무리: 너무 세면 쌓이고, 아니면 풀림
        if (over > 0f) strain += strainRise * over * dt;
        else strain -= strainFall * dt;
        strain = Mathf.Clamp01(strain);

        // 떨림 (무리만큼) + 손가락도 그만큼 빨리 풀림
        float t = Time.time * shakeSpeed;
        shake = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, Mathf.PerlinNoise(t, t) - 0.5f)
                * 2f * shakeAmount * strain;
        grip.extraSlip = Mathf.Lerp(1f, strainSlip, strain);

        height = Mathf.Clamp(height, 0f, targetHeight);
        Apply();

        // 결과
        if (strain >= 1f)
        {
            lifting = false;
            Debug.Log("미니게임 3: 무리해서 놓침");
            StartCoroutine(DropRoutine());
        }
        else if (height >= targetHeight)
        {
            lifting = false;
            shake = Vector3.zero;
            Apply();
            grip.End();                                                 // 손가락은 쥔 모양 그대로 멈춤
            Debug.Log("미니게임 3: 다 들어 올림");
            onLifted.Invoke();
        }
    }

    // 컵과 손을 같은 높이·떨림으로
    void Apply()
    {
        if (cup != null) cup.position = cupHome + Vector3.up * height + shake;
        if (anchor != null) { anchor.lift = height; anchor.shake = shake; }
    }

    // 실패: 손가락은 그 모양 그대로 멈추고, 손과 컵이 같이 제자리 높이로 미끄러져 내려간다 → onFailed
    IEnumerator DropRoutine()
    {
        onDropped.Invoke();                                // 안내 자막 끄기 등
        grip.End();                                        // 쥐기 끝 → 손가락은 지금 모양 그대로 멈춤
        shake = Vector3.zero;

        float from = height;
        for (float k = 0f; k < 1f; k += Time.deltaTime / dropTime)
        {
            height = Mathf.Lerp(from, 0f, k * k);          // 점점 빨라지며 내려감 (손과 컵이 같이)
            Apply();
            yield return null;
        }
        height = 0f;
        strain = 0f;
        Apply();
        yield return new WaitForSeconds(failPause);        // 잠깐 멈춰 보여 줌
        onFailed.Invoke();                                 // (대사 → 손 되돌리기 → 원래 자리로)
    }

    // 성공 후 천천히 내려놓기 (성공 대사의 On Finished에 연결)
    public void PutDown()
    {
        if (lifting) return;
        StartCoroutine(PutDownRoutine());
    }

    IEnumerator PutDownRoutine()
    {
        float from = height;
        for (float k = 0f; k < 1f; k += Time.deltaTime / putDownTime)
        {
            height = Mathf.Lerp(from, 0f, Mathf.SmoothStep(0f, 1f, k));
            Apply();
            yield return null;
        }
        height = 0f;
        Apply();
        onPutDown.Invoke();
    }
}