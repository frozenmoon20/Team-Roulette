#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using TeamRoulette.Room3;
using TeamRoulette.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

[InitializeOnLoad]
public static class TVGameSetup
{
    public const string Folder = "Assets/LivingRoom/TVGame";
    const string Request = Folder + "/Editor/Install.request.txt";
    const string Report = Folder + "/Editor/SetupReport.txt";
    static double nextCheck;
    static TVGameSetup() { EditorApplication.update += Install; }

    static void Install()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 2;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(Request) || File.ReadAllText(Request).Trim() != "install") return;
        var scene = SceneManager.GetActiveScene();
        if (scene.name != Room3FlowController.LivingRoom) return;
        // Work on the open scene so the user's unsaved Screen transform is preserved.
        if (!scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Any(t => t.name == "Screen")) return;
        try
        {
            Configure();
            File.WriteAllText(Request, "installed", new UTF8Encoding(false));
            AssetDatabase.Refresh();
        }
        catch (Exception error)
        {
            File.WriteAllText(Request, "error: " + error, new UTF8Encoding(false));
            Debug.LogException(error);
        }
    }

    [MenuItem("Tools/Room3/TVGame/Configure Living Room")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("재생을 종료한 후 연결하세요.");
        var scene = SceneManager.GetActiveScene();
        if (scene.name != Room3FlowController.LivingRoom) throw new InvalidOperationException("Room3_LivingRoom 씬을 열어 주세요.");
        var nodes = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var root = nodes.SingleOrDefault(t => t.name == "TVGame");
        if (!root) { root = new GameObject("TVGame").transform; SceneManager.MoveGameObjectToScene(root.gameObject, scene); }
        var screens = nodes.Where(t => t.name == "Screen").ToArray();
        var screen = screens.FirstOrDefault(t => t.IsChildOf(root));
        if (!screen && screens.Length == 1) screen = screens[0];
        if (!screen) throw new InvalidOperationException("TVGame에서 사용할 Screen을 하나로 지정해 주세요.");
        var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(Folder + "/Winter_TV.mp4");
        var thumbnail = AssetDatabase.LoadAssetAtPath<Texture>(Folder + "/Thumbnail.png");
        if (!clip || !thumbnail) throw new InvalidOperationException("Winter_TV.mp4 또는 Thumbnail.png를 찾을 수 없습니다.");
        var originalPosition = screen.position;
        var originalRotation = screen.rotation;
        var originalScale = screen.localScale;
        Undo.RecordObject(screen.gameObject, "TV 화면 활성화");
        screen.gameObject.SetActive(true);
        var game = root.GetComponent<TVGameController>();
        if (!game) game = Undo.AddComponent<TVGameController>(root.gameObject);
        Undo.RecordObject(game, "TV 영상 연결");
        game.screen = screen;
        game.clip = clip;
        game.thumbnail = thumbnail;
        game.player = nodes.Select(t => t.GetComponent<FirstPersonPlayer>()).FirstOrDefault(p => p);
        game.viewCamera = game.player ? game.player.GetComponentInChildren<Camera>(true) : Camera.main;
        var ui = nodes.Select(t => t.GetComponent<GameUI>()).FirstOrDefault(p => p);
        game.font = ui && ui.objectiveText ? ui.objectiveText.font : null;
        var target = screen.GetComponent<TVGameTarget>();
        if (!target) target = Undo.AddComponent<TVGameTarget>(screen.gameObject);
        Undo.RecordObject(target, "TV 클릭 연결");
        target.controller = game;
        if (!screen.GetComponentsInChildren<Collider>(true).Any(c => c.enabled && !c.isTrigger))
        {
            var collider = Undo.AddComponent<BoxCollider>(screen.gameObject);
            var mesh = screen.GetComponent<MeshFilter>();
            var bounds = mesh && mesh.sharedMesh ? mesh.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            collider.center = bounds.center;
            collider.size = Vector3.Max(bounds.size, Vector3.one * .015f);
        }
        // The supplied Screen is 1.4 cm behind the TV's front collider. Keep its
        // transform, and put only our visible/clickable surface slightly in front.
        var filter = screen.GetComponent<MeshFilter>();
        if (filter && filter.sharedMesh)
        {
            var surface = screen.Find("TV Click Surface");
            if (!surface)
            {
                surface = new GameObject("TV Click Surface").transform;
                Undo.RegisterCreatedObjectUndo(surface.gameObject, "TV 클릭 표면 추가");
                surface.SetParent(screen, false);
            }
            var bounds = filter.sharedMesh.bounds;
            var normal = filter.sharedMesh.normals.Length > 0 ? filter.sharedMesh.normals[0] : Vector3.back;
            surface.localPosition = bounds.center + normal * game.surfaceOffset;
            var collider = surface.GetComponent<BoxCollider>();
            if (!collider) collider = Undo.AddComponent<BoxCollider>(surface.gameObject);
            collider.center = Vector3.zero;
            collider.size = Vector3.Max(bounds.size, Vector3.one * .01f);
        }
        int removed = 0;
        foreach (var node in nodes)
        {
            // Remove only the superseded TV interaction, never a toy game's target.
            if (node.name != "TV" && node != screen) continue;
            foreach (var old in node.GetComponents<Room3FlowTarget>()) { Undo.DestroyObjectImmediate(old); removed++; }
            // The original placeholder is a unit cube; the user's actual TV is retained.
            var mesh = node.GetComponent<MeshFilter>();
            if (node.name == "TV" && node != screen && !screen.IsChildOf(node)
                && node.parent && node.parent.name == "TVGame" && mesh && mesh.sharedMesh && mesh.sharedMesh.name == "Cube")
            {
                Undo.RecordObject(node.gameObject, "이전 TV 임시 오브젝트 비활성화");
                node.gameObject.SetActive(false);
            }
        }
        if (screen.position != originalPosition || screen.rotation != originalRotation || screen.localScale != originalScale)
            throw new InvalidOperationException("Screen 배치가 변경되었습니다.");
        EditorUtility.SetDirty(game); EditorUtility.SetDirty(target);
        PrefabUtility.RecordPrefabInstancePropertyModifications(screen.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("씬 저장에 실패했습니다.");
        AssetDatabase.SaveAssets();
        File.WriteAllText(Report,
            "TVGame 설치 완료\n씬: " + scene.path + "\n화면: " + GetPath(screen) +
            "\nScreen 위치/회전/크기 유지: 확인\n영상: " + AssetDatabase.GetAssetPath(clip) +
            "\n썸네일: " + AssetDatabase.GetAssetPath(thumbnail) + "\n이전 TV 클릭 컴포넌트 제거: " + removed +
            "\n목표 완료: 최초 loopPointReached → GameManager.ReportComplete(GameStage.TV)\n" +
            "재시청: 확인창 생략, Esc 허용\n실제 영상 재생 검증: 별도 필요\n", new UTF8Encoding(false));
        Debug.Log("TVGame: Screen 배치를 유지하고 영상·목표 진행 연결을 완료했습니다.", game);
    }
    static string GetPath(Transform node) => node.parent ? GetPath(node.parent) + "/" + node.name : node.name;
}
#endif
