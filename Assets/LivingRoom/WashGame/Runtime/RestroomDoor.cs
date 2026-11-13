using UnityEngine;

public sealed class RestroomDoor : MonoBehaviour, IInteractable, IHasName
{
    public float openAngle = -95f;
    [Min(.1f)] public float duration = .35f;
    public bool IsOpen { get; private set; }
    public string DisplayName => IsOpen ? "화장실 문 닫기" : "화장실 문 열기";
    Quaternion closed, from, target;
    float elapsed;
    void Awake() { closed=transform.rotation; from=target=closed; elapsed=duration; }
    public void Interact()
    {
        if(Time.timeScale<=0)return;
        IsOpen=!IsOpen;
        from=transform.rotation;
        // Imported mesh axes can be tilted. Swing around the vertical world axis.
        target=Quaternion.AngleAxis(IsOpen?openAngle:0,Vector3.up)*closed;
        elapsed=0;
    }
    void Update()
    {
        if(elapsed>=duration)return;
        elapsed=Mathf.Min(duration,elapsed+Time.deltaTime);
        transform.rotation=Quaternion.Slerp(from,target,Mathf.SmoothStep(0,1,elapsed/duration));
    }
}
