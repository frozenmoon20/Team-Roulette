using UnityEngine;

// (거실용) 진행 단계 하나가 끝났을 때 집 지도 게이지를 한 칸 채운다.
// 사용: 아무 오브젝트에 붙이고 Step Name을 적은 뒤(예: 커튼), 그 단계가 끝나는 이벤트 칸
//       (On Finished, On Cleared 등)에서 [+] → 이 오브젝트 → ProgressStep > Complete 를 연결한다.
//   같은 이름은 여러 번 불러도 한 번만 센다. 팀원 스크립트는 고칠 필요 없음.
public class ProgressStep : MonoBehaviour
{
    public string stepName = "커튼";   // 커튼 / 세수 / 밥 / 게시판 / 장난감 / 달력 / TV

    public void Complete()
    {
        HouseProgress.CompleteLivingStep(stepName);
    }
}