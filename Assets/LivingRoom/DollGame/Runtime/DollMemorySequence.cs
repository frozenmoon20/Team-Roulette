using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TeamRoulette.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TeamRoulette.Room3.DollMemory
{
    public enum DollMemoryState { Inactive, Chase, TakeDoll, MoveToWasher, WasherInteraction, Washing, MoveOutside, Drying, ReturnDoll, RunToCushion, RestWithDoll, Complete }
    [DefaultExecutionOrder(900)]
    public sealed class DollMemorySequence : MonoBehaviour
    {
        public const string ToyId="인형";
        public FirstPersonPlayer player;
        public PlayerInteractor interactor;
        public Camera playerCamera;
        public GameUI sharedUI;
        public DollWinter winter;
        public GameObject sceneDoll;
        public Transform entry, washerButton, drum, washerView, washerSeat, cushion, restView;
        public Transform[] chasePoints, washerRoute; public Transform insideDoor, interiorEntry, entranceView; public DollDoor interiorDoor; public Renderer washerRenderer; public Material washerCutaway; Material[] washerMaterials; DollYardBindings yardBinding;
        public float catchDistance=1.6f, washingSeconds=7, dryingSeconds=2.5f, restSeconds=3;
        public UnityEvent onCompleted=new UnityEvent();
        public DollMemoryState State {get;private set;}
        public event Action<DollMemoryState> StateChanged;
        public bool Running => State!=DollMemoryState.Inactive && State!=DollMemoryState.Complete;
        public Transform ActiveDoll => doll;
        public bool ButtonPressed {get;private set;}
        public int JumpCount {get;private set;}
        public int CompletionCount {get;private set;}
        public int OutdoorRouteIndex {get;private set;} = -1;
        public float OutdoorMaxHeight {get;private set;}
        public bool previewWhenOpenedDirectly;
        readonly Dictionary<Behaviour,bool> blocked=new Dictionary<Behaviour,bool>();
        readonly Dictionary<GameObject,bool> hidden=new Dictionary<GameObject,bool>();
        readonly List<GameObject> suspended=new List<GameObject>();
        Scene memoryScene,yardScene; bool ownsYard, completed, cleaning;
        Camera film; Transform doll, rackPoint, yardDoor;
        PlayerHand hand; Transform handVisual; Vector3 handPosition; Quaternion handRotation; bool handVisible; Transform handParent;
        Vector3 dogStart, chaseTarget; float chaseElapsed, pauseUntil; int chaseIndex;
        Coroutine stateRoutine; AudioSource sound; AudioClip beep;
        CanvasGroup fade; readonly List<Material> memoryMaterials=new List<Material>();
        string oldObjective; CursorLockMode cursor; bool cursorVisible;
        Room3FlowController Flow => GameManager.Instance ? GameManager.Instance.Flow:null;
        IEnumerable<T> Components<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true));
        IEnumerator Start()
        {
            memoryScene=gameObject.scene;
            yield return null;
            while(Flow && Flow.IsTransitioning) yield return null;
            if(Flow && Flow.VisitingObject==ToyId || previewWhenOpenedDirectly && (!Flow || string.IsNullOrEmpty(Flow.VisitingObject))) Begin();
        }
        public void Begin()
        {
            if(State!=DollMemoryState.Inactive || !winter || !sceneDoll || !player || !drum || !cushion) return;
            cursor=Cursor.lockState; cursorVisible=Cursor.visible;
            oldObjective=sharedUI && sharedUI.objectiveText?sharedUI.objectiveText.text:"";
            winter.Capture(); dogStart=winter.dog.position;
            foreach(var target in Components<Room3FlowTarget>(memoryScene)) Block(target);
            // The carrot and duck share the same memory scene, but never own it together.
            foreach(var b in Components<MonoBehaviour>(memoryScene).Where(b=>b.GetType().Namespace=="TeamRoulette.Room3.Nosework")) Block(b);
            Block(interactor);
            Hide(interactor?interactor.handIcon:null); Hide(interactor && interactor.nameText?interactor.nameText.gameObject:null);
            hand=player.GetComponentInChildren<PlayerHand>(true); if(hand) { Block(hand); if(hand.handModel) { handVisual=hand.handModel.transform; handParent=handVisual.parent; handPosition=handVisual.localPosition; handRotation=handVisual.localRotation; handVisible=handVisual.gameObject.activeSelf; } }
            var copy=Instantiate(sceneDoll,transform); copy.name="Doll Memory Duck";
            foreach(var b in copy.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled=false;
            foreach(var c in copy.GetComponentsInChildren<Collider>(true)) c.enabled=false;
            foreach(var body in copy.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic=true; body.detectCollisions=false; }
                        foreach(var renderer in copy.GetComponentsInChildren<Renderer>(true)){
                renderer.sharedMaterials=renderer.sharedMaterials.Select(source=>{var mat=new Material(source);if(mat.HasProperty("_Metallic"))mat.SetFloat("_Metallic",0);if(mat.HasProperty("_Smoothness"))mat.SetFloat("_Smoothness",.08f);if(mat.HasProperty("_Glossiness"))mat.SetFloat("_Glossiness",.08f);if(mat.HasProperty("_SpecularHighlights"))mat.SetFloat("_SpecularHighlights",0);if(mat.HasProperty("_EnvironmentReflections"))mat.SetFloat("_EnvironmentReflections",0);mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");memoryMaterials.Add(mat);return mat;}).ToArray();
            }
            copy.SetActive(true); doll=copy.transform;
            // Preserve the original world scale when attaching to small imported bones.
            doll.SetParent(null,true); winter.Carry(doll); Hide(sceneDoll);
            film=new GameObject("Doll Memory Camera").AddComponent<Camera>(); SceneManager.MoveGameObjectToScene(film.gameObject,memoryScene);
            film.fieldOfView=52; film.enabled=false; film.nearClipPlane=.04f;
            sound=gameObject.AddComponent<AudioSource>(); sound.playOnAwake=false;
            float[] samples=new float[4410]; for(int i=0;i<samples.Length;i++) samples[i]=Mathf.Sin(i*2*Mathf.PI*1300/44100f)*.15f*(1-i/(float)samples.Length);
            beep=AudioClip.Create("Washer button tick",samples.Length,1,44100,false); beep.SetData(samples,0);
            if(entry) player.Teleport(entry.position,entry.rotation,Quaternion.identity);
            chaseTarget=chasePoints.Length>0?chasePoints[0].position:dogStart;
            SetState(DollMemoryState.Chase);
        }
        void Block(Behaviour b) { if(b && !blocked.ContainsKey(b)) { blocked.Add(b,b.enabled); b.enabled=false; } }
        void Hide(GameObject go) { if(!go) return; if(!hidden.ContainsKey(go)) hidden.Add(go,go.activeSelf); go.SetActive(false); }
        void Objective(string text) { if(sharedUI && sharedUI.objectiveText) sharedUI.objectiveText.text=text; }
        bool Paused => Time.timeScale<=0 || sharedUI && (sharedUI.IsPaused || sharedUI.IsDialogueOpen);
        void Update()
        {
            if(!Running || Paused) return;
            if(State==DollMemoryState.Chase)
            {
                chaseElapsed+=Time.deltaTime;
                float distance=Vector3.Distance(player.transform.position,winter.dog.position);
                bool catchable=distance<=catchDistance;
                Objective(catchable?"인형 뺏기 · 마우스 왼쪽 클릭":"오리 인형을 문 겨울이를 따라가세요.");
                // After two short playful escapes, wait and let the player catch up.
                if(chaseElapsed<9 && distance<3.2f && Time.time>pauseUntil)
                {
                    winter.Play("Trot_F_IP");
                    if(winter.Step(chaseTarget,.95f,true)) { chaseIndex=(chaseIndex+1)%Mathf.Max(1,chasePoints.Length); chaseTarget=chasePoints.Length>0?chasePoints[chaseIndex].position:dogStart; pauseUntil=Time.time+1.1f; winter.Play("Idle_1"); }
                }
                else { winter.Play("Idle_1"); winter.Face(player.transform.position); }
                if(catchable && Input.GetMouseButtonDown(0)) TryTakeDoll();
            }
            else if(State==DollMemoryState.MoveToWasher && Vector3.Distance(playerCamera.transform.position,washerButton.position)<2.2f) { SetState(DollMemoryState.WasherInteraction); }
            else if(State==DollMemoryState.WasherInteraction && Input.GetMouseButtonDown(0))
            {
                Ray ray=playerCamera.ViewportPointToRay(new Vector3(.5f,.5f,0));
                if(Physics.Raycast(ray,out var hit,4,~0,QueryTriggerInteraction.Ignore) && hit.transform==washerButton) PressWasherButton();
            }
        }
        public bool TryTakeDoll()
        {
            if(State!=DollMemoryState.Chase || Paused || Vector3.Distance(player.transform.position,winter.dog.position)>catchDistance) return false;
            SetState(DollMemoryState.TakeDoll); return true;
        }
        public bool PressWasherButton()
        {
            if((State!=DollMemoryState.WasherInteraction && State!=DollMemoryState.MoveToWasher) || Paused || ButtonPressed || Vector3.Distance(playerCamera.transform.position,washerButton.position)>2.4f) return false;
            ButtonPressed=true; SetState(DollMemoryState.Washing); return true;
        }
        void SetState(DollMemoryState next)
        {
            if(stateRoutine!=null) StopCoroutine(stateRoutine);
            State=next; StateChanged?.Invoke(next); stateRoutine=StartCoroutine(Enter(next));
        }
        // Each state has its own short routine. The dispatcher contains no scene choreography.
        IEnumerator Enter(DollMemoryState state)
        {
            switch(state)
            {
                case DollMemoryState.TakeDoll: yield return TakeDoll(); break;
                case DollMemoryState.MoveToWasher: yield return MoveToWasher(); break;
                case DollMemoryState.WasherInteraction: Objective("세탁기 시작 버튼을 클릭하세요."); break;
                case DollMemoryState.Washing: yield return Wash(); break;
                case DollMemoryState.MoveOutside: yield return MoveOutside(); break;
                case DollMemoryState.Drying: yield return Dry(); break;
                case DollMemoryState.ReturnDoll: yield return GiveBack(); break;
                case DollMemoryState.RunToCushion: yield return ToCushion(); break;
                case DollMemoryState.RestWithDoll: yield return Rest(); break;
                case DollMemoryState.Complete: yield return Complete(); break;
            }
        }
        void Next(DollMemoryState state) { stateRoutine=null; SetState(state); }
        IEnumerator TakeDoll()
        {
            Block(player); winter.Play("Pick_up_idle"); yield return new WaitForSeconds(.6f);
            HoldDoll(); if(handVisual) handVisual.gameObject.SetActive(true);
            if(blocked.TryGetValue(player,out var enabled))player.enabled=enabled;
            if(interactor) { interactor.enabled=true; if(interactor.nameText)interactor.nameText.gameObject.SetActive(true); }
            Next(DollMemoryState.MoveToWasher);
        }
        void HoldDoll()
        {
            doll.SetParent(film && film.enabled?film.transform:playerCamera.transform,true);
            doll.localPosition=new Vector3(.22f,-.24f,.6f); doll.localRotation=Quaternion.Euler(0,180,10);
        }
        IEnumerator MoveToWasher()
        {
            Objective("오리 인형을 세탁기로 가져가세요. 화장실 문과 세탁기 버튼을 클릭할 수 있습니다.");
            winter.Play("Idle_1");
            // PlayerInteractor retains normal door and collider-only button interactions.
            while(!ButtonPressed) yield return null;
        }
        IEnumerator Wash()
        {
            Objective(""); Cursor.visible=false; Cursor.lockState=CursorLockMode.Locked;
            Block(player); Block(interactor); foreach(var b in player.GetComponentsInChildren<PlayerMotionFeel>(true)) Block(b);
            film.transform.SetPositionAndRotation(playerCamera.transform.position,playerCamera.transform.rotation); film.enabled=true;playerCamera.enabled=false;HoldDoll();
            if(handVisual)handVisual.SetParent(film.transform,true);
            yield return TouchButton(); sound.PlayOneShot(beep);
            doll.SetParent(null,true); doll.position=drum.position; doll.rotation=drum.rotation;
            if(washerRenderer && washerCutaway){washerMaterials=washerRenderer.sharedMaterials;washerRenderer.sharedMaterials=washerMaterials.Select(m=>washerCutaway).ToArray();}
            winter.dog.SetPositionAndRotation(washerSeat.position,washerSeat.rotation);yield return winter.Sit();
            HideHUD();
            Vector3 seated=washerView.position;
            yield return CameraMove(seated,washerView.rotation,.7f); if(handVisual)handVisual.gameObject.SetActive(false);
            Vector3 origin=drum.position;
            for(float t=0;t<washingSeconds;t+=Time.deltaTime)
            {
                doll.position=origin+drum.right*Mathf.Sin(t*7)*.11f+drum.up*Mathf.Cos(t*7)*.11f;
                doll.Rotate(new Vector3(170,250,380)*Time.deltaTime,Space.Self);
                winter.HoldSitting();
                yield return null;
            }
            yield return new WaitForSeconds(.5f); HoldDoll(); Next(DollMemoryState.MoveOutside);
        }
        IEnumerator TouchButton()
        {
            if(!handVisual) { yield return new WaitForSeconds(.35f); yield break; }
            handVisual.gameObject.SetActive(true); Vector3 start=handVisual.position; Quaternion rotation=handVisual.rotation;
            for(float t=0;t<.7f;t+=Time.deltaTime) { float a=Mathf.Sin(t/.7f*Mathf.PI); handVisual.position=Vector3.Lerp(start,washerButton.position,a); yield return null; }
            handVisual.SetPositionAndRotation(start,rotation);
        }
        void HideHUD()
        {
            if(sharedUI) { Hide(sharedUI.hud); Hide(sharedUI.dialoguePanel); if(sharedUI.objectiveText) Hide(sharedUI.objectiveText.gameObject); }
            Hide(interactor?interactor.crosshairDot:null);
            foreach(var canvas in Components<Canvas>(memoryScene))
                if(!canvas.transform.IsChildOf(transform)) { Block(canvas); canvas.enabled=false; }
        }
        IEnumerator MoveOutside()
        {
            yield return Fade(1,.3f);
            yardScene=SceneManager.GetSceneByName(Room3FlowController.ToyRoom);
            ownsYard=!yardScene.IsValid() || !yardScene.isLoaded;
            if(ownsYard) yield return SceneManager.LoadSceneAsync(Room3FlowController.ToyRoom,LoadSceneMode.Additive);
            SceneManager.SetActiveScene(yardScene=SceneManager.GetSceneByName(Room3FlowController.ToyRoom));
            var binding=Components<DollYardBindings>(yardScene).FirstOrDefault();
            if(!binding || !binding.dryingPoint || !binding.houseDoor) { Debug.LogError("[DollMemory] 마당 건조대/집 문 연결이 없습니다.",this); Cleanup(); yield break; }
            yardBinding=binding; rackPoint=binding.dryingPoint; yardDoor=binding.houseDoor;
            foreach(var camera in Components<Camera>(yardScene)) Block(camera); foreach(var listener in Components<AudioListener>(yardScene)) Block(listener);
            foreach(var p in Components<FirstPersonPlayer>(yardScene)) { Block(p); foreach(var c in p.GetComponentsInChildren<Collider>()) c.enabled=false; }
            foreach(var ui in Components<GameUI>(yardScene)) Hide(ui.gameObject);
            foreach(var t in Components<Transform>(yardScene).Where(t=>t.name=="Spitz_anim_IP" || t.name=="08_duck_prefab")) Hide(t.gameObject);
            foreach(var root in memoryScene.GetRootGameObjects())
                if(root.activeSelf && root!=gameObject && !winter.dog.IsChildOf(root.transform) && !player.transform.IsChildOf(root.transform) && root!=film.gameObject) { suspended.Add(root); root.SetActive(false); }
            doll.SetParent(null,true); doll.SetPositionAndRotation(rackPoint.position,rackPoint.rotation);
            winter.dog.SetPositionAndRotation(binding.dogStand.position,binding.dogStand.rotation);
            film.transform.SetPositionAndRotation(binding.view.position,binding.view.rotation);
            yield return Fade(0,.35f); Next(DollMemoryState.Drying);
        }
        IEnumerator Dry()
        {
            for(int i=0;i<3;i++) { winter.Play("Idle_1"); yield return new WaitForSeconds(.65f); winter.Play("Jump_Place_IP"); JumpCount++; yield return new WaitForSeconds(1.1f); winter.Play("Idle_1"); }
            yield return new WaitForSeconds(dryingSeconds);
            yield return Fade(1,.5f); yield return new WaitForSeconds(.5f); yield return Fade(0,.5f);
            Next(DollMemoryState.ReturnDoll);
        }
        IEnumerator GiveBack()
        {
            Vector3 from=doll.position;
            Vector3 held=film.transform.TransformPoint(new Vector3(.22f,-.24f,.6f));
            if(handVisual)handVisual.gameObject.SetActive(true);
            for(float t=0;t<.9f;t+=Time.deltaTime){doll.position=Vector3.Lerp(from,held,Mathf.SmoothStep(0,1,t/.9f));yield return null;}
            HoldDoll(); yield return new WaitForSeconds(.5f);
            for(float t=0;t<.55f;t+=Time.deltaTime){winter.Face(film.transform.position);yield return null;}
            yield return CameraLook(winter.mouth.position,.65f);
            from=doll.position; doll.SetParent(null,true);
            for(float t=0;t<.8f;t+=Time.deltaTime){doll.position=Vector3.Lerp(from,winter.mouth.position,Mathf.SmoothStep(0,1,t/.8f));yield return null;}
            winter.Play("Pick_up"); yield return new WaitForSeconds(.6f); winter.Carry(doll);
            if(handVisual)handVisual.gameObject.SetActive(false);
            if(yardBinding.returnRoute==null || yardBinding.returnRoute.Length<4) {Debug.LogError("[DollMemory] 계단 복귀 경로 연결이 없습니다.",this);yield break;}
            for(int i=0;i<yardBinding.returnRoute.Length;i++)
            {
                OutdoorRouteIndex=i;
                winter.Play(i>=1?"Walk_F_IP":"Trot_F_IP");
                float limit=25;
                while(limit>0)
                {
                    bool reached=winter.GroundStep(yardBinding.returnRoute[i].position,i>=1?.85f:1.6f,yardBinding.walkingSurfaces,out var blocked);
                    if(blocked){Debug.LogError("[DollMemory] 계단 바닥 지지 검사 실패: "+i+" "+winter.dog.position,this);yield break;}
                    OutdoorMaxHeight=Mathf.Max(OutdoorMaxHeight,winter.dog.position.y);
                    if(reached)break;
                    limit-=Time.deltaTime;
                    film.transform.rotation=Quaternion.Slerp(film.transform.rotation,Quaternion.LookRotation(winter.dog.position+Vector3.up*.45f-film.transform.position),Time.deltaTime*3);
                    yield return null;
                }
                if(limit<=0){Debug.LogError("[DollMemory] 계단 이동 시간 초과",this);yield break;}
            }
            winter.Play("Idle_1");
            yield return CameraLook(yardDoor.position+Vector3.up*1.15f,.6f);
            Next(DollMemoryState.RunToCushion);
        }
        IEnumerator FollowRun(Vector3 target,float speed)
        {
            winter.Play("Run_F_IP");float limit=12;
            while(limit>0&&!winter.Step(target,speed,false)){
                limit-=Time.deltaTime;Vector3 look=winter.dog.position+Vector3.up*.45f;
                film.transform.rotation=Quaternion.Slerp(film.transform.rotation,Quaternion.LookRotation(look-film.transform.position),Time.deltaTime*3);
                yield return null;
            }
            winter.Play("Idle_1");
        }
        IEnumerator CameraLook(Vector3 target,float duration)
        {yield return CameraMove(film.transform.position,Quaternion.LookRotation(target-film.transform.position),duration);}
        IEnumerator ToCushion()
        {
            yield return Fade(1,.25f);
            SceneManager.SetActiveScene(memoryScene);
            foreach(var root in suspended)if(root)root.SetActive(true);suspended.Clear();HideHUD();
            foreach(var root in yardScene.GetRootGameObjects())root.SetActive(false);
            if(ownsYard)yield return SceneManager.UnloadSceneAsync(yardScene);
            winter.dog.position=interiorEntry?interiorEntry.position:new Vector3(-1,.7f,3.5f);
            film.transform.SetPositionAndRotation(entranceView?entranceView.position:restView.position,entranceView?entranceView.rotation:restView.rotation);
            yield return Fade(0,.3f);
            if(interiorDoor)yield return interiorDoor.Open();
            if(insideDoor)yield return FollowRun(insideDoor.position,1.8f);
            yield return FollowRun(cushion.position,1.6f);
            winter.dog.SetPositionAndRotation(cushion.position,cushion.rotation);
            yield return CameraMove(restView.position,restView.rotation,1);
            Next(DollMemoryState.RestWithDoll);
        }
        IEnumerator Rest()
        {
            HideHUD(); if(handVisual) handVisual.gameObject.SetActive(false);
            winter.Play("Lie_start"); yield return new WaitForSeconds(.9f); winter.Play("Lie_loop_1");
            doll.SetParent(winter.dog,true); doll.localPosition=winter.restOffset; doll.localRotation=Quaternion.Euler(0,90,0);
            yield return new WaitForSeconds(Mathf.Max(3,restSeconds)); Next(DollMemoryState.Complete);
        }
        IEnumerator Complete()
        {
            if(completed) yield break; completed=true; CompletionCount++; onCompleted.Invoke();
            bool hasVisit=Flow && Flow.VisitingObject==ToyId;
            Cleanup();
            if(hasVisit) { if(!Flow.CompleteVisit(ToyId)) Debug.LogError("[DollMemory] 회상 완료 전달 실패",this); }
            else if(Application.CanStreamedLevelBeLoaded(Room3FlowController.LivingRoom))
            { DollMemoryCompletion.Pending=true; SceneManager.LoadScene(Room3FlowController.LivingRoom); }
            yield break;
        }
        IEnumerator CameraMove(Vector3 position,Quaternion rotation,float duration)
        {
            Vector3 start=film.transform.position; Quaternion from=film.transform.rotation;
            for(float t=0;t<duration;t+=Time.deltaTime) { float a=Mathf.SmoothStep(0,1,t/duration); film.transform.SetPositionAndRotation(Vector3.Lerp(start,position,a),Quaternion.Slerp(from,rotation,a)); yield return null; }
            film.transform.SetPositionAndRotation(position,rotation);
        }
        IEnumerator Fade(float alpha,float seconds)
        {
            if(!fade)
            {
                var go=new GameObject("Doll Memory Fade",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));
                go.transform.SetParent(transform,false); go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                go.GetComponent<Canvas>().sortingOrder=32000; fade=go.GetComponent<CanvasGroup>(); fade.alpha=0; fade.blocksRaycasts=false;
                var panel=new GameObject("Black",typeof(RectTransform),typeof(Image)); panel.transform.SetParent(go.transform,false);
                panel.GetComponent<Image>().color=Color.black; panel.GetComponent<Image>().raycastTarget=false;
                var rect=panel.GetComponent<RectTransform>(); rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero;
            }
            float from=fade.alpha;
            for(float t=0;t<seconds;t+=Time.deltaTime) { fade.alpha=Mathf.Lerp(from,alpha,t/seconds); yield return null; }
            fade.alpha=alpha;
        }
        void Cleanup()
        {
            if(cleaning) return; cleaning=true;
            foreach(var pair in blocked) if(pair.Key) pair.Key.enabled=pair.Value;
            foreach(var pair in hidden) if(pair.Key) pair.Key.SetActive(pair.Value);
            foreach(var root in suspended) if(root) root.SetActive(true);
            blocked.Clear(); hidden.Clear(); suspended.Clear();
            if(washerRenderer && washerMaterials!=null)washerRenderer.sharedMaterials=washerMaterials; if(interiorDoor)interiorDoor.Restore();
            if(winter) winter.Restore(); if(playerCamera) playerCamera.enabled=true;
            if(handVisual) { handVisual.SetParent(handParent,false); handVisual.localPosition=handPosition; handVisual.localRotation=handRotation; handVisual.gameObject.SetActive(handVisible); }
            if(sharedUI && sharedUI.objectiveText) sharedUI.objectiveText.text=oldObjective;
            foreach(var material in memoryMaterials)if(material)Destroy(material);memoryMaterials.Clear();
            if(doll) Destroy(doll.gameObject); if(film) Destroy(film.gameObject); if(beep) Destroy(beep); if(sound) Destroy(sound); if(fade) Destroy(fade.gameObject);
            Cursor.lockState=cursor; Cursor.visible=cursorVisible;
        }
        void OnDisable() { if(Running) { StopAllCoroutines(); Cleanup(); } }
    }
}


