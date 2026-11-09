
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TeamRoulette.UI;

public class DrawerPuzzleManager : MonoBehaviour
{
    [Header("서랍 A~F 순서")]
    public DrawerPuzzleDrawer[] drawers =
        new DrawerPuzzleDrawer[6];

    [Header("서랍 이동 시간")]
    public float moveDuration = 0.35f;

    [Header("클리어 보상")]
    public GameObject collarReward;
    public GameObject nextPath;

    [Header("목표 UI")]
    public string objectiveId = "drawer_puzzle";

    [Header("추가 클리어 연출")]
    public UnityEvent onPuzzleCleared;

    // 초기 상태: A 닫힘, B 열림, C 닫힘
    //           D 열림, E 열림, F 열림
    readonly bool[] initialStates =
    {
        false, true, false,
        true, true, true
    };

    readonly bool[] states = new bool[6];

    // 상하좌우로 연결된 서랍
    readonly int[][] neighbors =
    {
        new int[] { 1, 3 },       // A: B, D
        new int[] { 0, 2, 4 },    // B: A, C, E
        new int[] { 1, 5 },       // C: B, F
        new int[] { 0, 4 },       // D: A, E
        new int[] { 1, 3, 5 },    // E: B, D, F
        new int[] { 2, 4 }        // F: C, E
    };

    bool isAnimating;
    bool isCleared;

    void Awake()
    {
        if (drawers == null || drawers.Length != 6)
        {
            Debug.LogError(
                "서랍 6개를 연결해야 합니다.", this
            );
            enabled = false;
            return;
        }

        for (int i = 0; i < 6; i++)
        {
            if (drawers[i] == null)
            {
                Debug.LogError(
                    $"서랍 {i}가 연결되지 않았습니다.", this
                );
                enabled = false;
                return;
            }

            drawers[i].Initialize(this, i);
            states[i] = initialStates[i];
        }
    }

    void Start()
    {
        if (!enabled)
            return;

        if (collarReward != null)
            collarReward.SetActive(false);

        // 게임 시작 시 서랍의 초기 상태 적용
        for (int i = 0; i < 6; i++)
        {
            drawers[i].MoveToState(
                states[i] ? 2 : 0,
                0.01f
            );
        }
    }

    public void PullDrawer(int index)
    {
        if (!enabled || isAnimating || isCleared)
            return;

        if (index < 0 || index >= 6)
            return;

        // 첫 번째 서랍 조작 시 당기기 해금
        if (OptionState.Instance != null &&
            !OptionState.Instance.IsUnlocked(OptionType.Pull))
        {
            OptionState.Instance.Unlock(
                OptionType.Pull
            );

            PlayerInteractor.ShowHint(
                "당기기 기능이 해금되었다."
            );
        }

        // 클릭한 서랍 열림/닫힘 전환
        states[index] = !states[index];

        // 상하좌우 서랍 상태도 반대로 전환
        foreach (int neighbor in neighbors[index])
        {
            states[neighbor] = !states[neighbor];
        }

        StartCoroutine(MoveDrawers());
    }

    IEnumerator MoveDrawers()
    {
        isAnimating = true;

        for (int i = 0; i < 6; i++)
        {
            // 0: 닫힘, 2: 완전히 열림
            drawers[i].MoveToState(
                states[i] ? 2 : 0,
                moveDuration
            );
        }

        yield return new WaitForSeconds(
            Mathf.Max(0.01f, moveDuration)
        );

        isAnimating = false;

        CheckClear();
    }

    void CheckClear()
    {
        // 서랍 6개가 전부 열려 있는지 확인
        for (int i = 0; i < 6; i++)
        {
            if (!states[i])
                return;
        }

        CompletePuzzle();
    }

    void CompletePuzzle()
    {
        if (isCleared)
            return;

        isCleared = true;

        // 겨울이 목줄 등장
        if (collarReward != null)
            collarReward.SetActive(true);

        // 다음 통로 개방
        if (nextPath != null)
            nextPath.SetActive(true);

        // 목표 UI 완료
        SceneUIContent ui =
            FindFirstObjectByType<SceneUIContent>();

        if (ui != null &&
            !string.IsNullOrEmpty(objectiveId))
        {
            ui.CompleteObjective(objectiveId);
        }

        // 추가 클리어 연출
        onPuzzleCleared?.Invoke();

        PlayerInteractor.ShowHint(
            "서랍이 모두 열렸다. 겨울이의 목줄을 발견했다."
        );

        Debug.Log("서랍 퍼즐 클리어!");
    }
}
