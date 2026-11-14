
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

    [Header("맵1 효과음")]
    public AudioClip map1ClearSound;
    public AudioClip separatorWallSound;

    [Range(0f, 1f)]
    public float soundVolume = 0.8f;

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
        // 기존 단계 전환
        currentStage = 2;

        // 추가: 맵1 클리어 효과음
        AudioManager.Play(
            map1ClearSound,
            soundVolume
        );

        // 기존 맵1 상자 고정
        if (map1Boxes != null)
        {
            foreach (SokobanPushBox box in map1Boxes)
            {
                if (box != null)
                    box.LockBox();
            }
        }

        // 기존 구분 벽 하강
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

        Renderer wallRenderer =
            separatorWall.GetComponentInChildren<Renderer>();

        float wallHeight = wallRenderer != null
            ? wallRenderer.bounds.size.y
            : 3f;

        Vector3 targetPosition =
            startPosition +
            Vector3.down * (wallHeight + 0.2f);

        float duration = Mathf.Max(
            0.01f,
            wallDisappearDuration
        );

        float elapsed = 0f;

        // 추가: 벽이 내려가기 시작할 때 효과음
        AudioManager.Play(
            separatorWallSound,
            soundVolume
        );

        // 기존 벽 하강 애니메이션
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / duration
            );

            t = Mathf.SmoothStep(0f, 1f, t);

            wall.position = Vector3.Lerp(
                startPosition,
                targetPosition,
                t
            );

            yield return null;
        }

        wall.position = targetPosition;

        separatorWall.SetActive(false);
    }

    public void CompleteMap2()
    {
        if (IsCleared)
            return;

        if (!gameStarted || currentStage != 2)
        {
            Debug.LogWarning(
                "소코반 맵2 완료 요청이 들어왔지만 " +
                "아직 맵2 진행 단계가 아닙니다.",
                this
            );
            return;
        }

        Debug.Log("소코반 맵2 클리어 신호 수신!");
        ClearGame();
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
