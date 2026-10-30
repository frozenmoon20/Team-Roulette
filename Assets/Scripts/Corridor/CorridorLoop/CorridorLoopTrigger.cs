using UnityEngine;

public class LoopCheckTrigger : MonoBehaviour
{
    public CorridorLoopManager manager;

    bool started = false;

    // 현재 플레이어 기준의 '앞'이
    // Trigger의 forward 방향과 같은지 저장
    bool currentForwardIsTriggerForward = true;

    private void OnTriggerEnter(Collider other)
    {
        FirstPersonPlayer player =
            other.GetComponentInParent<FirstPersonPlayer>();

        if (player == null || manager == null)
            return;

        CharacterController controller =
            player.GetComponent<CharacterController>();

        if (controller == null)
            return;

        Vector3 moveDirection = controller.velocity.normalized;

        bool physicalForward =
            Vector3.Dot(moveDirection, transform.forward) > 0f;

        // 최초 진입
        if (!started)
        {
            started = true;

            // 처음 들어오는 방향을 플레이어의 '앞'으로 저장
            currentForwardIsTriggerForward = physicalForward;

            manager.PrepareNextLoop();

            Debug.Log("반복 복도 시작");
            return;
        }

        // 현재 플레이어 기준으로 앞으로 간 건지 판단
        bool goingForward =
            physicalForward == currentForwardIsTriggerForward;

        bool correct;

        if (manager.hasAnomaly)
        {
            // 이상현상 → 뒤로 돌아가야 정답
            correct = !goingForward;
        }
        else
        {
            // 정상 → 그대로 진행해야 정답
            correct = goingForward;
        }

        if (correct)
            manager.successCount++;
        else
            manager.successCount = 0;

        Debug.Log(
            correct
                ? "정답! 현재 성공 횟수: " + manager.successCount
                : "오답! 성공 횟수 초기화"
        );

        // 이번에 플레이어가 선택한 방향이
        // 다음 복도의 새로운 '앞' 방향이 됨
        currentForwardIsTriggerForward = physicalForward;

        manager.PrepareNextLoop();
    }
}