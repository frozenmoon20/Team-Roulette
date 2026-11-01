using UnityEngine;
using TeamRoulette.UI;

public class Minigame1EntryTrigger : MonoBehaviour
{
    public GameObject mazeRoot;
    public GameObject entranceBlocker;

    bool activated;

    void OnTriggerEnter(Collider other)
    {
        if (activated)
            return;

        FirstPersonPlayer player =
            other.GetComponentInParent<FirstPersonPlayer>();

        if (player == null)
            return;

        activated = true;

        SceneUIContent ui =
            FindFirstObjectByType<SceneUIContent>();

        if (ui != null)
        {
            ui.CompleteObjective("maze");
            ui.RevealObjective("minigame1");

            Debug.Log(
                "미로 완료 → 미니게임1 목표 공개"
            );
        }
        else
        {
            Debug.LogError(
                "SceneUIContent를 찾지 못했습니다."
            );
        }

        if (mazeRoot != null)
            Destroy(mazeRoot);

        if (entranceBlocker != null)
            entranceBlocker.SetActive(true);
    }
}