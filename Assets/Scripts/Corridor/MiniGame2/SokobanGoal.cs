using UnityEngine;

public class SokobanGoal : MonoBehaviour
{
    public SokobanColor goalColor;
    public SokobanGameManager gameManager;

    public bool IsCorrect { get; private set; }

    public void PlaceBox(SokobanPushBox box)
    {
        IsCorrect = box.boxColor == goalColor;

        if (IsCorrect)
        {
            Debug.Log(goalColor + " 발판 정답!");
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
        if (IsCorrect)
            Debug.Log(goalColor + " 발판에서 상자가 빠짐");

        IsCorrect = false;

        if (gameManager != null)
            gameManager.CheckClear();
    }

    public void ResetGoal()
    {
        IsCorrect = false;
    }
}