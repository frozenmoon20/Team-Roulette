using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TeamRoulette.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TeamRoulette.Room3.Nosework
{
    [DefaultExecutionOrder(1000)]
    public sealed class NoseworkGame : MonoBehaviour
    {
        public Transform toy;
        public NoseworkCarrot[] carrots;
        public FirstPersonPlayer player;
        public PlayerInteractor interactor;
        public Camera playerCamera;
        public NoseworkWinter winter;
        public NoseworkPetting petting;
        public Transform gameView;
        public float gameFieldOfView = 48;
        public GameUI sharedUI;
        public float cueDuration = .85f, cueGap = .3f;
        public bool autoStartOnVisit = true;
        public UnityEvent onCompleted = new UnityEvent();
        public bool Running { get; private set; }
        public bool AcceptingInput { get; private set; }
        public bool Completed { get; private set; }
        public bool WaitingForPetting { get; private set; }
        public int Stage { get; private set; }
        public IReadOnlyList<NoseworkCarrot> Sequence => sequence;
        public int SelectedCount => selected;
        public const string ToyId = "carrot_snuffle_game";
        readonly List<NoseworkCarrot> sequence = new List<NoseworkCarrot>();
        readonly List<NoseworkCarrot> roundPulled = new List<NoseworkCarrot>();
        readonly List<GameObject> icons = new List<GameObject>();
        readonly Dictionary<Behaviour,bool> inputs = new Dictionary<Behaviour,bool>();
        readonly Dictionary<GameObject,bool> hidden = new Dictionary<GameObject,bool>();
        readonly Dictionary<Collider,bool> rootColliders = new Dictionary<Collider,bool>();
        readonly Dictionary<Collider,bool> playerColliders = new Dictionary<Collider,bool>();
        Room3FlowTarget placeholder;
        bool placeholderEnabled, savedCursorVisible, restoring;
        bool savedHudActive;
        string savedObjective;
        CursorLockMode savedCursor;
        Vector3 cameraPosition; Quaternion cameraRotation; float cameraFov;
        Canvas canvas; TMP_Text hint; int selected, stageIconStart;
        Bounds bounds;
        Room3FlowController Flow => GameManager.Instance ? GameManager.Instance.Flow : null;
        bool IsCarrotVisit => !Flow || string.IsNullOrEmpty(Flow.VisitingObject) || Flow.VisitingObject == ToyId;
        IEnumerator Start()
        {
            if (carrots == null || carrots.Length == 0) carrots = toy.GetComponentsInChildren<NoseworkCarrot>(true);
            foreach(var carrot in carrots) { carrot.game = this; carrot.Capture(); }
            bounds = new Bounds(toy.position,Vector3.zero);
            foreach(var r in toy.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            GuardPlaceholder();
            // Sync FirstPersonPlayer's private pitch with the authored entry view.
            if(IsCarrotVisit && player && player.cameraRoot)
                player.Teleport(player.transform.position,player.transform.rotation,player.cameraRoot.localRotation);
            yield return null;
            if(!autoStartOnVisit || !Flow || Flow.VisitingObject!=ToyId) yield break;
            while(Flow && (Flow.IsTransitioning || (sharedUI && sharedUI.IsDialogueOpen))) yield return null;
            Begin();
        }
        void GuardPlaceholder()
        {
            if (!IsCarrotVisit || !toy) return;
            var current = toy.GetComponent<Room3FlowTarget>();
            if (current && current != placeholder) { placeholder = current; placeholderEnabled = current.enabled; }
            if (placeholder) placeholder.enabled = false;
            foreach(var collider in toy.GetComponents<Collider>())
            {
                if(!rootColliders.ContainsKey(collider)) rootColliders.Add(collider,collider.enabled);
                collider.enabled=false;
            }
        }
        void LateUpdate() { GuardPlaceholder(); }
        void Update()
        {
            if (!Running || Time.timeScale <= 0 || (sharedUI && (sharedUI.IsPaused || sharedUI.IsDialogueOpen))) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { Cancel(); return; }
            if(WaitingForPetting && Input.GetMouseButtonDown(0))
            {
                if(petting && petting.Hit(playerCamera.ScreenPointToRay(Input.mousePosition))) Pet();
                return;
            }
            if (AcceptingInput && Input.GetMouseButtonDown(0))
            {
                Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray,out var hit,30,~0,QueryTriggerInteraction.Ignore))
                {
                    var carrot = hit.collider.GetComponentInParent<NoseworkCarrot>();
                    if (carrot && carrot.game == this) Select(carrot);
                }
            }
        }
        public void Begin()
        {
            if (Running || Completed || !IsCarrotVisit || (Flow && Flow.IsTransitioning) || !player || !playerCamera || !winter || !winter.winter) return;
            if (Time.timeScale <= 0 || (sharedUI && (sharedUI.IsPaused || sharedUI.IsDialogueOpen))) return;
            if (PlayerHand.Instance && PlayerHand.Instance.Held) { PlayerInteractor.ShowHint("손을 비워 주세요."); return; }
            if (carrots.Count(c=>c) < 6) { Debug.LogError("노즈워크에는 최소 6개의 독립된 당근이 필요합니다.",this); return; }
            Running = true; Stage = 1;
            cameraPosition = playerCamera.transform.localPosition; cameraRotation = playerCamera.transform.localRotation; cameraFov = playerCamera.fieldOfView;
            savedCursor = Cursor.lockState; savedCursorVisible = Cursor.visible;
            if(sharedUI) { savedHudActive=sharedUI.hud && sharedUI.hud.activeSelf; savedObjective=sharedUI.objectiveText ? sharedUI.objectiveText.text:null; }
            Block(sharedUI); Block(player); Block(interactor);
            foreach(var collider in player.GetComponentsInChildren<Collider>(true))
            { playerColliders.Add(collider,collider.enabled); collider.enabled=false; }
            if(sharedUI && sharedUI.hud) sharedUI.hud.SetActive(true);
            foreach(var hand in player.GetComponentsInChildren<PlayerHand>(true)) { Block(hand); Hide(hand.handModel); }
            foreach(var motion in player.GetComponentsInChildren<PlayerMotionFeel>(true)) Block(motion);
            if (interactor) { Hide(interactor.crosshairDot); Hide(interactor.handIcon); if(interactor.nameText) Hide(interactor.nameText.gameObject); }
            Vector3 side = gameView ? gameView.position-bounds.center : player.transform.position - bounds.center; side.y = 0;
            if (side.sqrMagnitude < .01f) side = Vector3.back; side.Normalize();
            winter.Capture(bounds,side);
            float radius = Mathf.Max(.65f, Mathf.Max(bounds.size.x,bounds.size.z));
            playerCamera.transform.position = gameView ? gameView.position : bounds.center+side*1.45f+Vector3.up*1.2f;
            playerCamera.transform.rotation = gameView ? gameView.rotation : Quaternion.LookRotation(bounds.center-side*.18f+Vector3.up*.12f-playerCamera.transform.position);
            playerCamera.fieldOfView = gameFieldOfView;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            BuildUI(); StartCoroutine(StartStage());
        }
        void Block(Behaviour b) { if (b && !inputs.ContainsKey(b)) { inputs.Add(b,b.enabled); b.enabled=false; } }
        void Hide(GameObject go) { if(go && !hidden.ContainsKey(go)) { hidden.Add(go,go.activeSelf); go.SetActive(false); } }
        IEnumerator StartStage()
        {
            AcceptingInput = false; selected = 0; roundPulled.Clear(); stageIconStart = icons.Count;
            sequence.Clear();
            var pool = carrots.Where(c=>c && !c.IsPulled).ToList();
            for(int i=0;i<Stage;i++) { int n=UnityEngine.Random.Range(0,pool.Count); sequence.Add(pool[n]); pool.RemoveAt(n); }
            yield return Replay();
        }
        IEnumerator Replay()
        {
            AcceptingInput = false; SetHint(Stage+"단계 · 당근의 순서를 기억하세요");
            yield return new WaitForSeconds(.65f);
            foreach(var c in sequence) { yield return c.Cue(cueDuration); yield return new WaitForSeconds(cueGap); }
            SetHint(Stage+"단계 · 같은 순서로 클릭하세요  0/"+Stage+"   ·   Esc 종료");
            yield return null; AcceptingInput = true;
        }
        public void Select(NoseworkCarrot carrot)
        {
            if (!AcceptingInput || !carrot || carrot.game != this || carrot.IsPulled || (sharedUI && (sharedUI.IsPaused || sharedUI.IsDialogueOpen))) return;
            AcceptingInput = false;
            StartCoroutine(carrot == sequence[selected] ? Correct(carrot) : Wrong());
        }
        IEnumerator Correct(NoseworkCarrot carrot)
        {
            SetHint("겨울이가 당근을 찾고 있어요");
            yield return winter.Extract(carrot); roundPulled.Add(carrot); AddIcon(); selected++;
            if(selected < Stage) { SetHint(Stage+"단계 · 다음 당근을 선택하세요  "+selected+"/"+Stage+"   ·   Esc 종료"); AcceptingInput=true; yield break; }
            yield return winter.ReturnToSeat();
            if(Stage < 3) { Stage++; yield return new WaitForSeconds(.6f); yield return StartStage(); yield break; }
            SetHint("겨울이가 당근을 모두 찾았어요!"); yield return winter.Celebrate();
            yield return winter.ApproachForPetting(player.transform);
            playerCamera.fieldOfView=cameraFov;
            // Leave enough space in front of the mat for winter's entire seated body.
            playerCamera.transform.position=winter.PettingPosition+winter.PlayerSide*1.0f+Vector3.up*1.35f;
            var head=petting && petting.head ? petting.head:winter.winter;
            playerCamera.transform.rotation=Quaternion.LookRotation(head.position-playerCamera.transform.position);
            if(petting) petting.Prepare(playerCamera,winter);
            if(hint) hint.text="겨울이를 클릭하면 머리를 쓰다듬을 수 있어요.";
            WaitingForPetting=true; SetHint("겨울이를 클릭해 쓰다듬어 주세요.");
        }
        public void Pet()
        {
            if(!Running || !WaitingForPetting || !petting) return;
            WaitingForPetting=false; StartCoroutine(FinishAfterPetting());
        }
        IEnumerator FinishAfterPetting()
        {
            SetHint("잘했어, 겨울아."); yield return petting.Play();
            if(!petting.WasPetted) { WaitingForPetting=true; SetHint("겨울이를 클릭해 쓰다듬어 주세요."); yield break; }
            Completed=true; Restore(false); onCompleted.Invoke();
            if(Flow && Flow.VisitingObject==ToyId && !Flow.CompleteVisit(ToyId)) Debug.LogError("노즈워크 복귀 요청이 거절되었습니다.",this);
        }
        IEnumerator Wrong()
        {
            SetHint("다시 한번 기억해 볼까요?"); yield return winter.Mistake(playerCamera.transform.position);
            foreach(var c in roundPulled) c.Restore(); roundPulled.Clear();
            while(icons.Count>stageIconStart) { int i=icons.Count-1; Destroy(icons[i]); icons.RemoveAt(i); }
            selected = 0; yield return winter.ReturnToSeat(); yield return Replay();
        }
        public void Cancel() { if(!Running) return; StopAllCoroutines(); Restore(true); }
        void Restore(bool reset)
        {
            if (!Running || restoring) return; restoring=true; AcceptingInput=false; WaitingForPetting=false;
            if(petting) petting.Restore();
            if(reset) foreach(var c in carrots) if(c) c.Restore();
            if(winter) winter.Restore();
            if(playerCamera) { playerCamera.transform.localPosition=cameraPosition; playerCamera.transform.localRotation=cameraRotation; playerCamera.fieldOfView=cameraFov; }
            foreach(var pair in playerColliders) if(pair.Key) pair.Key.enabled=pair.Value; playerColliders.Clear();
            foreach(var pair in inputs) if(pair.Key) pair.Key.enabled=pair.Value; inputs.Clear();
            if(sharedUI) { if(sharedUI.objectiveText) sharedUI.objectiveText.text=savedObjective; if(sharedUI.hud) sharedUI.hud.SetActive(savedHudActive); }
            foreach(var pair in hidden) if(pair.Key) pair.Key.SetActive(pair.Value); hidden.Clear();
            Cursor.lockState=savedCursor; Cursor.visible=savedCursorVisible;
            if(canvas) Destroy(canvas.gameObject); icons.Clear(); Running=false; restoring=false;
        }
        void OnDisable() { if(Running) { StopAllCoroutines(); Restore(true); } }
        void OnDestroy() { if(placeholder) placeholder.enabled=placeholderEnabled; foreach(var pair in rootColliders) if(pair.Key) pair.Key.enabled=pair.Value; }
        void SetHint(string message)
        {
            if(sharedUI && sharedUI.objectiveText) sharedUI.objectiveText.text=message;
            else if(hint) hint.text=message;
        }
        void BuildUI()
        {
            var go = new GameObject("Nosework UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler)); go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=120;
            var scaler=go.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1920,1080);
            var label=new GameObject("Guide",typeof(RectTransform),typeof(TextMeshProUGUI)); label.transform.SetParent(go.transform,false);
            var rect=label.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=new Vector2(.5f,.94f); rect.sizeDelta=new Vector2(950,60);
            hint=label.GetComponent<TextMeshProUGUI>(); if(sharedUI && sharedUI.objectiveText) hint.font=sharedUI.objectiveText.font;
            hint.fontSize=25; hint.alignment=TextAlignmentOptions.Center; hint.raycastTarget=false;
            hint.text="움직이는 당근의 순서대로 선택하세요.  ·  Esc 종료";
        }
        void AddIcon()
        {
            var go=new GameObject("Collected Carrot "+(icons.Count+1),typeof(RectTransform),typeof(CanvasRenderer),typeof(NoseworkCarrotIcon)); go.transform.SetParent(canvas.transform,false);
            var rect=go.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=new Vector2(.94f,.53f); rect.pivot=new Vector2(.5f,1);
            rect.anchoredPosition=new Vector2(0,-icons.Count*62); rect.sizeDelta=new Vector2(48,56); go.GetComponent<NoseworkCarrotIcon>().raycastTarget=false; icons.Add(go);
        }
    }
}
