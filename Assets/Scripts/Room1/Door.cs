using System;
using UnityEngine;

public class Door : ConditionalInteractable
{
    public float openAngle = 90f;
    private bool isOpen = false;

    protected override void OnUnlocked()
    {
        TogooleDoor();

    }

    protected override void OnRepeatInteract()
    {
        TogooleDoor();
    }

    void TogooleDoor()
    {
        transform.Rotate(0, isOpen ? -openAngle : openAngle, 0);
        isOpen = !isOpen;
        Debug.Log(isOpen ? "문이 열렸습니다" : "문이 닫혔습니다");
    }
}
