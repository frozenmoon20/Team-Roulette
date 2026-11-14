
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TeamRoulette.UI;

public class DrawerPuzzleManager : MonoBehaviour
{
    [Header("퍼즐 서랍 A~F")]
    public DrawerPuzzleDrawer[] drawers =
        new DrawerPuzzleDrawer[6];

    [Header("서랍 이동 설정")]
    public float moveDuration = 0.35f;

    [Header("R키 초기화")]
    public KeyCode resetKey = KeyCode.R;
    public float resetRange = 5f;

    [Header("클리어 후 자동 닫힘")]
    public float closeDelayAfterClear = 4f;

    [Header("보상 금고")]
    public DrawerRewardDrawer rewardDrawer;

    [Header("클릭커 획득 후 열릴 통로")]
    public GameObject nextPath;

    [Header("목표 UI")]
    public string objectiveId = "drawer_puzzle";

    [Header("추가 이벤트")]
    public UnityEvent onPuzzleCleared;
    public UnityEvent onClickerCollected;

    [Header("서랍 효과음")]
    public AudioClip drawerOpenSound;
    public AudioClip drawerCloseSound;

    [Range(0f, 1f)]
    public float drawerSoundVolume = 0.8f;

    [Header("서랍 퍼즐 클리어 효과음")]
public AudioClip puzzleClearSound;

[Range(0f, 1f)]
public float clearSoundVolume = 0.8f;

    /*
     * 실제 서랍 배치
     *
     *    A   C   E
     *    B   D   F
     *
     * 배열 인덱스
     * A = 0
     * B = 1
     * C = 2
     * D = 3
     * E = 4
     * F = 5
     */

    // 초기 상태
    // A, D 열림
    // B, C, E, F 닫힘
    readonly bool[] initialStates =
    {
        true, false, false,
        true, false, false
    };

    readonly bool[] states = new bool[6];

    /*
     * 서랍 연결 규칙
     *
     * A - B, C, D
     * B - A, D
     * C - A, D, E
     * D - A, B, C, F
     * E - C, F
     * F - D, E
     */
    readonly int[][] neighbors =
    {
        new int[] { 1, 2, 3 },    // A
        new int[] { 0, 3 },       // B
        new int[] { 0, 3, 4 },    // C
        new int[] { 0, 1, 2, 5 }, // D
        new int[] { 2, 5 },       // E
        new int[] { 3, 4 }        // F
    };

    bool isAnimating;
    bool isCleared;
    bool hasClicker;

    Coroutine moveRoutine;
    Coroutine resetRoutine;
    Coroutine closeRoutine;

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

        // 기존 금고 준비
        if (rewardDrawer != null)
            rewardDrawer.Prepare(this);
        else
            Debug.LogWarning(
                "Memory_Safe가 연결되지 않았습니다.", this
            );

