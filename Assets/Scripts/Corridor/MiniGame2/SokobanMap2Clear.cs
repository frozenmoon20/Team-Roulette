
using UnityEngine;
using UnityEngine.Events;

public class SokobanMap2Clear : MonoBehaviour
{
    [Header("맵 2 박스")]
    [SerializeField] private SokobanPushBox[] boxes =
        new SokobanPushBox[4];

    [Header("맵 2 목표")]
    [SerializeField] private SokobanGoal[] goals =
        new SokobanGoal[4];

    [Header("목표 판정 거리")]
    [SerializeField] private float clearDistance = 0.4f;

    [Header("클리어 이벤트")]
    [SerializeField] private UnityEvent onClear;

    private bool isCleared;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetMap2();
            return;
        }

        if (isCleared) return;

        if (boxes.Length != 4 || goals.Length != 4)
            return;

        foreach (SokobanPushBox box in boxes)
            if (box == null) return;

        foreach (SokobanGoal goal in goals)
            if (goal == null) return;

        bool[] usedBoxes = new bool[boxes.Length];

        if (CheckGoals(0, usedBoxes))
        {
            isCleared = true;
            Debug.Log("소코반 맵 2 클리어!");
            onClear?.Invoke();
        }
    }

    private bool CheckGoals(int goalIndex, bool[] usedBoxes)
    {
        if (goalIndex == goals.Length)
            return true;

        Vector3 goalPos = goals[goalIndex].transform.position;

        for (int i = 0; i < boxes.Length; i++)
        {
            if (usedBoxes[i])
                continue;

            Vector3 boxPos = boxes[i].transform.position;

            float distance = Vector2.Distance(
                new Vector2(boxPos.x, boxPos.z),
                new Vector2(goalPos.x, goalPos.z)
            );

            if (distance > clearDistance)
                continue;

            usedBoxes[i] = true;

            if (CheckGoals(goalIndex + 1, usedBoxes))
                return true;

            usedBoxes[i] = false;
        }

        return false;
    }

    public void ResetMap2()
    {
        for (int i = 0; i < boxes.Length; i++)
        {
            if (boxes[i] != null)
                boxes[i].ResetBox();
        }

        foreach (SokobanGoal goal in goals)
        {
            if (goal != null)
                goal.ResetGoal();
        }

        isCleared = false;

        Debug.Log("소코반 맵 2 리셋!");
    }
}
