#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TeamRoulette.Room3;
using TeamRoulette.Room3.BallPlay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TeamRoulette.Room3.BallPlayEditor
{
    [InitializeOnLoad]
    public static class BallGameVerification
    {
        const string Key="BallGame.Verify";
        const string Report="Temp/BallGameVerification.txt";
        static readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();
        static double due;
        static string failure;
        static int events;
        static BallGameVerification()
        {
            EditorApplication.playModeStateChanged+=StateChanged;
        }
        [MenuItem("Tools/Room3/BallGame/Verify Physics And Return")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("플레이 모드를 종료해주세요.");
            if(SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("현재 씬을 저장해주세요.");
            File.WriteAllText(Report,"테니스공 미니게임 물리/회수/Room3 복귀 검증\n");
            SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene("Assets/Scenes/Room3_LivingRoom.unity",OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        static void StateChanged(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
            {
                failure=null;events=0;stack.Clear();stack.Push(Verify());due=EditorApplication.timeSinceStartup+.7;
                Application.logMessageReceived+=Log; EditorApplication.update+=Pump;
            }
            if(state==PlayModeStateChange.ExitingPlayMode)
            { Application.logMessageReceived-=Log;EditorApplication.update-=Pump; }
            if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key,false))
            {
                SessionState.SetBool(Key,false);
                EditorApplication.delayCall+=()=>EditorSceneManager.OpenScene("Assets/Scenes/Room3_Toy.unity",OpenSceneMode.Single);
            }
        }
        static void Log(string message,string trace,LogType type)
        {
            if(type==LogType.Exception || (type==LogType.Error && (message.Contains("BallGame")||message.Contains("NavMesh")))) failure=message+"\n"+trace;
        }
        static void Pump()
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<due)return;
            try
            {
                if(failure!=null)throw new Exception(failure);
                while(stack.Count>0)
                {
                    var routine=stack.Peek();
                    if(!routine.MoveNext()){stack.Pop();continue;}
                    if(routine.Current is IEnumerator child){stack.Push(child);continue;}
                    due=EditorApplication.timeSinceStartup+(routine.Current==null?.035:Convert.ToDouble(routine.Current));return;
                }
                Finish(true,"모든 검증 통과");
            }
            catch(Exception ex){Finish(false,ex.ToString());}
        }
        static void Finish(bool success,string text)
        {
            EditorApplication.update-=Pump;Application.logMessageReceived-=Log;
            File.AppendAllText(Report,(success?"PASS: ":"FAIL: ")+text+"\n");
            Debug.Log("[BallGameVerification] "+(success?"PASS":"FAIL")+" "+Path.GetFullPath(Report));
            Time.timeScale=1;EditorApplication.ExitPlaymode();
        }
        static void Check(bool value,string label)
        { if(!value)throw new Exception(label);File.AppendAllText(Report,"PASS: "+label+"\n"); }
        static IEnumerator Until(Func<bool> condition,float seconds,string label)
        {
            double end=EditorApplication.timeSinceStartup+seconds;
            while(!condition()){if(EditorApplication.timeSinceStartup>end)throw new Exception("시간 초과: "+label);yield return .06;}
        }
        static IEnumerator Verify()
        {
            yield return Until(()=>GameManager.Instance,15,"Room3 Manager");
            var manager=GameManager.Instance;
            // Only the verification session selects the toy stage. No serialized progress is edited.
            typeof(GameManager).GetProperty("CurrentStage").SetValue(manager,GameStage.Toys);
            var flow=manager.Flow;
            var home=SceneManager.GetSceneByName(Room3FlowController.LivingRoom);
            var homePlayer=home.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<FirstPersonPlayer>(true)).First();
            Vector3 originalPosition=homePlayer.transform.position;Quaternion originalRotation=homePlayer.transform.rotation;
            var target=home.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Room3FlowTarget>(true)).First(x=>x.ObjectId==BallGame.ToyId);
            target.Interact();
            yield return Until(()=>SceneManager.GetActiveScene().name==Room3FlowController.ToyRoom&&!flow.IsTransitioning,20,"마당 방문");
            BallGame game=null;
            yield return Until(()=>{game=Object.FindFirstObjectByType<BallGame>();return game&&game.Phase==FetchPhase.Aiming;},20,"게임 시작");
            Check(flow.VisitingObject==BallGame.ToyId,"실제 거실 테니스공 클릭으로 마당 방문");
            Check(home.isLoaded&&!homePlayer.gameObject.activeInHierarchy,"거실 씬/플레이어 보존");
            Check(game.HasUsableNavMesh,"겨울이 NavMesh Agent 사용");
            Check(!game.winterAnimator.applyRootMotion,"In Place 이동 적용");
            Check(game.SuccessCount==0&&game.requiredSuccesses==5,"초기 빈 원 5개");
            var uiCircles=Object.FindObjectsByType<BallProgressCircle>(FindObjectsSortMode.None).Where(x=>x.name.StartsWith("Success ")).ToArray();
            Check(uiCircles.Length==5&&uiCircles.All(x=>x.GetComponent<CanvasRenderer>()&&!x.filled),"빈 원 5개 UI 렌더러 연결");
            Check(game.OwnsInput&&!game.interactor.enabled&&!game.player.canMove,"기존 플레이어 조준 유지/중복 상호작용 방지");
            game.onCompleted.AddListener(()=>events++);
            game.player.canLook=false;
            Time.timeScale=2;
            var visitingTarget=game.ball.GetComponent<Room3FlowTarget>();
            if(visitingTarget)visitingTarget.Interact();
            yield return .2;
            Check(!flow.IsTransitioning&&SceneManager.GetActiveScene().name==Room3FlowController.ToyRoom,"테니스공 재클릭으로 조기 복귀 불가");
            var seen=new List<Vector3>();
            for(int i=0;i<45;i++)
            {
                Check(game.ValidateTarget(game.TargetPosition,out _),"목표 전체 영역의 바닥/장애물/NavMesh 검사 "+i);
                seen.Add(game.TargetPosition);
                Check(game.CreateNextTarget(),"반복 목표 생성 "+i);
            }
            Check(seen.All(p=>game.IsInside(p,game.targetRadius+game.targetBorderClearance)),"모든 목표는 지정 네모 안쪽");
            Check(seen.Zip(seen.Skip(1),(a,b)=>Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z))).All(d=>d>=game.minimumTargetSeparation-.01f),"목표 위치 반복 방지");
            int count=game.SuccessCount;
            Vector3 miss=game.TargetPosition+Vector3.right*(game.targetRadius+2);
            if(!game.IsInside(miss,.5f))miss=game.TargetPosition-Vector3.right*(game.targetRadius+2);
            yield return ThrowAt(game,miss,1.1f);
            Check(game.SuccessCount==count&&!game.LastThrowSuccessful,"목표 밖 첫 착지는 실패, 감점 없음");
            Check(game.Phase==FetchPhase.Aiming,"실패 후 겨울이가 회수하고 다음 시도");
            Vector3 lawnMiss=game.playerStand.position+game.playerStand.forward*1.7f;
            yield return ThrowAt(game,lawnMiss,1.1f);
            Check(game.SuccessCount==count&&!game.LastThrowSuccessful,"목표 생성 네모 밖 / 울타리 안의 공도 실제 회수");
            // A high arc crosses the fence. The dog must not path through it or stall forever.
            Vector3 outside=new Vector3(-23,game.playerStand.position.y,game.playerStand.position.z);
            yield return ThrowAt(game,outside,2.25f,false,true);
            Check(game.SuccessCount==count,"울타리 밖 공은 점수 변화 없음");
            Check(game.Phase==FetchPhase.Aiming,"울타리 밖 공 회복 후 재시도 가능");
            for(int i=0;i<game.requiredSuccesses;i++)
            {
                yield return ThrowAt(game,game.TargetPosition,1.25f,i==game.requiredSuccesses-1);
                Check(game&&game.SuccessCount==i+1,"실제 Rigidbody 첫 착지 성공 "+(i+1));
                Check(uiCircles.Count(x=>x.filled)==i+1,"성공한 수만큼 원 채움 "+(i+1));
                if(i<game.requiredSuccesses-1)Check(events==0,"중간 회수에서 완료 이벤트 없음 "+(i+1));
            }
            yield return Until(()=>flow.VisitingObject==null&&!flow.IsTransitioning,20,"완료 후 원래 씬 복귀");
            Check(events==1,"마지막 회수/내려놓기/반응 후 완료 이벤트 1회");
            Check(SceneManager.GetActiveScene()==home&&homePlayer.gameObject.activeInHierarchy,"보존된 원래 거실 재개");
            Check(Vector3.Distance(homePlayer.transform.position,originalPosition)<.15f&&Quaternion.Angle(homePlayer.transform.rotation,originalRotation)<.1f,"플레이어 원래 위치와 시점 복원");
            Check(manager.IsToyComplete(BallGame.ToyId)&&manager.CompletedToyCount==1,"테니스공만 완료 처리");
            Check(!SceneManager.GetSceneByName(Room3FlowController.ToyRoom).isLoaded,"마당 방문 씬 정상 해제");
        }
        static IEnumerator ThrowAt(BallGame game,Vector3 point,float flightTime,bool final=false,bool expectRecovery=false)
        {
            Check(game.Phase==FetchPhase.Aiming,"투척 시작 상태");
            Vector3 origin=game.playerCamera.transform.TransformPoint(game.heldBallOffset);
            Vector3 landing=point+Vector3.up*(game.ballRadius+.002f);
            Vector3 velocity=(landing-origin)/flightTime-Physics.gravity*(.5f*flightTime);
            Vector3 predicted=game.TrajectoryPoint(origin,velocity,flightTime);
            Check(Vector3.Distance(predicted,landing)<.001f,"포물선 가이드와 물리 중력 수식 일치");
            int attempts=game.AttemptCount;
            Check(game.LaunchBall(velocity),"물리 투척 시작");
            Check(!game.LaunchBall(velocity),"회수 중 중복 투척 방지");
            Check(game.AttemptCount==attempts+1,"시도 수 1회만 증가");
            bool picked=false,dropped=false,recovered=false,carried=false;
            double deadline=EditorApplication.timeSinceStartup+25;
            while(game&&game.Phase!=FetchPhase.Aiming&&game.Phase!=FetchPhase.Celebrating&&!game.IsComplete)
            {
                picked|=game.Phase==FetchPhase.PickingUp; dropped|=game.Phase==FetchPhase.Dropping;
                recovered|=game.Phase==FetchPhase.Recovering; carried|=game.IsCarryingBall;
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("공 비행/겨울이 회수 시간 초과");
                yield return .04;
            }
            if(!expectRecovery)Check(picked&&dropped&&carried&&!recovered,"도착 → 실제 물기 → 입에 부착 → 복귀 → 내려놓기");
            else Check(!carried&&recovered,"울타리 밖 공에는 반응 후 빈손 복귀");
            if(final)Check(game&&game.Phase==FetchPhase.Celebrating,"마지막 공 회수 후 즐거운 반응 시작");
        }
    }
}
#endif
