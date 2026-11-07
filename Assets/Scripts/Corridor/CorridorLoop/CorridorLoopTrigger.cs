
using UnityEngine;
using TeamRoulette.UI;

public class LoopCheckTrigger : MonoBehaviour
{
    public CorridorLoopManager manager;
    public GameObject minigame1Root;

    bool started = false;
    bool currentForwardIsTriggerForward = true;

    private void OnTriggerEnter(Collider other)
    {
        FirstPersonPlayer player =
            other.GetComponentInParent<FirstPersonPlayer>();
            
        // 반복 복도 클리어 후에는 모든 판정 중지
        if (manager.IsCleared)
            return;

        if (player == null || manager == null)
            return;

        CharacterController controller =
            player.GetComponent<CharacterController>();

        if (controller == null)
            return;

        Vector3 moveDirection =
            controller.velocity.normalized;

        bool physicalForward =
            Vector3.Dot(moveDirection, transform.forward) > 0f;

        if (!started)
        {
            started = true;
            currentForwardIsTriggerForward = physicalForward;

            SceneUIContent ui =
                FindFirstObjectByType<SceneUIContent>();

            if (ui != null)
            {
                ui.CompleteObjective("corridor_start");
                ui.RevealObjective("corridor_loop");
            }

            manager.PrepareNextLoop();

            if (minigame1Root != null)
                minigame1Root.SetActive(false);

            Debug.Log("반복 복도 시작 - 미니게임 1 비활성화");
            return;
        }

        bool goingForward =
            physicalForward == currentForwardIsTriggerForward;

        bool correct;

        if (manager.hasAnomaly)
            correct = !goingForward;
        else
            correct = goingForward;

        if (correct)
            manager.successCount++;
        else
            manager.successCount = 0;

        Debug.Log(
            correct
                ? "정답! 현재 성공 횟수: " + manager.successCount
                : "오답! 성공 횟수 초기화"
        );

        currentForwardIsTriggerForward = physicalForward;
        manager.PrepareNextLoop();
    }
}
