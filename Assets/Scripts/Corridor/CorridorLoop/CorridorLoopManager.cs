
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

    [Header("클리어 후 회전할 문 경첩")]
    public Transform exitDoor;

    [Header("문 회전 각도")]
    public float doorOpenAngle = 90f;

    [Header("문 회전 속도")]
    public float doorOpenSpeed = 90f;

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

    Quaternion doorClosedRotation;
    Quaternion doorOpenRotation;

    SceneUIContent sceneUI;


    void Start()
    {
        sceneUI =
            FindFirstObjectByType<SceneUIContent>();

        if (exitDoor != null)
        {
            doorClosedRotation = exitDoor.localRotation;

            doorOpenRotation =
                doorClosedRotation *
                Quaternion.Euler(0f, doorOpenAngle, 0f);
        }

        if (nextPath != null)
            nextPath.SetActive(false);

        if (entranceBlocker != null)
            entranceBlocker.SetActive(false);

        UpdateObjectiveUI();
    }


    void Update()
    {
        if (!cleared)
            return;

        if (exitDoor != null)
        {
            exitDoor.localRotation =
                Quaternion.RotateTowards(
                    exitDoor.localRotation,
                    doorOpenRotation,
                    doorOpenSpeed * Time.deltaTime
                );
        }
    }


    public void PrepareNextLoop()
    {
        if (!initialized)
        {
            initialized = true;

            if (startWall != null)
                startWall.SetActive(false);

            if (entranceBlocker != null)
                entranceBlocker.SetActive(true);

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

        if (cleared)
            return;

        if (successCount >= clearCount)
        {
            ClearLoop();
            return;
        }

        if (firstLoop)
        {
            firstLoop = false;

            hasAnomaly = false;

            consecutiveNormals = 1;
            consecutiveAnomalies = 0;
        }
        else if (consecutiveNormals >= 2)
        {
            hasAnomaly = true;

            consecutiveAnomalies = 1;
            consecutiveNormals = 0;
        }
        else if (consecutiveAnomalies >= 2)
        {
            hasAnomaly = false;

            consecutiveNormals = 1;
            consecutiveAnomalies = 0;
        }
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

        if (anomalyObjects.Length == 1)
        {
            currentAnomalyIndex = 0;
            lastAnomalyIndex = 0;
            return;
        }

        int newIndex;

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

        UpdateObjectiveUI();

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

        if (nextPath != null)
            nextPath.SetActive(true);

        if (sceneUI != null)
            sceneUI.CompleteObjective(objectiveId);

        Debug.Log(
            "반복 복도 클리어! 문이 회전하며 열립니다."
        );
    }
}
