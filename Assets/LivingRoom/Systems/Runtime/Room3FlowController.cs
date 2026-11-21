using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TeamRoulette.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamRoulette.Room3
{
    // Scene travel belongs to the persistent GameManager. The living room is kept loaded:
    // suspend its active roots, load a visit additively, then resume those exact objects.
    public sealed class Room3FlowController : MonoBehaviour
    {
        public const string LivingRoom = "Room3_LivingRoom";
        public const string ToyRoom = "Room3_Toy";
        public static readonly string[] ToyIds = { "로프", "뼈다귀 장난감", "인형" };
        // Keep completion identities stable while using the replacement scene assets.
        static string ToyIdentity(string name) => name switch
        {
            "08_duck_prefab" => "인형",
            "DogAccesories-001" => "로프",
            "Bone_1" => "뼈다귀 장난감",
            _ => name
        };
        static bool IsStoredToy(Transform node)
        {
            for (var parent = node.parent; parent; parent = parent.parent)
                if (parent.name == "Toys" && parent.parent && parent.parent.name == "ToyGame") return true;
            return false;
        }

        static string StoredToyIdentity(string name)
        {
            // Unity appends " (1)" when the arranged copy is duplicated in the scene.
            if (name.EndsWith("(1)", StringComparison.Ordinal)) name = name.Substring(0, name.Length - 3).TrimEnd();
            return ToyIdentity(name);
        }

        void RefreshToyDisplay(Scene scene)
        {
            var nodes = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            foreach (var node in nodes)
            {
                if (node.parent && node.parent.name == "Toys" && node.parent.parent && node.parent.parent.name == "ToyGame")
                {
                    string id = StoredToyIdentity(node.name);
                    if (Array.IndexOf(ToyIds, id) >= 0) node.gameObject.SetActive(IsToyComplete(id));
                }
                else if (!IsStoredToy(node) && node.TryGetComponent<Room3FlowTarget>(out var target)
                    && Array.IndexOf(ToyIds, target.ObjectId) >= 0)
                    node.gameObject.SetActive(!IsToyComplete(target.ObjectId));
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void RefreshToyDisplayForDebug()
        {
            var scene = SceneManager.GetSceneByName(LivingRoom);
            if (scene.IsValid() && scene.isLoaded) RefreshToyDisplay(scene);
        }
#endif
        public bool IsTransitioning { get; private set; }
        public string VisitingObject { get; private set; }
        public bool EndingVisible { get; private set; }
        public int CompletedToyCount => manager.CompletedToyCount;
        public bool IsToyComplete(string id) => manager && manager.IsToyComplete(id);

        GameManager manager;
        bool optionsInitialized;
        readonly List<GameObject> suspendedRoots = new List<GameObject>();
        Scene homeScene, visitScene;
        FirstPersonPlayer homePlayer;
        Vector3 homePosition;
        Quaternion homeRotation, homeCameraRotation;

        void Awake() { manager = GetComponent<GameManager>(); }

        static T Find<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault();

        public void PrepareScene(Scene scene)
        {
            InitializeOptions();
            bool home = scene.name == LivingRoom;
            if (!home && scene.name != ToyRoom) return;
            if (home)
            {
                var nodes = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                foreach (var node in nodes)
                {
                    // Arranged copies are display-only and must never start another visit.
                    if (IsStoredToy(node)) continue;
                    string id = ToyIdentity(node.name);
                    if (Array.IndexOf(ToyIds, id) < 0) continue;
                    EnsureCollider(node.gameObject);
                    var binding = node.GetComponent<Room3FlowTarget>();
                    if (!binding) binding = node.gameObject.AddComponent<Room3FlowTarget>();
                    binding.Bind(this, id);
                }
                var closed = nodes.FirstOrDefault(node => node.name == "WindowClose");
                var open = nodes.FirstOrDefault(node => node.name == "WindowOpen");
                bool hasWindowGame = Find<WindowGameController>(scene);
                bool complete = manager.IsComplete(GameStage.Window);
                if (closed) closed.gameObject.SetActive(!hasWindowGame && !complete);
                if (open) open.gameObject.SetActive(!hasWindowGame && complete);
                RefreshToyDisplay(scene);
            }
            else
            {
                // The placeholder scenes contain a loose Main Camera as well as the player.
                foreach (var camera in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)))
                    if (!camera.GetComponentInParent<FirstPersonPlayer>())
                    {
                        camera.enabled = false;
                        if (camera.TryGetComponent<AudioListener>(out var listener)) listener.enabled = false;
                    }
            }
        }

        void InitializeOptions()
        {
            if (optionsInitialized) return;
            if (!OptionState.Instance) new GameObject("Room3 OptionState").AddComponent<OptionState>();
            foreach (OptionType type in Enum.GetValues(typeof(OptionType)))
            {
                if (type == OptionType.Blur) OptionState.Instance.Lock(type);
                else if (!OptionState.Instance.IsUnlocked(type)) OptionState.Instance.Unlock(type);
            }
            optionsInitialized = true;
        }

        static void EnsureCollider(GameObject target)
        {
            if (target.GetComponentsInChildren<Collider>(true).Any(c => c.enabled && !c.isTrigger)) return;
            // Fit a collider in the model's local space when an imported toy has none.
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (var renderer in renderers)
            {
                var world = renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = world.center + Vector3.Scale(world.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var local = target.transform.InverseTransformPoint(corner);
                    if (first) { bounds = new Bounds(local, Vector3.zero); first = false; }
                    else bounds.Encapsulate(local);
                }
            }
            var collider = target.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = first ? Vector3.one : Vector3.Max(bounds.size, Vector3.one * .01f);
        }

        public void Click(Room3FlowTarget target)
        {
            if (!target || !target.isActiveAndEnabled || target.gameObject.scene.name != LivingRoom ||
                VisitingObject != null || IsTransitioning || EndingVisible || Time.timeScale <= 0) return;
            if (Array.IndexOf(ToyIds, target.ObjectId) < 0 || IsToyComplete(target.ObjectId)) return;
            var ui = FindFirstObjectByType<GameUI>();
            if (ui && ui.IsPaused) return;
            if (PlayerHand.Instance && PlayerHand.Instance.Held) { PlayerInteractor.ShowHint("먼저 내려놓자."); return; }
            BeginToyVisit(target);
        }

        void BeginToyVisit(Room3FlowTarget target)
        {
            if (!GameManager.CanPlay(GameStage.Toys)) return;
            if (!Application.CanStreamedLevelBeLoaded(ToyRoom))
            { Debug.LogError("Room3: add scene to Build Settings: " + ToyRoom, target); return; }
            homeScene = target.gameObject.scene;
            homePlayer = Find<FirstPersonPlayer>(homeScene);
            if (!homePlayer) { Debug.LogError("Room3: living room player is missing.", target); return; }
            homePosition = homePlayer.transform.position;
            homeRotation = homePlayer.transform.rotation;
            homeCameraRotation = homePlayer.cameraRoot ? homePlayer.cameraRoot.localRotation : Quaternion.identity;
            VisitingObject = target.ObjectId;
            StartCoroutine(VisitToyRoom());
        }

        IEnumerator VisitToyRoom()
        {
            IsTransitioning = true;
            suspendedRoots.Clear();
            foreach (var root in homeScene.GetRootGameObjects())
                if (root.activeSelf) { suspendedRoots.Add(root); root.SetActive(false); }
            AsyncOperation load = null;
            try { load = SceneManager.LoadSceneAsync(ToyRoom, LoadSceneMode.Additive); }
            catch (Exception ex) { Debug.LogException(ex); }
            if (load == null)
            {
                ResumeHome();
                VisitingObject = null;
                IsTransitioning = false;
                yield break;
            }
            yield return load;
            visitScene = SceneManager.GetSceneByName(ToyRoom);
            SceneManager.SetActiveScene(visitScene);
            // The destination DialogueManager is now bound, so start lines play in the visit.
            GameManager.TryBegin(GameStage.Toys);
            // Let the scene-local UI and player finish initializing before starting a minigame.
            yield return null;
            IsTransitioning = false;
        }

        // The scene-local mini-game calls this only after its final fetch and celebration.
        public bool CompleteVisit(string id)
        {
            if (IsTransitioning || string.IsNullOrEmpty(id) || VisitingObject != id
                || !visitScene.IsValid() || !visitScene.isLoaded || SceneManager.GetActiveScene() != visitScene) return false;
            StartCoroutine(ReturnHome());
            return true;
        }

        IEnumerator ReturnHome()
        {
            IsTransitioning = true;
            string id = VisitingObject;
            foreach (var root in visitScene.GetRootGameObjects()) root.SetActive(false);
            SceneManager.SetActiveScene(homeScene);
            yield return SceneManager.UnloadSceneAsync(visitScene);
            ResumeHome();
            VisitingObject = null;
            manager.CompleteToyVisit(id);
            RefreshToyDisplay(homeScene);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            // Let the living-room player and UI resume before accepting input.
            yield return null;
            IsTransitioning = false;
        }

        void ResumeHome()
        {
            foreach (var root in suspendedRoots) if (root) root.SetActive(true);
            suspendedRoots.Clear();
            if (homePlayer) homePlayer.Teleport(homePosition, homeRotation, homeCameraRotation);
            Physics.SyncTransforms();
        }

        public void BeginEnding() { EndingVisible = true; }

        void OnDestroy() { if (EndingVisible) Time.timeScale = 1; }
    }
}
