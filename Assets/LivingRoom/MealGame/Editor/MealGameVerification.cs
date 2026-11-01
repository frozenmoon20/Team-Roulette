using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class MealGameVerification
{
    const string Report = MealGameSetup.Folder + "/Verification.txt";
    static IEnumerator checks;
    static double next;
    static string report;
    static MealGameVerification() { EditorApplication.playModeStateChanged += OnPlay; }

    [MenuItem("Tools/MealGame/2 - Verify full sequence")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play mode first.");
        File.WriteAllText(Report, "MealGame Play Mode verification\n");
        SessionState.SetInt("MealGame.TestRun", 1);
        SessionState.SetBool("MealGame.TestPending", true);
        EditorApplication.EnterPlaymode();
    }
    [MenuItem("Tools/MealGame/3 - Verify mouse pickup and return")]
    public static void RunMouse()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play mode first.");
        SessionState.SetBool("MealGame.MousePending", true);
        EditorApplication.EnterPlaymode();
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("MealGame.MousePending", false))
        {
            SessionState.SetBool("MealGame.MousePending", false);
            report = File.ReadAllText(Report) + "Real mouse input verification\n";
            checks = VerifyMouse();
            SessionState.SetInt("MealGame.TestRun", 3);
            next = EditorApplication.timeSinceStartup + .5;
            EditorApplication.update += Pump;
            return;
        }
        if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool("MealGame.RunAgain", false))
        {
            SessionState.SetBool("MealGame.RunAgain", false);
            SessionState.SetInt("MealGame.TestRun", 2);
            SessionState.SetBool("MealGame.TestPending", true);
            EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
        }
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("MealGame.TestPending", false)) return;
        SessionState.SetBool("MealGame.TestPending", false);
        report = File.ReadAllText(Report) + "Run " + SessionState.GetInt("MealGame.TestRun", 1) + "\n";
        checks = Verify(SessionState.GetInt("MealGame.TestRun", 1) == 2);
        next = EditorApplication.timeSinceStartup + .5;
        EditorApplication.update += Pump;
    }
    static void Pump()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Pump; return; }
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (!checks.MoveNext())
            {
                bool again = SessionState.GetInt("MealGame.TestRun", 1) == 1;
                File.WriteAllText(Report, report + (again ? "FIRST ORDER PASSED\n" : "ALL CHECKS PASSED\n"));
                SessionState.SetBool("MealGame.RunAgain", again);
                EditorApplication.update -= Pump;
                EditorApplication.ExitPlaymode();
                AssetDatabase.Refresh();
                return;
            }
            next = EditorApplication.timeSinceStartup + Convert.ToDouble(checks.Current);
        }
        catch (Exception ex)
        {
            File.WriteAllText(Report, report + "FAILED: " + ex + "\n");
            SessionState.SetBool("MealGame.RunAgain", false);
            EditorApplication.update -= Pump;
            Debug.LogException(ex);
            EditorApplication.ExitPlaymode();
            AssetDatabase.Refresh();
        }
    }
    static void Check(bool condition, string text)
    {
        if (!condition) throw new Exception(text);
        report += "PASS: " + text + "\n";
    }
    static void Aim(MealGameController g, MealAction action)
    {
        var target = Object.FindObjectsByType<MealTarget>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .First(t => t.game == g && t.action == action);
        Bounds bounds = target.GetComponent<Renderer>().bounds;
        var cam = g.interactor.playerCamera.transform;
        cam.position = bounds.center + Vector3.left * 1.2f;
        cam.LookAt(bounds.center);
        Physics.SyncTransforms();
        var actual = g.GetComponent<MealInput>().FindTarget();
        Check(actual && actual.action == action, "Camera ray reaches " + action);
    }
    static IEnumerator VerifyMouse()
    {
        var g = Object.FindFirstObjectByType<MealGameController>();
        OptionState.Instance.Unlock(OptionType.Hand);
        OptionState.Instance.Unlock(OptionType.Grab);
        OptionState.Instance.Unlock(OptionType.Saturation);
        g.player.enabled = false;
        var feel = g.player.GetComponent<PlayerMotionFeel>();
        if (feel) feel.enabled = false;
        // Keep the original camera and enabled MealInput/PlayerInteractor for real clicks.
        Aim(g, MealAction.Fridge);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        double limit = EditorApplication.timeSinceStartup + 120;
        while (PlayerHand.Instance.Held != g.rawToast && EditorApplication.timeSinceStartup < limit) yield return .1;
        Check(PlayerHand.Instance.Held == g.rawToast, "Real left mouse click picks up toast through MealInput");
        yield return .25;
        Check(!g.interactor.enabled, "Shared interactor is suppressed while handling MealGame");
        limit = EditorApplication.timeSinceStartup + 120;
        while (PlayerHand.Instance.Held && EditorApplication.timeSinceStartup < limit) yield return .1;
        Check(!PlayerHand.Instance.Held && !g.rawToast.gameObject.activeSelf, "Real right mouse click returns toast to fridge");
        // Look away, ensuring normal interactions are restored outside MealGame.
        g.interactor.playerCamera.transform.rotation = Quaternion.LookRotation(Vector3.up);
        yield return .2;
        Check(g.interactor.enabled, "Shared interactor is restored when looking away");
    }
    static IEnumerator Verify(bool reverse)
    {
        var g = Object.FindFirstObjectByType<MealGameController>();
        var hand = PlayerHand.Instance;
        var input = g.GetComponent<MealInput>();
        Vector3 originalCameraPosition = g.interactor.playerCamera.transform.localPosition;
        Quaternion originalCameraRotation = g.interactor.playerCamera.transform.localRotation;
        input.enabled = false;
        g.interactor.enabled = false;
        g.player.enabled = false;
        var feel = g.player.GetComponent<PlayerMotionFeel>();
        if (feel) feel.enabled = false;
        OptionState.Instance.Unlock(OptionType.Hand);
        OptionState.Instance.Unlock(OptionType.Grab);
        Time.timeScale = 1;
        yield return .1;
        Check(!g.rawToast.gameObject.activeSelf && !g.jam.gameObject.activeSelf && !g.juice.gameObject.activeSelf &&
            !g.cookedToast.gameObject.activeSelf && !g.jamToast.activeSelf && !g.juiceCup.gameObject.activeSelf, "All ingredients/replacement objects start hidden");
        Check(g.plate.gameObject.activeSelf && g.emptyCup.activeSelf, "Empty plate and cup start visible");
        Check(g.jamToast.transform.IsChildOf(g.plate.transform), "Jam toast is pre-parented to plate");
        Check(g.toastSeconds == 10 && g.blackoutSeconds == .5f && g.eatingSeconds == 3, "Requested timings: 10 / 0.5 / 3 seconds");
        Aim(g, MealAction.Fridge);
        g.Click(MealAction.Fridge);
        Check(hand.Held == g.rawToast, "Fridge gives raw toast");
        g.ReturnHeld();
        Check(!hand.Held && !g.rawToast.gameObject.activeSelf, "Immediate pickup cancellation returns hidden toast to fridge");
        g.Click(MealAction.Fridge);
        yield return .15;
        Check(hand.Held == g.rawToast && g.rawToast.transform.IsChildOf(hand.transform), "Shared hand attaches raw toast");
        g.Click(MealAction.Counter);
        Check(hand.Held == g.rawToast && !g.JamOnCounter, "Wrong destination does not consume item");
        Aim(g, MealAction.Toaster);
        float cookStart = Time.time;
        g.Click(MealAction.Toaster);
        Check(!hand.Held && !g.rawToast.gameObject.activeSelf && g.bakingToast.activeSelf && g.timerPanel.activeSelf, "Insert toast shows baking top and timer");
        g.Click(MealAction.Fridge);
        Check(hand.Held == g.jam, "Fridge gives jam while timer runs");
        yield return .15;
        g.ReturnHeld();
        Check(!g.jam.gameObject.activeSelf, "Jam cancellation is repeatable");
        g.Click(MealAction.Fridge);
        yield return .15;
        g.Click(MealAction.Counter);
        Check(g.JamOnCounter && !hand.Held && g.jam.transform.parent == g.jamPoint, "Jam is placed on counter");
        Check(g.SecondsRemaining > 8 && g.SecondsRemaining < 10, "Timer decreases independently during jam preparation");
        int waited = 0;
        while (!g.ToastReady && waited++ < 130) yield return .1;
        Check(g.ToastReady && Time.time - cookStart >= 9.95f, "Toast finishes after full ten seconds");
        Check(!g.bakingToast.activeSelf && g.cookedToast.gameObject.activeSelf && g.timerText.text == "00", "Baking toast is replaced with ready toast");
        Check(g.cookedToast.transform.position.y > g.bakingToast.transform.position.y && g.dingSound, "Cooked toast pops higher; ding clip is assigned");
        Vector3 pop = g.cookedToast.transform.position;
        g.Click(MealAction.Toaster);
        yield return .15;
        Check(hand.Held == g.cookedToast && Vector3.Dot(g.cookedToast.transform.up, Vector3.up) > .999f,
            "Cooked toast is horizontal in world space, independent of wrist rotation");
        // Inspect the real player/camera/hand hierarchy, not a detached camera.
        g.interactor.playerCamera.transform.localPosition = originalCameraPosition;
        g.interactor.playerCamera.transform.localRotation = originalCameraRotation;
        var body = g.player.GetComponent<CharacterController>();
        body.enabled = false;
        g.player.transform.SetPositionAndRotation(g.playerAfterMeal.position, g.playerAfterMeal.rotation);
        body.enabled = true;
        g.player.cameraRoot.localRotation = Quaternion.Euler(22, 0, 0);
        yield return .3;
        Check(Vector3.Dot(g.cookedToast.transform.up, Vector3.up) > .999f, "Cooked toast stays level while camera looks down");
        Bounds toastBounds = g.cookedToast.GetComponent<Renderer>().bounds;
        Check(Mathf.Abs(toastBounds.min.y - hand.palmPoint.position.y) < .002f, "Toast bottom rests at palm height");
        if (!reverse) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MealGame-held-toast.png"));
        yield return .3;
        g.ReturnHeld();
        Check(Vector3.Distance(g.cookedToast.transform.position, pop) < .001f, "Cooked toast cancellation returns to toaster, not initial scene location");
        Aim(g, MealAction.CookedToast);
        g.Click(MealAction.CookedToast);
        yield return .15;
        Aim(g, MealAction.Plate);
        g.Click(MealAction.Plate);
        Check(g.ToastOnPlate && !hand.Held && g.cookedToast.transform.parent == g.toastOnPlatePoint, "Cooked toast is placed on plate");
        Aim(g, MealAction.Jam);
        Aim(g, MealAction.CookedToast);
        g.Click(MealAction.Jam);
        g.Click(MealAction.None);
        g.Click(MealAction.CookedToast);
        Check(!g.JamApplied, "Empty-space click breaks jam/toast sequence");
        g.Click(MealAction.Table);
        g.Click(MealAction.Jam);
        Check(!g.JamApplied, "Other-object click breaks jam/toast sequence");
        g.ClearSelection();
        g.Click(reverse ? MealAction.Jam : MealAction.CookedToast);
        g.Click(reverse ? MealAction.CookedToast : MealAction.Jam);
        Check(g.JamApplied && g.jamToast.activeSelf && !g.cookedToast.gameObject.activeSelf, reverse ? "Jam then toast applies jam" : "Toast then jam applies jam");
        var plateHome = g.plate.transform.position;
        g.Click(MealAction.Plate);
        yield return .15;
        Check(hand.Held == g.plate && g.jamToast.transform.IsChildOf(g.plate.transform), "Plate carries its active jam toast");
        Check(Vector3.Dot(g.plate.transform.up, Vector3.up) > .999f, "Carried plate and child toast remain horizontal");
        g.ReturnHeld();
        Check(Vector3.Distance(g.plate.transform.position, plateHome) < .001f, "Plate cancellation preserves meal and location");
        g.Click(MealAction.JamToast);
        yield return .15;
        g.Click(MealAction.Table);
        Check(g.PlateOnTable && !hand.Held && !g.Ending, "Plate can be placed by clicking table; waits for cup");
        g.Click(MealAction.Fridge);
        Check(hand.Held == g.juice, "Fridge now gives juice");
        g.ReturnHeld();
        Check(!g.juice.gameObject.activeSelf, "Juice cancellation returns to fridge");
        g.Click(MealAction.Fridge);
        yield return .15;
        Aim(g, MealAction.EmptyCup);
        g.Click(MealAction.EmptyCup);
        Check(g.JuicePoured && !hand.Held && !g.juice.gameObject.activeSelf && !g.emptyCup.activeSelf && g.juiceCup.gameObject.activeSelf,
            "Pour consumes juice and replaces empty cup");
        Check(g.actionAudio.isPlaying, "Pour effect plays");
        Aim(g, MealAction.JuiceCup);
        g.Click(MealAction.JuiceCup);
        yield return .15;
        Check(Vector3.Dot(g.juiceCup.transform.up, Vector3.up) > .999f, "Juice cup remains upright on palm");
        g.ReturnHeld();
        Check(g.juiceCup.gameObject.activeSelf && !hand.Held, "Full cup can be returned and picked up again");
        g.Click(MealAction.JuiceCup);
        yield return .15;
        g.player.enabled = true;
        if (feel) feel.enabled = true;
        g.Click(MealAction.Table);
        Check(g.Ending && g.blackScreen.alpha == 1 && !g.player.enabled && !hand.Held, "Both dishes trigger blackout and lock controls");
        Check(Vector3.Distance(g.player.transform.position, g.playerAfterMeal.position) < .001f, "Blackout moves player body to table-front marker");
        yield return .2;
        Check(g.blackScreen.alpha == 1, "Blackout remains during initial half second");
        yield return .4;
        Check(g.blackScreen.alpha == 0 && g.Ending && g.actionAudio.isPlaying, "Blackout ends; eating sound plays");
        var cam = g.interactor.playerCamera;
        Check(cam.WorldToViewportPoint(g.plate.transform.position).x < cam.WorldToViewportPoint(g.juiceCup.transform.position).x,
            "Eating view shows plate on left and juice on right");
        yield return 1.0;
        Check(!g.Completed && g.jamToast.activeSelf, "Food remains during eating interval");
        yield return 2.1;
        Check(g.Completed && !g.Ending && !g.jamToast.activeSelf && !g.juiceCup.gameObject.activeSelf && g.emptyCup.activeSelf,
            "Eating finishes with empty plate and empty cup");
        Check(g.player.enabled && (!feel || feel.enabled) && !g.actionAudio.isPlaying && g.blackScreen.alpha == 0, "Controls, camera and sound restored");
        Check(Vector2.Distance(new Vector2(g.player.transform.position.x, g.player.transform.position.z),
            new Vector2(g.playerAfterMeal.position.x, g.playerAfterMeal.position.z)) < .02f,
            "Player stays in front of table after eating and control restoration");
        Check(Mathf.Abs(g.player.transform.position.y - g.playerAfterMeal.position.y) < .2f, "Player stands at floor height after gravity resumes");
        g.Click(MealAction.Fridge);
        Check(!hand.Held && g.Completed, "Completed sequence cannot dispense again");
    }
}
