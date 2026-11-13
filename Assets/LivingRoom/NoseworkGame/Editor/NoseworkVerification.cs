#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TeamRoulette.Room3.Nosework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamRoulette.Room3.NoseworkEditor
{
    [InitializeOnLoad]
    public static class NoseworkVerification
    {
        static NoseworkGame game;
        static int phase;
        static double started;
        static Vector3 cameraPosition;
        static Quaternion cameraRotation;
        static bool playerEnabled, interactorEnabled;
        static int completed;
        static string objectiveBefore;
        static bool observedDownwardStroke, wasDescending;
        static float previousPalmHeight;
        static double sceneEntered;
        static NoseworkVerification() { EditorApplication.update+=Tick; }
        [MenuItem("Tools/Room3/NoseworkGame/Run Play Verification %&n")]
        static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool("Nosework.Verify",true); SessionState.SetInt("Nosework.Verify.Phase",0);
            EditorApplication.isPlaying=true;
        }
        static void Check(bool value,string label) { if(!value) throw new Exception(label); File.AppendAllText(NoseworkSetup.Folder+"/Editor/PlayVerification.txt","PASS: "+label+"\n"); }
        static void Tick()
        {
            if(!SessionState.GetBool("Nosework.Verify",false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if(!game && phase<5)
                {
                    game=UnityEngine.Object.FindFirstObjectByType<NoseworkGame>(); if(!game) return;
                    phase=0; started=EditorApplication.timeSinceStartup;
                    File.WriteAllText(NoseworkSetup.Folder+"/Editor/PlayVerification.txt","노즈워크 실제 플레이 검사\n");
                }
                if(EditorApplication.timeSinceStartup-started>300) throw new Exception("시간 초과 / phase="+phase);
                if(phase==0)
                {
                    if(Time.timeSinceLevelLoad<4 || (game.sharedUI && game.sharedUI.IsDialogueOpen)) return;
                    cameraPosition=game.playerCamera.transform.localPosition; cameraRotation=game.playerCamera.transform.localRotation;
                    playerEnabled=game.player.enabled; interactorEnabled=game.interactor.enabled;
                    objectiveBefore=game.sharedUI.objectiveText.text;
                    Check(game.carrots.Length==12,"실제 독립 당근 12개 연결");
                    Physics.SyncTransforms();
                    var startRay=game.playerCamera.ViewportPointToRay(new Vector3(.5f,.5f,0));
                    Check(Physics.SphereCast(startRay,game.interactor.aimRadius,out var startHit,game.interactor.InteractRange)
                        && (startHit.collider.GetComponentInParent<NoseworkCarrot>() || startHit.collider.GetComponentInParent<NoseworkStartPoint>()),"입장 위치에서 기존 중앙 조준 상호작용으로 장난감 접근 가능");
                    game.onCompleted.AddListener(()=>completed++); game.Begin();
                    Check(game.Running && !game.player.enabled && !game.interactor.enabled,"시작 시 이동과 일반 상호작용 제한"); phase=1;
                    Check(game.winter.IsSitting,"시작 시 반대편에서 앉아 대기");
                    Check(game.sharedUI.hud.activeInHierarchy && game.sharedUI.objectiveText.gameObject.activeInHierarchy,"기존 목표 UI가 놀이 중 표시됨");
                    var guide=game.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Single();
                    Check(guide.rectTransform.anchorMin.y>.8f,"게임 설명이 화면 상단에 표시됨");
                    Check(Vector3.Dot(game.winter.WaitingPosition-game.toy.position,game.player.transform.position-game.toy.position)<0,"플레이어 반대편 대기 위치");
                    foreach(var carrot in game.carrots)
                    {
                        Physics.SyncTransforms();
                        var b=carrot.GetComponents<Collider>().First(c=>c.enabled).bounds;
                        bool correctHit=false;
                        foreach(float h in new[]{0f,.5f,.85f})
                        {
                            var target=b.center+Vector3.up*b.extents.y*h;
                            foreach(float x in new[]{-.4f,0f,.4f})
                            {
                                var ray=new Ray(game.playerCamera.transform.position,target+game.playerCamera.transform.right*Mathf.Max(b.extents.x,b.extents.z)*x-game.playerCamera.transform.position);
                                if(Physics.Raycast(ray,out var hit,30) && hit.collider.GetComponentInParent<NoseworkCarrot>()==carrot) { correctHit=true; break; }
                            }
                            if(correctHit) break;
                        }
                        Check(correctHit,"고정 카메라에서 당근 "+carrot.index+"의 노출된 몸통 또는 잎 선택 광선 확인");
                    }
                }
                else if(phase==1 && game.AcceptingInput) { game.Select(game.Sequence[game.SelectedCount]); if(game.Stage==2) phase=2; }
                else if(phase==2 && game.AcceptingInput)
                {
                    if(game.SelectedCount==0) game.Select(game.Sequence[0]);
                    else { game.Select(game.carrots.First(c=>!c.IsPulled && c!=game.Sequence[1])); phase=3; }
                }
                else if(phase==3 && game.AcceptingInput)
                {
                    Check(game.Stage==2 && game.SelectedCount==0 && game.carrots.Count(c=>c.IsPulled)==1,"오답 시 2단계만 재시작, 1단계 결과 유지");
                    game.Cancel();
                    Check(!game.Running && game.carrots.All(c=>!c.IsPulled && c.gameObject.activeSelf),"종료 시 당근 전체 복원");
                    Check(game.player.enabled==playerEnabled && game.interactor.enabled==interactorEnabled,"종료 시 입력 상태 복구");
                    Check(Vector3.Distance(cameraPosition,game.playerCamera.transform.localPosition)<.001f && Quaternion.Angle(cameraRotation,game.playerCamera.transform.localRotation)<.1f,"종료 시 카메라 복구");
                    Check(completed==0,"종료로 완료 이벤트가 호출되지 않음");
                    Check(game.sharedUI.objectiveText.text==objectiveBefore,"종료 시 기존 목표 문구 복원");
                    game.Begin(); phase=4;
                }
                else if(phase==4)
                {
                    if(game.petting.IsPetting)
                    {
                        var palm=game.petting.hand.palmPoint ? game.petting.hand.palmPoint:game.petting.hand.handBone;
                        float height=palm.position.y;
                        if(wasDescending && game.petting.DescendingContact && height<previousPalmHeight-.0001f) observedDownwardStroke=true;
                        previousPalmHeight=height; wasDescending=game.petting.DescendingContact;
                    }
                    if(game.AcceptingInput) game.Select(game.Sequence[game.SelectedCount]);
                    if(game.WaitingForPetting)
                    {
                        Check(game.winter.PettingClearance>0 && game.winter.IsSitting,"겨울이 몸 전체가 판을 넘어와 앉은 후 쓰다듬기 시작");
                        Check(completed==0 && !game.Completed,"쓰다듬기 전 완료와 거실 복귀 보류");
                        Check(game.winter.DirectApproachCount==6 && game.winter.StandCount==4,"당근 6회 직접 이동, 라운드당 1회 일어나기와 마지막 쓰다듬기 접근");
                        game.Pet();
                    }
                    if(game.Completed)
                    {
                        Check(observedDownwardStroke,"실제 손바닥 위치가 접촉 중 위에서 아래로 이동함");
                        Check(game.petting.WasPetted,"실제 손 모델 쓰다듬기 완료");
                        Check(game.carrots.Count(c=>c.IsPulled)==6 && completed==1,"1+2+3개 추출 후 완료 이벤트 1회");
                        Check(!game.Running && game.player.enabled==playerEnabled,"완료 후 일반 입력 복구");
                        phase=5; sceneEntered=EditorApplication.timeSinceStartup;
                        SceneManager.LoadScene("Room3_LivingRoom");
                    }
                }
                else if(phase==5)
                {
                    if(EditorApplication.timeSinceStartup-sceneEntered<5) return;
                    var manager=GameManager.Instance;
                    foreach(var stage in GameManager.Progression.TakeWhile(s=>s!=GameStage.Toys))
                        if(!manager.IsComplete(stage)) GameManager.ReportComplete(stage);
                    var ui=UnityEngine.Object.FindFirstObjectByType<TeamRoulette.UI.GameUI>();
                    if(ui && ui.IsDialogueOpen) return;
                    var home=SceneManager.GetSceneByName("Room3_LivingRoom");
                    var target=home.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Room3FlowTarget>(true)).First(t=>t.ObjectId==NoseworkGame.ToyId && t.gameObject.activeInHierarchy);
                    target.Interact(); phase=6;
                }
                else if(phase==6)
                {
                    if(GameManager.Instance.Flow.IsTransitioning) return;
                    game=UnityEngine.Object.FindFirstObjectByType<NoseworkGame>();
                    if(!game) return;
                    var placeholder=game.toy.GetComponent<Room3FlowTarget>();
                    placeholder.Interact();
                    game.Begin();
                    if(game.Running)
                    {
                        Check(GameManager.Instance.Flow.VisitingObject==NoseworkGame.ToyId,"실제 거실 당근 클릭으로 추억 씬 방문");
                        Check(!GameManager.Instance.Flow.IsTransitioning && !placeholder.enabled,"재클릭으로 즉시 복귀하지 않음"); phase=7;
                    }
                }
                else if(phase==7)
                {
                    if(game && game.AcceptingInput) game.Select(game.Sequence[game.SelectedCount]);
                    if(game && game.WaitingForPetting) game.Pet();
                    if(GameManager.Instance.IsToyComplete(NoseworkGame.ToyId))
                    {
                        Check(SceneManager.GetActiveScene().name=="Room3_LivingRoom" && string.IsNullOrEmpty(GameManager.Instance.Flow.VisitingObject),"3단계 클리어 후 거실 자동 복귀 및 장난감 완료 기록");
                        SessionState.SetBool("Nosework.Verify",false); Debug.Log("[Nosework] 전체 플레이 검증 통과"); EditorApplication.isPlaying=false;
                    }
                }
            }
            catch(Exception ex)
            {
                File.AppendAllText(NoseworkSetup.Folder+"/Editor/PlayVerification.txt","FAIL: "+ex+"\n");
                SessionState.SetBool("Nosework.Verify",false); Debug.LogException(ex); EditorApplication.isPlaying=false;
            }
        }
    }
}
#endif
