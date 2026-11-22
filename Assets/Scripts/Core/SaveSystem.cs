using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TeamRoulette.UI;

// 자동저장. 씬에 붙이지 않아도 게임이 켜지면 저절로 동작한다.
// 저장하는 것:
//   - 마지막 씬 + 마지막 저장 지점(SavePoint) → 이어하기 때 그 자리에서 시작
//   - 해금 상태, 거실 진행 단계, 그때 하프톤 값, 엔딩을 봤는지
//   - 완료한 행동 목록 (SaveFlag) → 이어하기 때 "이미 한 것"을 끝난 모양으로 되돌려 놓음 (문 열림, 텀블러 완료 등)
//   - 목표창 상태 (어떤 목표가 보이고/끝났는지, 목표창이 켜져 있는지) → 따로 연결할 것 없이 자동
// 저장 시점:
//   - Checkpoint Scenes 중 하나에 새로 들어갈 때 (그 씬의 시작 지점)
//   - 무언가 해금될 때, 거실 단계(ProgressStep)가 끝날 때, 목표가 바뀔 때 (보이기·완료·숨기기)
//   - SaveFlag.Done()이 불릴 때 (행동 하나 완료)
//   - SavePoint.Reach()가 불릴 때 (구간 하나 완료)
// 원리:
//   - PlayerPrefs(유니티가 컴퓨터에 작게 저장해 두는 칸)에 JSON 글자 한 줄로 저장한다.
//   - 이어하기를 누르면 저장 내용을 "대기"에 올려 두고 씬을 연다.
//     → OptionState가 깨어날 때(Awake) 해금 상태를 조용히 되살리고 (해금 알림 없이)
//     → 각 SaveFlag가 "완료돼 있으면" 끝난 모양으로 되돌리고
//     → 해당 SavePoint가 플레이어를 그 자리로 옮긴다.
//     → 마지막으로 SaveRestorer가 목표창을 저장된 모양 그대로 덮어쓴다 (그래서 SaveFlag에 목표 함수를 넣을 필요 없음)
//   - 저장하는 씬은 "마지막으로 들어간 체크포인트 씬"이다. 거실 미니게임처럼 잠깐 겹쳐 연 씬에서 저장해도
//     이어하기는 거실 씬으로 들어간다.
//   - 엔딩: 거실(마지막 체크포인트 씬)에서 타이틀로 돌아오면 "엔딩을 봤다"로 저장한다 (따로 연결할 것 없음).
//     → 타이틀 하프톤이 완전히 걷히고, 이어하기 버튼이 숨는다.
// 미니게임 도중: SaveGuard.Begin ~ End 사이에는 저장하지 않는다 (도중에 꺼도 미니게임 시작 전 상태로 이어하기)
// 저장 표시: 저장 내용이 바뀔 때마다 Saved 알림 → SaveIndicator가 화면 구석에 "저장 중..."을 잠깐 띄움
// 테스트: 에디터 맨 위 메뉴 Tools > Restore > 저장 지우기 / 저장 내용 보기
public static class SaveSystem
{
    const string Key = "Restore_Save";

    // 저장 지점이 되는 씬 (씬 이름이 바뀌면 여기도 바꿔야 함)
    public static readonly string[] CheckpointScenes =
        { "Room1_Room", "Corridor", "Room1_TransitionCorridor", "Room3_LivingRoom" };

    // 타이틀 씬 이름 (마지막 체크포인트 씬에서 이 씬으로 오면 엔딩을 본 것으로 저장)
    public const string TitleScene = "Title";

    [Serializable]
    public class Data
    {
        public string scene;
        public string point;                 // 마지막 SavePoint 이름 (비어 있으면 씬 시작 지점)
        public List<string> unlocked = new List<string>();
        public List<string> living = new List<string>();
        public List<string> flags = new List<string>();
        public float halftone;
        public bool endingSeen;
        public string objScene;              // 목표 상태를 저장한 씬
        public List<ObjState> objectives = new List<ObjState>();
        public bool objPanel;                // 목표창이 켜져 있었는지
        public Room3State room3;             // 거실 진행 (Room3SaveBridge)
    }

