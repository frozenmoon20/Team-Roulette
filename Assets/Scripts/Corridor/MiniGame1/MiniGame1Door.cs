using UnityEngine;

public class MiniGame1Door : MonoBehaviour, IInteractable, IHasName
{
    public string displayName = "";
    public Transform door;

    public float openAngle = 90f;
    public float openSpeed = 3f;

    bool opened;
    Quaternion closedRotation;
    Quaternion openRotation;

    public string DisplayName => displayName;

    void Start()
    {
        if (door == null)
            door = transform;

        closedRotation = door.localRotation;

        openRotation =
            closedRotation *
            Quaternion.Euler(0f, openAngle, 0f);
    }

    void Update()
    {
        if (!opened)
            return;

        door.localRotation = Quaternion.Slerp(
            door.localRotation,
            openRotation,
            openSpeed * Time.deltaTime
        );
    }

    public void Interact()
    {
        if (opened)
            return;

        if (!MiniGame1State.HasKey)
        {
            PlayerInteractor.ShowHint("잠겨 있다.");
            return;
        }

        opened = true;

        PlayerInteractor.ShowHint("문이 열렸다.");
    }
}