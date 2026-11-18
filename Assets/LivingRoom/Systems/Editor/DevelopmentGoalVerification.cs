using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TeamRoulette.Room3;
using TeamRoulette.Room3.TugPlay;
using TeamRoulette.Room3.BonePlay;
using TeamRoulette.Room3.DollMemory;
using TeamRoulette.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Exercise the same entry point as F1 against the actual controllers and scenes.
[InitializeOnLoad]
public static class DevelopmentGoalVerification
{
    const string Request = "Temp/DevelopmentGoalVerification.request", Report = "Temp/DevelopmentGoalVerification.txt";
    static readonly Stack<IEnumerator> work = new Stack<IEnumerator>();
    static double due;
    static DevelopmentGoalVerification()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("Room3.DebugVerify", false))
            { SessionState.SetBool("Room3.DebugVerify", false); work.Clear(); work.Push(Verify()); due=EditorApplication.timeSinceStartup+1; EditorApplication.update+=Pump; }
            if (state == PlayModeStateChange.ExitingPlayMode) EditorApplication.update-=Pump;
        };
    }
    static void Poll()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        try { File.Delete(Request); } catch(IOException) { return; }
        Run();
    }
    [MenuItem("Tools/Room3/Verify F1 completion")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory("Temp"); File.WriteAllText(Report,"F1 completion verification\n");
        EditorSceneManager.OpenScene("Assets/Scenes/Room3_LivingRoom.unity");
        SessionState.SetBool("Room3.DebugVerify",true); EditorApplication.EnterPlaymode();
    }
    static void Pump()
    {
        if(EditorApplication.timeSinceStartup<due) return;
        try {
            while(work.Count>0) {
                var step=work.Peek();
                if(!step.MoveNext()) {work.Pop();continue;}
                if(step.Current is IEnumerator child) {work.Push(child);continue;}
                due=EditorApplication.timeSinceStartup+(step.Current==null?.05:Convert.ToDouble(step.Current));return;
            }
            Finish("ALL CHECKS PASSED");
        } catch(Exception e) {Finish("FAILED: "+e);}
    }
    static void Finish(string result)
    { EditorApplication.update-=Pump;File.AppendAllText(Report,result+"\n");Debug.Log(result+" "+Report);Time.timeScale=1;EditorApplication.ExitPlaymode(); }
    static void Check(bool ok,string label)
    { if(!ok)throw new Exception(label);File.AppendAllText(Report,"PASS: "+label+"\n"); }
    static IEnumerator Until(Func<bool> ready,string label)
    { double end=EditorApplication.timeSinceStartup+25;while(!ready()){if(EditorApplication.timeSinceStartup>end)throw new Exception(label);yield return .05;}yield return .1; }
    static T Find<T>() where T:Component => Object.FindFirstObjectByType<T>();
    static IEnumerator Skip(GameStage stage)
    {
        var manager=GameManager.Instance;Check(manager.CurrentStage==stage,"Current stage "+stage);
        int events=0; Action<GameStage> completed=s=>{if(s==stage)events++;}; manager.Completed+=completed;
        Check(manager.DebugCompleteCurrentObjective(),"F1 accepted for "+stage);
        yield return Until(()=>manager.CurrentStage!=stage,"Advance from "+stage);
        Check(manager.CurrentStage==GameManager.NextStage(stage) && manager.IsComplete(stage) && events==1,"Normal completion event and next stage: "+stage);
        manager.Completed-=completed;
    }
    static IEnumerator Verify()
    {
        Application.runInBackground=true;
        var m=GameManager.Instance;Check(m && m.Flow,"Existing manager owns debug shortcut");
        var ui=Find<GameUI>();ui.OpenSettings();Check(!m.DebugCompleteCurrentObjective(),"Paused input cannot skip a stage");ui.CloseSettings();yield return .15;
        var barriers=Object.FindObjectsByType<Room3Barrier>(FindObjectsSortMode.None);
        var barrier=barriers.Single(b=>b.Remark=="올라갈 필요 없다.");
        var windowBoundary=barriers.Single(b=>b.Remark=="아직은...");
        Check(barrier.transform.parent && barrier.transform.parent==windowBoundary.transform.parent &&
            barrier.transform.parent.name=="Barrier","Stair and window boundaries share the Barrier parent");
        Check(barrier && barrier.GetComponent<BoxCollider>().enabled && !barrier.GetComponent<BoxCollider>().isTrigger,"Stair barrier is a solid collider");
        var testPlayer=Find<FirstPersonPlayer>();var body=testPlayer.GetComponent<CharacterController>();
        var savedPosition=testPlayer.transform.position;
        body.enabled=false;testPlayer.transform.position=barrier.transform.position+new Vector3(0,0,1);body.enabled=true;Physics.SyncTransforms();
        body.Move(Vector3.back*2);
        Check(testPlayer.transform.position.z>barrier.transform.position.z,"Character cannot cross upper stair boundary");
        body.enabled=false;testPlayer.transform.position=savedPosition;body.enabled=true;Physics.SyncTransforms();
        foreach(var action in new[]{MealAction.Fridge,MealAction.Toaster,MealAction.EmptyCup}) {
            var target=Object.FindObjectsByType<MealTarget>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(t=>t.action==action);
            Check(!string.IsNullOrWhiteSpace(target.DisplayName) && target is IInteractable,"Kitchen hover target labelled outside meal stage: "+action);
        }
        yield return Until(()=>ui.IsDialogueOpen && ui.dialogueText.text==barrier.Remark,"Stair contact shows its remark");
        Check(true,"Stair contact shows its remark");
        yield return Skip(GameStage.Curtain);
        Check(Find<CurtainGameController>().Completed && Find<FirstPersonPlayer>().transform.localScale==Vector3.one,"Curtain completion restores player scale");
        yield return Skip(GameStage.Window);
        Check(Find<WindowGameController>().WindowOpened && Find<FirstPersonPlayer>().canMove,"Window opened and movement restored");
        var windowCollider=windowBoundary.GetComponent<BoxCollider>();
        Check(windowCollider.enabled && !windowCollider.isTrigger,"Window has a fixed solid barrier after opening");
        Check(Find<CurtainGameController>().dog.GetComponentsInChildren<Collider>().Any(c=>c.enabled && !c.isTrigger),
            "Winter has solid body collision");
        Check(!testPlayer.GetComponentsInChildren<Light>(true).Any(l=>l.name=="HandLight" && l.enabled),
            "Living room HandLight is disabled");
        savedPosition=testPlayer.transform.position;
        var windowPlane=windowBoundary.transform;
        var direction=windowPlane.forward * Mathf.Sign(Vector3.Dot(savedPosition-windowPlane.position,windowPlane.forward));
        body.enabled=false;
        testPlayer.transform.position=new Vector3(windowPlane.position.x,windowCollider.bounds.min.y+.1f,windowPlane.position.z)+direction;
        body.enabled=true;
        Physics.SyncTransforms();
        body.Move(-direction*2);
        Check(Vector3.Dot(testPlayer.transform.position-windowPlane.position,direction)>0,"Player cannot cross the open window");
        body.enabled=false;
        testPlayer.transform.position=savedPosition;
        body.enabled=true;
        Physics.SyncTransforms();
        yield return Until(()=>ui.IsDialogueOpen && ui.dialogueText.text==windowBoundary.Remark,"Window contact shows its remark");
        Check(true,"Window contact shows its remark");
        Check(ObjectiveManager.GetObjective(GameStage.Wash)=="화장실에 가자","Wash begins with bathroom objective");
        Find<RestroomDoor>().Interact();
        Check(Find<SceneUIContent>().objectives.Any(o=>o.visible && o.text=="물을 틀자"),"Bathroom door click changes objective to water");
        var wash=Find<WashGameController>();
        var aim=Find<PlayerInteractor>();var camera=aim.playerCamera.transform;
        var originalCameraPosition=camera.position;var originalCameraRotation=camera.rotation;
        var sink=Object.FindObjectsByType<WashTarget>(FindObjectsSortMode.None).First(t=>t.name=="T_sink");
        Check(sink.game==wash,"Visible sink references the current wash controller");
        var sinkBounds=sink.GetComponent<Collider>().bounds;
        camera.SetPositionAndRotation(sinkBounds.center+new Vector3(0,.55f,1),Quaternion.LookRotation(new Vector3(0,-.55f,-1)));
        typeof(PlayerInteractor).GetMethod("UpdateHover",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(aim,new object[]{true});
        typeof(PlayerInteractor).GetMethod("TryInteract",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(aim,null);
        Check(wash.IsActive && wash.CurrentStage==WashGameController.Stage.WaterRunning,"Aiming and clicking the sink starts WashGame");
        wash.Click(WashAction.Water);wash.Click(WashAction.Soap);
        yield return Skip(GameStage.Wash);
        Check(!wash.IsActive && !OptionState.Instance.IsUnlocked(OptionType.Blur) && wash.viewCamera.targetTexture==null,"Wash releases blur, painting input and render target");
        camera.SetPositionAndRotation(originalCameraPosition,originalCameraRotation);
        var counter=Object.FindObjectsByType<MealTarget>(FindObjectsSortMode.None).First(t=>t.action==MealAction.Counter);
        var counterBounds=counter.GetComponentsInChildren<Collider>().First(c=>c.enabled&&!c.isTrigger).bounds;
        camera.SetPositionAndRotation(counterBounds.center+Vector3.up*(counterBounds.extents.y+.7f),Quaternion.LookRotation(Vector3.down,Vector3.forward));
        Physics.SyncTransforms();
        Check(Find<MealInput>().FindTarget()==counter,"Countertop aim resolves the jam placement target");
        for(int i=0;i<12;i++) {
            Check(aim.TryAimHit(out var hit) && hit.collider.GetComponentInParent<MealTarget>()==Find<MealInput>().FindTarget(),"Shared and meal aim agree on counter sample "+i);
            yield return null;
        }
        camera.SetPositionAndRotation(originalCameraPosition,originalCameraRotation);
        var meal=Find<MealGameController>();meal.Click(MealAction.Fridge);yield return .2;
        yield return Skip(GameStage.Meal);
        Check(meal.Completed && !meal.Ending && !meal.timerPanel.activeSelf && meal.blackScreen.alpha==0,"Meal releases held items, countdown and blackout");
        // Visit all real toy games; F1 must use each game's own completion callback.
        foreach(string id in new[]{"로프","뼈다귀 장난감","인형"}) {
            var source=Object.FindObjectsByType<Room3FlowTarget>(FindObjectsSortMode.None).First(t=>t.ObjectId==id && t.gameObject.scene.name==Room3FlowController.LivingRoom);
            source.Interact();
            yield return Until(()=>!m.Flow.IsTransitioning && m.Flow.VisitingObject==id,"Enter toy "+id);
            MonoBehaviour game=id=="로프"?(MonoBehaviour)Find<TugGame>():id=="뼈다귀 장난감"?Find<BoneCatchGame>():Find<DollMemorySequence>();
            Check(game,"Real toy controller exists: "+id);
            int events=0;((UnityEvent)game.GetType().GetField("onCompleted").GetValue(game)).AddListener(()=>events++);
            yield return .3;
            if(id=="인형") {
                typeof(DollMemorySequence).GetMethod("SetState",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{DollMemoryState.MoveOutside});
                yield return Until(()=>SceneManager.GetActiveScene().name==Room3FlowController.ToyRoom,"Doll memory reaches yard");
                yield return .3;
            }
            Check(m.DebugCompleteCurrentObjective(),"F1 accepted during toy "+id);
            Check(!m.DebugCompleteCurrentObjective(),"Repeated debug request blocked during async completion");
            yield return Until(()=>!m.Flow.IsTransitioning && m.IsToyComplete(id) && string.IsNullOrEmpty(m.Flow.VisitingObject),"Toy completion returns home "+id);
            Check(events==1 && SceneManager.GetActiveScene().name==Room3FlowController.LivingRoom && SceneManager.sceneCount==1,"Toy callback once and all visit scenes unloaded: "+id);
            Check(!source.gameObject.activeSelf,"Completed source toy hidden: "+id);
        }
        Check(m.CompletedToyCount==3 && m.CurrentStage==GameStage.TV,"Three toy completions unlock TV");
        yield return Skip(GameStage.TV);
        Check(Find<TVGameController>().IsComplete && Find<FirstPersonPlayer>().enabled,"TV completion restores room input");
        yield return Skip(GameStage.Board);
        Check(Find<BoardGameController>().Completed && Find<PlayerInteractor>().enabled,"Board follows valid finish path and releases input");
        yield return Skip(GameStage.Entrance);
        Check(m.IsFinished && Find<PorchGameController>().IsRunning,"Entrance follows normal credits ending");
        Check(!m.DebugCompleteCurrentObjective(),"F1 has no effect after ending");
    }
}
