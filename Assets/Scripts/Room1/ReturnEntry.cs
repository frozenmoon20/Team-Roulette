using UnityEngine;
using UnityEngine.Events;

// 미로 복도(Corridor)에서 침실 씬(Test_NightRoom)으로 돌아올 때, 침실을 처음부터가 아니라 "일자 복도 복귀 지점"에서 시작하게 한다.
// 원리: DevSkip과 같은 방식 (모든 Awake가 Start보다 먼저 실행된다는 점을 이용)
//   - 돌아오는 쪽(SceneGate의 Return To Hallway)이 Pending = true로 표시해 두고 씬을 넘긴다.
//   - Awake: 표시가 있으면 오프닝 대사(→ 미니게임1 시작)의 Play On Start를 끄고, 복귀 때 필요 없는 것들(검은 화면, 복도 트리거 등)을 끈다.
//   - Start: 플레이어를 복귀 지점으로 옮기고, onReturn(목표 바꾸기 등)과 복귀 대사("거실로 가자" 등)를 실행한다.
//   - 해금 상태(OptionState)는 원래 씬이 바뀌어도 유지되므로 손·이동·달리기 등은 그대로다.
// 표시가 없으면(평소 게임 시작) 아무것도 안 한다.
// 사용: 침실 씬에 빈 오브젝트 "ReturnEntry"를 만들어 붙이고 칸을 채운다.
public class ReturnEntry : MonoBehaviour
{
    public static bool Pending;              // 미로 복도에서 돌아오는 중인지 (SceneGate가 켬)

    [Header("복귀 때 끌 것")]
    public DialogueStep openingDialogue;     // 오프닝 대사 (Dialogue_Opening) → 미니게임1이 시작되지 않게
    public GameObject[] hideOnReturn;        // 검은 화면(Blackout), 달리기 해금 트리거(Zone_HallStart), 전화 트리거(Zone_Phone) 등

    [Header("복귀 지점")]
    public Transform startPoint;             // 일자 복도에서 플레이어가 설 자리 (파란 화살표가 바라볼 방향)

    [Header("복귀 후")]
    public DialogueStep returnDialogue;      // 돌아오자마자 나올 대사 ("…거실로 내려가 보자." 등, 비워도 됨)
    public UnityEvent onReturn;              // 그 밖에 할 일 (목표 바꾸기: GameUI의 RevealObjective 등)

    bool active;

    void Awake()
    {
        active = Pending;
        if (!active) return;

        if (openingDialogue != null) openingDialogue.playOnStart = false;
        foreach (GameObject g in hideOnReturn) if (g != null) g.SetActive(false);
    }

    void Start()
    {
        if (!active) return;
        Pending = false;

        if (startPoint != null)
        {
            FirstPersonPlayer player = FindFirstObjectByType<FirstPersonPlayer>();
            if (player != null) player.Teleport(startPoint.position, startPoint.rotation, Quaternion.identity);
        }

        onReturn.Invoke();
        if (returnDialogue != null) returnDialogue.Play();
        Debug.Log("ReturnEntry: 미로 복도에서 돌아옴 → 일자 복도 복귀 지점에서 시작");
    }
}