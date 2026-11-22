using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using TeamRoulette.UI;

namespace TeamRoulette.Room3
{
    [Serializable]
    public class BoardItemRecord
    {
        public string id;
        public BoardItemState state;
        public Vector2 position;
        public Vector3 worldPosition;
        public Quaternion worldRotation;
        public float angle;
        public int order;
    }

    public enum GameStage { Curtain = 0, Window = 1, Wash = 3, Meal = 4, Board = 5, Toys = 6, TV = 7, Entrance = 8, Finished = 9 }

    [Serializable]
    public class StageContent
    {
        public GameStage stage;
        public UnityEvent onAvailable = new UnityEvent();
        public UnityEvent onCompleted = new UnityEvent();
    }

    // Only this manager survives scene changes. Scene UI and game controllers stay local.
    [DefaultExecutionOrder(-9000)]
    public sealed class GameManager : MonoBehaviour
    {
        // Enum IDs stay stable for serialized prefab references; this is the gameplay order.
        public static readonly GameStage[] Progression = {
            GameStage.Curtain, GameStage.Window, GameStage.Wash, GameStage.Meal,
            GameStage.Toys, GameStage.TV, GameStage.Board, GameStage.Entrance
        };
        public static GameStage NextStage(GameStage current)
        {
            int index = Array.IndexOf(Progression, current);
            return index >= 0 && index + 1 < Progression.Length ? Progression[index + 1] : GameStage.Finished;
        }

        public static GameManager Instance { get; private set; }
        [Header("목표 시작·완료 이벤트")]
        [Tooltip("목표별 이벤트 연결입니다. 목표 순서는 GameManager.Progression, 목표 문구는 ObjectiveManager에서 편집합니다.")]
        public StageContent[] stages = Defaults();
        public UnityEvent onRoomCompleted = new UnityEvent();
        public event Action Changed;
        public event Action<GameStage> Began, Completed;
        readonly HashSet<GameStage> completed = new HashSet<GameStage>();
        readonly HashSet<GameStage> started = new HashSet<GameStage>();
        readonly HashSet<string> completedToys = new HashSet<string>(StringComparer.Ordinal);
        public Room3FlowController Flow { get; private set; }
        public int CompletedToyCount => completedToys.Count;
        public bool IsToyComplete(string id) => completedToys.Contains(id);
        internal void CompleteToyVisit(string id)
        {
            if (CurrentStage != GameStage.Toys) return;
            RecordToyMemoryCompletion(id);
        }
        public void RecordToyMemoryCompletion(string id)
        {
            if (!Room3FlowController.ToyIds.Contains(id) || !completedToys.Add(id)) return;
            if (CurrentStage == GameStage.Toys && completedToys.Count == Room3FlowController.ToyIds.Length) ReportComplete(GameStage.Toys);
            else Changed?.Invoke();
        }
        bool openingDialogueQueued;
        public bool TryQueueOpeningDialogue()
        {
            if (openingDialogueQueued) return false;
            openingDialogueQueued = true;
            return true;
        }
        public GameStage CurrentStage { get; private set; }
        public bool IsFinished => CurrentStage == GameStage.Finished;
        [NonSerialized] public BoardItemRecord[] boardResult;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.name.StartsWith("Room3_", StringComparison.Ordinal)) return;
            if (!Instance)
            {
                var prefab = Resources.Load<GameObject>("Room3GameManager");
                if (prefab) Instantiate(prefab);
            }
            if (Instance)
            {
                Instance.Flow.PrepareScene(scene);
                Instance.StartCoroutine(Instance.RestoreScene(scene));
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Flow = gameObject.AddComponent<Room3FlowController>();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool debugCompletionPending;
        void Update()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            bool pressed = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f1Key.wasPressedThisFrame;
#else
            bool pressed = Input.GetKeyDown(KeyCode.F1);
#endif
            if (pressed) DebugCompleteCurrentObjective();
        }

        // Development shortcut only. Stage ownership/events still belong to ReportComplete.
        public bool DebugCompleteCurrentObjective()
        {
            if (debugCompletionPending) return false;
            var stage = CurrentStage;
            var visit = Flow ? Flow.VisitingObject : null;
            debugCompletionPending = true;
            bool accepted = DispatchDebugCompletion();
            if (accepted) StartCoroutine(WaitForDebugCompletion(stage, visit));
            else debugCompletionPending = false;
            return accepted;
        }
        IEnumerator WaitForDebugCompletion(GameStage stage, string visit)
        {
            float timeout = Time.realtimeSinceStartup + 15;
            do { yield return null; }
            while (Time.realtimeSinceStartup < timeout && !IsFinished &&
                (Flow.IsTransitioning || (CurrentStage == stage && Flow.VisitingObject == visit)));
            debugCompletionPending = false;
        }
        bool DispatchDebugCompletion()
        {
            if (Instance != this || IsFinished || !Flow || Flow.IsTransitioning || Flow.EndingVisible || Time.timeScale <= 0) return false;
            var ui = FindFirstObjectByType<GameUI>();
            if (ui && ui.IsPaused) return false;
            var stage = CurrentStage;
            if (!TryBegin(stage)) return false;
            if (PlayerHand.Instance && PlayerHand.Instance.Held) PlayerHand.Instance.ReturnHeld();
            switch (stage)
            {
                case GameStage.Curtain: return DebugFinish<CurtainGameController>(g => g.DebugComplete());
                case GameStage.Window: return DebugFinish<WindowGameController>(g => g.DebugComplete());
                case GameStage.Wash: return DebugFinish<WashGameController>(g => g.DebugComplete());
                case GameStage.Meal: return DebugFinish<MealGameController>(g => g.DebugComplete());
                case GameStage.Board: return DebugFinish<BoardGameController>(g => g.DebugComplete());
                case GameStage.TV: return DebugFinish<TVGameController>(g => g.DebugComplete());
                case GameStage.Entrance: return DebugFinish<PorchGameController>(g => g.Interact());
                case GameStage.Toys:
                    switch (Flow.VisitingObject)
                    {
                        case "로프": return DebugFinish<TugPlay.TugGame>(g => g.DebugComplete());
                        case "뼈다귀 장난감": return DebugFinish<BonePlay.BoneCatchGame>(g => g.DebugComplete());
                        case "인형": return DebugFinish<DollMemory.DollMemorySequence>(g => g.DebugComplete());
                        case null:
                        case "":
                            foreach (var id in Room3FlowController.ToyIds) CompleteToyVisit(id);
                            if (CurrentStage == GameStage.Toys && CompletedToyCount == Room3FlowController.ToyIds.Length)
                                ReportComplete(GameStage.Toys);
                            Flow.RefreshToyDisplayForDebug();
                            return true;
                    }
                    break;
            }
            return false;
        }
        bool DebugFinish<T>(Action<T> finish) where T : MonoBehaviour
        {
            var game = FindFirstObjectByType<T>();
            if (!game || !game.isActiveAndEnabled) { Debug.LogWarning("F1: active controller missing: " + typeof(T).Name, this); return false; }
            finish(game);
            return true;
        }
#endif
        public bool IsComplete(GameStage stage) => completed.Contains(stage);
        public bool HasStarted(GameStage stage) => started.Contains(stage);
        public StageContent Content(GameStage stage) => Array.Find(stages, x => x != null && x.stage == stage);

