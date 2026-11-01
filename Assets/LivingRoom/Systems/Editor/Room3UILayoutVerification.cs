using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeamRoulette.Room3;
using TeamRoulette.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Focused integration checks for the minigame UI positions, using the real scene.
[InitializeOnLoad]
public static class Room3UILayoutVerification
{
    const string Pending = "Room3.UI.VerificationPending";
    const string Report = "Temp/Room3UILayoutVerification.txt";
    const string Request = "Temp/Room3UILayoutVerify.request";
    static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
    static double due;
    static string report;

    static Room3UILayoutVerification()
    {
        EditorApplication.playModeStateChanged += OnPlay;
        EditorApplication.update += Poll;
    }

    static void Poll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Request)) return;
        try { File.Delete(Request); }
        catch (IOException) { return; } // The external trigger may still be finishing its write.
        Run();
    }

    [MenuItem("Tools/Room3/Verify minigame UI")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before UI verification.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory("Temp");
        File.WriteAllText(Report, "Room3 minigame UI verification\n");
        EditorSceneManager.OpenScene("Assets/Scenes/Room3_LivingRoom.unity", OpenSceneMode.Single);
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    static void OnPlay(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode) EditorApplication.update -= Pump;
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        report = File.ReadAllText(Report);
        routines.Clear();
        routines.Push(Verify());
        due = EditorApplication.timeSinceStartup + 1;
        EditorApplication.update += Pump;
    }

    static void Pump()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < due) return;
        try
        {
            while (routines.Count > 0)
            {
                var current = routines.Peek();
                if (!current.MoveNext()) { routines.Pop(); continue; }
                if (current.Current is IEnumerator child) { routines.Push(child); continue; }
                due = EditorApplication.timeSinceStartup + (current.Current == null ? .05 : Convert.ToDouble(current.Current));
                return;
            }
            Finish("ALL CHECKS PASSED\n");
        }
        catch (Exception error) { Finish("FAILED: " + error + "\n"); }
    }

    static void Finish(string result)
    {
        EditorApplication.update -= Pump;
        File.WriteAllText(Report, report + result);
        Debug.Log("Room3 UI verification: " + result + Path.GetFullPath(Report));
        Time.timeScale = 1;
        EditorApplication.ExitPlaymode();
    }

    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        report += "PASS: " + label + "\n";
        File.WriteAllText(Report, report);
    }

    static IEnumerator WaitFor(Func<bool> complete, string label, double seconds = 20)
    {
        double timeout = EditorApplication.timeSinceStartup + seconds;
        while (!complete())
        {
            if (EditorApplication.timeSinceStartup > timeout) throw new TimeoutException(label);
            yield return .05;
        }
    }

    static Rect ScreenRect(RectTransform transform)
    {
        Canvas.ForceUpdateCanvases();
        var corners = new Vector3[4];
        transform.GetWorldCorners(corners);
        var low = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        var high = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
        return Rect.MinMaxRect(low.x, low.y, high.x, high.y);
    }

    static IEnumerator Verify()
    {
        Application.runInBackground = true;
        var manager = GameManager.Instance;
        var ui = Object.FindFirstObjectByType<GameUI>();
        var wash = Object.FindFirstObjectByType<WashGameController>();
        var meal = Object.FindFirstObjectByType<MealGameController>();
        var board = Object.FindFirstObjectByType<BoardGameController>();
        Check(manager && ui && wash && meal && board, "Real living-room controllers and shared UI found");
        var player = wash.player;
        var originalCamera = meal.interactor.playerCamera.transform;
        Vector3 originalCameraPosition = originalCamera.localPosition;
        Quaternion originalCameraRotation = originalCamera.localRotation;
        player.enabled = false;
        var motion = player.GetComponent<PlayerMotionFeel>();
        if (motion) motion.enabled = false;
        Object.FindFirstObjectByType<CurtainGameController>().RestoreCompleted();
        Check(GameManager.ReportComplete(GameStage.Curtain) && GameManager.ReportComplete(GameStage.Window), "Advance to wash without changing minigame internals");

        wash.Click(WashAction.Water);
        wash.Click(WashAction.Water);
        wash.Click(WashAction.Soap);
        Check(wash.CurrentStage == WashGameController.Stage.Soaping, "Soap mode opens the production wash overlay");
        string objective = ui.objectiveText.text;
        wash.Paint(new Vector2(.25f, .5f), new Vector2(.75f, .5f));
        yield return .15;
        var washTrack = wash.progressFill.transform.parent as RectTransform;
        var trackRect = ScreenRect(washTrack);
        Check(washTrack.gameObject.activeInHierarchy && trackRect.yMin >= Screen.height * .85f && trackRect.yMax <= Screen.height + 1,
            $"Wash completion gauge is visible at the top of the screen ({trackRect.yMin:0.0}..{trackRect.yMax:0.0} / {Screen.height})");
        Check(wash.progressFill.fillAmount > 0 && wash.progressFill.fillAmount < 1, "Wash gauge shows actual partial soap coverage");
        Check(ui.objectiveText.text == objective, "Painting progress does not replace the shared objective text");
        ScreenCapture.CaptureScreenshot("Temp/Room3WashUI.png");
        yield return .25;
        wash.Cancel();
        wash.RestoreCompleted();
        Check(GameManager.ReportComplete(GameStage.Wash), "Advance to meal after releasing wash controls");

        var mealInput = meal.GetComponent<MealInput>();
        mealInput.enabled = false;
        meal.interactor.enabled = false;
        player.enabled = false;
        if (motion) motion.enabled = false;
        var camera = meal.interactor.playerCamera;
        camera.transform.position = new Vector3(35.8f, 1.65f, 7.5f);
        camera.transform.LookAt(new Vector3(38.5f, 2, 7.5f));
        var timerCanvas = meal.timerPanel.GetComponent<Canvas>();
        Check(timerCanvas && timerCanvas.renderMode == RenderMode.WorldSpace && meal.timerText.transform.IsChildOf(meal.timerPanel.transform),
            "Meal timer is the toaster's world-space UI, separate from the shared HUD");
        var toaster = Object.FindObjectsByType<MealTarget>(FindObjectsSortMode.None).First(target => target.action == MealAction.Toaster);
        var toasterBounds = toaster.GetComponent<Renderer>().bounds;
        Check(meal.timerPanel.transform.position.y > toasterBounds.max.y
            && Vector2.Distance(new Vector2(meal.timerPanel.transform.position.x, meal.timerPanel.transform.position.z),
                new Vector2(toasterBounds.center.x, toasterBounds.center.z)) < .1f,
            "Meal timer is directly above the physical toaster");
        Check(meal.toastSeconds == 10, "Toaster retains the requested ten-second cooking duration");
        meal.Click(MealAction.Fridge);
        yield return .2;
        Check(PlayerHand.Instance.Held == meal.rawToast, "Real meal pickup supplies toast");
        meal.Click(MealAction.Toaster);
        Check(meal.CookingStarted && meal.timerPanel.activeInHierarchy && meal.timerText.text == "10", "Toaster insertion starts its visible countdown at ten");
        yield return .5;
        Check(meal.SecondsRemaining < 10 && meal.SecondsRemaining > 8, "Countdown decreases while the toast cooks");
        ScreenCapture.CaptureScreenshot("Temp/Room3MealUI.png");
        yield return .2;
        ui.OpenSettings();
        float pausedAt = meal.SecondsRemaining;
        yield return .3;
        Check(Mathf.Abs(pausedAt - meal.SecondsRemaining) < .001f, "Toaster countdown pauses with settings");
        ui.CloseSettings();
        yield return WaitFor(() => meal.ToastReady, "Wait for all ten cooking seconds");
        Check(meal.timerText.text == "00" && meal.SecondsRemaining == 0, "The countdown reaches zero and completes cooking");
        Check(ui.GetComponentsInChildren<Transform>(true).Where(node => node.name == "MealTimer")
            .All(node => !node.gameObject.activeInHierarchy), "Shared HUD does not show a second meal timer");
        meal.RestoreCompleted();
        Check(GameManager.ReportComplete(GameStage.Meal), "Advance to board after checking the real countdown");

        player.enabled = true;
        camera.transform.localPosition = originalCameraPosition;
        camera.transform.localRotation = originalCameraRotation;
        player.Teleport(board.surface.position + board.surface.forward * 2.7f - Vector3.up * 1.4f,
            Quaternion.LookRotation(-board.surface.forward), Quaternion.identity);
        camera.transform.LookAt(board.surface.position);
        Cursor.lockState = CursorLockMode.Locked;
        board.SendMessage("OnApplicationFocus", true);
        board.Begin();
        yield return .15;
        Check(board.Active && board.overlay.activeInHierarchy && board.instruction.gameObject.activeInHierarchy,
            "Board opens its own visible control overlay");
        Check(board.instruction.text.Contains("좌클릭") && board.instruction.text.Contains("Q·E")
            && board.instruction.text.Contains("F:") && board.instruction.text.Contains("G 2초"),
            "Board top instructions include all controls and G completion");
        Rect helpRect = ScreenRect(board.instruction.rectTransform);
        Check(helpRect.yMin > Screen.height * .65f, "Board control instructions remain at the top");
        Check(!board.progress.gameObject.activeSelf, "Board center progress stays hidden before holding G");
        ScreenCapture.CaptureScreenshot("Temp/Room3BoardUI.png");
        yield return .25;
        board.TickFinish(true, .8f);
        Rect holdRect = ScreenRect(board.progress.rectTransform);
        Check(board.progress.gameObject.activeSelf && board.progress.text.Contains("40%")
            && Vector2.Distance(holdRect.center, new Vector2(Screen.width, Screen.height) * .5f) < 1,
            "G hold shows its two-second progress in the exact screen center");
        Check(board.instruction.gameObject.activeSelf && board.instruction.text.Contains("G 2초"),
            "Top G instructions remain visible while center progress is shown");
        board.TickFinish(false, 0);
        Check(!board.progress.gameObject.activeSelf, "Releasing G hides and resets the center progress");
        foreach (var item in board.items) { board.Pick(item); board.Discard(); }
        board.TickFinish(true, 1.9f);
        Check(!board.Completed, "Board completion still requires a full two-second hold");
        board.TickFinish(false, 0);
        board.TickFinish(true, .2f);
        Check(!board.Completed, "A released hold does not carry completion progress");
        board.TickFinish(true, 1.8f);
        Check(board.Completed && !board.Active && !board.overlay.activeSelf, "A full two-second G hold completes the board and closes its overlay");
        Check(ui.objectiveText && manager.CurrentStage == GameStage.Toys, "Board completion leaves shared UI and progression working");
    }
}
