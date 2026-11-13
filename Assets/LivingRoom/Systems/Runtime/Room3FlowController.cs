using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TeamRoulette.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TeamRoulette.Room3
{
    // Scene travel belongs to the persistent GameManager. The living room is kept loaded:
    // suspend its active roots, load a visit additively, then resume those exact objects.
    public sealed class Room3FlowController : MonoBehaviour
    {
        public const string LivingRoom = "Room3_LivingRoom";
        public const string ToyRoom = "Room3_Toy";
        public const string OldLivingRoom = "Room3_OldLivingRoom";
        public static readonly string[] ToyIds = { "테니스공", "로프", "뼈다귀 장난감", "인형", "carrot_snuffle_game" };
        // Keep completion identities stable while using the replacement scene assets.
        static string ToyIdentity(string name) => name switch
        {
            "08_duck_prefab" => "인형",
            "DogAccesories-001" => "로프",
            "Bone_1" => "뼈다귀 장난감",
            "Ball_3" => "테니스공",
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
        GameStage visitStage;

        void Awake() { manager = GetComponent<GameManager>(); }

        static T Find<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault();

        public void PrepareScene(Scene scene)
        {
            InitializeOptions();
            bool home = scene.name == LivingRoom;
            if (!home && scene.name != ToyRoom && scene.name != OldLivingRoom) return;
            foreach (var node in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)))
            {
                // Arranged copies are display-only and must never start another visit.
                if (home && IsStoredToy(node)) continue;
                string id = ToyIdentity(node.name);
                bool toy = Array.IndexOf(ToyIds, id) >= 0;
                bool target = home ? toy || id == "WindowClose" || id == "TV" || id == "Door" : toy || id == "TV";
                if (!target) continue;
                EnsureCollider(node.gameObject);
                var binding = node.GetComponent<Room3FlowTarget>();
                if (!binding) binding = node.gameObject.AddComponent<Room3FlowTarget>();
                binding.Bind(this, id);
            }
            if (home)
            {
                var nodes = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                var closed = nodes.FirstOrDefault(node => node.name == "WindowClose");
                var open = nodes.FirstOrDefault(node => node.name == "WindowOpen");
                bool complete = manager.IsComplete(GameStage.Window);
                if (closed) closed.gameObject.SetActive(!Find<WindowGameController>(scene) && !complete);
                if (open) open.gameObject.SetActive(!Find<WindowGameController>(scene) && complete);
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
            // Imported carrot model has no click collider. Fit one in the model's local space.
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
            if (!target || !target.isActiveAndEnabled || IsTransitioning || EndingVisible || Time.timeScale <= 0) return;
            var ui = FindFirstObjectByType<GameUI>();
            if (ui && ui.IsPaused) return;
            if (PlayerHand.Instance && PlayerHand.Instance.Held) { PlayerInteractor.ShowHint("먼저 내려놓자."); return; }
            if (!string.IsNullOrEmpty(VisitingObject))
            {
                // Tennis is completed by its independent 3D game, never by a second click.
                if (VisitingObject == "테니스공" || VisitingObject == "인형") return;
                // Only the exact object used to enter can finish this visit.
                if (target.gameObject.scene == visitScene && target.ObjectId == VisitingObject)
                    StartCoroutine(ReturnHome());
                return;
            }
            if (target.gameObject.scene.name != LivingRoom) return;
            switch (target.ObjectId)
            {
                case "WindowClose":
                    var windowGame = Find<WindowGameController>(target.gameObject.scene);
                    if (windowGame) windowGame.Interact();
                    break;
                case "Door":
                    if (!GameManager.TryBegin(GameStage.Entrance)) return;
                    if (GameManager.ReportComplete(GameStage.Entrance)) ShowEnding();
                    break;
                case "TV":
                    BeginVisit(target, GameStage.TV, ToyRoom);
                    break;
                default:
                    if (Array.IndexOf(ToyIds, target.ObjectId) < 0 || IsToyComplete(target.ObjectId)) return;
                    BeginVisit(target, GameStage.Toys,
                        target.ObjectId == "인형" || target.ObjectId == "carrot_snuffle_game" ? OldLivingRoom : ToyRoom);
                    break;
            }
        }

        void BeginVisit(Room3FlowTarget target, GameStage stage, string destination)
        {
            if (!GameManager.CanPlay(stage)) return;
            if (!Application.CanStreamedLevelBeLoaded(destination))
            { Debug.LogError("Room3: add scene to Build Settings: " + destination, target); return; }
            homeScene = target.gameObject.scene;
            homePlayer = Find<FirstPersonPlayer>(homeScene);
            if (!homePlayer) { Debug.LogError("Room3: living room player is missing.", target); return; }
            homePosition = homePlayer.transform.position;
            homeRotation = homePlayer.transform.rotation;
            homeCameraRotation = homePlayer.cameraRoot ? homePlayer.cameraRoot.localRotation : Quaternion.identity;
            visitStage = stage;
            VisitingObject = target.ObjectId;
            StartCoroutine(Visit(destination));
        }

        IEnumerator Visit(string destination)
        {
            IsTransitioning = true;
            suspendedRoots.Clear();
            foreach (var root in homeScene.GetRootGameObjects())
                if (root.activeSelf) { suspendedRoots.Add(root); root.SetActive(false); }
            AsyncOperation load = null;
            try { load = SceneManager.LoadSceneAsync(destination, LoadSceneMode.Additive); }
            catch (Exception ex) { Debug.LogException(ex); }
            if (load == null)
            {
                ResumeHome();
                VisitingObject = null;
                IsTransitioning = false;
                yield break;
            }
            yield return load;
            visitScene = SceneManager.GetSceneByName(destination);
            SceneManager.SetActiveScene(visitScene);
            // The destination DialogueManager is now bound, so start lines play in the visit.
            GameManager.TryBegin(visitStage);
            // Let scene-local UI and player Start run before accepting a return click.
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
            if (visitStage == GameStage.Toys)
            {
                manager.CompleteToyVisit(id);
                RefreshToyDisplay(homeScene);
            }
            else GameManager.ReportComplete(GameStage.TV);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            // The returning click must not also interact with a living-room object.
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

        void ShowEnding()
        {
            EndingVisible = true;
            var ui = FindFirstObjectByType<GameUI>();
            var font = ui && ui.objectiveText ? ui.objectiveText.font : TMP_Settings.defaultFontAsset;
            // Suspend the whole room so movement, settings, hands and other click handlers stop.
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
            var screen = new GameObject("Room3 Ending", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            screen.transform.SetParent(transform, false);
            var canvas = screen.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var scaler = screen.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var background = new GameObject("Black", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(screen.transform, false);
            Stretch(background.GetComponent<RectTransform>());
            background.GetComponent<Image>().color = Color.black;
            var label = new GameObject("Game Over", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(background.transform, false);
            Stretch(label.GetComponent<RectTransform>());
            var text = label.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = "게임 종료";
            text.fontSize = 64;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            Time.timeScale = 0;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        void OnDestroy() { if (EndingVisible) Time.timeScale = 1; }
    }
}
