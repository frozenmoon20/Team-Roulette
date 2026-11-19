using System.Collections;
using UnityEngine;

// 저장 지점: 여기에 오면 저장하고, 이어하기 때 플레이어를 이 자리에서 시작시키는 부품.
// 예: 복도의 각 미니게임이 끝난 자리, 거실의 각 단계가 끝난 자리
// 사용:
//   1) 빈 오브젝트를 "이어하기 때 서 있을 자리"에 놓고 이 스크립트를 붙인다. 오브젝트의 파란 화살표(앞)가 바라볼 방향.
//   2) Id에 겹치지 않는 이름을 적는다. (예: Corridor_AfterSokoban, Living_AfterCurtain)
//   3) 저장하는 방법은 둘 중 하나:
//        - 미니게임 클리어 이벤트 등에 SavePoint.Reach를 연결
//        - Save On Enter를 체크하고 Box Collider(Is Trigger)를 붙이면, 플레이어가 지나갈 때 저장
// 원리: 이어하기로 들어왔고 마지막 저장 지점이 이 Id면, 씬 준비가 끝난 뒤(두 프레임 뒤)
//       플레이어를 이 자리로 옮긴다. (캐릭터 컨트롤러를 잠깐 꺼야 순간이동이 됨)
public class SavePoint : MonoBehaviour
{
    [Tooltip("겹치지 않는 이름 (예: Corridor_AfterSokoban)")]
    public string id = "";
    [Tooltip("체크하면 플레이어가 이 오브젝트의 Trigger Collider에 들어올 때 저장")]
    public bool saveOnEnter = false;

    IEnumerator Start()
    {
        yield return null;                   // SaveFlag가 먼저 끝난 모양으로 바꾼 뒤에
        yield return null;
        if (SaveSystem.ConsumePendingPoint(id)) MovePlayerHere();
    }

    // 클리어 이벤트 등에 연결
    public void Reach()
    {
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("SavePoint: Id가 비어 있어서 저장하지 못했어요.", this);
            return;
        }
        SaveSystem.ReachPoint(id);
    }

    void OnTriggerEnter(Collider other)
    {
        if (saveOnEnter && other.GetComponentInParent<FirstPersonPlayer>() != null) Reach();
    }

    void MovePlayerHere()
    {
        FirstPersonPlayer player = FindFirstObjectByType<FirstPersonPlayer>();
        if (player == null) return;
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.transform.SetPositionAndRotation(transform.position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
        if (cc != null) cc.enabled = true;
        player.SetLookPitch(0f);
        Debug.Log("이어하기: 저장 지점 " + id + "에서 시작", this);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, 0.3f);
        Gizmos.DrawLine(transform.position + Vector3.up * 0.1f, transform.position + Vector3.up * 0.1f + transform.forward * 0.6f);
    }
}