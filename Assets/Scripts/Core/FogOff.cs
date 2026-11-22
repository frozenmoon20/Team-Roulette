using UnityEngine;

// 이어하기용: 복도 미로 안개를 "바로" 끄는 부품.
// 왜 필요한가:
//   - 원래 안개 끄기(MazeFogController.ClearFog)는 2초 동안 서서히 걷히는 코루틴이다.
//   - 그런데 MazeFogManager가 Minigame1 오브젝트 안에 있고, 반복 복도에 처음 들어가면 Minigame1이 꺼진다.
//   - Loop 지점에서 이어하면 2초가 지나기 전에 반복 복도에 들어가서 Minigame1이 꺼지고,
//     꺼진 오브젝트의 코루틴은 멈추므로 안개가 다 걷히지 못하고 남는다.
// 원리: 코루틴 없이 그 자리에서 안개 설정(RenderSettings.fog)을 끈다. (이어하기는 화면이 밝아지는 중이라 바로 꺼도 티가 안 남)
// 사용: SP_Loop에 붙이고, SavePoint의 On Continued에서 MazeFogController.ClearFog 대신 FogOff.TurnOff를 연결한다.
public class FogOff : MonoBehaviour
{
    public void TurnOff()
    {
        RenderSettings.fog = false;
        Debug.Log("이어하기: 미로 안개 끔", this);
    }
}