    [Serializable]
    public class ObjState
    {
        public string id;
        public bool visible;
        public bool completed;
    }

    public static bool IsRestoring { get; private set; }   // 이어하기로 목표창을 되살리는 중 (SavePoint가 이때 생긴 목표 변화는 무시)
    static int hold;
    static bool pointHold;                   // "다음 저장 지점까지 멈춤" 중인지 (SavePoint의 Hold Until Next Point)                         // 0보다 크면 저장 멈춤 (미니게임 도중, SaveGuard가 올리고 내림)

    static SceneUIContent content;           // 지금 씬의 목표 목록 (GameUI에 붙은 것)
    static ObjectivePanel panel;             // 지금 씬의 목표창 보이기/숨기기 (없을 수도 있음)

    static string currentScene = "";        // 마지막으로 들어간 체크포인트 씬
    static string currentPoint = "";        // 마지막 저장 지점
    public static string CurrentPoint => currentPoint;
    static readonly HashSet<string> flags = new HashSet<string>();   // 이번 플레이에서 완료한 행동

    static Data pending;                     // 이어하기로 되살릴 해금·거실 내용 (OptionState가 깨어날 때 씀)
    static string pendingPoint;              // 이어하기로 옮겨 갈 저장 지점 (SavePoint가 확인)
    static Data continued;                   // 이어하기로 불러온 내용 (그 씬에 있는 동안 DevSkip 등이 확인)
    static string continuedScene = "";       // 이어하기로 들어간 씬 (다른 씬으로 넘어가면 비움)
    static bool continueApplied;             // 이어하기 내용을 이미 한 번 적용했는지 (같은 씬을 다시 열 때 또 적용하지 않게)

    public static bool HasSave => PlayerPrefs.HasKey(Key);

    public static Data Load()
    {
        if (!HasSave) return null;
        try { return JsonUtility.FromJson<Data>(PlayerPrefs.GetString(Key)); }
        catch { return null; }
    }

    // 저장에 그 옵션이 해금돼 있는지 (타이틀처럼 게임 밖에서 확인할 때. 저장이 없으면 false)
    public static bool SavedUnlocked(OptionType type)
    {
        Data d = Load();
        return d != null && d.unlocked.Contains(type.ToString());
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        // 게임(Play)이 시작될 때마다 기록을 비움 (에디터 설정에 따라 지난 Play의 값이 남는 걸 막음)
        currentScene = ""; currentPoint = ""; flags.Clear(); hold = 0; pointHold = false;
        pending = null; pendingPoint = null; continued = null; continuedScene = ""; continueApplied = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OptionState.OnOptionChanged -= OnOptionChanged;     // 해금될 때마다 저장
        OptionState.OnOptionChanged += OnOptionChanged;
        HouseProgress.OnChanged -= OnProgressChanged;       // 거실 단계가 끝날 때마다 저장
        HouseProgress.OnChanged += OnProgressChanged;
    }

    static void OnOptionChanged(OptionType type, bool unlocked)
    {
        if (hold > 0) SaveUnlockOnly(type, unlocked);   // 저장 멈춤 중(미니게임·구간 도중)이어도 해금만은 바로 기록
        else Save();
    }

    // 저장 멈춤 중에 해금되면: 위치·목표·완료 목록은 그대로 두고, 저장된 해금 목록에만 추가
    // (구간을 처음부터 다시 하더라도 이미 얻은 기능은 유지)
    static void SaveUnlockOnly(OptionType type, bool unlocked)
    {
        Data d = Load();
        if (d == null || string.IsNullOrEmpty(currentScene) || d.scene != currentScene) return;
        string name = type.ToString();
        if (unlocked && !d.unlocked.Contains(name)) d.unlocked.Add(name);
        else if (!unlocked) d.unlocked.Remove(name);
        else return;
        Write(d);
        Debug.Log("자동저장(해금만): " + name);
    }
    static void OnProgressChanged() { Save(); }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;                       // 겹쳐 연 씬(거실 미니게임)은 무시
        hold = 0; pointHold = false;                                    // 씬이 바뀌면 미니게임 저장 멈춤도 풀림

