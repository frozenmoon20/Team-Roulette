#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TeamRoulette.Room3;
using TeamRoulette.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[InitializeOnLoad]
public static class TVGameVerification
{
    const string Request = TVGameSetup.Folder + "/Editor/Verify.request.txt";
    const string Report = TVGameSetup.Folder + "/Editor/VerificationReport.txt";
    static IEnumerator routine;
    static string results;
    static double deadline;
    static TVGameVerification()
    {
        EditorApplication.playModeStateChanged += OnPlay;
        EditorApplication.delayCall += CheckRequest;
    }
    static void CheckRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += CheckRequest; return; }
        if (File.Exists(Request) && File.ReadAllText(Request).Trim() == "verify") Run();
    }
    [MenuItem("Tools/Room3/TVGame/Verify In Play Mode")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Room3_LivingRoom") return;
        TVGameSetup.Configure();
        File.WriteAllText(Request, "running");
        SessionState.SetBool("TVGame.Verify", true);
        EditorApplication.EnterPlaymode();
    }
    static void OnPlay(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("TVGame.Verify", false))
        {
            SessionState.SetBool("TVGame.Verify", false);
            results = "TVGame 실제 씬 재생 검증\n";
            deadline = EditorApplication.timeSinceStartup + 100;
            routine = Verify();
            EditorApplication.update += Pump;
        }
        if (change == PlayModeStateChange.ExitingPlayMode) EditorApplication.update -= Pump;
    }
    static void Check(bool value, string message)
    { if (!value) throw new Exception(message); results += "통과: " + message + "\n"; }
    static void Pump()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("검증 시간 초과");
            if (routine != null && routine.MoveNext()) return;
            results += "전체 검증 통과\n";
            Finish();
        }
        catch (Exception error) { results += "실패: " + error + "\n"; Finish(); }
    }
    static void Finish()
    {
        File.WriteAllText(Report, results);
        File.WriteAllText(Request, "finished");
        routine = null;
        EditorApplication.update -= Pump;
        EditorApplication.ExitPlaymode();
    }
    static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    static void Escape(TVGameController game) => typeof(TVGameController).GetMethod("HandleEscape", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
    static IEnumerator Verify()
    {
        float wait = Time.realtimeSinceStartup + .7f;
        while (Time.realtimeSinceStartup < wait) yield return null;
        var game = UnityEngine.Object.FindFirstObjectByType<TVGameController>();
        var manager = GameManager.Instance;
        Check(game && game.enabled && manager && game.player && game.viewCamera, "실제 Screen·플레이어·카메라·진행 관리자 연결");
        Check(!game.Ready && !Field<RawImage>(game, "preview").gameObject.activeSelf, "시작은 검은 화면");
        game.Interact();
        Check(!game.Ready && manager.CurrentStage == GameStage.Curtain, "TV 목표 이전에는 상호작용과 목표 건너뛰기 차단");
        foreach (var stage in GameManager.Progression.TakeWhile(stage => stage != GameStage.TV))
            Check(GameManager.ReportComplete(stage), "검증용 선행 목표 완료: " + stage);
        var mesh = game.screen.GetComponent<MeshFilter>().sharedMesh;
        var normal = game.screen.TransformDirection(mesh.normals[0]).normalized;
        var desired = game.screen.position + normal * .85f;
        game.player.Teleport(game.player.transform.position + desired - game.viewCamera.transform.position,
            Quaternion.LookRotation(-normal, Vector3.up), Quaternion.identity);
        Physics.SyncTransforms();
        game.Interact();
        Check(game.Ready && Field<RawImage>(game, "preview").gameObject.activeSelf, "첫 클릭으로 썸네일과 반투명 플레이 표시");
        var ray = game.viewCamera.ViewportPointToRay(new Vector3(.5f, .5f));
        var interactor = game.player.GetComponentInChildren<PlayerInteractor>();
        results += "판정 진단: screen=" + game.screen.position + "; normal=" + normal + "; ray=" + ray + "\n";
        foreach (var obstacle in Physics.SphereCastAll(ray, interactor.aimRadius, interactor.InteractRange).OrderBy(h => h.distance))
            results += "충돌: " + obstacle.collider.name + "; 거리=" + obstacle.distance + "; 대상=" + obstacle.collider.GetComponentInParent<IInteractable>() + "\n";
        Check(Physics.SphereCast(ray, interactor.aimRadius, out var hit, interactor.InteractRange)
            && hit.collider.GetComponentInParent<IInteractable>() is TVGameTarget, "기존 클릭 판정으로 Screen 도달");
        var ui = UnityEngine.Object.FindFirstObjectByType<GameUI>();
        bool playerEnabled = game.player.enabled;
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .Where(c => !c.transform.IsChildOf(game.transform) && !c.transform.IsChildOf(game.screen))
            .ToDictionary(c => c, c => c.enabled);
        game.Interact();
        Check(Field<GameObject>(game, "prompt").activeInHierarchy && !game.IsPlaying, "플레이 클릭은 영상 없이 하단 확인창만 표시");
        Check(!game.player.enabled && !ui.enabled, "확인 중 이동·시점·설정 입력 차단");
        ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetTempPath(), "TVGame-confirm.png"));
        wait = Time.realtimeSinceStartup + .3f;
        while (Time.realtimeSinceStartup < wait) yield return null;
        game.Decline();
        wait = Time.realtimeSinceStartup + .2f;
        while (Time.realtimeSinceStartup < wait) yield return null;
        Check(game.Ready && !game.IsComplete && game.player.enabled == playerEnabled && ui.enabled,
            "아니오는 썸네일 대기·조작 복구, 클리어하지 않음");
        game.player.Teleport(game.player.transform.position + desired - game.viewCamera.transform.position,
            Quaternion.LookRotation(-normal, Vector3.up), Quaternion.identity);
        game.Interact();
        Check(Field<GameObject>(game, "prompt").activeInHierarchy, "취소 후 플레이 버튼 재선택으로 확인창 다시 표시");
        game.Accept();
        while (!game.IsPlaying) { CheckPreparing(game); yield return null; }
        var video = Field<VideoPlayer>(game, "video");
        while (video.frame < 2) { CheckPreparing(game); yield return null; }
        Check(video.isPlaying && video.texture && !game.IsComplete, "실제 Winter_TV 영상 디코딩·재생, 조기 완료 없음");
        Check(canvases.Keys.All(c => !c.enabled), "전체화면 재생 중 기존 UI 모두 숨김");
        Check(!game.player.enabled && !ui.enabled, "영상 중 이동·카메라·설정 차단");
        Escape(game);
        Check(game.IsPlaying && !game.IsComplete, "최초 영상은 Esc로 중단 불가");
        ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetTempPath(), "TVGame-playing.png"));
        // Seek only in this test, then require the real VideoPlayer end-of-stream event.
        video.time = Math.Max(0, video.length - .8);
        while (!game.IsComplete) { CheckPreparing(game); yield return null; }
        wait = Time.realtimeSinceStartup + .2f;
        while (Time.realtimeSinceStartup < wait) yield return null;
        Check(manager.CurrentStage == GameStage.Entrance && manager.IsComplete(GameStage.TV), "실제 영상 종료 이벤트로 TV 완료 및 현관 목표 진행");
        Check(game.player.enabled == playerEnabled && ui.enabled && canvases.All(pair => pair.Key.enabled == pair.Value),
            "종료 후 기존 플레이어·UI·카메라 입력 상태 복구");
        Check(game.Ready && Field<RawImage>(game, "preview").gameObject.activeSelf, "완료 후 TV 썸네일 유지");
        game.player.Teleport(game.player.transform.position + desired - game.viewCamera.transform.position,
            Quaternion.LookRotation(-normal, Vector3.up), Quaternion.identity);
        game.Interact();
        Check(!Field<GameObject>(game, "prompt").activeInHierarchy, "재시청은 확인창 생략");
        while (!game.IsPlaying) { CheckPreparing(game); yield return null; }
        Escape(game);
        wait = Time.realtimeSinceStartup + .2f;
        while (Time.realtimeSinceStartup < wait) yield return null;
        Check(!game.IsPlaying && game.Ready && ui.enabled && game.player.enabled == playerEnabled,
            "재시청 Esc 종료 및 조작 복구");
        Check(manager.CurrentStage == GameStage.Entrance, "재시청 종료로 다음 목표 건너뛰지 않음");
        Check(Field<RenderTexture>(game, "movieTexture") == null, "재생 종료 시 영상 렌더 텍스처 해제");
    }
    static void CheckPreparing(TVGameController game)
    {
        if (!game || !game.enabled) throw new Exception("TVGame 재생 상태 소실");
        if (!game.IsPlaying && !Field<bool>(game, "captured") && !game.IsComplete)
            throw new Exception("영상 준비 또는 디코딩 실패");
    }
}
#endif
