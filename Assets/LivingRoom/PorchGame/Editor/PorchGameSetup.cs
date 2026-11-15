#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TeamRoulette.Room3;
using TeamRoulette.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PorchGameSetup
{
    const string Folder = "Assets/LivingRoom/PorchGame";
    const string Request = Folder + "/Editor/Install.request.txt";
    static double nextCheck;
    static PorchGameSetup() { EditorApplication.update += Check; }
    static void Check()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 2;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(Request) || File.ReadAllText(Request).Trim() != "install") return;
        if (SceneManager.GetActiveScene().name != Room3FlowController.LivingRoom) return;
        try { Configure(); File.WriteAllText(Request, "installed"); }
        catch (Exception error) { File.WriteAllText(Request, "error: " + error); Debug.LogException(error); }
    }
    [MenuItem("Tools/Room3/PorchGame/Configure Living Room")]
    public static void Configure()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != Room3FlowController.LivingRoom)
            throw new InvalidOperationException("편집 모드에서 Room3_LivingRoom 씬을 열어 주세요.");
        var nodes = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var door = nodes.Single(t => t.name == "Door_exterior_windowed.001" &&
            Ancestor(t, "suburb_house"));
        var root = nodes.SingleOrDefault(t => t.name == "PorchGame");
        if (!root) { root = new GameObject("PorchGame").transform; Undo.RegisterCreatedObjectUndo(root.gameObject, "현관 게임 추가"); }
        if (root.parent) Undo.SetTransformParent(root, null, "현관 게임 독립");
        var game = root.GetComponent<PorchGameController>();
        if (!game) game = Undo.AddComponent<PorchGameController>(root.gameObject);
        var ui = nodes.Select(t => t.GetComponent<GameUI>()).FirstOrDefault(t => t);
        Undo.RecordObject(game, "크레딧 글꼴 연결");
        if (!game.font) game.font = ui && ui.objectiveText ? ui.objectiveText.font : null;
        var click = root.Find("Porch Click Area");
        if (!click)
        {
            click = new GameObject("Porch Click Area").transform;
            Undo.RegisterCreatedObjectUndo(click.gameObject, "현관 클릭 영역 추가"); click.SetParent(root, false);
        }
        Undo.RecordObject(click, "현관 클릭 영역 배치");
        click.SetPositionAndRotation(door.position, door.rotation);
        click.localScale = Vector3.one;
        var renderers = door.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("현관문 메시를 찾을 수 없습니다.");
        bool first = true; var bounds = new Bounds();
        foreach (var renderer in renderers)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            var meshBounds = filter && filter.sharedMesh ? filter.sharedMesh.bounds : new Bounds();
            for (int i = 0; i < 8; i++)
            {
                var sign = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                var world = filter && filter.sharedMesh ? renderer.transform.TransformPoint(meshBounds.center + Vector3.Scale(meshBounds.extents, sign)) : renderer.bounds.center + Vector3.Scale(renderer.bounds.extents, sign);
                var p = click.InverseTransformPoint(world);
                if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
            }
        }
        var collider = click.GetComponent<BoxCollider>();
        if (!collider) collider = Undo.AddComponent<BoxCollider>(click.gameObject);
        Undo.RecordObject(collider, "현관 클릭 영역 크기");
        collider.isTrigger = false; collider.center = bounds.center;
        collider.size = Vector3.Max(bounds.size + Vector3.one * .03f, Vector3.one * .08f);
        var target = click.GetComponent<PorchGameTarget>();
        if (!target) target = Undo.AddComponent<PorchGameTarget>(click.gameObject);
        Undo.RecordObject(target, "현관 클릭 연결"); target.controller = game;
        // Only retire the old unit-cube placeholder; the house prefab remains untouched.
        foreach (var old in nodes.Where(t => t.name == "Door" && t.IsChildOf(root)))
        {
            Undo.RecordObject(old.gameObject, "기존 현관 임시 오브젝트 비활성화"); old.gameObject.SetActive(false);
        }
        EditorUtility.SetDirty(game); EditorUtility.SetDirty(target); EditorUtility.SetDirty(collider);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("씬 저장에 실패했습니다.");
        File.WriteAllText(Folder + "/Editor/SetupReport.txt", "현관 크기의 독립 클릭 영역 연결 완료\n대상: " + PathOf(door) + "\n영역: " + PathOf(click) + "\n크기: " + collider.size + "\n집 프리팹 변경 없음\n");
    }
    static bool Ancestor(Transform t, string name)
    { for (var p = t.parent; p; p = p.parent) if (p.name == name) return true; return false; }
    static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;
}
#endif
