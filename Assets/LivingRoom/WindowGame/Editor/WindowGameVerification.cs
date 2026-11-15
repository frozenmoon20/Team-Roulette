#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TeamRoulette.Room3;

[InitializeOnLoad]
public static class WindowGameVerification
{
    const string Report = WindowGameSetup.Folder + "/Editor/VerificationReport.txt";
    static IEnumerator routine;
    static string results, runtimeError;
    static WindowGameVerification() { EditorApplication.playModeStateChanged += OnPlay; }
    [MenuItem("Tools/Room3/WindowGame/Verify In Play Mode")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Room3_LivingRoom")
            throw new InvalidOperationException("Open Room3_LivingRoom first.");
        SessionState.SetBool("WindowGame.Verify", true);
        EditorApplication.EnterPlaymode();
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("WindowGame.Verify", false))
        {
            SessionState.SetBool("WindowGame.Verify", false);
            results = "WindowGame Play mode integration verification\n"; runtimeError = null;
            Application.logMessageReceived += Log;
            routine = Verify(); EditorApplication.update += Pump;
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
        { EditorApplication.update -= Pump; Application.logMessageReceived -= Log; routine = null; }
    }
    static void Log(string text, string trace, LogType type)
    { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) runtimeError = text + "\n" + trace; }
    static void Check(bool value, string label)
    { if (!value) throw new Exception(label); results += "PASS: " + label + "\n"; }
    static void Pump()
    {
        try
        {
            if (!string.IsNullOrEmpty(runtimeError)) throw new Exception(runtimeError);
            if (routine != null && routine.MoveNext()) return;
            results += "ALL CHECKS PASSED\n"; Finish();
        }
        catch (Exception error) { results += "FAILED: " + error + "\n"; Finish(); }
    }
    static void Finish()
    { File.WriteAllText(Report, results); EditorApplication.update -= Pump; Application.logMessageReceived -= Log; routine = null; EditorApplication.ExitPlaymode(); }
    static IEnumerator Verify()
    {
        for (int i = 0; i < 15; i++) yield return null;
        var game = UnityEngine.Object.FindFirstObjectByType<WindowGameController>();
        var manager = GameManager.Instance;
        Check(game && game.enabled && manager && AudioManager.Instance, "Real scene controllers and shared AudioManager initialized");
        Check(game.curtainGame.gameObject.activeInHierarchy, "Existing CurtainGame enabled without rewriting its controller");
        Check(!game.animatedCurtains.Any(c => c.activeSelf) && !game.windSource.isPlaying, "No animated curtain or wind before opening");
        game.Interact();
        Check(!game.IsOpening && !game.WindowOpened && !manager.IsComplete(GameStage.Window), "Interaction before curtain completion rejected");
        Check(GameManager.ReportComplete(GameStage.Curtain), "Existing manager advances Curtain -> Window");
        game.curtainGame.RestoreCompleted();
        yield return null;
        Check(game.CanInteract && game.staticCurtains.All(c => c.activeSelf), "Curtain finale exposes static end curtains and unlocks Window");
        var interactor = game.player.GetComponentInChildren<PlayerInteractor>();
        var collider = game.interactionCollider;
        var camera = interactor.playerCamera;
        var cameraPosition = collider.bounds.center + Vector3.forward * Mathf.Min(1.2f, interactor.InteractRange * .7f);
        game.player.Teleport(game.player.transform.position + cameraPosition - camera.transform.position,
            Quaternion.LookRotation(Vector3.back), Quaternion.identity);
        Physics.SyncTransforms();
        var ray = camera.ViewportPointToRay(new Vector3(.5f, .5f));
        results += "DIAGNOSTIC: window bounds=" + collider.bounds + "; camera=" + camera.transform.position + "; ray=" + ray + "\n";
        foreach (var obstacle in Physics.SphereCastAll(ray, interactor.aimRadius, interactor.InteractRange).OrderBy(h => h.distance))
            results += "DIAGNOSTIC hit: " + obstacle.collider.name + "; distance=" + obstacle.distance + "; target=" + obstacle.collider.GetComponentInParent<IInteractable>() + "\n";
        bool found = Physics.SphereCast(ray, interactor.aimRadius, out var hit, interactor.InteractRange);
        results += "DIAGNOSTIC first hit=" + (found ? hit.collider.name : "none") + "\n";
        Check(found
            && hit.collider.GetComponentInParent<IInteractable>() is WindowGameTarget, "Existing player-camera interaction ray reaches the actual sliding window");
        var closed = game.slidingWindow.position;
        bool move = game.player.canMove, look = game.player.canLook;
        game.interactionTarget.Interact();
        Check(game.IsOpening && !game.WindowOpened && !manager.IsComplete(GameStage.Window), "Click starts movement without completing stage");
        Check(!game.player.canMove && !game.player.canLook, "Existing player movement and look temporarily blocked");
        float begin = Time.time;
        while (Time.time - begin < game.openDuration * .45f) yield return null;
        float distance = Vector3.Distance(game.slidingWindow.position, closed);
        Check(distance > .05f && distance < 1.9f && !game.WindowOpened && !game.WindActive,
            "Window moves gradually; no premature completion or curtain swap");
        game.enabled = false;
        Check(!game.IsOpening && !manager.IsComplete(GameStage.Window) && game.player.canMove == move && game.player.canLook == look,
            "Interrupted opening restores input and remains retryable");
        Check(Vector3.Distance(game.slidingWindow.localPosition, game.closedPosition) < .001f, "Interrupted window returns to its closed pose");
        game.enabled = true; yield return null;
        game.Interact();
        begin = Time.time;
        while (game.IsOpening && Time.time - begin < game.openDuration + 2) yield return null;
        Check(game.WindowOpened && game.WindActive && manager.IsComplete(GameStage.Window), "Completion occurs only after actual open pose");
        Check(Vector3.Distance(game.slidingWindow.position, closed + Vector3.left * 2) < .005f, "Left window moves exactly world -X 2 units");
        Check(game.staticCurtains.All(c => !c.activeSelf) && game.animatedCurtains.All(c => c.activeSelf), "Static OFF and animated ON at completion");
        Check(game.animatedCurtains.All(c => c.GetComponentsInChildren<Animation>().All(a => !a.playAutomatically && a.IsPlaying("idle") && a["idle"].wrapMode == WrapMode.Loop)), "Legacy idle plays explicitly and loops without automatic playback");
        Check(game.player.canMove == move && game.player.canLook == look, "Player input restored to previous state");
        var positions = game.animatedCurtains.Select(c => c.transform.position).ToArray();
        begin = Time.time; while (Time.time - begin < game.windFadeSeconds + .2f) yield return null;
        Check(game.windSource.isPlaying && Mathf.Abs(game.windSource.volume - game.windVolume) < .005f, "Shared AudioManager fades the assigned wind loop to target volume");
        Check(game.animatedCurtains.Select((c, i) => Vector3.Distance(c.transform.position, positions[i]) < .001f).All(x => x), "Wind curtain roots remain at their arranged positions");
        // Recreate the state restoration order used when the living room is resumed.
        game.gameObject.SetActive(false); yield return null; game.gameObject.SetActive(true);
        game.curtainGame.RestoreCompleted(); game.RestoreCompleted(); yield return null;
        Check(game.WindowOpened && game.WindActive && game.staticCurtains.All(c => !c.activeSelf) && game.animatedCurtains.All(c => c.activeSelf), "Room resume restores open window and wind after curtain restoration");
        Check(game.windSource.isPlaying && game.animatedCurtains.All(c => c.GetComponentsInChildren<Animation>().All(a => a.IsPlaying("idle"))), "Room resume restarts wind audio and Legacy animation");
        var position = game.slidingWindow.localPosition;
        game.Interact(); yield return null;
        Check(!game.IsOpening && game.slidingWindow.localPosition == position && manager.CurrentStage == GameStage.Wash,
            "Repeated input does not reopen or skip the following stage");
        var reload = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Room3_LivingRoom");
        while (!reload.isDone) yield return null;
        for (int i = 0; i < 15; i++) yield return null;
        game = UnityEngine.Object.FindFirstObjectByType<WindowGameController>();
        Check(GameManager.Instance == manager && manager.IsComplete(GameStage.Window), "Actual scene reload retains completion in the existing persistent manager");
        Check(game && game.curtainGame.Completed && game.WindowOpened && game.WindActive && !game.IsOpening,
            "Actual scene reload restores curtain completion, window open and wind state");
        Check(game.staticCurtains.All(c => !c.activeSelf) && game.animatedCurtains.All(c => c.activeSelf) &&
            game.animatedCurtains.All(c => c.GetComponentsInChildren<Animation>().All(a => a.IsPlaying("idle"))) && game.windSource.isPlaying,
            "Actual scene reload restores visible wind curtains, animation and audio");
        // Leave a short inspection window before automatically returning to Edit mode.
        var viewCamera = game.player.GetComponentInChildren<PlayerInteractor>().playerCamera;
        Vector3 viewPosition = new Vector3(22.7f, 1.8f, 4.5f);
        game.player.Teleport(game.player.transform.position + viewPosition - viewCamera.transform.position,
            Quaternion.LookRotation(Vector3.back), Quaternion.identity);
        game.player.canMove = game.player.canLook = false;
        File.WriteAllText(Report, results + "VISUAL INSPECTION READY\n");
        begin = Time.time; while (Time.time - begin < 12) yield return null;
    }
}
#endif