        // 거실(마지막 체크포인트 씬)에서 타이틀로 왔으면 = 엔딩을 보고 돌아온 것
        if (scene.name == TitleScene)
        {
            if (currentScene == CheckpointScenes[CheckpointScenes.Length - 1]) MarkEndingSeen();
            currentScene = ""; currentPoint = "";
            return;
        }

        if (Array.IndexOf(CheckpointScenes, scene.name) < 0) return;    // 저장 지점이 아닌 씬

        // 이어하기로 들어간 씬을 떠났으면 이어하기 기록을 비움
        if (continuedScene != scene.name) { continuedScene = ""; continued = null; pendingPoint = null; }
        // 같은 씬을 한 번 더 연 것(예: 거실 인형 회상이 끝나고 거실을 다시 열 때)은 이어하기가 아님
        //   → 이어하기 때 불러온 옛 저장(목표, 거실 진행)으로 지금 진행을 되돌리지 않게 비움
        else if (continueApplied) { continuedScene = ""; continued = null; pendingPoint = null; }
        continueApplied = !string.IsNullOrEmpty(continuedScene);

        // 새 씬에 들어왔으면 그 씬의 시작 지점부터 (이어하기면 PrepareContinue가 미리 맞춰 둬서 그 지점 유지)
        if (currentScene != scene.name) { currentScene = scene.name; currentPoint = ""; }

        // 이 씬의 목표창을 찾아서, 목표가 바뀔 때마다 저장되게 연결
        if (content != null) content.Changed -= OnObjectivesChanged;
        content = UnityEngine.Object.FindFirstObjectByType<SceneUIContent>(FindObjectsInactive.Include);
        panel = UnityEngine.Object.FindFirstObjectByType<ObjectivePanel>(FindObjectsInactive.Include);
        if (content != null) content.Changed += OnObjectivesChanged;

        // 거실: 거실 매니저 연결 + 이어하기면 거실 진행 되살리기 (각 스크립트의 Start보다 먼저)
        if (scene.name == Room3SaveBridge.LivingScene)
            Room3SaveBridge.OnLivingLoaded(scene, (continued != null && continuedScene == scene.name) ? continued.room3 : null);

        // 이어하기면: 씬 준비가 다 끝난 뒤 목표창을 저장된 모양으로 되돌림
        if (continued != null && continuedScene == scene.name)
            new GameObject("SaveRestorer").AddComponent<SaveRestorer>();

        // 저장 지점(씬 중간)에서 이어하면: 씬 시작 대사(오프닝)를 다시 틀지 않음
        if (!string.IsNullOrEmpty(pendingPoint) && continuedScene == scene.name)
            SkipOpeningDialogues();

