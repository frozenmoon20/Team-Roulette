using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

[DefaultExecutionOrder(500)]
public sealed class BoardGameController : MonoBehaviour
{
    public Transform surface;
    public Vector2 boardSize = new Vector2(4.8f,1.8f);
    public BoardTarget boardTarget, trashTarget;
    public BoardItem[] items;
    public Camera viewCamera;
    public PlayerInteractor interactor;
    public FirstPersonPlayer player;
    public Behaviour[] otherInputs;
    public GameObject overlay;
    public TMP_Text instruction, prompt, progress;
    [Range(.05f,.8f)] public float overlapLimit=.25f;
    public float reach=3.5f;
    public bool Completed { get; private set; }
    public bool Active { get; private set; }
    public BoardItem Held { get; private set; }
    public int TrashCount => trash.Count;
    readonly Stack<BoardItem> trash=new Stack<BoardItem>();
    struct BlockedInput { public Behaviour component; public bool wasEnabled; }
    // Input ownership is runtime-only and must never be saved into the scene.
    [System.NonSerialized] readonly List<BlockedInput> blocked=new List<BlockedInput>();
    float finishHold;
    bool finishLatched, focused=true, wasLocked;
    int beginFrame, nextOrder;
    string message;
    BoardItem hovered;
    BoardTarget target;
    Vector2 candidate;
    bool canPlace;
    Vector3 targetPoint;
    public const string ControlHelp="좌클릭: 집기 / 붙이기 / 버리기 / 다시 꺼내기\nQ·E: 회전   F: 바닥에 놓기   G 2초 누르기: 완성";

    void Awake() { ConfigureOverlayLayout(); }
    public void ConfigureOverlayLayout()
    {
        var top=instruction.rectTransform;
        top.anchorMin=new Vector2(.15f,.88f); top.anchorMax=new Vector2(.85f,.88f);
        top.pivot=new Vector2(.5f,1); top.anchoredPosition=Vector2.zero; top.sizeDelta=new Vector2(0,110);
        instruction.fontSize=26;
        instruction.enableAutoSizing=true; instruction.fontSizeMin=18; instruction.fontSizeMax=26;
        instruction.alignment=TextAlignmentOptions.Top;
        instruction.text=ControlHelp;
        var center=progress.rectTransform;
        center.anchorMin=center.anchorMax=center.pivot=new Vector2(.5f,.5f);
        center.anchoredPosition=Vector2.zero; center.sizeDelta=new Vector2(600,80);
        progress.alignment=TextAlignmentOptions.Center;
        progress.fontSize=32;
        progress.gameObject.SetActive(false);
    }

    void RefreshOverlay()
    {
        instruction.text=message==null ? ControlHelp : ControlHelp+"\n"+message;
        instruction.gameObject.SetActive(true);
        progress.text=finishHold>0 ? "완성 확인 중… "+Mathf.CeilToInt(finishHold/2*100)+"%" : "";
        progress.gameObject.SetActive(finishHold>0);
    }

