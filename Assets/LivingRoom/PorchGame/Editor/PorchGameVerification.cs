#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TeamRoulette.Room3;
using TeamRoulette.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PorchGameVerification
{
    const string Folder = "Assets/LivingRoom/PorchGame/Editor/";
    static IEnumerator routine;
    static double deadline;
    static string results;
    static PorchGameVerification()
    {
        EditorApplication.update += CheckRequest;
        EditorApplication.playModeStateChanged += OnPlay;
    }
    static void CheckRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(Folder + "Verify.request.txt") || File.ReadAllText(Folder + "Verify.request.txt").Trim() != "verify") return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != Room3FlowController.LivingRoom) return;
        File.WriteAllText(Folder + "Verify.request.txt", "running");
        SessionState.SetBool("PorchGame.Verify", true);
        EditorApplication.EnterPlaymode();
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("PorchGame.Verify", false))
        {
            SessionState.SetBool("PorchGame.Verify", false);
            results = "현관 엔딩 플레이 모드 검증\n";
            deadline = EditorApplication.timeSinceStartup + 45;
            routine = Verify(); EditorApplication.update += Pump;
        }
        if (state == PlayModeStateChange.ExitingPlayMode) EditorApplication.update -= Pump;
    }
    static void Check(bool ok, string message)
    { if (!ok) throw new Exception(message); results += "통과: " + message + "\n"; }
    static void Pump()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("검증 시간 초과");
            if (routine.MoveNext()) return;
            results += "전체 통과\n"; Finish();
        }
        catch (Exception error) { results += "실패: " + error + "\n"; Finish(); }
    }
    static void Finish()
    {
        File.WriteAllText(Folder + "VerificationReport.txt", results);
        File.WriteAllText(Folder + "Verify.request.txt", "finished");
        EditorApplication.update -= Pump; routine = null; EditorApplication.ExitPlaymode();
    }
    static IEnumerator Verify()
    {
        yield return null; yield return null;
        var game = UnityEngine.Object.FindFirstObjectByType<PorchGameController>();
        var target = UnityEngine.Object.FindFirstObjectByType<PorchGameTarget>();
        var manager = GameManager.Instance;
        Check(game && target && manager && target.controller == game, "기존 매니저와 독립 클릭 영역 연결");
        Check(!PrefabUtility.IsPartOfPrefabInstance(target), "집 프리팹 밖의 별도 상호작용 영역");
        Check(game.font && game.creditsText == "게임이 끝났습니다.\n엔딩 크레딧이 내려갑니다.", "한글 글꼴과 수정 가능한 임시 크레딧");
        game.Interact();
        Check(!game.IsRunning && !game.CanStart && manager.CurrentStage == GameStage.Curtain, "목표 미완료 상태에서 엔딩 차단");
        foreach (var stage in GameManager.Progression.TakeWhile(s => s != GameStage.Entrance))
        {
            Check(!game.CanStart, "선행 목표 완료 전 차단: " + stage);
            Check(GameManager.ReportComplete(stage), "검증 세션에서만 목표 완료: " + stage);
        }
        Check(game.CanStart, "모든 선행 목표 완료 후 시작 허용");
        // Confirm an actual player-sized sphere ray hits the separate proxy before the door.
        var collider = target.GetComponent<BoxCollider>();
        Physics.SyncTransforms();
        var centre = collider.transform.TransformPoint(collider.center);
        int axis = collider.size.x < collider.size.y ? 0 : 1;
        if (collider.size.z < collider.size[axis]) axis = 2;
        var direction = collider.transform.TransformDirection(axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward);
        int hitSides = 0;
        foreach (float side in new[] { -1f, 1f })
            if (Physics.SphereCast(centre + direction * side * .6f, .03f, -direction * side, out var hit, 1.2f) && hit.collider == collider) hitSides++;
        Check(hitSides == 2, "문 양쪽에서 실제 클릭 광선이 독립 영역에 적중");
        // Speed up only this non-persistent test run.
        game.scrollSpeed = 1100; game.endDelay = .1f;
        Check(game.titleSceneName == "Title" && Application.CanStreamedLevelBeLoaded("Title"), "Title is configured and loadable");
        game.Interact();
        var ui = UnityEngine.Object.FindFirstObjectByType<GameUI>();
        float startDeadline = Time.realtimeSinceStartup + 30;
        while (Time.realtimeSinceStartup < startDeadline &&
            (!ui.IsDialogueOpen || ui.dialogueText.text != Room3DialogueScript.EntranceStart[0]))
            yield return null;
        Check(ui.IsDialogueOpen && ui.dialogueText.text == Room3DialogueScript.EntranceStart[0] && Time.timeScale > 0,
            "Entrance dialogue is shown before credits pause the room");
        ui.AdvanceDialogue();
        while (Time.timeScale > 0 && Time.realtimeSinceStartup < startDeadline) yield return null;
        Check(game.IsRunning && manager.IsFinished && manager.Flow.EndingVisible, "현관 목표 완료와 크레딧 시작");
        Check(Time.timeScale == 0 && !UnityEngine.Object.FindFirstObjectByType<FirstPersonPlayer>(), "방과 플레이어 조작 정지");
        yield return null;
        var text = game.GetComponentsInChildren<TextMeshProUGUI>().Single();
        while (text.text != game.creditsText && Time.realtimeSinceStartup < startDeadline)
            yield return null;
        Check(text.text == game.creditsText, "Credits are visible after the entrance dialogue");
        var initialY = text.rectTransform.anchoredPosition.y;
        float until = Time.realtimeSinceStartup + .15f;
        while (Time.realtimeSinceStartup < until) yield return null;
        Check(text.rectTransform.anchoredPosition.y > initialY, "시간 정지 중에도 아래에서 위로 크레딧 이동");
        var enumerator = typeof(PorchGameController).GetMethod("RollCredits", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(enumerator != null, "크레딧 코루틴 연결");
        until = Time.realtimeSinceStartup + 8;
        while (ui && !ui.IsDialogueOpen && Time.realtimeSinceStartup < until)
            yield return null;
        Check(ui && ui.IsDialogueOpen && ui.dialoguePanel.activeInHierarchy &&
            ui.dialogueText.text == Room3DialogueScript.EntranceFinish[0] && text.text == "" &&
            Time.timeScale == 0 && !game.CreditsFinished,
            "The original dialogue panel shows the final line after the credits");
        until = Time.realtimeSinceStartup + 10;
        while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Title" && Time.realtimeSinceStartup < until)
            yield return null;
        Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Title" && Time.timeScale == 1,
            "Title loads and the game clock resumes");
    }
}
#endif
