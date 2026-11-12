using UnityEngine;
using UnityEngine.Events;

// 플레이어가 이 구역에 들어오면 이벤트를 실행한다 (기본: 처음 한 번만).
// 원리: 보이지 않는 Collider(Is Trigger 체크)를 깔아 두면, 플레이어(CharacterController)가 들어올 때
//       Unity가 OnTriggerEnter를 불러 준다. 들어온 게 플레이어(FirstPersonPlayer)일 때만 onEnter를 실행한다.
// 사용 예: 복도 입구에 두고 On Enter에 DialogueStep.Play 연결 (복도에 나오자마자 대사 + 달리기 해금)
//          전화기 앞에 두고 On Enter에 전화벨 연출 시작 연결
// 사용: 빈 오브젝트에 Box Collider + 이 스크립트를 붙이고 크기를 맞춘다. (Is Trigger는 자동으로 켜짐)
[RequireComponent(typeof(Collider))]
public class TriggerZone : MonoBehaviour
{
    public bool once = true;                 // 체크: 처음 한 번만 실행 / 해제: 들어올 때마다 실행
    public UnityEvent onEnter;               // 플레이어가 들어왔을 때 할 일

    bool done;

    // 컴포넌트를 처음 붙일 때 Is Trigger를 자동으로 켬
    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (once && done) return;
        if (other.GetComponentInParent<FirstPersonPlayer>() == null) return;   // 플레이어만
        done = true;
        onEnter.Invoke();
    }
}