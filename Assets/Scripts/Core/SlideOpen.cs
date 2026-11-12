using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// 서랍처럼 물건을 지정한 자리(openPoint)까지 스르륵 미끄러지게 한다.
// 원리: 처음 위치에서 openPoint 위치까지 정해진 시간 동안 조금씩 옮긴다 (SmoothStep: 천천히 출발 → 빠르게 → 천천히 멈춤).
//       열릴 자리는 코드 숫자가 아니라 씬의 빈 오브젝트(openPoint)로 받는다 → Scene에서 옮기기만 하면 조정됨.
//       target의 자식(자물쇠, 컵 등)은 같이 따라 움직인다.
// 사용: 빈 오브젝트에 붙이고 Target(움직일 것)과 Open Point(열린 자리)를 넣는다. 다른 이벤트에서 Open()을 부른다.
public class SlideOpen : MonoBehaviour
{
    public Transform target;                 // 움직일 것 (예: desk_drawer). 비워 두면 이 오브젝트
    public Transform openPoint;              // 다 열렸을 때 target이 갈 자리 (빈 오브젝트)
    public float time = 0.8f;                // 다 열리는 데 걸리는 시간 (초)

    public UnityEvent onOpened;              // 다 열린 뒤 실행 (비워도 됨)

    public bool IsOpened { get; private set; }

    // 열기
    [ContextMenu("테스트: 열기 (Open)")]
    public void Open()
    {
        if (IsOpened || openPoint == null) return;
        if (target == null) target = transform;
        IsOpened = true;
        StartCoroutine(Move());
    }

    IEnumerator Move()
    {
        Vector3 from = target.position;
        Vector3 to = openPoint.position;

        for (float t = 0f; t < 1f; t += Time.deltaTime / time)
        {
            target.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        target.position = to;
        onOpened.Invoke();
    }
}