using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TeamRoulette.UI;

// 저장 지점: 여기에 오면 저장하고, 이어하기 때 플레이어를 이 자리에서 시작시키는 부품.
// 저장하는 방법 (셋 중 아무거나, 같이 써도 됨):
//   1) Objective Id (추천, 연결 필요 없음): 그 목표가 새로 보이는 순간 저장. 복도처럼 목표가 바뀔 때마다 한 칸씩 저장할 때.
//   2) 이벤트에 SavePoint.Reach 연결 (미니게임 클리어 등)
//   3) Save On Enter 체크 + Box Collider(Is Trigger): 플레이어가 지나갈 때 저장
// 이어하기 때: 마지막 저장 지점이 이 Id면, 씬 준비가 끝난 뒤 플레이어를 이 자리로 옮기고 On Continued를 실행한다.
//   On Continued: 이 자리에서 시작할 때 앞 구간의 흔적을 정리하는 일 (예: 미로 배경음 끄기, 안개 걷기)
// Hold Until Next Point: 체크하면 이 지점을 저장한 뒤로는 "다음 저장 지점"(또는 씬 전환)까지 저장하지 않는다.
//   → 구간 중간에 끄면 이 지점부터 다시. 구간 도중의 목표·해금이 저장돼서 위치와 어긋나는 걸 막는다.
//   (마지막 지점이면 씬이 바뀔 때까지 = 그 뒤 어디서 끄든 여기부터 다시)
// 사용: 빈 오브젝트를 "이어하기 때 서 있을 자리"에 놓고 붙인다. 파란 화살표(앞)가 바라볼 방향.
//       Id는 겹치지 않게 (예: Corridor_Sokoban). 입장 트리거가 있는 구간이면 트리거 "바로 앞"에 두면
//       이어하기 때 걸어 들어가면서 입장 이벤트가 원래대로 실행된다.
public class SavePoint : MonoBehaviour
{
    [Tooltip("겹치지 않는 이름 (예: Corridor_Sokoban)")]
    public string id = "";

    [Tooltip("이 목표가 새로 보이는 순간 이 지점을 저장 (GameUI 목표 id, 비워 두면 안 씀)")]
    public string objectiveId = "";

    [Tooltip("체크하면 플레이어가 이 오브젝트의 Trigger Collider에 들어올 때 저장")]
    public bool saveOnEnter = false;

    [Tooltip("체크: 이 지점 저장 뒤로는 다음 저장 지점까지 저장 안 함 (구간 중간에 끄면 여기부터 다시)")]
    public bool holdUntilNextPoint = false;

    [Tooltip("이어하기로 이 자리에서 시작할 때 할 일 (앞 구간 정리 등)")]
    public UnityEvent onContinued;

    SceneUIContent content;
    bool wasActive;                          // 지난번 확인 때 목표가 "보이고 아직 안 끝남"이었는지

    IEnumerator Start()
    {
        if (!string.IsNullOrEmpty(objectiveId))
        {
            content = FindFirstObjectByType<SceneUIContent>();
            if (content != null)
            {
                wasActive = ObjectiveActive();
                content.Changed += OnObjectivesChanged;
            }
        }

        yield return null;                   // SaveFlag가 먼저 끝난 모양으로 바꾼 뒤에
        yield return null;
        if (SaveSystem.ConsumePendingPoint(id)) MovePlayerHere();
    }

    void OnDestroy()
    {
        if (content != null) content.Changed -= OnObjectivesChanged;
    }

    // 목표가 바뀔 때마다: 내 목표가 "방금 새로 보이기 시작했으면" 저장
    void OnObjectivesChanged()
    {
        bool active = ObjectiveActive();
        bool justStarted = active && !wasActive;
        wasActive = active;
        if (justStarted && !SaveSystem.IsRestoring) Reach();   // 이어하기로 되살리는 중에 생긴 변화는 무시
    }

    bool ObjectiveActive()
    {
        var item = content.objectives.Find(x => x != null && x.id == objectiveId);
        return item != null && item.visible && !item.completed;
    }

    // 클리어 이벤트 등에 연결
    public void Reach()
    {
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("SavePoint: Id가 비어 있어서 저장하지 못했어요.", this);
            return;
        }
        if (SaveSystem.CurrentPoint == id) return;   // 이미 이 지점 (다시 지나가도 또 저장할 필요 없음)
        SaveSystem.ReachPoint(id, holdUntilNextPoint);
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
        onContinued.Invoke();
        if (holdUntilNextPoint) SaveSystem.HoldForPoint();   // 여기부터 다시 하는 중 → 다음 지점까지 다시 멈춤
        Debug.Log("이어하기: 저장 지점 " + id + "에서 시작", this);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, 0.3f);
        Gizmos.DrawLine(transform.position + Vector3.up * 0.1f, transform.position + Vector3.up * 0.1f + transform.forward * 0.6f);
    }
}