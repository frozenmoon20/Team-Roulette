
using System.Collections;
using UnityEngine;
using TeamRoulette.UI;

public class SokobanGameManager : MonoBehaviour
{
    [Header("맵1 발판 (기존 Goals 연결 유지)")]
    public SokobanGoal[] goals;

    [Header("맵2 발판")]
    public SokobanGoal[] map2Goals;

    [Header("맵1 상자")]
    public SokobanPushBox[] map1Boxes;

    [Header("맵2 상자")]
    public SokobanPushBox[] map2Boxes;

    [Header("R키 초기화 위치")]
    public Transform playerResetPoint;
    public Transform map2ResetPoint;

    [Header("맵1 클리어 시 사라질 벽")]
    public GameObject separatorWall;

    [Header("벽 사라지는 연출")]
    public float wallDisappearDuration = 1.5f;

    [Header("맵2 클리어 후 열릴 다음 길")]
    public GameObject nextPath;

    public bool IsCleared { get; private set; }

    bool gameStarted;
    int currentStage = 1;

    void Start()
    {
        if (nextPath != null)
            nextPath.SetActive(false);

        // 구분 벽은 맵1 진행 중 유지
        if (separatorWall != null)
            separatorWall.SetActive(true);
    }

    void Update()
    {
        if (gameStarted &&
            !IsCleared &&
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
        if (!gameStarted || IsCleared)
            return;

        if (currentStage == 1)
        {
            if (AllGoalsCorrect(goals))
                ClearMap1();
        }
        else if (currentStage == 2)
        {
            if (AllGoalsCorrect(map2Goals))
                ClearGame();
        }
    }

    bool AllGoalsCorrect(SokobanGoal[] targetGoals)
    {
        // 발판이 없으면 클리어되지 않음
        if (targetGoals == null ||
            targetGoals.Length == 0)
            return false;

        foreach (SokobanGoal goal in targetGoals)
        {
            if (goal == null || !goal.IsCorrect)
                return false;
        }

        return true;
    }

    void ClearMap1()
    {
        currentStage = 2;

        // 맵1의 정답 상자는 고정
        if (map1Boxes != null)
        {
            foreach (SokobanPushBox box in map1Boxes)
            {
                if (box != null)
                    box.LockBox();
            }
        }

        // 맵1과 맵2 사이의 벽이 부드럽게 내려가도록 변경
        if (separatorWall != null)
        {
            StartCoroutine(DisappearWall());
        }
        else
        {
            Debug.LogWarning(
                "맵1과 맵2 사이의 벽이 연결되지 않았습니다."
            );
        }

        PlayerInteractor.ShowHint(
            "길이 열렸다. 다음 공간으로 이동하자."
        );

        Debug.Log(
            "소코반 맵1 클리어! 맵2 통로 개방"
        );
    }

    IEnumerator DisappearWall()
    {
        Transform wall = separatorWall.transform;

        Vector3 startPosition = wall.position;

        // 벽의 실제 높이를 기준으로 이동 거리 계산
        Renderer wallRenderer =
            separatorWall.GetComponentInChildren<Renderer>();

        float wallHeight = wallRenderer != null
            ? wallRenderer.bounds.size.y
            : 3f;

        Vector3 targetPosition =
            startPosition + Vector3.down * (wallHeight + 0.2f);

        float duration = Mathf.Max(0.01f, wallDisappearDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            // 부드럽게 시작하고 끝나는 움직임
            t = Mathf.SmoothStep(0f, 1f, t);

            wall.position = Vector3.Lerp(
                startPosition,
                targetPosition,
                t
            );

            yield return null;
        }

        wall.position = targetPosition;

        // 연출 종료 후 벽 완전히 비활성화
        separatorWall.SetActive(false);
    }

    void ClearGame()
    {
        IsCleared = true;

        if (map2Boxes != null)
        {
            foreach (SokobanPushBox box in map2Boxes)
            {
                if (box != null)
                    box.LockBox();
            }
        }

        if (nextPath != null)
            nextPath.SetActive(true);

        SceneUIContent ui =
            FindFirstObjectByType<SceneUIContent>();

        if (ui != null)
            ui.CompleteObjective("sokoban");

        Debug.Log("소코반 전체 클리어!");

        PlayerInteractor.ShowHint(
            "모든 퍼즐을 완료했다."
        );
    }

    void ResetGame()
    {
        SokobanGoal[] currentGoals =
            currentStage == 1 ? goals : map2Goals;

        SokobanPushBox[] currentBoxes =
            currentStage == 1 ? map1Boxes : map2Boxes;

        Transform resetPoint =
            currentStage == 1
                ? playerResetPoint
                : map2ResetPoint;

        if (currentBoxes == null ||
            currentBoxes.Length == 0)
        {
            Debug.LogWarning(
                "현재 맵의 상자가 Inspector에 연결되지 않았습니다."
            );
            return;
        }

        if (currentGoals != null)
        {
            foreach (SokobanGoal goal in currentGoals)
            {
                if (goal != null)
                    goal.ResetGoal();
            }
        }

        foreach (SokobanPushBox box in currentBoxes)
        {
            if (box != null)
                box.ResetBox();
        }

        FirstPersonPlayer player =
            FindFirstObjectByType<FirstPersonPlayer>();

        if (player != null && resetPoint != null)
        {
            CharacterController controller =
                player.GetComponent<CharacterController>();

            if (controller != null)
                controller.enabled = false;

            player.transform.position = resetPoint.position;
            player.transform.rotation = resetPoint.rotation;

            if (controller != null)
                controller.enabled = true;
        }

        PlayerInteractor.ShowHint(
            "현재 퍼즐을 처음부터 다시 시작한다."
        );
    }
}
