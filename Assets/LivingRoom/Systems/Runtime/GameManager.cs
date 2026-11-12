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

    public enum GameStage { Curtain, Window, Calendar, Wash, Meal, Board, Toys, TV, Entrance, Finished }

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
            GameStage.Board, GameStage.Toys, GameStage.Calendar, GameStage.TV, GameStage.Entrance
        };
        public static GameStage NextStage(GameStage current)
        {
            int index = Array.IndexOf(Progression, current);
            return index >= 0 && index + 1 < Progression.Length ? Progression[index + 1] : GameStage.Finished;
        }

        public static GameManager Instance { get; private set; }
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
            if (CurrentStage != GameStage.Toys || !Room3FlowController.ToyIds.Contains(id) || !completedToys.Add(id)) return;
            if (completedToys.Count == Room3FlowController.ToyIds.Length) ReportComplete(GameStage.Toys);
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
            var calendar = Find<CalGameController>();
            var wash = Find<WashGameController>();
            var meal = Find<MealGameController>();
            var board = Find<BoardGameController>();
            if (IsComplete(GameStage.Curtain) && curtain) curtain.RestoreCompleted();
            if (IsComplete(GameStage.Calendar) && calendar) calendar.RestoreCompleted();
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
