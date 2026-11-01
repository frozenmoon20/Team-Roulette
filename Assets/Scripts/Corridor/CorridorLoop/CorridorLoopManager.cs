using UnityEngine;
using TeamRoulette.UI;

public class CorridorLoopManager : MonoBehaviour
{
    public bool hasAnomaly = false;
    public int successCount = 0;

    public GameObject anomalyObject;
    public GameObject nextPath;

    public int clearCount = 7;

    int consecutiveAnomalies = 0;
    int consecutiveNormals = 0;

    bool firstLoop = true;
    bool cleared = false;

    public void PrepareNextLoop()
    {
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

        if (anomalyObject != null)
            anomalyObject.SetActive(
                hasAnomaly
            );

        Debug.Log(
            hasAnomaly
                ? "이번 복도: 이상현상 있음"
                : "이번 복도: 정상"
        );
    }

    void ClearLoop()
    {
        cleared = true;

        hasAnomaly = false;

        if (anomalyObject != null)
            anomalyObject.SetActive(false);

        if (nextPath != null)
            nextPath.SetActive(true);

        SceneUIContent ui =
            FindFirstObjectByType<SceneUIContent>();

        if (ui != null)
        {
            ui.CompleteObjective(
                "corridor_loop"
            );
        }

        PlayerInteractor.ShowHint(
            "반복되는 복도를 빠져나왔다."
        );

        Debug.Log(
            "반복 복도 클리어!"
        );
    }
}