#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeamRoulette.Room3.TugPlay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace TeamRoulette.Room3.TugEditor
{
    [InitializeOnLoad]
    public static class TugGameVerification
    {
        const string SessionKey = "TugGame.Verify";
        const string Report = "Assets/LivingRoom/TugGame/Editor/VerificationReport.txt";
        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        static double due;
        static string failure;
        static Keyboard keyboard;
        static int events;
        static TugGameVerification() { EditorApplication.playModeStateChanged += Changed; }
        [MenuItem("Tools/Room3/TugGame/Verify Visit Input And Completion")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("플레이 모드를 종료해주세요.");
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("씬을 저장해주세요.");
            File.WriteAllText(Report, "로프 터그놀이 통합 검증\n");
            SessionState.SetBool(SessionKey, true);
            EditorSceneManager.OpenScene("Assets/Scenes/Room3_LivingRoom.unity", OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(SessionKey, false))
            {
                failure = null; events = 0; stack.Clear(); stack.Push(Verify()); due = EditorApplication.timeSinceStartup + .8;
                Application.logMessageReceived += Log; EditorApplication.update += Pump;
            }
            if (state == PlayModeStateChange.ExitingPlayMode) Cleanup();
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(SessionKey, false))
            {
                SessionState.SetBool(SessionKey, false);
                EditorApplication.delayCall += () => EditorSceneManager.OpenScene("Assets/Scenes/Room3_Toy.unity", OpenSceneMode.Single);
            }
        }
        static void Log(string message, string trace, LogType type)
        { if (type == LogType.Exception || (type == LogType.Error && message.Contains("TugGame"))) failure = message + "\n" + trace; }
        static void Pump()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < due) return;
            try
            {
                if (failure != null) throw new Exception(failure);
                while (stack.Count > 0)
                {
                    var next = stack.Peek();
                    if (!next.MoveNext()) { stack.Pop(); continue; }
                    if (next.Current is IEnumerator child) { stack.Push(child); continue; }
                    due = EditorApplication.timeSinceStartup + (next.Current == null ? .035 : Convert.ToDouble(next.Current)); return;
                }
                Finish(true, "모든 검증 통과");
            }
            catch (Exception ex) { Finish(false, ex.ToString()); }
        }
        static void Cleanup()
        {
            EditorApplication.update -= Pump; Application.logMessageReceived -= Log;
            if (keyboard != null) { InputSystem.RemoveDevice(keyboard); keyboard = null; }
            Time.timeScale = 1;
        }
        static void Finish(bool passed, string message)
        {
            Cleanup(); File.AppendAllText(Report, (passed ? "PASS: " : "FAIL: ") + message + "\n");
            Debug.Log("[TugGameVerification] " + (passed ? "PASS" : "FAIL") + " " + message);
            EditorApplication.ExitPlaymode();
        }
        static void Check(bool success, string label)
        { if (!success) throw new Exception(label); File.AppendAllText(Report, "PASS: " + label + "\n"); }
        static IEnumerator Until(Func<bool> condition, float seconds, string label)
        {
            double end = EditorApplication.timeSinceStartup + seconds;
            while (!condition()) { if (EditorApplication.timeSinceStartup > end) throw new Exception("시간 초과: " + label); yield return .05; }
        }
        static IEnumerator Press(Key key, float held = .12f)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return held;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return .08;
        }
        static Key KeyFor(TugDirection direction) => direction == TugDirection.W ? Key.W : direction == TugDirection.A ? Key.A : direction == TugDirection.S ? Key.S : Key.D;

        static IEnumerator CheckCarriedRope(TugGame game, Func<bool> carrying, float timeout)
        {
            double deadline = EditorApplication.timeSinceStartup + timeout;
            GameObject instance = null;
            Transform[] joints = null;
            SkinnedMeshRenderer skin = null;
            var mesh = new Mesh();
            var vertices = new List<Vector3>();
            int samples = 0;
            try
            {
                while (carrying())
                {
                    if (EditorApplication.timeSinceStartup > deadline) throw new Exception("로프 운반 시간 초과");
                    if (game.CurrentRope != instance)
                    {
                        instance = game.CurrentRope;
                        joints = instance.GetComponentsInChildren<Transform>(true)
                            .Where(t => t.name.StartsWith("DEF_Rope_") || t.name == "RopeTip").ToArray();
                        skin = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.enabled);
                    }
                    Vector3 bite = game.MouthGrip.position;
                    Vector3 outward = Vector3.ProjectOnPlane(bite - game.winter.position, Vector3.up).normalized;
                    if (joints.Length != 7) throw new Exception("검사할 로프 마디가 7개가 아닙니다.");
                    foreach (var joint in joints)
                        if (Vector3.Dot(joint.position - bite, outward) < -.005f ||
                            joint.position.y < game.playerStand.position.y + .035f)
                            throw new Exception("운반 중 로프 마디가 주둥이 뒤 또는 바닥 아래로 들어감: " + joint.name);
                    if (samples++ % 3 == 0)
                    {
                        skin.BakeMesh(mesh);
                        mesh.GetVertices(vertices);
                        foreach (Vector3 local in vertices)
                        {
                            Vector3 delta = skin.transform.TransformPoint(local) - bite;
                            if (delta.y < -.11f && Vector3.Dot(delta, outward) < -.015f)
                                throw new Exception("운반 중 로프 표면이 주둥이 아래에서 가슴 쪽으로 들어감");
                        }
                    }
                    yield return null;
                }
            }
            finally { UnityEngine.Object.Destroy(mesh); }
            Check(samples >= 2, "운반 중 회전과 흔들림에서 로프 마디와 표면이 몸 앞에 유지됨");
        }

        static IEnumerator Verify()
        {
            yield return Until(() => GameManager.Instance && GameManager.Instance.Flow, 12, "기존 매니저 초기화");
            var manager = GameManager.Instance; var flow = manager.Flow;
            // Start this isolated play session at the toy stage, as the fetch-game integration check does.
            typeof(GameManager).GetProperty("CurrentStage").SetValue(manager, GameStage.Toys);
            yield return .8;
            var rope = UnityEngine.Object.FindObjectsByType<Room3FlowTarget>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.ObjectId == TugGame.ToyId && t.gameObject.scene.name == Room3FlowController.LivingRoom && t.isActiveAndEnabled);
            Check(rope, "거실의 실제 DogAccesories-001 로프 진입 대상");
            rope.Interact();
            yield return Until(() => !flow.IsTransitioning && flow.VisitingObject == TugGame.ToyId && SceneManager.GetActiveScene().name == Room3FlowController.ToyRoom, 20, "거실 로프 클릭 입장");
            var game = UnityEngine.Object.FindFirstObjectByType<TugGame>();
            Check(game, "로프 미니게임 로드");
            yield return Until(() => game.Phase == TugPhase.Waiting, 10, "입력 대기");
            Check(game.roundCount == 2 && game.successesPerRound == 5, "기본 2라운드 × 5회");
            Check(game.OwnsInput && !game.player.canMove && !game.player.canLook, "미니게임 이동·시점 잠금");
            var ball = UnityEngine.Object.FindFirstObjectByType<TeamRoulette.Room3.BallPlay.BallGame>();
            Check(!ball || !ball.OwnsInput, "테니스공 게임 동시 실행 없음");
            game.onCompleted.AddListener(() => events++);
            var targets = UnityEngine.Object.FindObjectsByType<Room3FlowTarget>(FindObjectsSortMode.None).Where(t => t.ObjectId == TugGame.ToyId && t.gameObject.scene == game.gameObject.scene).ToArray();
            foreach (var target in targets) target.Interact();
            yield return .2;
            Check(flow.VisitingObject == TugGame.ToyId && !flow.IsTransitioning, "마당 로프 재클릭으로 퇴장 불가");
            var joint = game.CurrentRope.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "DEF_Rope_01");
            var tip = game.CurrentRope.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "RopeTip");
            Check(joint && tip && Vector3.Distance(joint.position, game.MouthGrip.position) < .03f && Vector3.Distance(tip.position, game.HandGrip.position) < .03f, "3D 로프 양 끝이 입·손 Attachment 추적");
            keyboard = InputSystem.AddDevice<Keyboard>();
            var viewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(viewType).Focus();
            int before = game.TotalSuccesses;
            var wrong = (TugDirection)(((int)game.ExpectedDirection + 1) % 4);
            Vector3 dogBefore = game.winter.position;
            yield return Press(KeyFor(wrong));
            Check(game.TotalSuccesses == before && game.WrongInputs == 1, "실제 WASD 오답은 점수 증가 없음");
            Check(Vector3.Distance(dogBefore, game.winter.position) > .015f, "오답에서 겨울이가 실제 3D 공간에서 당김");
            yield return Until(() => game.Phase == TugPhase.Waiting, 5, "오답 뒤 재시도");
            Time.timeScale = 0;
            yield return Press(KeyFor(game.ExpectedDirection));
            Check(game.TotalSuccesses == before && game.Phase == TugPhase.Waiting, "일시정지 중 입력 무시");
            Time.timeScale = 1;
            var first = game.ExpectedDirection;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(KeyFor(first)));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Until(() => game.TotalSuccesses == 1, 5, "실제 키 입력 성공");
            Check(game.TotalSuccesses == 1, "한 프레임 안에 눌렀다 떼는 짧은 입력 인식");
            yield return Until(() => game.Phase == TugPhase.Waiting, 5, "짧은 입력 뒤 다음 방향");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(KeyFor(game.ExpectedDirection)));
            yield return Until(() => game.TotalSuccesses == 2, 5, "두 번째 실제 키 입력 성공");
            yield return .85;
            Check(game.TotalSuccesses == 2, "키를 누르고 있어도 연속 점수 없음");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return .1;
            while (game.TotalSuccesses < 5)
            {
                yield return Until(() => game.Phase == TugPhase.Waiting, 5, "라운드1 입력 대기");
                yield return Press(KeyFor(game.ExpectedDirection));
            }
            yield return Until(() => game.Phase == TugPhase.Exchanging, 5, "5회 성공 후 로프 교체 연출");
            Check(!flow.IsTransitioning && events == 0, "첫 라운드에서 완료·복귀 없음");
            yield return .45;
            Check(game.HandGrip.position.y < game.MouthGrip.position.y - .1f,
                "혼자 물고 이동할 때 로프 끝이 중력 방향으로 늘어짐");
            Check(game.HandGrip.position.y >= game.playerStand.position.y + .04f,
                "늘어진 로프 끝이 바닥 아래로 관통하지 않음");
            yield return CheckCarriedRope(game, () => game.Phase == TugPhase.Exchanging && game.RoundIndex == 1, 14);
            yield return Until(() => game.RoundIndex == 2 && game.Phase == TugPhase.Waiting, 14, "일어나기·하늘·짖기·재착석 후 2라운드");
            Check(game.RoundSuccesses == 0 && game.TotalSuccesses == 5, "두 번째 라운드 카운트 초기화, 총점 유지");
            Check(game.CurrentRope && game.ropePrefabs.Length == 1, "로프 1개 목록 재사용 교체");
            while (game.TotalSuccesses < 10)
            {
                yield return Until(() => game.Phase == TugPhase.Waiting, 5, "라운드2 입력 대기");
                yield return Press(KeyFor(game.ExpectedDirection));
            }
            yield return Until(() => game.Phase == TugPhase.Celebrating, 5, "마지막 로프 흔들기");
            yield return .35;
            yield return CheckCarriedRope(game, () => game.Phase == TugPhase.Celebrating, 8);
            yield return Until(() => events == 1, 8, "성공 10회 후 축하와 완료 이벤트");
            Check(game.TotalSuccesses == 10, "총 성공 10회");
            yield return Until(() => SceneManager.GetActiveScene().name == Room3FlowController.LivingRoom && !flow.IsTransitioning, 15, "클리어 완료 후 기존 경로로 거실 복귀");
            Check(manager.IsToyComplete(TugGame.ToyId), "기존 GameManager에 로프 완료 기록");
            Check(events == 1, "완료 이벤트 중복 호출 없음");
            var player = UnityEngine.Object.FindFirstObjectByType<FirstPersonPlayer>();
            Check(player && player.enabled && player.canMove, "거실 플레이어 이동 복원");
        }
    }
}
#endif
