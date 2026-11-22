using UnityEngine;

// 이어하기용: 복도 서랍 퍼즐을 "이미 끝낸 모양"으로 만드는 부품 (미니게임4 지점에서 이어할 때)
//   - 서랍 6개: 클리어 뒤처럼 전부 닫힘 + 더 이상 당겨지지 않음
//   - 금고: 문이 열린 모양 + 클릭해도 반응 없음 (클릭커는 ClickerRestore가 손에 쥐여 줌)
// 원리: 신비 스크립트(DrawerPuzzleManager, DrawerRewardDrawer)는 고치지 않고, 공개된 칸만 써서 모양을 맞춘다.
//   - DrawerPuzzleManager를 끄면(enabled = false) 서랍을 눌러도 아무 일이 없다 (원래 코드가 그렇게 되어 있음)
//   - 금고 문은 원래 여는 방식과 같은 계산(경첩 기준 회전)으로 열린 각도에 바로 놓는다
// 사용: SP_LongCorridor에 붙이고 Manager 칸에 Minigame3_DrawerPuzzle의 DrawerPuzzleManager를 넣는다.
//       SavePoint의 On Continued에 DrawerPuzzleRestore.RestoreCleared 연결 (ClickerRestore.GiveClicker보다 위에)
public class DrawerPuzzleRestore : MonoBehaviour
{
    public DrawerPuzzleManager manager;

    public void RestoreCleared()
    {
        if (manager == null) { Debug.LogWarning("DrawerPuzzleRestore: Manager 칸이 비어 있어요.", this); return; }

        // 서랍: 전부 닫힌 자리로 바로 + 퍼즐 끄기
        foreach (DrawerPuzzleDrawer d in manager.drawers)
            if (d != null) d.MoveToState(0, 0.01f);
        manager.enabled = false;

        // 금고: 문을 열린 각도로 바로 + 클릭 막기
        DrawerRewardDrawer safe = manager.rewardDrawer;
        if (safe == null) return;

        Transform door = safe.safeDoor != null ? safe.safeDoor : safe.transform;
        if (safe.safeHinge != null)
        {
            Vector3 axis = door.parent != null ? door.parent.up : Vector3.up;
            if (axis.sqrMagnitude < 0.0001f) axis = Vector3.up;
            axis.Normalize();
            Quaternion rot = Quaternion.AngleAxis(safe.openAngle, axis);
            Vector3 hinge = safe.safeHinge.position;
            door.SetPositionAndRotation(hinge + rot * (door.position - hinge), rot * door.rotation);
        }

        // 금고를 눌러도 "잠겨 있다"가 안 나오게: 금고 쪽 Collider를 끔 (손에 든 클릭커는 제외)
        foreach (Collider c in safe.GetComponentsInChildren<Collider>(true))
        {
            if (safe.clicker != null && c.transform.IsChildOf(safe.clicker.transform)) continue;
            c.enabled = false;
        }
        safe.enabled = false;

        Debug.Log("이어하기: 서랍 퍼즐을 끝낸 모양으로", this);
    }
}