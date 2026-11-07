using System.Collections.Generic;
using UnityEngine;
using TeamRoulette.UI;

public class CorridorLoopManager : MonoBehaviour
{
    [Header("반복 복도 상태")]
    public bool hasAnomaly = false;
    public int successCount = 0;

    [Header("이상현상 후보들 (실제 평소 물체가 아닌 연출용 오브젝트)")]
    public GameObject[] anomalyObjects;

    [Header("물체 사라짐 이상현상에서만 숨길 평소 오브젝트들")]
    public GameObject[] normalObjects;

    [Header("물체 사라짐 이상현상 오브젝트 (Anomaly Objects 중 하나)")]
    public GameObject disappearanceAnomaly;

    [Header("같은 이상현상 재등장 전 필요한 다른 이상현상 수")]
    [Min(0)] public int anomalyCooldown = 4;

    [Header("반복 복도 시작 시 사라질 벽 (별도 트리거에서 처리)")]
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
    bool warnedNoCandidates = false;

    int currentAnomalyIndex = -1;

    readonly Queue<GameObject> recentAnomalies = new Queue<GameObject>();
    readonly Dictionary<GameObject, bool> normalInitialStates =
        new Dictionary<GameObject, bool>();

    Quaternion doorClosedRotation;
    Quaternion doorOpenRotation;
    SceneUIContent sceneUI;

    void Start()
    {
        sceneUI = FindFirstObjectByType<SceneUIContent>();

        if (exitDoor != null)
        {
            doorClosedRotation = exitDoor.localRotation;
            doorOpenRotation =
                doorClosedRotation * Quaternion.Euler(0f, doorOpenAngle, 0f);
        }

        if (normalObjects != null)
        {
            foreach (GameObject obj in normalObjects)
            {
                if (obj != null && !normalInitialStates.ContainsKey(obj))
                    normalInitialStates.Add(obj, obj.activeSelf);
            }
        }

        ResetAnomalyVisuals();

        if (nextPath != null)
            nextPath.SetActive(false);

        if (entranceBlocker != null)
            entranceBlocker.SetActive(false);

        UpdateObjectiveUI();
    }

    void Update()
    {
        if (!cleared || exitDoor == null)
            return;

        exitDoor.localRotation = Quaternion.RotateTowards(
            exitDoor.localRotation,
            doorOpenRotation,
            doorOpenSpeed * Time.deltaTime
        );
    }

    public void PrepareNextLoop()
    {
        if (!initialized)
        {
            initialized = true;

            if (entranceBlocker != null)
                entranceBlocker.SetActive(true);

            if (sceneUI == null)
                sceneUI = FindFirstObjectByType<SceneUIContent>();

            if (sceneUI != null)
                sceneUI.RevealObjective(objectiveId);

            UpdateObjectiveUI();
            Debug.Log("반복 복도 진입 - 뒤쪽 입구 차단");
        }

        if (cleared)
            return;

        if (successCount >= clearCount)
        {
            ClearLoop();
            return;
        }

        bool nextHasAnomaly;

        if (firstLoop)
        {
            firstLoop = false;
            nextHasAnomaly = false;
        }
        else if (consecutiveNormals >= 2)
        {
            nextHasAnomaly = true;
        }
        else if (consecutiveAnomalies >= 2)
        {
            nextHasAnomaly = false;
        }
        else
        {
            nextHasAnomaly = Random.value < 0.5f;
        }

        currentAnomalyIndex = -1;

        if (nextHasAnomaly)
        {
            currentAnomalyIndex = SelectRandomAnomaly();

            if (currentAnomalyIndex < 0)
            {
                nextHasAnomaly = false;

                if (!warnedNoCandidates)
                {
                    Debug.LogWarning(
                        "최근 이상현상 4종과 겹치지 않는 후보가 없습니다. " +
                        "중복을 강제로 허용하지 않고 정상 복도로 처리합니다. " +
                        "4종을 건너뛰려면 최소 5종의 서로 다른 이상현상이 필요합니다."
                    );
                    warnedNoCandidates = true;
                }
            }
            else
            {
                warnedNoCandidates = false;
                RememberAnomaly(anomalyObjects[currentAnomalyIndex]);
            }
        }

        hasAnomaly = nextHasAnomaly;

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

        ApplyLoopState();
        UpdateObjectiveUI();

        if (hasAnomaly)
            Debug.Log("이번 복도: 이상현상 하나 활성화 - " +
                      anomalyObjects[currentAnomalyIndex].name);
        else
            Debug.Log("이번 복도: 정상");
    }

