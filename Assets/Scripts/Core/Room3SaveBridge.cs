using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using TeamRoulette.Room3;

// 거실 진행 저장용 다리. 지호의 거실 매니저(GameManager)는 고치지 않는다.
// 거실 매니저는 이미 "지금 단계 / 끝낸 단계 / 끝낸 장난감 / 게시판 결과"를 기억하고,
// 거실 씬이 열릴 때 끝낸 단계를 끝난 모양으로 되돌리는 기능(RestoreScene)까지 갖고 있다.
// 그래서 그 기억만 저장했다가, 이어하기 때 매니저에 다시 넣어 주면 거실 전체가 이어진다.
// 원리:
//   - 저장: 매니저가 공개해 둔 정보(CurrentStage, IsComplete, HasStarted, IsToyComplete, boardResult)를 읽는다.
//           매니저가 바뀔 때마다(Changed 알림) 자동 저장.
//   - 불러오기: 매니저 안쪽 기억칸(completed, started, completedToys, CurrentStage)에 직접 넣는다 (C# 리플렉션).
//           거실 씬이 열린 직후(각 스크립트의 Start보다 먼저) 넣으므로, 매니저의 RestoreScene이 그대로 되살린다.
//   - 시작 자리: 거실 씬의 플레이어 원래 위치는 마당이다. 지호 코드는 "커튼 시작 전"일 때만 계단(Room3_Start_Stairs)으로 옮긴다.
//           그래서 이어하기 때는 단계와 상관없이 계단 자리로 옮긴다 (매니저의 RestoreScene이 끝난 뒤).
//   - 집 지도(Tab) 거실 칸: 매니저가 단계를 끝낼 때(Completed 알림) 아래 Steps 표에 있는 단계면 한 칸 채운다.
//           이어하기 때는 매니저에 "끝낸 단계"로 들어간 것 중 칸이 안 채워진 게 있으면 같이 채운다.
//   ※ 지호가 매니저 안쪽 칸 이름을 바꾸면 불러오기가 안 되고 Console에 경고가 뜬다.
public static class Room3SaveBridge
{
    public const string LivingScene = "Room3_LivingRoom";

    static GameManager hooked;

    // 집 지도 거실 칸 6개 = 이 표에 있는 단계 (창문, 현관은 칸에 안 들어감)
    // 칸 이름은 HouseProgress에 기록되는 이름 (저장에도 이 이름으로 들어감)
    static readonly GameStage[] StepStages = { GameStage.Curtain, GameStage.Wash, GameStage.Meal, GameStage.Toys, GameStage.TV, GameStage.Board };
    static readonly string[] StepNames = { "커튼", "세수", "밥", "장난감", "TV", "게시판" };

