using UnityEngine;
using TeamRoulette.UI;

public class LoopCheckTrigger : MonoBehaviour
{
    public CorridorLoopManager manager;

    bool started = false;

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

        Vector3 moveDirection =
            controller.velocity.normalized;

        bool physicalForward =
            Vector3.Dot(
                moveDirection,
                transform.forward
            ) > 0f;

        // 반복 복도 최초 진입
        if (!started)
        {
            started = true;

            currentForwardIsTriggerForward =
                physicalForward;

            SceneUIContent ui =
                FindFirstObjectByType<SceneUIContent>();

            if (ui != null)
            {
                ui.CompleteObjective(
                    "corridor_start"
                );

                ui.RevealObjective(
                    "corridor_loop"
                );
            }

            manager.PrepareNextLoop();

            Debug.Log("반복 복도 시작");

            return;
        }

        bool goingForward =
            physicalForward ==
            currentForwardIsTriggerForward;

        bool correct;

        if (manager.hasAnomaly)
        {
            correct = !goingForward;
        }
        else
        {
            correct = goingForward;
        }

        if (correct)
            manager.successCount++;
        else
            manager.successCount = 0;

        Debug.Log(
            correct
                ? "정답! 현재 성공 횟수: "
                    + manager.successCount
                : "오답! 성공 횟수 초기화"
        );

        currentForwardIsTriggerForward =
            physicalForward;

        manager.PrepareNextLoop();
    }
}