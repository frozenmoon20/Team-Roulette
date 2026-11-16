using System.Collections;
using UnityEngine;

// MG5 회상: 화면 왼쪽 아래에 걸쳐 둔 클릭커가, Q(클릭커 키)를 누를 때마다 "딸깍" 살짝 눌렸다가 돌아온다.
// 원리: 처음 자리(로컬 위치·각도)를 기억해 두고, 키를 누르면 아래로 조금 내려가며 앞으로 살짝 기울었다가
//       빠르게 원래 자리로 돌아온다. (손 없이도 엄지로 누르는 느낌)
// 소리는 여기서 내지 않는다 → 원래대로 HurdleTraining의 Whistle(Audio Source, 클릭커 소리)이 낸다.
// 사용: 카메라(PlayerCameraRoot 또는 카메라) 아래에 클릭커 모델을 넣고, 화면 왼쪽 아래에 반쯤 잘리게 위치를 맞춘 뒤 이 스크립트를 붙인다.
//       처음엔 꺼 두고, MemoryTrip의 On White에서 SetActive(true), HurdleTraining의 On Cleared에서 SetActive(false).
public class ClickerPress : MonoBehaviour
{
    public KeyCode key = KeyCode.Q;          // HurdleTraining의 Whistle Key와 같게
    public float pressDepth = 0.008f;        // 눌릴 때 내려가는 거리 (m)
    public float pressTilt = 6f;             // 눌릴 때 앞으로 기우는 각도 (도)
    public float downTime = 0.05f;           // 눌리는 시간 (초)
    public float upTime = 0.12f;             // 돌아오는 시간 (초)

    Vector3 homePos;
    Quaternion homeRot;
    Coroutine running;

    void Awake()
    {
        homePos = transform.localPosition;
        homeRot = transform.localRotation;
    }

    void OnDisable()
    {
        // 꺼질 때 원래 자리로 (중간에 꺼져도 다음에 켜질 때 어긋나지 않게)
        transform.localPosition = homePos;
        transform.localRotation = homeRot;
        running = null;
    }

    void Update()
    {
        if (Input.GetKeyDown(key))
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Press());
        }
    }

    IEnumerator Press()
    {
        Vector3 downPos = homePos + Vector3.down * pressDepth;
        Quaternion downRot = homeRot * Quaternion.Euler(pressTilt, 0f, 0f);
        yield return Move(downPos, downRot, downTime);
        yield return Move(homePos, homeRot, upTime);
        running = null;
    }

    IEnumerator Move(Vector3 toPos, Quaternion toRot, float time)
    {
        Vector3 fromPos = transform.localPosition;
        Quaternion fromRot = transform.localRotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(time, 0.01f))
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            transform.localPosition = Vector3.Lerp(fromPos, toPos, k);
            transform.localRotation = Quaternion.Slerp(fromRot, toRot, k);
            yield return null;
        }
        transform.localPosition = toPos;
        transform.localRotation = toRot;
    }
}