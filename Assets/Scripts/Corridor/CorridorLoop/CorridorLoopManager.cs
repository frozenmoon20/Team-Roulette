using System.Collections.Generic;
using UnityEngine;
using TeamRoulette.UI;

public class CorridorLoopManager : MonoBehaviour
{
    [Header("반복 복도 상태")]
    public bool hasAnomaly = false;
    public int successCount = 0;

    [Header("개별 등장/변화 이상현상 (선택 시 해당 오브젝트 하나 활성화)")]
    public GameObject[] anomalyObjects;

    [Header("개별 사라짐 이상현상 (선택 시 해당 평소 물체 하나 비활성화)")]
    public GameObject[] normalObjects;

    [Header("같은 이상현상 재등장 전 필요한 다른 이상현상 수")]
    [Min(0)] public int anomalyCooldown = 4;

    [Header("반복 복도 시작 벽 (별도 트리거에서 처리)")]
    public GameObject startWall;

    [Header("반복 복도 진입 후 뒤를 막을 벽")]
    public GameObject entranceBlocker;

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

    // 한 후보 = 한 이상현상. 등장 효과와 사라짐 효과를 같은 후보 목록으로 관리한다.
    struct AnomalyCandidate
    {
        public GameObject target;
        public bool disappear;

        public AnomalyCandidate(GameObject target, bool disappear)
        {
            this.target = target;
            this.disappear = disappear;
        }
    }

    readonly List<AnomalyCandidate> candidates = new List<AnomalyCandidate>();
    readonly Queue<GameObject> recentAnomalies = new Queue<GameObject>();
    readonly Dictionary<GameObject, bool> normalInitialStates =
        new Dictionary<GameObject, bool>();

    int consecutiveAnomalies = 0;
    int consecutiveNormals = 0;
    int currentAnomalyIndex = -1;

    bool firstLoop = true;
    bool initialized = false;
    bool cleared = false;
    public bool IsCleared => cleared;
    bool warnedNoCandidates = false;

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

        // 평소 물체의 시작 활성화 상태를 저장한다.
        if (normalObjects != null)
        {
            foreach (GameObject obj in normalObjects)
            {
                if (obj != null && !normalInitialStates.ContainsKey(obj))
                    normalInitialStates.Add(obj, obj.activeSelf);
            }
        }

        BuildCandidateList();
        ResetAnomalyVisuals();

        if (entranceBlocker != null)
            entranceBlocker.SetActive(false);

        UpdateObjectiveUI();
    }

    void BuildCandidateList()
    {
        candidates.Clear();
        HashSet<GameObject> registered = new HashSet<GameObject>();

        // Anomaly Objects: 개별 등장/소리/조명/사진 변화
        if (anomalyObjects != null)
        {
            foreach (GameObject obj in anomalyObjects)
            {
                if (obj == null)
                    continue;

                if (!registered.Add(obj))
                {
                    Debug.LogWarning("이상현상 후보 중복 등록: " + obj.name, obj);
                    continue;
                }

                candidates.Add(new AnomalyCandidate(obj, false));
            }
        }

        // Normal Objects: 각 물체가 사라지는 것이 각각 별개의 이상현상
        if (normalObjects != null)
        {
            foreach (GameObject obj in normalObjects)
            {
                if (obj == null)
                    continue;

                if (!registered.Add(obj))
                {
                    Debug.LogWarning(
                        "같은 물체가 Anomaly Objects와 Normal Objects에 중복 등록되어 있습니다: "
                        + obj.name, obj);
                    continue;
                }

                // 원래부터 꺼져 있는 물체는 사라져도 변화가 없으므로 후보로 선택하지 않음.
                if (normalInitialStates.TryGetValue(obj, out bool wasActive) && wasActive)
                    candidates.Add(new AnomalyCandidate(obj, true));
            }
        }
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

        // 첫 복도는 정상, 정상/이상 각각 최대 두 번 연속 (기존 규칙 유지).
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
                        "쿨다운 조건에 맞는 이상현상 후보가 없습니다. " +
                        "재등장 제한을 무시하지 않고 정상 복도로 처리합니다."
                    );
                    warnedNoCandidates = true;
                }
            }
            else
            {
                warnedNoCandidates = false;
                RememberAnomaly(candidates[currentAnomalyIndex].target);
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
        {
            AnomalyCandidate selected = candidates[currentAnomalyIndex];
            Debug.Log("이번 복도: 이상현상 하나 활성화 - " +
                      (selected.disappear ? "사라짐: " : "등장/변화: ") +
                      selected.target.name);
        }
        else
        {
            Debug.Log("이번 복도: 정상");
        }
    }

    int SelectRandomAnomaly()
    {
        if (candidates.Count == 0)
            return -1;

        HashSet<GameObject> excluded = new HashSet<GameObject>(recentAnomalies);
        List<int> available = new List<int>();

        for (int i = 0; i < candidates.Count; i++)
        {
            GameObject target = candidates[i].target;
            if (target != null && !excluded.Contains(target))
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

        // 정상 복도가 아니라 실제로 발생한 다른 이상현상 4개를 기준으로 한다.
        while (recentAnomalies.Count > anomalyCooldown)
            recentAnomalies.Dequeue();
    }

    void ResetAnomalyVisuals()
    {
        // 등장/변화 연출은 모두 해제한다.
        if (anomalyObjects != null)
        {
            foreach (GameObject obj in anomalyObjects)
            {
                // 동일한 평소 물체를 잘못 중복 등록해도 평소 상태를 우선 보존.
                if (obj != null && !normalInitialStates.ContainsKey(obj))
                    obj.SetActive(false);
            }
        }

        // 사라졌던 평소 물체는 개별적으로 원래 상태로 되돌린다.
        foreach (KeyValuePair<GameObject, bool> entry in normalInitialStates)
        {
            if (entry.Key != null)
                entry.Key.SetActive(entry.Value);
        }
    }

    void ApplyLoopState()
    {
        ResetAnomalyVisuals();

        if (!hasAnomaly || currentAnomalyIndex < 0 ||
            currentAnomalyIndex >= candidates.Count)
            return;

        AnomalyCandidate selected = candidates[currentAnomalyIndex];
        if (selected.target == null)
            return;

        if (selected.disappear)
        {
            // 해당 물체 하나만 사라짐. 다른 Normal Objects에는 영향 없음.
            selected.target.SetActive(false);
        }
        else
        {
            // 해당 연출 오브젝트 하나만 등장/활성화.
            selected.target.SetActive(true);
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

    if (sceneUI == null)
        sceneUI = FindFirstObjectByType<SceneUIContent>();

    if (sceneUI != null)
    {
        // 반복 복도 목표 완료 처리
        sceneUI.CompleteObjective(objectiveId);

        // 클리어 메시지 표시
        GameUI gameUI = sceneUI.GetComponent<GameUI>();

        if (gameUI != null && gameUI.objectiveText != null)
        {
            gameUI.objectiveText.text = "목표 완료";
        }
    }

    Debug.Log("반복 복도 클리어.");
}

}
