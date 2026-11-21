using UnityEngine;

// 미니게임 도중에는 저장을 멈추는 부품.
// 문제: 목표·해금은 바뀔 때마다 자동 저장된다. 그래서 미니게임 도중(예: 회상)에 끄면
//       "미니게임 속 목표"가 저장된 채로, 몸은 미니게임 밖(방)에서 이어하게 된다.
// 해결: 미니게임이 시작될 때 Begin → 끝날 때 End. 그 사이에는 아무것도 저장하지 않는다.
//       도중에 끄면 미니게임 시작 직전 상태로 이어하고, 미니게임을 처음부터 다시 한다.
//       End 때 한 번에 저장한다.
// 사용: 미니게임 오브젝트에 붙이고
//       - 미니게임이 시작되는 이벤트(가장 먼저 불리는 것)에 SaveGuard.Begin
//       - 미니게임이 끝나는 이벤트(마지막)에 SaveGuard.End
//       (씬이 바뀌거나 이 오브젝트가 꺼지면 저절로 풀림)
// Begin On Start: 오브젝트가 켜질 때 바로 Begin. 단 DevSkip이 인트로를 건너뛴 씬에서는 하지 않음.
public class SaveGuard : MonoBehaviour
{
    [Tooltip("체크하면 이 오브젝트가 켜질 때 바로 Begin (미니게임 오브젝트를 켜서 시작하는 경우)")]
    public bool beginOnStart = false;

    // DevSkip이 인트로를 건너뛸 때 그 씬 이름을 적어 둠 (그 씬에서는 Begin On Start를 안 함)
    public static string introSkippedScene = "";

    bool holding;

    void Start()
    {
        if (beginOnStart && gameObject.scene.name != introSkippedScene) Begin();
    }

    public void Begin()
    {
        if (holding) return;
        holding = true;
        SaveSystem.Hold();
    }

    public void End()
    {
        if (!holding) return;
        holding = false;
        SaveSystem.Release();             // 여기서 한 번에 저장
    }

    // 꺼지거나 씬이 닫히거나 Play를 멈출 때: 저장하지 않고 멈춤만 풀기 (그 순간의 미니게임 도중 상태가 저장되지 않게)
    void OnDisable()
    {
        if (!holding) return;
        holding = false;
        SaveSystem.Release(false);
    }
}