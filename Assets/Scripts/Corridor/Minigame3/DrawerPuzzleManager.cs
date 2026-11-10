
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TeamRoulette.UI;

public class DrawerPuzzleManager : MonoBehaviour
{
    [Header("퍼즐 서랍 A~F")]
    public DrawerPuzzleDrawer[] drawers =
        new DrawerPuzzleDrawer[6];

    [Header("서랍 이동 시간")]
    public float moveDuration = 0.35f;

    [Header("보상 서랍")]
    public DrawerRewardDrawer rewardDrawer;

    [Header("클릭커 획득 후 열릴 통로")]
    public GameObject nextPath;

    [Header("목표 UI")]
    public string objectiveId = "drawer_puzzle";

    [Header("추가 이벤트")]
    public UnityEvent onPuzzleCleared;
    public UnityEvent onClickerCollected;

    // 초기 상태
    // A 닫힘 / B 열림 / C 닫힘
    // D 열림 / E 열림 / F 열림
    readonly bool[] initialStates =
    {
        false, true, false,
        true, true, true
    };

    readonly bool[] states = new bool[6];

    // 상하좌우 연결
    readonly int[][] neighbors =
    {
        new int[] { 1, 3 },
        new int[] { 0, 2, 4 },
        new int[] { 1, 5 },
        new int[] { 0, 4 },
        new int[] { 1, 3, 5 },
        new int[] { 2, 4 }
    };

    bool isAnimating;
    bool isCleared;
    bool hasClicker;

    public bool HasClicker => hasClicker;

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

        if (nextPath != null)
            nextPath.SetActive(false);

        if (rewardDrawer != null)
            rewardDrawer.Prepare(this);

        // 초기 서랍 상태 적용
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

        // 첫 서랍 조작 시 당기기 해금
        if (OptionState.Instance != null &&
            !OptionState.Instance.IsUnlocked(OptionType.Pull))
        {
            OptionState.Instance.Unlock(OptionType.Pull);

            PlayerInteractor.ShowHint(
                "당기기 기능이 해금되었다."
            );
        }

        // 선택한 서랍 반전
        states[index] = !states[index];

        // 상하좌우 서랍도 반전
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
        // 6개 모두 열려야 클리어
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

        Debug.Log("서랍 퍼즐 클리어!");

        // 잠긴 보상 서랍 개방
        if (rewardDrawer != null)
        {
            rewardDrawer.OpenDrawer();
        }
        else
        {
            Debug.LogWarning(
                "보상 서랍이 연결되지 않았습니다.", this
            );
        }

        onPuzzleCleared?.Invoke();

        PlayerInteractor.ShowHint(
            "어디선가 잠금장치가 풀리는 소리가 들린다."
        );
    }

    // 클릭커를 직접 클릭했을 때 호출
    public void CollectClicker()
    {
        if (!isCleared || hasClicker)
            return;

        hasClicker = true;

        // 클릭커 획득 후 다음 길 열기
        if (nextPath != null)
            nextPath.SetActive(true);

        // 목표 완료
        SceneUIContent ui =
            FindFirstObjectByType<SceneUIContent>();

        if (ui != null &&
            !string.IsNullOrEmpty(objectiveId))
        {
            ui.CompleteObjective(objectiveId);
        }

        onClickerCollected?.Invoke();

        PlayerInteractor.ShowHint(
            "겨울이의 클릭커를 얻었다."
        );

        Debug.Log(
            "겨울이 클릭커 획득! 다음 구간 개방"
        );
    }
}
