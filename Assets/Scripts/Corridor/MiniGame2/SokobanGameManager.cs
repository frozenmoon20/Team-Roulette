using UnityEngine;

public class SokobanGameManager : MonoBehaviour
{
    public SokobanGoal[] goals;
    public Transform playerResetPoint;

    public bool IsCleared { get; private set; }

    bool gameStarted;
    SokobanPushBox[] boxes;

    void Start()
    {
        boxes = FindObjectsByType<SokobanPushBox>(
            FindObjectsSortMode.None
        );
    }

    void Update()
    {
        if (gameStarted && !IsCleared &&
            Input.GetKeyDown(KeyCode.R))
        {
            ResetGame();
        }
    }

    public void BeginGame()
    {
        gameStarted = true;
    }

    public void CheckClear()
    {
        if (IsCleared)
            return;

        foreach (SokobanGoal goal in goals)
        {
            if (goal == null || !goal.IsCorrect)
                return;
        }

        ClearGame();
    }

    void ClearGame()
    {
        IsCleared = true;

        foreach (SokobanPushBox box in boxes)
        {
            box.LockBox();
        }

        Debug.Log("소코반 미니게임 클리어!");
        PlayerInteractor.ShowHint(
            "모든 상자를 올바른 위치에 놓았다."
        );
    }

    void ResetGame()
    {
        foreach (SokobanGoal goal in goals)
        {
            if (goal != null)
                goal.ResetGoal();
        }

        foreach (SokobanPushBox box in boxes)
        {
            if (box != null)
                box.ResetBox();
        }

        FirstPersonPlayer player =
            FindFirstObjectByType<FirstPersonPlayer>();

        if (player != null && playerResetPoint != null)
        {
            CharacterController controller =
                player.GetComponent<CharacterController>();

            if (controller != null)
                controller.enabled = false;

            player.transform.position =
                playerResetPoint.position;

            player.transform.rotation =
                playerResetPoint.rotation;

            if (controller != null)
                controller.enabled = true;
        }

        Debug.Log("소코반 리셋!");
        PlayerInteractor.ShowHint(
            "퍼즐을 처음부터 다시 시작한다."
        );
    }
}