    void Start() { overlay.SetActive(false); nextOrder=items.Max(i=>i.order); RefreshOverlap(); }
    public void Begin()
    {
        if(!Application.isPlaying || !isActiveAndEnabled || Completed || Active || Time.timeScale<=0) return;
        if(PlayerHand.Instance && PlayerHand.Instance.Held) { PlayerInteractor.ShowHint("먼저 내려놓자."); return; }
        if(!TeamRoulette.Room3.GameManager.TryBegin(TeamRoulette.Room3.GameStage.Board)) return;
        Active=true; beginFrame=Time.frameCount; message=null;
        RefreshOverlap();
        // Disable competing minigame input before taking ownership of the shared interactor.
        if(otherInputs!=null) foreach(var input in otherInputs) Block(input);
        Block(interactor);
        overlay.SetActive(true); RefreshOverlay();
    }
    void Block(Behaviour b)
    {
        if(!b || b==this || blocked.Any(entry=>entry.component==b)) return;
        blocked.Add(new BlockedInput { component=b, wasEnabled=b.enabled }); b.enabled=false;
    }
    void Release()
    {
        if(!Active && blocked.Count==0) return;
        Active=false; finishHold=0;
        if(overlay) overlay.SetActive(false);
        var restore=blocked.ToArray();
        blocked.Clear();
        foreach(var entry in restore) if(entry.component) entry.component.enabled=entry.wasEnabled;
        if(interactor && interactor.nameText) interactor.nameText.text="";
    }
    void LateUpdate()
    {
        if(!Active) return;
        bool locked=Cursor.lockState==CursorLockMode.Locked;
        bool allowed=focused && locked && Time.timeScale>0 && player && player.enabled && player.canLook;
        overlay.SetActive(allowed);
        if(!allowed) { finishHold=0; wasLocked=false; RefreshOverlay(); return; }
        if(interactor) { if(interactor.nameText) interactor.nameText.text=""; if(interactor.handIcon) interactor.handIcon.SetActive(false); if(interactor.crosshairDot) interactor.crosshairDot.SetActive(true); }
        Aim();
        if(Held)
        {
            float turn=(Input.GetKey(KeyCode.E)?-1:0)+(Input.GetKey(KeyCode.Q)?1:0);
            Held.angle=Mathf.Repeat(Held.angle+turn*90*Time.deltaTime,360);
            Aim(); UpdateHeld();
        }
        if(wasLocked && Time.frameCount>beginFrame)
        {
            if(Input.GetMouseButtonDown(0))
            {
                if(message!=null) message=null;
                else if(target==trashTarget) { if(Held) Discard(); else Retrieve(); }
                else if(Held && canPlace) Place(candidate);
                else if(!Held && hovered) Pick(hovered);
            }
            if(Input.GetKeyDown(KeyCode.F) && Held) Drop();
            TickFinish(Input.GetKey(KeyCode.G),Time.deltaTime);
        }
        wasLocked=locked;
        RefreshOverlay();
        string label=target==trashTarget ? (Held?"버리기":trash.Count>0?"다시 꺼내기":"버린 물건이 없다") :
            Held ? (canPlace?"붙이기":target==boardTarget?"게시판 안에 놓자.":"") : hovered ? (hovered.state==BoardItemState.Board?"떼기":"집기") : "";
        prompt.text=label;
        if(label.Length>0) prompt.transform.position=viewCamera.WorldToScreenPoint(targetPoint)+new Vector3(70,20,0);
    }
    void Aim()
    {
        hovered=null; target=null; canPlace=false;
        Ray ray=viewCamera.ViewportPointToRay(new Vector3(.5f,.5f,0));
        if(!Physics.Raycast(ray,out var hit,reach,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) return;
        targetPoint=hit.point;
        var item=hit.collider.GetComponentInParent<BoardItem>();
        if(item && items.Contains(item)) { hovered=item; if(item.state==BoardItemState.Board) target=boardTarget; }
        else { var t=hit.collider.GetComponentInParent<BoardTarget>(); if(t && t.game==this) target=t; }
        if(!Held || target!=boardTarget) return;
        var plane=new Plane(surface.forward,surface.position);
        if(!plane.Raycast(ray,out float distance) || distance>reach) return;
        Vector3 p=surface.InverseTransformPoint(ray.GetPoint(distance)); candidate=new Vector2(p.x,p.y);
        canPlace=BoardGeometry.Fits(candidate,Held.size,Held.angle,boardSize);
    }
    void UpdateHeld()
    {
        if(canPlace)
        {
            Held.transform.SetPositionAndRotation(surface.TransformPoint(new Vector3(candidate.x,candidate.y,.035f)),surface.rotation*Quaternion.Euler(0,0,Held.angle));
            Held.SetOutline(items.Any(i=>i!=Held && i.state==BoardItemState.Board && BoardGeometry.Overlap(candidate,Held.size,Held.angle,i.boardPosition,i.size,i.angle)>overlapLimit));
        }
        else
        {
            Held.transform.SetPositionAndRotation(viewCamera.transform.position+viewCamera.transform.forward*.65f-viewCamera.transform.up*.12f,viewCamera.transform.rotation*Quaternion.Euler(0,180,-Held.angle));
            Held.SetOutline(target==boardTarget);
        }
    }
    public void Pick(BoardItem item)
    {
        if(!Active || Held || !item || !items.Contains(item) || (item.state!=BoardItemState.Board && item.state!=BoardItemState.Floor)) return;
        Held=item; item.state=BoardItemState.Held; item.hitbox.enabled=false; RefreshOverlap();
    }
    public bool Place(Vector2 p)
    {
        if(!Active || !Held || !BoardGeometry.Fits(p,Held.size,Held.angle,boardSize)) return false;
        var item=Held; Held=null; item.state=BoardItemState.Board; item.boardPosition=p; item.order=++nextOrder;
        item.hitbox.enabled=true; ArrangeBoard(); RefreshOverlap(); return true;
    }
    public void Discard()
    {
        if(!Active || !Held) return;
        var item=Held; Held=null; item.state=BoardItemState.Trash; item.SetOutline(false); item.gameObject.SetActive(false); trash.Push(item); RefreshOverlap();
    }
    public void Retrieve()
    {
        if(!Active || Held) return;
        if(trash.Count==0) { message="버린 물건이 없다"; return; }
        var item=trash.Pop(); item.gameObject.SetActive(true); item.state=BoardItemState.Floor; Pick(item);
    }
    public bool Drop()
    {
        if(!Active || !Held) return false;
        // Only put an item down on reachable, nearly horizontal ground below the player.
        Vector3 origin=viewCamera.transform.position+Vector3.ProjectOnPlane(viewCamera.transform.forward,Vector3.up).normalized*.7f;
        foreach(var hit in Physics.RaycastAll(origin,Vector3.down,4,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
        {
            if(hit.collider.GetComponentInParent<FirstPersonPlayer>() || hit.collider.GetComponentInParent<BoardItem>()) continue;
            if(Vector3.Dot(hit.normal,Vector3.up)<.9f || hit.point.y>player.transform.position.y+.25f) continue;
            var item=Held; Held=null; item.state=BoardItemState.Floor;
            item.transform.SetPositionAndRotation(hit.point+Vector3.up*.025f,Quaternion.Euler(-90,0,item.angle));
            item.hitbox.enabled=true; item.SetOutline(false); return true;
        }
        message="내려놓을 바닥이 가까이 없다."; return false;
    }
    public bool RefreshOverlap()
    {
        bool bad=false;
        foreach(var item in items) if(item!=Held) item.SetOutline(false);
        for(int a=0;a<items.Length;a++) for(int b=a+1;b<items.Length;b++)
        {
            var x=items[a]; var y=items[b];
            if(x.state!=BoardItemState.Board || y.state!=BoardItemState.Board) continue;
            if(BoardGeometry.Overlap(x.boardPosition,x.size,x.angle,y.boardPosition,y.size,y.angle)<=overlapLimit) continue;
            x.SetOutline(true); y.SetOutline(true); bad=true;
        }
        if(Active && !Completed)
        {
            var goal = bad ? TeamRoulette.Room3.ObjectiveStep.ResolveOverlap :
                Held || items.Any(i=>i.state==BoardItemState.Floor || i.state==BoardItemState.Held) ?
                TeamRoulette.Room3.ObjectiveStep.ArrangeBoard : TeamRoulette.Room3.ObjectiveStep.FinishBoard;
            TeamRoulette.Room3.ObjectiveManager.Request(TeamRoulette.Room3.GameStage.Board, goal);
        }
        return bad;
    }
    public string CompletionIssue()
    {
        if(Held || items.Any(i=>i.state==BoardItemState.Held)) return "손에 든 것을 마저 정리하자.";
        if(items.Any(i=>i.state==BoardItemState.Floor)) return "바닥에 아직 정리되지 않은 것이 남아있다.";
        if(RefreshOverlap()) return "겹쳐서 잘 안 보여.";
        return null;
    }
    public void TickFinish(bool pressed,float seconds)
    {
        if(!Active) return;
        if(!pressed) { finishHold=0; finishLatched=false; RefreshOverlay(); return; }
        if(finishLatched) return;
        finishHold+=seconds;
        RefreshOverlay();
        if(finishHold<2) return;
        finishHold=0; finishLatched=true; TryFinish();
    }
    public bool TryFinish()
    {
        if(!Active) return false;
        message=CompletionIssue(); if(message!=null) { RefreshOverlay(); return false; }
        Completed=true;
        Release(); PlayerInteractor.ShowHint("게시판 정리 완료");
        TeamRoulette.Room3.GameManager.ReportComplete(TeamRoulette.Room3.GameStage.Board);
        return true;
    }
    public void RestoreCompleted(TeamRoulette.Room3.BoardItemRecord[] records)
    {
        Completed = true;
        Active = false;
        overlay.SetActive(false);
        if(records==null) return;
        foreach(var record in records)
        {
            var item=items.FirstOrDefault(x=>x.id==record.id);
            if(!item) continue;
            item.state=record.state; item.boardPosition=record.position; item.angle=record.angle; item.order=record.order;
            item.gameObject.SetActive(record.state!=BoardItemState.Trash);
            item.hitbox.enabled=true;
            if(record.state==BoardItemState.Floor) item.transform.SetPositionAndRotation(record.worldPosition,record.worldRotation);
            item.SetOutline(false);
        }
        ArrangeBoard();
    }

    void ArrangeBoard()
    {
        int depth=0;
        foreach(var i in items.Where(i=>i.state==BoardItemState.Board).OrderBy(i=>i.order))
            i.transform.SetPositionAndRotation(surface.TransformPoint(new Vector3(i.boardPosition.x,i.boardPosition.y,.015f+depth++*.001f)),surface.rotation*Quaternion.Euler(0,0,i.angle));
    }
    void OnApplicationFocus(bool value) { focused=value; if(!value) finishHold=0; }
    void OnDisable() { Release(); }
}