    int SelectRandomAnomaly()
    {
        if (anomalyObjects == null || anomalyObjects.Length == 0)
            return -1;

        HashSet<GameObject> excluded = new HashSet<GameObject>(recentAnomalies);
        HashSet<GameObject> seen = new HashSet<GameObject>();
        List<int> available = new List<int>();

        for (int i = 0; i < anomalyObjects.Length; i++)
        {
            GameObject anomaly = anomalyObjects[i];

            if (anomaly == null || !seen.Add(anomaly))
                continue;

            if (!excluded.Contains(anomaly))
                available.Add(i);
        }

        if (available.Count == 0)
            return -1;

        return available[Random.Range(0, available.Count)];
    }

    void RememberAnomaly(GameObject anomaly)
    {
        if (anomaly == null || anomalyCooldown <= 0)
            return;

        recentAnomalies.Enqueue(anomaly);

        while (recentAnomalies.Count > anomalyCooldown)
            recentAnomalies.Dequeue();
    }

    void ResetAnomalyVisuals()
    {
        if (anomalyObjects != null)
        {
            foreach (GameObject anomaly in anomalyObjects)
            {
                if (anomaly != null && !normalInitialStates.ContainsKey(anomaly))
                    anomaly.SetActive(false);
            }
        }

        foreach (KeyValuePair<GameObject, bool> pair in normalInitialStates)
        {
            if (pair.Key != null)
                pair.Key.SetActive(pair.Value);
        }
    }

    void ApplyLoopState()
    {
        ResetAnomalyVisuals();

        if (!hasAnomaly || currentAnomalyIndex < 0 ||
            anomalyObjects == null || currentAnomalyIndex >= anomalyObjects.Length)
            return;

        GameObject selected = anomalyObjects[currentAnomalyIndex];

        if (selected == null)
            return;

        if (selected == disappearanceAnomaly)
        {
            if (!normalInitialStates.ContainsKey(selected))
                selected.SetActive(true);

            if (normalObjects != null)
            {
                foreach (GameObject obj in normalObjects)
                {
                    if (obj != null)
                        obj.SetActive(false);
                }
            }
        }
        else if (normalInitialStates.ContainsKey(selected))
        {
            selected.SetActive(false);
        }
        else
        {
            selected.SetActive(true);
        }
    }

    void UpdateObjectiveUI()
    {
        if (sceneUI == null)
            sceneUI = FindFirstObjectByType<SceneUIContent>();

        if (sceneUI == null)
            return;

        SceneUIContent.Objective objective = sceneUI.objectives.Find(
            x => x != null && x.id == objectiveId
        );

        if (objective == null)
            return;

        objective.text = objectiveMessage + " (" + successCount + "/" + clearCount + ")";
        sceneUI.Refresh();
    }

    void ClearLoop()
    {
        cleared = true;
        hasAnomaly = false;
        currentAnomalyIndex = -1;

        UpdateObjectiveUI();
        ResetAnomalyVisuals();

        if (nextPath != null)
            nextPath.SetActive(true);

        if (sceneUI == null)
            sceneUI = FindFirstObjectByType<SceneUIContent>();

        if (sceneUI != null)
            sceneUI.CompleteObjective(objectiveId);

        Debug.Log("반복 복도 클리어! 문이 열립니다.");
    }
}
