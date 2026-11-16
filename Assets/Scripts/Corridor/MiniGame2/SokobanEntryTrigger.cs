
using UnityEngine;
using TeamRoulette.UI;

public class SokobanEntryTrigger : MonoBehaviour
{
    [Header("소코반 게임 매니저")]
    public SokobanGameManager gameManager;

    [Header("소코반 진입 시 비활성화할 반복 복도")]
    public GameObject corridorLoopRoot;

    [Header("소코반 진입 시 활성화할 뒤쪽 벽")]
    public GameObject entranceBlocker;


    bool activated;

    private void OnTriggerEnter(Collider other)
    {
        if (activated)
            return;

        FirstPersonPlayer player =
            other.GetComponentInParent<FirstPersonPlayer>();

        if (player == null)
            return;

        activated = true;

        // 소코반 목표 공개
        SceneUIContent ui =
            FindFirstObjectByType<SceneUIContent>();

        if (ui != null)
        {
            ui.RevealObjective("sokoban");
        }

        // 밀기 기능 해금
        if (OptionState.Instance != null)
            OptionState.Instance.Unlock(OptionType.Push);

        // 소코반 게임 시작
        if (gameManager != null)
            gameManager.BeginGame();

        PlayerInteractor.ShowHint(
            "밀기 기능이 해금되었다."
        );

        // 소코반 진입 후 반복 복도 비활성화
        if (corridorLoopRoot != null)
        {
            corridorLoopRoot.SetActive(false);

            Debug.Log(
                "소코반 진입 - 반복 복도 비활성화 완료"
            );
        }

        // 소코반 진입 후 뒤쪽 벽 활성화
if (entranceBlocker != null)
{
    entranceBlocker.SetActive(true);

    Debug.Log("소코반 진입 - 뒤쪽 벽 활성화 완료");
}
    }
}