    // 지금 거실 매니저 상태 → 저장용 (거실 매니저가 없으면 null)
    public static Room3State Export()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return null;
        Room3State s = new Room3State { has = true, current = (int)gm.CurrentStage };
        foreach (GameStage st in Enum.GetValues(typeof(GameStage)))
        {
            if (gm.IsComplete(st)) s.completed.Add((int)st);
            if (gm.HasStarted(st)) s.started.Add((int)st);
        }
        foreach (string id in Room3FlowController.ToyIds)
            if (gm.IsToyComplete(id)) s.toys.Add(id);
        s.board = gm.boardResult;
        return s;
    }

    // 거실 씬이 열렸을 때 (SaveSystem이 부름). state가 있으면 이어하기.
    public static void OnLivingLoaded(Scene scene, Room3State state)
    {
        bool existed = GameManager.Instance != null;     // 매니저가 이미 있으면 = 매니저 쪽 준비가 먼저 끝남
        if (!existed)
        {
            GameObject prefab = Resources.Load<GameObject>("Room3GameManager");
            if (prefab != null) UnityEngine.Object.Instantiate(prefab);
        }
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        Hook(gm);

        if (state != null && state.has)
        {
            Import(gm, state);
            SyncHouseMap(gm);
            // 매니저가 먼저 씬을 준비했다면(빈 상태 기준) 되살린 상태로 한 번 더 준비 (창문·장난감 모양)
            if (existed && gm.Flow != null) gm.Flow.PrepareScene(scene);
            gm.StartCoroutine(MoveToStart(scene));
            Debug.Log("이어하기: 거실 진행 되살림 (지금 단계 " + (GameStage)state.current + ", 끝낸 단계 " + state.completed.Count + "개, 장난감 " + state.toys.Count + "개)");
        }
    }

    public const string StartMarker = "Room3_Start_Stairs";

    // 이어하기: 플레이어를 거실 시작 자리(계단)로. 매니저의 RestoreScene(1프레임 뒤)보다 늦게 하려고 2프레임 기다림
    static IEnumerator MoveToStart(Scene scene)
    {
        yield return null;
        yield return null;
        if (!scene.IsValid() || !scene.isLoaded) yield break;

        FirstPersonPlayer player = null;
        Transform marker = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (player == null) player = root.GetComponentInChildren<FirstPersonPlayer>(true);
            if (marker == null)
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == StartMarker) { marker = t; break; }
        }
        if (player == null || marker == null)
        {
            Debug.LogWarning("Room3SaveBridge: 플레이어나 " + StartMarker + "를 못 찾아서 시작 자리로 못 옮겼어요.");
            yield break;
        }

        CharacterController body = player.GetComponent<CharacterController>();
        bool wasEnabled = body != null && body.enabled;
        if (body != null) body.enabled = false;
        player.transform.SetPositionAndRotation(marker.position, marker.rotation);
        player.SetLookPitch(0f);                        // 위아래 시선은 정면으로
        if (body != null) body.enabled = wasEnabled;
        Physics.SyncTransforms();
        Debug.Log("이어하기: 거실 시작 자리(계단)에서 시작");
    }

    static void Hook(GameManager gm)
    {
        if (hooked == gm) return;
        if (hooked != null) { hooked.Changed -= OnChanged; hooked.Completed -= OnStageCompleted; }
        hooked = gm;
        gm.Changed += OnChanged;
        gm.Completed += OnStageCompleted;
    }

    static void OnChanged() { SaveSystem.Save(); }

    // 거실 단계 하나 끝 → 집 지도 거실 칸 한 칸 (표에 있는 단계만)
    static void OnStageCompleted(GameStage stage)
    {
        int i = Array.IndexOf(StepStages, stage);
        if (i >= 0) HouseProgress.CompleteLivingStep(StepNames[i]);
    }

    // 이어하기: 매니저에 끝난 단계인데 지도 칸이 비어 있으면 채움 (예전 저장 호환)
    static void SyncHouseMap(GameManager gm)
    {
        List<string> steps = new List<string>(HouseProgress.LivingSteps);
        bool missing = false;
        for (int i = 0; i < StepStages.Length; i++)
            if (gm.IsComplete(StepStages[i]) && !steps.Contains(StepNames[i])) { steps.Add(StepNames[i]); missing = true; }
        if (missing) HouseProgress.RestoreLiving(steps);
    }

    static void Import(GameManager gm, Room3State state)
    {
        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
        Type t = typeof(GameManager);
        var completed = t.GetField("completed", F)?.GetValue(gm) as HashSet<GameStage>;
        var started = t.GetField("started", F)?.GetValue(gm) as HashSet<GameStage>;
        var toys = t.GetField("completedToys", F)?.GetValue(gm) as HashSet<string>;
        MethodInfo setStage = t.GetProperty("CurrentStage")?.GetSetMethod(true);
        if (completed == null || started == null || toys == null || setStage == null)
        {
            Debug.LogWarning("Room3SaveBridge: 거실 매니저(GameManager)의 칸 이름이 바뀌어서 거실 진행을 되살리지 못했어요. 재문에게 알려 주세요.");
            return;
        }

        completed.Clear();
        foreach (int s in state.completed) completed.Add((GameStage)s);
        started.Clear();
        foreach (int s in state.started) started.Add((GameStage)s);
        toys.Clear();
        foreach (string id in state.toys) toys.Add(id);
        setStage.Invoke(gm, new object[] { (GameStage)state.current });
        gm.boardResult = (state.board != null && state.board.Length > 0) ? state.board : null;

        // 이미 시작한 거실이면 거실 첫 대사를 다시 틀지 않음
        if (state.started.Count > 0) t.GetField("openingDialogueQueued", F)?.SetValue(gm, true);
    }
}

[Serializable]
public class Room3State
{
    public bool has;                                  // 거실 진행이 저장돼 있는지
    public int current;                               // 지금 단계 (GameStage 숫자)
    public List<int> completed = new List<int>();     // 끝낸 단계
    public List<int> started = new List<int>();       // 시작한 단계
    public List<string> toys = new List<string>();    // 끝낸 장난감 (로프, 뼈다귀 장난감, 인형)
    public BoardItemRecord[] board;                   // 게시판 결과
}