        public static bool CanPlay(GameStage stage)
        {
            // Existing standalone game test scenes still work without a GameManager.
            return !Instance || (Instance.CurrentStage == stage && !Instance.IsComplete(stage));
        }

        public static bool TryBegin(GameStage stage)
        {
            if (!CanPlay(stage)) return false;
            if (Time.timeScale <= 0) return false;
            if (!Instance) return true;
            var ui = FindFirstObjectByType<GameUI>();
            if (ui && ui.IsPaused) return false;
            if (Instance.started.Add(stage))
            {

                Instance.Began?.Invoke(stage);
            }
            return true;
        }

        public static bool ReportComplete(GameStage stage)
        {
            if (!Instance || stage >= GameStage.Finished || !CanPlay(stage)) return false;
            Instance.completed.Add(stage);
            Instance.started.Add(stage);
            Instance.CaptureResult(stage);
            Instance.CurrentStage = NextStage(stage);
            Instance.Content(stage)?.onCompleted.Invoke();
            Instance.Completed?.Invoke(stage);
            Instance.Changed?.Invoke();
            if (Instance.IsFinished) Instance.onRoomCompleted.Invoke();
            else Instance.Content(Instance.CurrentStage)?.onAvailable.Invoke();
            return true;
        }

        // UnityEvent hooks for the window, toy games, agility and entrance as they are built.
        public void BeginStage(string id)
        {
            if (Enum.TryParse(id, out GameStage stage)) TryBegin(stage);
            else Debug.LogError("Unknown Room3 stage: " + id, this);
        }
        public void CompleteStage(string id)
        {
            if (Enum.TryParse(id, out GameStage stage) && stage != GameStage.Finished) ReportComplete(stage);
            else Debug.LogError("Unknown Room3 stage: " + id, this);
        }

        IEnumerator RestoreScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            // Restore after the existing controllers have initialized their own visuals.
            yield return null;
            if (!scene.IsValid() || !scene.isLoaded || scene.name != "Room3_LivingRoom") yield break;
            T Find<T>() where T : Component => scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true)).FirstOrDefault();
            var curtain = Find<CurtainGameController>();
            var wash = Find<WashGameController>();
            var meal = Find<MealGameController>();
            var board = Find<BoardGameController>();
            if (IsComplete(GameStage.Curtain) && curtain) curtain.RestoreCompleted();
            var window = Find<WindowGameController>();
            if (IsComplete(GameStage.Window) && window) window.RestoreCompleted();
            if (IsComplete(GameStage.Wash) && wash) wash.RestoreCompleted();
            if (IsComplete(GameStage.Meal) && meal) meal.RestoreCompleted();
            if (IsComplete(GameStage.Board) && board) board.RestoreCompleted(boardResult);
            if (CurrentStage == GameStage.Curtain && !HasStarted(GameStage.Curtain))
            {
                var player = Find<FirstPersonPlayer>();
                var marker = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).FirstOrDefault(x => x.name == "Room3_Start_Stairs");
                if (player && marker)
                {
                    var body = player.GetComponent<CharacterController>();
                    bool wasEnabled = body && body.enabled;
                    if (body) body.enabled = false;
                    player.transform.SetPositionAndRotation(marker.position, marker.rotation);
                    if (player.cameraRoot) player.cameraRoot.localRotation = Quaternion.identity;
                    if (body) body.enabled = wasEnabled;
                    Physics.SyncTransforms();
                }
            }
        }
        void CaptureResult(GameStage stage)
        {
            if (stage != GameStage.Board) return;
            var board = FindFirstObjectByType<BoardGameController>();
            if (!board) return;
            boardResult = board.items.Select(item => new BoardItemRecord {
                id = item.id, state = item.state, position = item.boardPosition, angle = item.angle,
                order = item.order, worldPosition = item.transform.position, worldRotation = item.transform.rotation
            }).ToArray();
        }

        // Only stage event wiring belongs here. Objective text is defined in ObjectiveManager.cs.
        public static StageContent[] Defaults() => Progression.Select(stage => new StageContent { stage = stage }).ToArray();
    }
}
