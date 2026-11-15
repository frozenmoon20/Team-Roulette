#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeamRoulette.Room3.BonePlay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace TeamRoulette.Room3.BoneEditor
{
    [InitializeOnLoad]
    public static class BoneCatchVerification
    {
        const string Key = "BoneCatch.Verify", Report = BoneCatchSetup.Folder + "/Editor/VerificationReport.txt";
        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        static double due;
        static string failure;
        static Mouse mouse;
        static int completed;
        static bool oldBackground;
        static BoneCatchVerification() { EditorApplication.playModeStateChanged += Changed; }
        [MenuItem("Tools/Room3/BoneGame/Verify Catch And LivingRoom Return")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("씬을 저장하고 플레이를 중지해주세요.");
            File.WriteAllText(Report, "뼈다귀 캐치 실제 씬 통합 검증\n");
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene("Assets/Scenes/Room3_LivingRoom.unity", OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            {
                failure = null; completed = 0; stack.Clear(); stack.Push(Verify()); due = EditorApplication.timeSinceStartup + .8;
                oldBackground = Application.runInBackground; Application.runInBackground = true;
                Application.logMessageReceived += Log; EditorApplication.update += Pump;
            }
            if (state == PlayModeStateChange.ExitingPlayMode) Cleanup();
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key, false))
            {
                SessionState.SetBool(Key, false);
                EditorApplication.delayCall += () => EditorSceneManager.OpenScene("Assets/Scenes/Room3_Toy.unity", OpenSceneMode.Single);
            }
        }
        static void Log(string message, string trace, LogType type) { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) failure = message + "\n" + trace; }
        static void Pump()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < due) return;
            try
            {
                if (failure != null) throw new Exception(failure);
                while (stack.Count > 0)
                {
                    var routine = stack.Peek();
                    if (!routine.MoveNext()) { stack.Pop(); continue; }
                    if (routine.Current is IEnumerator child) { stack.Push(child); continue; }
                    due = EditorApplication.timeSinceStartup + (routine.Current == null ? .025 : Convert.ToDouble(routine.Current)); return;
                }
                Finish(true, "모든 검사 통과");
            }
            catch (Exception ex) { Finish(false, ex.ToString()); }
        }
        static void Cleanup()
        {
            EditorApplication.update -= Pump; Application.logMessageReceived -= Log;
            if (mouse != null) { InputSystem.RemoveDevice(mouse); mouse = null; }
            Time.timeScale = 1; Application.runInBackground = oldBackground;
        }
        static void Finish(bool pass, string message) { Cleanup(); File.AppendAllText(Report, (pass ? "PASS: " : "FAIL: ") + message + "\n"); Debug.Log("[BoneCatchVerification] " + (pass ? "PASS " : "FAIL ") + message); EditorApplication.ExitPlaymode(); }
        static void Check(bool condition, string label) { if (!condition) throw new Exception(label); File.AppendAllText(Report, "PASS: " + label + "\n"); }
        static IEnumerator Until(Func<bool> condition, float timeout, string label)
        {
            double end = EditorApplication.timeSinceStartup + timeout;
            while (!condition()) { if (EditorApplication.timeSinceStartup > end) throw new TimeoutException(label); yield return .025; }
        }
        static IEnumerator Verify()
        {
            yield return Until(() => GameManager.Instance && GameManager.Instance.Flow, 12, "매니저 초기화");
            var manager = GameManager.Instance; var flow = manager.Flow;
            typeof(GameManager).GetProperty("CurrentStage").SetValue(manager, GameStage.Toys);
            yield return .8;
            var originalPlayer = UnityEngine.Object.FindFirstObjectByType<FirstPersonPlayer>();
            Vector3 originalPosition = originalPlayer.transform.position; Quaternion originalRotation = originalPlayer.transform.rotation;
            var origin = UnityEngine.Object.FindObjectsByType<Room3FlowTarget>(FindObjectsSortMode.None).FirstOrDefault(t => t.ObjectId == BoneCatchGame.ToyId && t.gameObject.scene.name == Room3FlowController.LivingRoom && t.isActiveAndEnabled);
            Check(origin, "거실 Bone_1에 기존 뼈다귀 방문 연결"); origin.Interact();
            yield return Until(() => !flow.IsTransitioning && flow.VisitingObject == BoneCatchGame.ToyId && SceneManager.GetActiveScene().name == Room3FlowController.ToyRoom, 20, "거실 클릭으로 Room3_Toy 입장");
            var game = UnityEngine.Object.FindFirstObjectByType<BoneCatchGame>(); Check(game, "캐치 게임 로드");
            yield return Until(() => game.Phase == CatchPhase.Aiming, 10, "캐치 입력 대기");
            game.onCompleted.AddListener(() => completed++);
            Check(game.requiredSuccesses == 5 && game.flightSeconds < .6f && game.dogDistance < 3, "기본 5회, 가까운 거리의 빠른 투척");
            Check(game.OwnsInput && !game.player.canMove && !game.player.canLook, "기존 플레이어 이동과 시점 고정");
            Check(!UnityEngine.Object.FindFirstObjectByType<TeamRoulette.Room3.BallPlay.BallGame>().OwnsInput && !UnityEngine.Object.FindFirstObjectByType<TeamRoulette.Room3.TugPlay.TugGame>().OwnsInput, "테니스공·터그 동시 실행 없음");
            foreach (var target in UnityEngine.Object.FindObjectsByType<Room3FlowTarget>(FindObjectsSortMode.None).Where(t => t.ObjectId == BoneCatchGame.ToyId)) target.Interact();
            yield return .1;
            Check(!flow.IsTransitioning && flow.VisitingObject == BoneCatchGame.ToyId, "마당 뼈다귀 재클릭으로 조기 복귀 불가");
            var ring = game.GetComponentsInChildren<LineRenderer>(true).Single();
            Check(ring.positionCount >= 32 && ring.transform.position == game.TargetPosition, "월드 공간에 원형 캐치 목표 표시");
            Check(!game.TryThrow(new Vector3(float.NaN, 0, 0)) && game.AttemptCount == 0, "잘못된 좌표 입력 방어");
            Time.timeScale = 0; Check(!game.TryThrow(game.TargetPosition), "일시정지 중 투척 차단"); Time.timeScale = 1;
            Check(game.TryThrow(game.TargetPosition + game.playerCamera.transform.right * .8f), "범위 밖 실패 투척 시작");
            Check(!game.TryThrow(game.TargetPosition), "투척 중 중복 클릭 차단");
            yield return Until(() => game.Phase == CatchPhase.Recovering, 4, "실패 회수 시작");
            Time.timeScale = 0; Vector3 frozen = game.winter.position; yield return .25;
            Check(Vector3.Distance(frozen, game.winter.position) < .001f, "회수 이동 중 일시정지 유지"); Time.timeScale = 1;
            yield return Until(() => game.Phase == CatchPhase.Aiming, 10, "실패 후 줍기·반환·재시도");
            Check(game.SuccessCount == 0 && game.AttemptCount == 1 && game.SqueakCount == 1, "실패 점수 없음, 주워 물 때 삑 소리 호출");
            var viewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView"); EditorWindow.GetWindow(viewType).Focus();
            yield return .15;
            mouse = InputSystem.AddDevice<Mouse>();
            for (int i = 0; i < 5; i++)
            {
                yield return Until(() => game.Phase == CatchPhase.Aiming, 10, "다음 캐치 대기");
                game.SelectTarget((CatchDirection)(i % 4));
                var screen = game.playerCamera.WorldToScreenPoint(game.TargetPosition);
                Check(screen.z > 0 && game.playerCamera.pixelRect.Contains(new Vector2(screen.x, screen.y)), "목표가 카메라 안에 표시: " + game.Direction);
                Vector3 dogStart = game.winter.position;
                if (i == 0)
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(screen.x, screen.y) }.WithButton(MouseButton.Left));
                    yield return Until(() => game.AttemptCount == 2, 4, "실제 Mouse 클릭으로 투척");
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(screen.x, screen.y) });
                }
                else Check(game.TryThrow(game.TargetPosition), "목표 중심 투척: " + game.Direction);
                int expected = i + 1;
                yield return Until(() => game.SuccessCount == expected, 4, "캐치 성공: " + game.Direction);
                Check(game.LastThrowSuccessful && game.SqueakCount == expected + 1, "캐치 1회당 점수·삑 소리 1회");
                Check(Vector3.Distance(game.winter.position, dogStart) > .15f, "목표에 맞춰 겨울이 3D 이동: " + game.Direction);
                yield return .05;
                var visual = game.ActiveBone.GetComponentInChildren<Renderer>().bounds;
                Check(Vector3.Distance(visual.center, game.BitePosition) < .04f, "캐치 후 장난감 중심이 실제 주둥이의 물기 위치를 추적");
                if (i < 4) Check(completed == 0 && !flow.IsTransitioning, "5회 전 완료 이벤트와 복귀 없음");
            }
            yield return Until(() => game.Phase == CatchPhase.Celebrating, 4, "마지막 캐치 후 신나는 움직임");
            Check(completed == 0, "축하와 바라보기 이전 완료 이벤트 없음");
            yield return Until(() => completed == 1, 8, "축하·바라보기 후 완료 이벤트");
            yield return Until(() => SceneManager.GetActiveScene().name == Room3FlowController.LivingRoom && !flow.IsTransitioning, 15, "클리어 후 기존 거실 복귀");
            Check(manager.IsToyComplete(BoneCatchGame.ToyId) && completed == 1, "기존 매니저에 완료 1회 기록");
            Check(originalPlayer && originalPlayer.isActiveAndEnabled && Vector3.Distance(originalPosition, originalPlayer.transform.position) < .03f && Quaternion.Angle(originalRotation, originalPlayer.transform.rotation) < .1f, "거실 플레이어 원본 인스턴스·위치·시점 복원");
        }
    }
}
#endif