        // 초기 서랍 상태 적용
        // 게임 시작 시 효과음은 재생하지 않음
        for (int i = 0; i < 6; i++)
        {
            drawers[i].MoveToState(
                states[i] ? 2 : 0,
                0.01f
            );
        }
    }

    void Update()
    {
        // 게임 화면에서만 R키 입력 처리
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        if (!Input.GetKeyDown(resetKey))
            return;

        // 클리어 이후에는 초기화 불가능
        if (isCleared)
            return;

        // 서랍 근처에서만 R키 초기화
        if (!IsPlayerNearDrawers())
            return;

        ResetPuzzle();
    }

    // 플레이어가 서랍 근처에 있는지 확인
    bool IsPlayerNearDrawers()
    {
        Camera playerCamera = null;

        PlayerInteractor interactor =
            FindFirstObjectByType<PlayerInteractor>();

        if (interactor != null)
            playerCamera = interactor.playerCamera;

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (playerCamera == null)
            return false;

        Vector3 playerPosition =
            playerCamera.transform.position;

        for (int i = 0; i < 6; i++)
        {
            if (drawers[i] == null)
                continue;

            float distance = Vector3.Distance(
                playerPosition,
                drawers[i].transform.position
            );

            if (distance <= resetRange)
                return true;
        }

        return false;
    }

    // 서랍 열기/닫기 효과음 재생
    void PlayDrawerMoveSound(bool opening)
    {
        AudioClip clip = opening
            ? drawerOpenSound
            : drawerCloseSound;

        AudioManager.Play(
            clip,
            drawerSoundVolume
        );
    }

    // 서랍 클릭
    public void PullDrawer(int index)
    {
        if (!enabled || isAnimating || isCleared)
            return;

        if (index < 0 || index >= 6)
            return;

        // 첫 서랍 조작 시 당기기 기능 해금
        if (OptionState.Instance != null &&
            !OptionState.Instance.IsUnlocked(OptionType.Pull))
        {
            OptionState.Instance.Unlock(OptionType.Pull);

            PlayerInteractor.ShowHint(
                "당기기 기능이 해금되었다."
            );
        }

        // 클릭한 서랍이 열리는지 닫히는지 확인
        bool opening = !states[index];

        // 클릭한 서랍 상태 반전
        states[index] = !states[index];

        // 연결된 서랍 상태 반전
        foreach (int neighbor in neighbors[index])
        {
            states[neighbor] = !states[neighbor];
        }

        // 연동 서랍이 여러 개여도 효과음은 한 번만 재생
        PlayDrawerMoveSound(opening);

        moveRoutine = StartCoroutine(MoveDrawers());
    }

    // 서랍 상태에 맞게 이동
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
        moveRoutine = null;

        CheckClear();
    }

    // R키 퍼즐 초기화
    public void ResetPuzzle()
    {
        if (!enabled || isCleared)
            return;

        // 기존 이동 중단
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        // 이전 초기화 진행 중이라면 중단
        if (resetRoutine != null)
        {
            StopCoroutine(resetRoutine);
            resetRoutine = null;
        }

        isAnimating = true;

        // 초기화 시 닫기 효과음 한 번 재생
        PlayDrawerMoveSound(false);

        // 처음 상태로 복구
        for (int i = 0; i < 6; i++)
        {
            states[i] = initialStates[i];

            drawers[i].MoveToState(
                states[i] ? 2 : 0,
                moveDuration
            );
        }

        resetRoutine = StartCoroutine(
            FinishResetRoutine()
        );

        PlayerInteractor.ShowHint(
            "서랍이 처음 상태로 돌아간다."
        );

        Debug.Log("서랍 퍼즐 초기화!");
    }

    IEnumerator FinishResetRoutine()
    {
        yield return new WaitForSeconds(
            Mathf.Max(0.01f, moveDuration)
        );

        isAnimating = false;
        resetRoutine = null;
    }

    // 퍼즐 클리어 조건 확인
    void CheckClear()
    {
        // 모든 서랍이 열려야 클리어
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

    // 추가: 모든 서랍을 열었을 때 클리어 효과음
    // isCleared로 중복 재생 방지
    AudioManager.Play(
        puzzleClearSound,
        clearSoundVolume
    );

    // 기존 금고 잠금 해제
    // 문은 플레이어가 직접 클릭해야 열림
    if (rewardDrawer != null)
    {
        rewardDrawer.OpenDrawer();
    }
    else
    {
        Debug.LogWarning(
            "보상 금고가 연결되지 않았습니다.", this
        );
    }

    // 기존 클리어 이벤트 유지
    onPuzzleCleared?.Invoke();

    PlayerInteractor.ShowHint(
        "어디선가 잠금장치가 풀리는 소리가 들린다."
    );

    // 4초 후 모든 서랍 자동 닫힘
    closeRoutine = StartCoroutine(
        CloseAllDrawersAfterDelay()
    );
}

    IEnumerator CloseAllDrawersAfterDelay()
    {
        yield return new WaitForSeconds(
            Mathf.Max(0f, closeDelayAfterClear)
        );

        isAnimating = true;

        // 자동 닫힘 시 닫기 효과음 한 번 재생
        PlayDrawerMoveSound(false);

        // 모든 서랍을 닫힌 상태로 변경
        for (int i = 0; i < 6; i++)
        {
            states[i] = false;

            drawers[i].MoveToState(
                0,
                moveDuration
            );
        }

        Debug.Log(
            "퍼즐 클리어 후 모든 서랍 닫기 시작"
        );

        yield return new WaitForSeconds(
            Mathf.Max(0.01f, moveDuration)
        );

        isAnimating = false;
        closeRoutine = null;

        Debug.Log(
            "모든 서랍이 닫혔습니다."
        );
    }

    // 클릭커 획득 시 호출
    public void CollectClicker()
    {
        if (!isCleared || hasClicker)
            return;

        hasClicker = true;

        // 다음 통로 개방
        if (nextPath != null)
            nextPath.SetActive(true);

        // 목표 UI 완료 처리
        SceneUIContent ui =
            FindFirstObjectByType<SceneUIContent>();

        if (ui != null &&
            !string.IsNullOrEmpty(objectiveId))
        {
            ui.CompleteObjective(objectiveId);
        }

        onClickerCollected?.Invoke();

        PlayerInteractor.ShowHint(
            "Gyeoul의 클릭커를 얻었다."
        );

        Debug.Log(
            "Gyeoul 클릭커 획득! 다음 구간 개방"
        );
    }
}
