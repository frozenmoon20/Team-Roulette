using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// "이 행동을 끝냈다"를 저장하고, 다시 켰을 때 끝난 모양으로 되돌려 놓는 부품.
// 예: 문 열기, 텀블러(잡기) 완료, 핸드크림 완료, 서랍 열기, 미니게임 클리어 …
// 사용:
//   1) 그 행동을 맡은 오브젝트(또는 아무 빈 오브젝트)에 붙이고, Id에 겹치지 않는 이름을 적는다.
//      예: Bedroom_DoorOpened, Bedroom_Tumbler, Corridor_Sokoban, Living_Curtain
//   2) 행동이 끝나는 이벤트(미니게임 클리어, 대사 끝 등)에 SaveFlag.Done을 연결한다 → 그 순간 저장됨
//   3) "끝난 모양"을 아래 칸으로 정해 둔다 (다시 켰을 때 이미 끝나 있으면 씬이 열리자마자 이렇게 바뀜)
//        - Hide If Done : 숨길 것 (미니게임 입구, 버린 쓰레기, 막고 있던 벽 …)
//        - Show If Done : 보일 것 (놓아 둔 물건, 열린 통로 …)
//        - Pose Target  : 문 경첩·서랍처럼 위치·각도만 바뀌는 것 → 완료 위치·각도를 기록해 둠
//        - On Restored  : 그 밖에 할 일 (목표창 갱신, DialogueInteractable.MarkUsed 등)
// 원리: 씬은 다시 켜면 처음 모양으로 열린다. 그래서 저장된 "완료 목록"에 이 Id가 있으면,
//       다른 스크립트들이 준비를 마친 뒤(한 프레임 뒤) 끝난 모양으로 덮어쓴다.
// 완료 위치·각도 기록법 (Pose Target): 에디터에서 문을 "열린 각도"로 돌려 놓고 →
//       이 컴포넌트 오른쪽 ⋮ → "지금 모양을 완료 모양으로 기록" → 문을 다시 닫힌 각도로 돌려 놓기
public class SaveFlag : MonoBehaviour
{
    [Tooltip("겹치지 않는 이름 (예: Bedroom_DoorOpened)")]
    public string id = "";

    [Header("다시 켰을 때 이미 끝나 있으면")]
    public GameObject[] hideIfDone;
    public GameObject[] showIfDone;

    [Header("위치·각도만 바뀌는 것 (문 경첩, 서랍 등)")]
    public Transform poseTarget;
    public bool setLocalPosition = false;
    public Vector3 doneLocalPosition;
    public bool setLocalRotation = false;
    public Vector3 doneLocalEuler;

    [Header("그 밖에 할 일 (목표창 갱신 등)")]
    public UnityEvent onRestored;

    public bool IsDone => SaveSystem.HasFlag(id);

    IEnumerator Start()
    {
        yield return null;                   // 다른 스크립트들의 Start가 끝난 뒤에 덮어쓰기
        if (!SaveSystem.HasFlag(id)) yield break;

        foreach (GameObject g in hideIfDone) if (g != null) g.SetActive(false);
        foreach (GameObject g in showIfDone) if (g != null) g.SetActive(true);
        if (poseTarget != null)
        {
            if (setLocalPosition) poseTarget.localPosition = doneLocalPosition;
            if (setLocalRotation) poseTarget.localEulerAngles = doneLocalEuler;
        }
        onRestored.Invoke();
    }

    // 행동이 끝나는 이벤트에 연결
    public void Done()
    {
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("SaveFlag: Id가 비어 있어서 저장하지 못했어요.", this);
            return;
        }
        SaveSystem.SetFlag(id);
    }

    [ContextMenu("지금 모양을 완료 모양으로 기록")]
    void RecordPose()
    {
        if (poseTarget == null) { Debug.LogWarning("SaveFlag: Pose Target을 먼저 넣어 주세요.", this); return; }
        doneLocalPosition = poseTarget.localPosition;
        doneLocalEuler = poseTarget.localEulerAngles;
        Debug.Log("SaveFlag: 완료 모양 기록 (위치 " + doneLocalPosition + ", 각도 " + doneLocalEuler + "). 이제 원래 모양으로 되돌려 놓으세요.", this);
    }
}