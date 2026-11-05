using UnityEngine;
using TeamRoulette.UI;

public class CorridorLoopManager : MonoBehaviour
{
    [Header("반복 복도 상태")]
    public bool hasAnomaly = false;
    public int successCount = 0;

    [Header("이상현상 후보들")]
    public GameObject[] anomalyObjects;

    [Header("이상현상일 때 숨길 평소 오브젝트들")]
    public GameObject[] normalObjects;

    [Header("반복 복도 시작 시 사라질 벽")]
    public GameObject startWall;

    [Header("반복 복도 진입 후 뒤를 막을 벽")]
    public GameObject entranceBlocker;

    [Header("클리어 후 열리는 다음 길")]
    public GameObject nextPath;

    [Header("클리어 후 열리는 문")]
    public Transform exitDoor;

    [Header("문 열리는 높이")]
    public float doorOpenHeight = 3f;

    [Header("문 열리는 속도")]
    public float doorOpenSpeed = 3f;

    [Header("클리어에 필요한 성공 횟수")]
    public int clearCount = 6;

    [Header("목표 UI")]
    public string objectiveId = "corridor_loop";

    [TextArea]
    public string objectiveMessage =
        "반복되는 복도의 이상 현상을 확인하자.";

    int consecutiveAnomalies = 0;
    int consecutiveNormals = 0;

    bool firstLoop = true;
    bool initialized = false;
    bool cleared = false;

    int currentAnomalyIndex = -1;
    int lastAnomalyIndex = -1;

    Vector3 doorClosedPosition;
    Vector3 doorOpenPosition;

    SceneUIContent sceneUI;


    void Start()
    {
        sceneUI =
            FindFirstObjectByType<SceneUIContent>();

        // 출구 문 위치 기억
        if (exitDoor != null)
        {
            doorClosedPosition =
                exitDoor.localPosition;

            doorOpenPosition =
                doorClosedPosition +
                Vector3.up * doorOpenHeight;
        }

        // 다음 길은 처음에는 닫아둠
        if (nextPath != null)
            nextPath.SetActive(false);

        // 뒤를 막는 벽은
        // 반복 복도 들어오기 전에는 없어야 함
        if (entranceBlocker != null)
            entranceBlocker.SetActive(false);

        UpdateObjectiveUI();
    }


    void Update()
    {
        if (!cleared)
            return;

        // 클리어 후 문을 위로 천천히 올림
        if (exitDoor != null)
        {
            exitDoor.localPosition =
                Vector3.MoveTowards(
                    exitDoor.localPosition,
                    doorOpenPosition,
                    doorOpenSpeed * Time.deltaTime
                );
        }
    }


    public void PrepareNextLoop()
    {
        // ============================
        // 반복 복도 최초 진입
        // ============================

        if (!initialized)
        {
            initialized = true;

            // 시작할 때 막고 있던 벽 제거
            if (startWall != null)
                startWall.SetActive(false);

            // 플레이어가 들어왔던 길 막기
            if (entranceBlocker != null)
                entranceBlocker.SetActive(true);

            // 반복 복도 목표 공개
            if (sceneUI == null)
                sceneUI =
                    FindFirstObjectByType<SceneUIContent>();

            if (sceneUI != null)
                sceneUI.RevealObjective(objectiveId);

            UpdateObjectiveUI();

            Debug.Log(
                "반복 복도 진입 - 뒤쪽 입구 차단"
            );
        }


        // 이미 클리어한 경우
        if (cleared)
            return;


        // ============================
        // 클리어 여부
        // ============================

        if (successCount >= clearCount)
        {
            ClearLoop();
            return;
        }


        // ============================
        // 정상 / 이상현상 결정
        // ============================

        // 첫 번째는 무조건 정상
        if (firstLoop)
        {
            firstLoop = false;

            hasAnomaly = false;

            consecutiveNormals = 1;
            consecutiveAnomalies = 0;
        }

        // 정상 2번 연속이면
        // 다음은 반드시 이상현상
        else if (consecutiveNormals >= 2)
        {
            hasAnomaly = true;

            consecutiveAnomalies = 1;
            consecutiveNormals = 0;
        }

        // 이상현상 2번 연속이면
        // 다음은 반드시 정상
        else if (consecutiveAnomalies >= 2)
        {
            hasAnomaly = false;

            consecutiveNormals = 1;
            consecutiveAnomalies = 0;
        }

        // 그 외에는 랜덤
        else
        {
            hasAnomaly =
                Random.value < 0.5f;

            if (hasAnomaly)
            {
                consecutiveAnomalies++;
                consecutiveNormals = 0;
            }
            else
            {
                consecutiveNormals++;
                consecutiveAnomalies = 0;
            }
        }


        // ============================
        // 이상현상 선택
        // ============================

        if (hasAnomaly)
        {
            SelectRandomAnomaly();
        }
        else
        {
            currentAnomalyIndex = -1;
        }


        ApplyLoopState();

        UpdateObjectiveUI();


        if (hasAnomaly)
        {
            Debug.Log(
                "이번 복도: 이상현상 있음 / 번호: "
                + currentAnomalyIndex
            );
        }
        else
        {
            Debug.Log("이번 복도: 정상");
        }
    }


