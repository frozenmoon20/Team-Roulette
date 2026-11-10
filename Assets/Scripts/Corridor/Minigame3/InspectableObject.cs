
using UnityEngine;

public class InspectableObject : MonoBehaviour, IInteractable, IHasName
{
    public string displayName = "살펴보기";

    public string DisplayName => displayName;

    public void Interact()
    {
        if (ObjectInspectionManager.Instance == null)
        {
            Debug.LogWarning("ObjectInspectionManager가 없습니다.", this);
            return;
        }

        ObjectInspectionManager.Instance.BeginInspection(this);
    }
}
