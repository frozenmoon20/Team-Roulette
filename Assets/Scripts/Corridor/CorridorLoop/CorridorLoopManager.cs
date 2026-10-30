using UnityEngine;

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

    public void PrepareNextLoop()
    {
        if (successCount >= clearCount)
        {
            ClearLoop();
            return;
        }

        // 첫 번째 복도는 무조건 정상
        if (firstLoop)
        {
            firstLoop = false;

            hasAnomaly = false;

            consecutiveNormals = 1;
            consecutiveAnomalies = 0;
        }

        // 정상 복도가 이미 2번 연속 나왔다면
        // 다음은 무조건 이상현상
        else if (consecutiveNormals >= 2)
        {
            hasAnomaly = true;

            consecutiveAnomalies = 1;
            consecutiveNormals = 0;
        }

        // 이상현상이 이미 2번 연속 나왔다면
        // 다음은 무조건 정상
        else if (consecutiveAnomalies >= 2)
        {
            hasAnomaly = false;

            consecutiveNormals = 1;
            consecutiveAnomalies = 0;
        }

        // 그 외에는 랜덤
        else
        {
            hasAnomaly = Random.value < 0.5f;

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
            anomalyObject.SetActive(hasAnomaly);

        Debug.Log(
            hasAnomaly
                ? "이번 복도: 이상현상 있음"
                : "이번 복도: 정상"
        );
    }

    void ClearLoop()
    {
        hasAnomaly = false;

        if (anomalyObject != null)
            anomalyObject.SetActive(false);

        if (nextPath != null)
            nextPath.SetActive(true);

        Debug.Log("반복 복도 클리어!");
    }
}