    void SelectRandomAnomaly()
    {
        if (anomalyObjects == null ||
            anomalyObjects.Length == 0)
        {
            currentAnomalyIndex = -1;
            return;
        }

        // 하나뿐이면 그것 사용
        if (anomalyObjects.Length == 1)
        {
            currentAnomalyIndex = 0;
            lastAnomalyIndex = 0;
            return;
        }

        int newIndex;

        // 직전 이상현상과
        // 같은 것은 연속 등장 금지
        do
        {
            newIndex =
                Random.Range(
                    0,
                    anomalyObjects.Length
                );
        }
        while (newIndex == lastAnomalyIndex);

        currentAnomalyIndex = newIndex;
        lastAnomalyIndex = newIndex;
    }


    void ApplyLoopState()
    {
        // ============================
        // 이상현상 전부 끄기
        // ============================

        if (anomalyObjects != null)
        {
            for (
                int i = 0;
                i < anomalyObjects.Length;
                i++
            )
            {
                if (anomalyObjects[i] != null)
                {
                    anomalyObjects[i]
                        .SetActive(false);
                }
            }
        }


        // ============================
        // 선택된 이상현상 하나만 켜기
        // ============================

        if (
            hasAnomaly &&
            anomalyObjects != null &&
            currentAnomalyIndex >= 0 &&
            currentAnomalyIndex <
            anomalyObjects.Length
        )
        {
            GameObject selected =
                anomalyObjects[
                    currentAnomalyIndex
                ];

            if (selected != null)
                selected.SetActive(true);
        }


        // ============================
        // 평소 오브젝트
        // ============================

        if (normalObjects != null)
        {
            for (
                int i = 0;
                i < normalObjects.Length;
                i++
            )
            {
                if (normalObjects[i] != null)
                {
                    normalObjects[i]
                        .SetActive(!hasAnomaly);
                }
            }
        }
    }


    void UpdateObjectiveUI()
    {
        if (sceneUI == null)
            sceneUI =
                FindFirstObjectByType<SceneUIContent>();

        if (sceneUI == null)
            return;

        SceneUIContent.Objective objective =
            sceneUI.objectives.Find(
                x =>
                    x != null &&
                    x.id == objectiveId
            );

        if (objective == null)
            return;

        objective.text =
            objectiveMessage +
            " (" +
            successCount +
            "/" +
            clearCount +
            ")";

        sceneUI.Refresh();
    }


    void ClearLoop()
    {
        cleared = true;

        hasAnomaly = false;
        currentAnomalyIndex = -1;

        // 성공 횟수 마지막 갱신
        UpdateObjectiveUI();


        // ============================
        // 이상현상 전부 끄기
        // ============================

        if (anomalyObjects != null)
        {
            for (
                int i = 0;
                i < anomalyObjects.Length;
                i++
            )
            {
                if (anomalyObjects[i] != null)
                {
                    anomalyObjects[i]
                        .SetActive(false);
                }
            }
        }


        // ============================
        // 평소 오브젝트 복구
        // ============================

        if (normalObjects != null)
        {
            for (
                int i = 0;
                i < normalObjects.Length;
                i++
            )
            {
                if (normalObjects[i] != null)
                {
                    normalObjects[i]
                        .SetActive(true);
                }
            }
        }


        // ============================
        // 다음 길 생성
        // ============================

        if (nextPath != null)
            nextPath.SetActive(true);


        // ============================
        // 목표 완료
        // ============================

        if (sceneUI != null)
            sceneUI.CompleteObjective(objectiveId);


        Debug.Log(
            "반복 복도 클리어! 문이 열립니다."
        );
    }
}