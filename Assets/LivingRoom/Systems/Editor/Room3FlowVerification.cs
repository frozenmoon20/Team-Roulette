using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeamRoulette.Room3;
using TeamRoulette.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Exercises the real scene and target wiring. Existing minigame internals are
// completed through their public manager API; this verifies orchestration only.
[InitializeOnLoad]
public static class Room3FlowVerification
{
    const string Prefix = "Room3Flow.Verification.";
    const string ReportPath = "Temp/Room3FlowVerification.txt";
    const string LivingPath = "Assets/Scenes/Room3_LivingRoom.unity";
    static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
    static double due;
    static string report;
    static string runtimeError;

    static Room3FlowVerification()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Tools/Room3/Verify full progression")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Start(false);
    }

    // Unity -batchmode -projectPath ... -executeMethod Room3FlowVerification.RunBatch
    // The runner exits Unity after leaving Play mode; do not pass -quit.
    public static void RunBatch() { Start(true); }

    static void Start(bool batch)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before running Room3 verification.");
        Directory.CreateDirectory("Temp");
        File.WriteAllText(ReportPath, "Room3 flow Play mode verification\n");
        SessionState.SetBool(Prefix + "Batch", batch);
        SessionState.SetBool(Prefix + "Pending", true);
        SessionState.SetBool(Prefix + "Exit", false);
        EditorSceneManager.OpenScene(LivingPath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Prefix + "Pending", false))
        {
            SessionState.SetBool(Prefix + "Pending", false);
            report = File.ReadAllText(ReportPath);
            runtimeError = null;
            Application.logMessageReceived += OnRuntimeLog;
            routines.Clear();
            routines.Push(Verify());
            due = EditorApplication.timeSinceStartup + 1;
            EditorApplication.update += Pump;
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= Pump;
            Application.logMessageReceived -= OnRuntimeLog;
        }
        if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Prefix + "Exit", false))
        {
            SessionState.SetBool(Prefix + "Exit", false);
            if (SessionState.GetBool(Prefix + "Batch", false))
                EditorApplication.delayCall += () => EditorApplication.Exit(SessionState.GetInt(Prefix + "Code", 1));
        }
    }

    static void OnRuntimeLog(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            runtimeError = message + "\n" + stack;
    }

    static void Pump()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < due) return;
        try
        {
            if (runtimeError != null) throw new Exception("Runtime error: " + runtimeError);
            while (routines.Count > 0)
            {
                var routine = routines.Peek();
                if (!routine.MoveNext()) { routines.Pop(); continue; }
                if (routine.Current is IEnumerator child) { routines.Push(child); continue; }
                due = EditorApplication.timeSinceStartup + (routine.Current == null ? .05 : Convert.ToDouble(routine.Current));
                return;
            }
            Finish(true, "ALL CHECKS PASSED\n");
        }
        catch (Exception exception) { Finish(false, "FAILED: " + exception + "\n"); }
    }

    static void Finish(bool success, string text)
    {
        EditorApplication.update -= Pump;
        Application.logMessageReceived -= OnRuntimeLog;
        File.WriteAllText(ReportPath, report + text);
        Debug.Log((success ? "Room3 verification passed. " : "Room3 verification failed. ") + Path.GetFullPath(ReportPath));
        SessionState.SetInt(Prefix + "Code", success ? 0 : 1);
        SessionState.SetBool(Prefix + "Exit", true);
        Time.timeScale = 1;
        EditorApplication.ExitPlaymode();
    }

    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        report += "PASS: " + label + "\n";
        File.WriteAllText(ReportPath, report);
    }

    static IEnumerator WaitFor(Func<bool> ready, string label)
    {
        double timeout = EditorApplication.timeSinceStartup + 30;
        while (!ready())
        {
            if (EditorApplication.timeSinceStartup >= timeout) throw new TimeoutException(label);
            yield return .05;
        }
        // Give Start/OnEnable and target setup a frame after a scene operation.
        yield return .1;
    }

    static Room3FlowTarget Target(string id, Scene scene)
    {
        var target = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Room3FlowTarget>(true))
            .FirstOrDefault(item => item.ObjectId == id);
        if (!target) throw new Exception("Missing flow target " + id + " in " + scene.name);
        return target;
    }

    static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault();

    static void CheckOptions(OptionState original)
    {
        Check(OptionState.Instance == original, "OptionState instance survives scene travel");
        Check(Enum.GetValues(typeof(OptionType)).Cast<OptionType>()
            .All(option => original.IsUnlocked(option) == (option != OptionType.Blur)),
            "All options remain enabled except Blur");
    }

    static void CheckSinglePlayerAndUI(Scene scene)
    {
        Check(Object.FindObjectsByType<FirstPersonPlayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length == 1,
            "Exactly one active player in " + scene.name);
        Check(Object.FindObjectsByType<PlayerHand>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length == 1
            && PlayerHand.Instance && PlayerHand.Instance.gameObject.scene == scene,
            "Active hand singleton belongs to " + scene.name);
        Check(Object.FindObjectsByType<GameUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length == 1,
            "Exactly one active GameUI in " + scene.name);
        Check(Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Count(listener => listener.enabled) == 1,
            "Exactly one enabled AudioListener in " + scene.name);
    }

    static void CheckObjective(Scene scene, GameStage stage)
    {
        var content = Find<SceneUIContent>(scene);
        Check(content && content.objectives.Count(item => item.visible && item.id == stage.ToString()) == 1,
            "Objective reflects current stage " + stage + " in " + scene.name);
    }

    static void CheckRay(Room3FlowTarget target, Scene scene)
    {
        var shared = Find<PlayerInteractor>(scene);
        var player = Find<FirstPersonPlayer>(scene);
        var feel = player.GetComponent<PlayerMotionFeel>();
        bool playerEnabled = player.enabled, feelEnabled = feel && feel.enabled;
        player.enabled = false;
        if (feel) feel.enabled = false;
        var camera = shared.playerCamera.transform;
        Vector3 localPosition = camera.localPosition;
        Quaternion localRotation = camera.localRotation;
        var collider = target.GetComponentsInChildren<Collider>().FirstOrDefault(item => item.enabled && !item.isTrigger);
        Check(collider, "Clickable collider exists for " + target.ObjectId);
        var bounds = collider.bounds;
        // Aim at the object with the production sphere cast, from outside its bounds.
        camera.position = bounds.center + Vector3.back * (bounds.extents.z + .8f);
        camera.LookAt(bounds.center);
        Physics.SyncTransforms();
        var ray = new Ray(camera.position, camera.forward);
        Check(Physics.SphereCast(ray, shared.aimRadius, out var hit, shared.InteractRange,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<Room3FlowTarget>() == target,
            "Player interaction ray reaches " + target.ObjectId + " in " + scene.name);
        camera.localPosition = localPosition;
        camera.localRotation = localRotation;
        player.enabled = playerEnabled;
        if (feel) feel.enabled = feelEnabled;
        Physics.SyncTransforms();
    }

    static IEnumerator Verify()
    {
        Application.runInBackground = true;
        var living = SceneManager.GetActiveScene();
        var manager = GameManager.Instance;
        Check(living.name == "Room3_LivingRoom" && manager && manager.Flow, "Living room bootstraps GameManager and flow");
        var flow = manager.Flow;
        var options = OptionState.Instance;
        CheckOptions(options);
        CheckSinglePlayerAndUI(living);
        Check(manager.CurrentStage == GameStage.Curtain, "Fresh session begins with curtain");
        CheckObjective(living, GameStage.Curtain);
        var player = Find<FirstPersonPlayer>(living);
        player.enabled = false; // Keep the exact saved pose stable during unattended verification.
        var feel = player.GetComponent<PlayerMotionFeel>();
        if (feel) feel.enabled = false;
        var curtain = Find<CurtainGameController>(living);
        var board = Find<BoardGameController>(living);
        var window = Target("WindowClose", living);
        var windowOpen = living.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .First(item => item.name == "WindowOpen").gameObject;
        var door = Target("Door", living);
        window.Interact(); Target("TV", living).Interact(); Target("테니스공", living).Interact(); door.Interact();
        yield return .15;
        Check(manager.CurrentStage == GameStage.Curtain && !flow.IsTransitioning && !flow.EndingVisible
            && window.gameObject.activeSelf && !windowOpen.activeSelf, "Later stages cannot start before curtain");
        curtain.RestoreCompleted();
        Check(GameManager.ReportComplete(GameStage.Curtain), "Curtain completion unlocks window");
        CheckRay(window, living);
        window.Interact();
        Check(manager.CurrentStage == GameStage.Wash && !window.gameObject.activeSelf && windowOpen.activeSelf,
            "Window click permanently opens window and advances to wash");
        window.Interact();
        Check(manager.CurrentStage == GameStage.Wash && windowOpen.activeSelf, "Completed window cannot toggle closed");
        Find<WashGameController>(living).RestoreCompleted();
        Check(GameManager.ReportComplete(GameStage.Wash), "Wash completion unlocks meal");
        Find<MealGameController>(living).RestoreCompleted();
        Check(GameManager.ReportComplete(GameStage.Meal), "Meal completion unlocks board");
        var savedItem = board.items[0];
        savedItem.transform.position += new Vector3(.17f, .09f, .13f);
        savedItem.angle = 23;
        Vector3 itemPosition = savedItem.transform.position;
        Quaternion itemRotation = savedItem.transform.rotation;
        Check(GameManager.ReportComplete(GameStage.Board), "Board completion unlocks toys");
        var sentinel = new GameObject("Flow verification inactive state");
        SceneManager.MoveGameObjectToScene(sentinel, living);
        sentinel.SetActive(false);
        Vector3 leftCurtain = curtain.curtainL.position, rightCurtain = curtain.curtainR.position;
        player.Teleport(player.transform.position, Quaternion.Euler(0, 37, 0), Quaternion.Euler(12, 0, 0));
        Vector3 savedPosition = player.transform.position;
        Quaternion savedRotation = player.transform.rotation, savedLook = player.cameraRoot.localRotation;
        string[] toys = { "carrot_snuffle_game", "로프", "인형", "테니스공", "뼈다귀 장난감" };
        for (int i = 0; i < toys.Length; i++)
        {
            string id = toys[i];
            string destination = id == "인형" || id == "carrot_snuffle_game" ? "Room3_OldLivingRoom" : "Room3_Toy";
            var origin = Target(id, living);
            CheckRay(origin, living);
            origin.Interact();
            origin.Interact(); // Duplicate input during transition must not queue another trip.
            yield return WaitFor(() => !flow.IsTransitioning && SceneManager.GetActiveScene().name == destination, "Enter " + destination);
            var guest = SceneManager.GetActiveScene();
            Check(flow.VisitingObject == id && flow.CompletedToyCount == i, "Visit identity retained; toy counts only on return");
            Check(living.isLoaded && !player.gameObject.activeInHierarchy, "Living scene remains loaded and suspended");
            CheckOptions(options);
            CheckSinglePlayerAndUI(guest);
            CheckObjective(guest, GameStage.Toys);
            string other = destination == "Room3_OldLivingRoom" ? (id == "인형" ? "carrot_snuffle_game" : "인형") : "TV";
            Target(other, guest).Interact();
            yield return .1;
            Check(!flow.IsTransitioning && flow.VisitingObject == id && SceneManager.GetActiveScene() == guest,
                "Different guest object cannot complete " + id);
            var returning = Target(id, guest);
            CheckRay(returning, guest);
            returning.Interact();
            yield return WaitFor(() => !flow.IsTransitioning && SceneManager.GetActiveScene() == living, "Return from " + id);
            Check(flow.CompletedToyCount == i + 1 && flow.IsToyComplete(id), "One-time toy completion recorded: " + id);
            Check(SceneManager.sceneCount == 1 && living.isLoaded, "Guest scene unloaded and original living room resumed");
            Check(player && Find<FirstPersonPlayer>(living) == player && savedItem && board.items[0] == savedItem,
                "Original player and board item instances survive travel");
            Check(Vector3.Distance(player.transform.position, savedPosition) < .001f
                && Quaternion.Angle(player.transform.rotation, savedRotation) < .01f
                && Quaternion.Angle(player.cameraRoot.localRotation, savedLook) < .01f, "Player position, heading and camera pitch restored exactly");
            Check(curtain.Completed && curtain.curtainL.position == leftCurtain && curtain.curtainR.position == rightCurtain
                && !window.gameObject.activeSelf && windowOpen.activeSelf, "Opened curtains and window stay unchanged");
            Check(savedItem.transform.position == itemPosition && savedItem.transform.rotation == itemRotation
                && savedItem.angle == 23 && !sentinel.activeSelf, "Board arrangement and inactive scene object remain unchanged");
            CheckOptions(options);
            CheckSinglePlayerAndUI(living);
            origin.Interact();
            yield return .1;
            Check(!flow.IsTransitioning && SceneManager.GetActiveScene() == living && flow.CompletedToyCount == i + 1,
                "Completed toy cannot be entered again: " + id);
            Check(manager.CurrentStage == (i == toys.Length - 1 ? GameStage.Calendar : GameStage.Toys),
                "All five distinct toys are required before calendar");
        }
        Target("TV", living).Interact();
        door.Interact();
        Check(!flow.IsTransitioning && !flow.EndingVisible && manager.CurrentStage == GameStage.Calendar,
            "TV and entrance remain unavailable until calendar completion");
        var calendar = Find<CalGameController>(living);
        calendar.Click(false); calendar.Click(true);
        // Calendar intentionally rejects duplicate same-frame clicks.
        yield return .1;
        calendar.Click(true);
        Check(manager.CurrentStage == GameStage.TV, "Calendar gameplay reports completion and unlocks TV");
        Target("TV", living).Interact();
        yield return WaitFor(() => !flow.IsTransitioning && SceneManager.GetActiveScene().name == "Room3_Toy", "Enter TV scene");
        var tvScene = SceneManager.GetActiveScene();
        Target("테니스공", tvScene).Interact();
        Check(!flow.IsTransitioning && flow.VisitingObject == "TV", "Toy object cannot finish TV visit");
        CheckOptions(options);
        CheckRay(Target("TV", tvScene), tvScene);
        Target("TV", tvScene).Interact();
        yield return WaitFor(() => !flow.IsTransitioning && SceneManager.GetActiveScene() == living, "Return from TV");
        Check(manager.CurrentStage == GameStage.Entrance && flow.CompletedToyCount == 5,
            "TV return unlocks entrance without changing toy completion");
        Check(Vector3.Distance(player.transform.position, savedPosition) < .001f
            && Quaternion.Angle(player.transform.rotation, savedRotation) < .01f
            && Quaternion.Angle(player.cameraRoot.localRotation, savedLook) < .01f, "TV preserves original player pose");
        Check(savedItem.transform.position == itemPosition && !sentinel.activeSelf && windowOpen.activeSelf,
            "TV visit also preserves living scene state");
        CheckObjective(living, GameStage.Entrance);
        player.enabled = true;
        door.Interact();
        yield return .1;
        Check(manager.IsFinished && flow.EndingVisible, "Entrance completes flow and opens temporary ending");
        Check(!player.isActiveAndEnabled && !Find<PlayerInteractor>(living).isActiveAndEnabled,
            "Ending stops player movement, camera input and interactions");
        Check(Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Any(text => text.text == "게임 종료"), "Ending displays Korean game-over label");
        Check(Time.timeScale == 0 && !Find<GameUI>(living).isActiveAndEnabled,
            "Ending freezes time and disables settings input");
        door.Interact();
        Check(manager.IsFinished && flow.EndingVisible && !flow.IsTransitioning, "Ending cannot restart progression");
        ScreenCapture.CaptureScreenshot("Temp/Room3Ending.png");
        yield return .5;
    }
}
