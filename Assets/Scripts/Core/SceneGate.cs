using UnityEngine;

// 이벤트 칸에서 다른 씬으로 넘어가게 하는 연결용 스크립트 (SceneFader로 까매졌다가 넘어가서 밝아짐).
// 사용: 아무 오브젝트에 붙이고 Scene Name을 적은 뒤, 이벤트 칸(TriggerZone의 On Enter, 대사의 On Finished 등)에서 Go()를 부른다.
//   - Return To Hallway 체크: 침실 씬(Test_NightRoom)을 "처음부터"가 아니라 "일자 복도 복귀 지점"에서 시작하게 함 (ReturnEntry가 처리)
//     → 미로 복도(Corridor)에서 침실 씬의 일자 복도로 돌아갈 때 체크한다.
public class SceneGate : MonoBehaviour
{
    public string sceneName = "";            // 넘어갈 씬 이름 (Build Profiles의 Scene List에 있어야 함)
    public bool returnToHallway = false;     // 체크: 침실 씬을 일자 복도 복귀 지점에서 시작
    public float fadeOutTime = 1f;           // 까매지는 시간 (초)
    public float fadeInTime = 1.2f;          // 넘어간 뒤 밝아지는 시간 (초)
    public Color fadeColor = Color.black;    // 넘어갈 때 화면 색 (하얗게 넘어가려면 흰색)

    bool used;

    public void Go()
    {
        if (used || SceneFader.IsBusy) return;
        used = true;

        // 넘어가는 동안 못 움직이게
        FirstPersonPlayer player = FindFirstObjectByType<FirstPersonPlayer>();
        if (player != null) { player.canMove = false; player.StopMovement(); }

        if (returnToHallway) ReturnEntry.Pending = true;
        SceneFader.Go(sceneName, fadeOutTime, fadeInTime, 0.3f, false, fadeColor);
    }
}