using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FaucetWaterSwap
{
    const string Request = "Assets/LivingRoom/WashGame/Editor/FaucetWaterSwap.request";
    const string Report = "Assets/LivingRoom/WashGame/Reports/FaucetWaterSwap.txt";
    static FaucetWaterSwap() => EditorApplication.update += Poll;

    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Room3_LivingRoom.unity") return;
        File.Delete(Request);
        try
        {
            var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var game = all.Select(t => t.GetComponent<WashGameController>()).Single(g => g);
            var waterfall = all.Single(t => t.name == "WaterFall_2_Realistic");
            var previous = all.SingleOrDefault(t => t.name == "VFX_Pee_Stream_01");
            var baseEffect = waterfall.Find("waterfall_base");
            var stream = waterfall.Find("waterfall_effect");
            if (!stream || !baseEffect) throw new System.InvalidOperationException("Waterfall children are missing.");

            Undo.SetCurrentGroupName("Replace WashGame water effect");
            Undo.SetTransformParent(waterfall, game.transform, "Group faucet water");
            Undo.RecordObject(baseEffect.gameObject, "Enable waterfall base");
            baseEffect.gameObject.SetActive(true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(baseEffect.gameObject);
            Undo.RecordObject(waterfall.gameObject, "Start with water off");
            waterfall.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(waterfall.gameObject);
            Undo.RecordObject(game, "Connect faucet water");
            var oldWater = game.water;
            game.water = null;
            game.waterVisual = waterfall.gameObject;
            if (oldWater)
            {
                var renderer = oldWater.GetComponent<ParticleSystemRenderer>();
                if (renderer) Undo.DestroyObjectImmediate(renderer);
                Undo.DestroyObjectImmediate(oldWater);
                Undo.RecordObject(game.runningAudio.gameObject, "Rename wash audio holder");
                game.runningAudio.gameObject.name = "WashWaterAudio";
            }
            if (previous) Undo.DestroyObjectImmediate(previous.gameObject);
            EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene could not be saved.");
            typeof(WashGameController).GetMethod("SetWaterFlow", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(game, new object[] { true });
            bool starts = waterfall.gameObject.activeInHierarchy && stream.gameObject.activeInHierarchy && baseEffect.gameObject.activeInHierarchy;
            typeof(WashGameController).GetMethod("SetWaterFlow", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(game, new object[] { false });
            EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Report, $"Saved. Stream and base activate together: {starts}. Water starts off: {!waterfall.gameObject.activeSelf}. Former particle removed: {!game.water}. Former scene prefab removed: {!previous}.");
        }
        catch (System.Exception ex) { File.WriteAllText(Report, ex.ToString()); Debug.LogException(ex); }
    }
}
