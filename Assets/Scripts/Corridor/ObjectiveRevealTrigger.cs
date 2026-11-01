using UnityEngine;
using TeamRoulette.UI;

public class ObjectiveRevealTrigger : MonoBehaviour
{
    [Header("이 구역에서 보여줄 목표 ID")]
    public string objectiveId;

    [Header("진입하면서 완료할 이전 목표 ID")]
    public string completePreviousId;

    bool triggered;

    void OnTriggerEnter(Collider other)
    {
        if (triggered)
            return;

        FirstPersonPlayer player =
            other.GetComponentInParent<FirstPersonPlayer>();

        if (player == null)
            return;

        SceneUIContent ui =
            FindFirstObjectByType<SceneUIContent>();

        if (ui == null)
        {
            Debug.LogError(
                "SceneUIContent를 찾지 못했습니다."
            );
            return;
        }

        var target =
            ui.objectives.Find(
                x => x != null &&
                     x.id == objectiveId
            );

        if (target == null)
        {
            Debug.LogError(
                "목표 ID를 찾지 못함: " +
                objectiveId
            );

            return;
        }

        triggered = true;

        // 이전 목표 완료
        if (!string.IsNullOrWhiteSpace(
                completePreviousId))
        {
            ui.CompleteObjective(
                completePreviousId
            );
        }

        // 현재 목표 공개
        target.visible = true;

        // UI 즉시 갱신
        ui.Refresh();

        Debug.Log(
            "목표 공개 성공: " +
            objectiveId
        );
    }
}