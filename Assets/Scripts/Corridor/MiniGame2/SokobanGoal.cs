
using UnityEngine;

public class SokobanGoal : MonoBehaviour
{
    [Header("맵 1 전용 색상 판정")]
    public bool useColorCheck = true;

    public SokobanColor goalColor;
    public SokobanGameManager gameManager;

    public bool IsCorrect { get; private set; }

    public void PlaceBox(SokobanPushBox box)
    {
        if (useColorCheck)
        {
            IsCorrect = box.boxColor == goalColor;
        }
        else
        {
            IsCorrect = true;
        }

        if (IsCorrect)
        {
            Debug.Log("발판 정답!");
        }
        else
        {
            Debug.Log("색깔이 맞지 않음");
        }

        if (gameManager != null)
            gameManager.CheckClear();
    }

    public void RemoveBox()
    {
        IsCorrect = false;

        if (gameManager != null)
            gameManager.CheckClear();
    }

    public void ResetGoal()
    {
        IsCorrect = false;
    }
}