        Save(true);
    }

    static void OnObjectivesChanged() { Save(); }

    // 씬 시작 때 자동으로 나오는 대사를 끔 (sceneLoaded는 각 스크립트의 Start보다 먼저라서 여기서 끄면 안 나옴)
    // 대사가 끝날 때 할 일(이벤트)이 연결된 대사는 건드리지 않음 (진행에 필요한 일을 빠뜨리지 않게)
    static void SkipOpeningDialogues()
    {
        foreach (DialogueStep step in UnityEngine.Object.FindObjectsByType<DialogueStep>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!step.playOnStart) continue;
            if (step.onFinished.GetPersistentEventCount() > 0)
            {
                Debug.LogWarning("이어하기: 시작 대사 " + step.name + "에 끝날 때 이벤트가 있어서 그대로 둠", step);
                continue;
            }
            step.playOnStart = false;
        }
        if (content != null && content.playDialogueOnStart && content.onDialogueFinished.GetPersistentEventCount() == 0)
            content.playDialogueOnStart = false;
    }

    // SaveRestorer가 부름: 저장된 목표 상태를 그대로 덮어씀
    public static void RestoreObjectives()
    {
        if (continued == null || continued.objScene != currentScene) return;
        if (content != null && continued.objectives.Count > 0)
        {
            foreach (ObjState o in continued.objectives)
            {
                var item = content.objectives.Find(x => x != null && x.id == o.id);
                if (item == null) continue;
                item.visible = o.visible;
                item.completed = o.completed;
            }
            IsRestoring = true;
            content.Refresh();              // 화면 갱신 (+ 저장)
            IsRestoring = false;
        }
        if (continued.objPanel && panel != null) panel.Show();
        Debug.Log("이어하기: 목표창 되살림 (" + continued.objectives.Count + "개)");
    }

    // ===== 행동 완료 (SaveFlag가 씀) =====

    public static bool HasFlag(string id) => !string.IsNullOrEmpty(id) && flags.Contains(id);

    public static void SetFlag(string id)
    {
        if (string.IsNullOrEmpty(id) || !flags.Add(id)) return;
        Save();
    }

    // ===== 저장 지점 (SavePoint가 씀) =====

    // holdUntilNext: 이 지점을 저장한 뒤로는 다음 저장 지점까지 저장을 멈춤 (구간 중간에 끄면 이 지점부터 다시)
    public static void ReachPoint(string id, bool holdUntilNext = false)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (pointHold) { pointHold = false; if (hold > 0) hold--; }   // 앞 지점의 멈춤 풀기
        currentPoint = id;
        Save();
        if (holdUntilNext) HoldForPoint();
    }

    // 이어하기로 그 지점에서 다시 시작할 때도 멈춤을 다시 걸기 (SavePoint가 부름)
    public static void HoldForPoint()
    {
        if (pointHold) return;
        pointHold = true;
        hold++;
    }

    // 이어하기로 들어왔고 저장 지점이 이 이름이면 true (한 번만)
    public static bool ConsumePendingPoint(string id)
    {
        if (string.IsNullOrEmpty(pendingPoint) || pendingPoint != id) return false;
        pendingPoint = null;
        return true;
    }

    // 이어하기로 들어와서, 아직 그 씬에 머물러 있는지 (침실 오프닝 건너뛰기 등에 씀)
    public static bool ContinuedInto(string sceneName) => !string.IsNullOrEmpty(continuedScene) && continuedScene == sceneName;

    // 이어하기로 불러온 저장에서 그 옵션이 해금돼 있었는지 (OptionState보다 먼저 깨어나는 스크립트도 쓸 수 있게)
    public static bool ContinueUnlocked(OptionType type) => continued != null && continued.unlocked.Contains(type.ToString());

    // ===== 미니게임 도중 저장 멈춤 (SaveGuard가 씀) =====

    public static void Hold() { hold++; }

    // save = false: 씬이 닫히거나 게임을 끌 때 (그 순간의 미니게임 도중 상태를 저장하지 않게)
    public static void Release(bool save = true)
    {
        if (hold > 0) hold--;
        if (hold == 0 && save) Save();
    }

    // ===== 저장 =====

    // keepHalftone: 씬이 막 바뀐 순간엔 하프톤 값이 잠깐 0이 되므로 이전에 저장한 값을 그대로 씀
    public static void Save(bool keepHalftone = false)
    {
        if (string.IsNullOrEmpty(currentScene)) return;
        if (hold > 0) return;                // 미니게임 도중이면 저장 안 함 (끝나면 SaveGuard.End가 저장)
        Data old = Load();
        Data d = new Data { scene = currentScene, point = currentPoint };
        if (OptionState.Instance != null)
            foreach (OptionType t in Enum.GetValues(typeof(OptionType)))
                if (OptionState.Instance.IsUnlocked(t)) d.unlocked.Add(t.ToString());
        d.living.AddRange(HouseProgress.LivingSteps);
        d.flags.AddRange(flags);
        // 하프톤: 진행도 기준 값 (전화 연출처럼 잠깐 바꾼 값은 빼고). 못 읽으면 이전 저장값
        float h = HalftoneController.CurrentValue;
        d.halftone = (!keepHalftone && h > 0f) ? h : (old != null ? old.halftone : 0f);   // 0 = 아직 모름 (타이틀이 기본값 사용)
        d.endingSeen = old != null && old.endingSeen;
        // 목표창 상태
        if (content != null)
        {
            d.objScene = currentScene;
            foreach (var item in content.objectives)
                if (item != null && !string.IsNullOrEmpty(item.id))
                    d.objectives.Add(new ObjState { id = item.id, visible = item.visible, completed = item.completed });
        }
        d.objPanel = panel != null && panel.IsShown;
        // 거실 진행: 거실 매니저가 있으면 지금 상태, 없으면(다른 씬) 이전 저장값 유지
        Room3State r3 = Room3SaveBridge.Export();
        d.room3 = r3 ?? (old != null ? old.room3 : null);
        Write(d);
        Debug.Log("자동저장: " + d.scene + (string.IsNullOrEmpty(d.point) ? "" : " / " + d.point)
                  + " (해금 " + d.unlocked.Count + ", 완료 " + d.flags.Count + ", 거실 " + d.living.Count + ")");
    }

    // 엔딩을 봤을 때: 하프톤이 완전히 걷힌 상태로 기록 (타이틀이 깨끗해짐). 위 OnSceneLoaded가 부름
    static void MarkEndingSeen()
    {
        Data d = Load() ?? new Data { scene = CheckpointScenes[0] };
        d.endingSeen = true;
        d.halftone = 1f;
        Write(d, false);
        Debug.Log("자동저장: 엔딩 봄");
    }

    // ===== 타이틀에서 =====

    // 이어하기: 저장 내용을 대기에 올려 두고, 열어야 할 씬 이름을 돌려줌 (씬은 타이틀이 연다)
    public static string PrepareContinue()
    {
        pending = Load();
        if (pending == null) return null;
        flags.Clear();
        foreach (string f in pending.flags) flags.Add(f);
        currentScene = pending.scene;
        currentPoint = pending.point;
        pendingPoint = pending.point;
        continued = pending;
        continuedScene = pending.scene;
        continueApplied = false;             // 이번에 여는 씬이 이어하기 첫 적용
        return pending.scene;
    }

    // 새 게임: 기록을 모두 처음으로 (이전 저장도 지움)
    public static void PrepareNewGame()
    {
        PlayerPrefs.DeleteKey(Key);
        pending = null;
        pendingPoint = null;
        continued = null;
        continuedScene = "";
        continueApplied = false;
        flags.Clear();
        currentScene = "";
        currentPoint = "";
        HouseProgress.ResetLiving();
    }

    // OptionState.Awake에서 부름: 대기 중인 해금 상태·거실 단계를 조용히 되살림
    public static void RestoreInto(OptionState state)
    {
        if (pending == null || state == null) return;
        foreach (string name in pending.unlocked)
            if (Enum.TryParse(name, out OptionType t)) state.SetSilently(t, true);
        HouseProgress.RestoreLiving(pending.living);
        Debug.Log("이어하기: " + pending.scene + " (해금 " + pending.unlocked.Count + "개, 완료 " + pending.flags.Count + "개 되살림)");
        pending = null;
    }

    // 저장 내용이 실제로 바뀌었을 때 알림 ("저장 중..." 표시 SaveIndicator가 들음)
    public static event Action Saved;

    // notify = false: 알림 없이 조용히 저장 (엔딩 기록처럼 타이틀에서 하는 저장)
    static void Write(Data d, bool notify = true)
    {
        string json = JsonUtility.ToJson(d);
        bool changed = PlayerPrefs.GetString(Key, "") != json;   // 같은 내용을 다시 저장한 거면 표시 안 함
        PlayerPrefs.SetString(Key, json);
        PlayerPrefs.Save();
        if (changed && notify) Saved?.Invoke();
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Restore/저장 지우기")]
    static void DeleteSave()
    {
        PlayerPrefs.DeleteKey(Key);
        Debug.Log("저장을 지웠어요.");
    }

    [UnityEditor.MenuItem("Tools/Restore/저장 내용 보기")]
    static void ShowSave()
    {
        Debug.Log(HasSave ? "저장 내용: " + PlayerPrefs.GetString(Key) : "저장 없음");
    }
#endif
}