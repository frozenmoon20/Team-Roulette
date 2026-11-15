#if UNITY_EDITOR
using System.IO;
using TeamRoulette.Room3;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PorchGameCleanup
{
    static PorchGameCleanup() { EditorApplication.update += Run; }
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        const string done = "Assets/LivingRoom/PorchGame/Editor/Cleanup.done.txt";
        if (File.Exists(done)) { EditorApplication.update -= Run; return; }
        var scene = SceneManager.GetActiveScene();
        if (scene.name != Room3FlowController.LivingRoom) return;
        PorchGameController game = null;
        foreach (var root in scene.GetRootGameObjects())
            if (root.TryGetComponent<PorchGameController>(out game)) break;
        if (!game) return;
        var old = game.transform.Find("Door");
        if (old)
        {
            var filter = old.GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh || PrefabUtility.IsPartOfPrefabInstance(old) || old.gameObject.activeSelf || old.childCount != 0)
                throw new System.InvalidOperationException("기존 현관 임시 큐브가 아닌 오브젝트는 삭제하지 않습니다.");
            Undo.DestroyObjectImmediate(old.gameObject);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("씬 저장 실패");
        File.WriteAllText(done, "removed placeholder Door; retained credits controller and click target");
        EditorApplication.update -= Run;
    }
}
#endif
