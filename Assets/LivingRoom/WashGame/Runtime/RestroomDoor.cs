using UnityEngine;

public sealed class RestroomDoor : MonoBehaviour, IInteractable, IHasName
{
    public float openAngle = -95f;
    [Min(.1f)] public float duration = .85f;
    public bool IsOpen { get; private set; }
    public string DisplayName => "화장실 문";
    Quaternion closed, from, target;
    float elapsed;
    void Awake() { closed=transform.rotation; from=target=closed; elapsed=duration; }
    public void Interact()
    {
        if(Time.timeScale<=0)return;
        IsOpen=!IsOpen;
        TeamRoulette.Room3.RoomAudio.For(this)?.Play("Bathroom Door");
        var wash = FindFirstObjectByType<WashGameController>();
        if (!wash || wash.CurrentStage == WashGameController.Stage.Idle)
            TeamRoulette.Room3.ObjectiveManager.Request(TeamRoulette.Room3.GameStage.Wash,
                IsOpen ? TeamRoulette.Room3.ObjectiveStep.CloseBathroomDoor : TeamRoulette.Room3.ObjectiveStep.TurnOnWater);
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
