#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TeamRoulette.Room3;
using TeamRoulette.Room3.DollMemory;
using TeamRoulette.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
[InitializeOnLoad]
public static class DollMemoryVerification
{
    const string Folder="Assets/LivingRoom/DollGame/Editor/";
    static double began; static int phase,completed; static DollMemoryState previous; static float restBegan;
    static float chaseStarted=-1, stateSince; static readonly System.Collections.Generic.HashSet<string> pictures=new System.Collections.Generic.HashSet<string>();
    static DollMemoryVerification() { EditorApplication.update+=Tick; Application.logMessageReceived+=Log; }
    static void Log(string message,string stack,LogType type) { if(SessionState.GetBool("Doll.Verify",false) && (type==LogType.Exception || type==LogType.Error)) File.AppendAllText(Folder+"PlayVerification.txt","ERROR: "+message+"\n"+stack+"\n"); }
    static void Check(bool test,string label) { if(!test) throw new Exception(label); File.AppendAllText(Folder+"PlayVerification.txt","PASS: "+label+"\n"); }
    static void Tick()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if(!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Folder+"Verify.request") && File.ReadAllText(Folder+"Verify.request").Trim()=="verify")
        {
            File.WriteAllText(Folder+"Verify.request","running"); File.WriteAllText(Folder+"PlayVerification.txt","오리 인형 회상 통합 재생 검사\n");
            SessionState.SetBool("Doll.Verify",true); EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Room3_LivingRoom.unity"); EditorApplication.isPlaying=true; return;
        }
        if(!SessionState.GetBool("Doll.Verify",false) || !EditorApplication.isPlaying) return;
        try
        {
            if(began==0) began=EditorApplication.timeSinceStartup;
            if(EditorApplication.timeSinceStartup-began>150) throw new Exception("통합 검사 시간 초과 phase="+phase);
            foreach(var ui in UnityEngine.Object.FindObjectsByType<GameUI>(FindObjectsSortMode.None)) if(ui.IsDialogueOpen) ui.AdvanceDialogue();
            var manager=GameManager.Instance; if(!manager) return;
            if(phase==0)
            {
                if(Time.timeSinceLevelLoad<1) return;
                while(manager.CurrentStage!=GameStage.Toys) { if(!GameManager.ReportComplete(manager.CurrentStage)) return; }
                var toy=UnityEngine.Object.FindObjectsByType<Room3FlowTarget>(FindObjectsSortMode.None).First(t=>t.ObjectId==DollMemorySequence.ToyId);
                manager.Flow.Click(toy); phase=1; return;
            }
            var game=UnityEngine.Object.FindFirstObjectByType<DollMemorySequence>();
            if(phase==1)
            {
                if(!game || game.State!=DollMemoryState.Chase) return;
                if(chaseStarted<0) { chaseStarted=Time.time; return; }
                if(Time.time-chaseStarted<5) return;
                Check(game.player.transform.position.y>=game.entry.position.y-.15f,"추격 시작 후 5초 동안 플레이어 바닥 관통 없음");
                Check(manager.Flow.VisitingObject==DollMemorySequence.ToyId,"현재 거실 인형 클릭으로 과거 거실 추격 시작");
                Check(game.player.enabled,"추격 중 기존 플레이어 조작 유지");
                Check(!game.TryTakeDoll(),"거리 밖에서는 인형을 뺏을 수 없음");
                game.player.Teleport(game.winter.dog.position+Vector3.back*.9f,Quaternion.identity,Quaternion.identity);
                game.onCompleted.AddListener(()=>completed++);
                Check(game.TryTakeDoll(),"가까이 접근한 뒤 인형 뺏기");
                Check(!game.player.enabled,"인형을 뺏는 짧은 동작 동안 입력 잠금"); phase=2;
            }
            if(phase==2 && game)
            {
                if(game.State==DollMemoryState.MoveToWasher || game.State==DollMemoryState.WasherInteraction)
                {
                    var door=UnityEngine.Object.FindFirstObjectByType<RestroomDoor>();if(door&&!door.IsOpen)door.Interact();
                    if(game.State==DollMemoryState.MoveToWasher){
                        var goal=game.washerRoute[Mathf.Min(walkPoint,game.washerRoute.Length-1)].position;goal.y=game.player.transform.position.y;
                        var delta=goal-game.player.transform.position;
                        if(delta.magnitude<.2f)walkPoint=Mathf.Min(walkPoint+1,game.washerRoute.Length-1);
                        else game.player.GetComponent<CharacterController>().Move(delta.normalized*.07f);
                    }
                }
                if(game.State!=previous)
                {
                    File.AppendAllText(Folder+"PlayVerification.txt","STATE: "+game.State+"\n"); previous=game.State; stateSince=Time.time;
                    if(game.State==DollMemoryState.RunToCushion){Check(game.OutdoorRouteIndex>=5,"계단 각 단과 현관 바닥 경로를 통과");Check(game.OutdoorMaxHeight>.65f,"겨울이가 마당에서 현관 높이까지 상승");}
                    if(game.State==DollMemoryState.RestWithDoll) restBegan=Time.time;
                }
                if(game.State==DollMemoryState.WasherInteraction)
                {
                    var camera=game.playerCamera;
                    camera=game.playerCamera; var ray=new Ray(camera.transform.position,game.washerButton.position-camera.transform.position);
                    Check(Physics.Raycast(ray,out var hit,4) && hit.transform==game.washerButton,"세탁기 시작 버튼 실제 콜라이더 클릭 가능");
                    Check(game.PressWasherButton(),"세탁기 시작 1회"); Check(!game.PressWasherButton(),"세탁기 중복 시작 차단");
                }
                if(game.State==DollMemoryState.Drying){CheckYardOnce(game);if(Time.time-stateSince>2)Frame("Drying");}
                if(game.State==DollMemoryState.Washing && Time.time-stateSince>3){if(!pictures.Contains("Washing"))Check(game.winter.IsSitting,"세탁 관람 중 Sitting loop 상태 유지");Frame("Washing");}
                if(game.State==DollMemoryState.ReturnDoll && Time.time-stateSince>2.7f)Frame("ReturnDoll");
                if(game.State==DollMemoryState.ReturnDoll && game.OutdoorRouteIndex>=2 && game.OutdoorRouteIndex<=4)Frame("Stairs");
                if(game.State==DollMemoryState.RunToCushion && Time.time-stateSince>2){if(!pictures.Contains("Entrance"))Check(game.interiorDoor.IsOpen,"집 문이 열린 뒤 겨울이가 진입");Frame("Entrance");}
                if(game.State==DollMemoryState.RestWithDoll && Time.time-restBegan>1.5f && !File.Exists(Folder+"Rest.png")) Capture(game);
            }
            if(phase==2 && completed>0)
            {
                if(manager.Flow.IsTransitioning) return;
                Check(completed==1,"완료 이벤트 정확히 1회"); Check(SceneManager.GetActiveScene().name==Room3FlowController.LivingRoom,"현재 거실 복귀");
                Check(manager.IsToyComplete(DollMemorySequence.ToyId),"Room3 인형 정리 완료 기록");
                Check(!SceneManager.GetSceneByName(Room3FlowController.ToyRoom).isLoaded && !SceneManager.GetSceneByName(Room3FlowController.OldLivingRoom).isLoaded,"임시 회상 씬 정리");
                Check(UnityEngine.Object.FindFirstObjectByType<FirstPersonPlayer>().enabled,"기존 현재 시점 플레이어 조작 복구");
                End("passed");
            }
        }
        catch(Exception e) { File.AppendAllText(Folder+"PlayVerification.txt","FAIL: "+e+"\n"); End("failed"); }
    }
    static bool yardChecked; static int walkPoint;
    static void CheckYardOnce(DollMemorySequence game)
    {
        if(yardChecked) return; yardChecked=true;
        Check(SceneManager.GetActiveScene().name==Room3FlowController.ToyRoom,"실제 Room3_Toy 마당으로 이동");
        Check(!game.sharedUI.hud.activeInHierarchy,"감상 구간 HUD 숨김");
    }
    static void Capture(DollMemorySequence game)
    {
        Check(Mathf.Abs(game.winter.dog.position.y-game.cushion.position.y)<.03f,"겨울이가 쿠션 안으로 파묻히지 않고 방석 높이에 위치");
        Check(game.JumpCount==3,"건조대 아래 점프 3회");
        Check(!game.sharedUI.hud.activeInHierarchy,"마지막 장면 UI 없음");
        var camera=UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(c=>c.name=="Doll Memory Camera");
        File.AppendAllText(Folder+"PlayVerification.txt","REST DOG: "+game.winter.dog.position+" active="+game.winter.dog.gameObject.activeInHierarchy+" doll="+game.ActiveDoll.position+"\n");
        foreach(var renderer in game.winter.dog.GetComponentsInChildren<Renderer>(true)) File.AppendAllText(Folder+"PlayVerification.txt",renderer.name+" enabled="+renderer.enabled+" active="+renderer.gameObject.activeInHierarchy+" bounds="+renderer.bounds+"\n");
        var rt=RenderTexture.GetTemporary(960,540,24); var old=camera.targetTexture; camera.targetTexture=rt; camera.Render();
        var previousRT=RenderTexture.active; RenderTexture.active=rt; var image=new Texture2D(960,540,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,960,540),0,0); image.Apply();
        File.WriteAllBytes(Folder+"Rest.png",image.EncodeToPNG()); RenderTexture.active=previousRT; camera.targetTexture=old; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(image);
    }
    static void Frame(string name){
        if(!pictures.Add(name))return;
        var c=UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(v=>v.name=="Doll Memory Camera");
        var rt=RenderTexture.GetTemporary(960,540,24);var saved=c.targetTexture;var old=RenderTexture.active;c.targetTexture=rt;c.Render();RenderTexture.active=rt;
        var tex=new Texture2D(960,540,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,960,540),0,0);tex.Apply();File.WriteAllBytes(Folder+name+".png",tex.EncodeToPNG());
        c.targetTexture=saved;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(tex);
    }
    static void End(string result) { SessionState.SetBool("Doll.Verify",false); File.WriteAllText(Folder+"Verify.request",result); EditorApplication.isPlaying=false; EditorSceneManager.playModeStartScene=null; }
}
#endif
