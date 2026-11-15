using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CalGameSetup
{
    const string Folder = "Assets/LivingRoom/CalGame";
    static double ready;
    static CalGameController test;
    static int step;
    static CalGameSetup()
    {
        ready = EditorApplication.timeSinceStartup + 8;
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += OnPlay;
    }
    static void Poll()
    {
        const string request = Folder + "/Editor/SetupPNG.request";
        if (EditorApplication.timeSinceStartup < ready || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
        File.Delete(request);
        try { BuildAndVerify(); }
        catch (Exception e) { Report("FAILED: " + e); Debug.LogException(e); }
    }
    static void Report(string text) { File.WriteAllText(Folder + "/Verification.txt", text); }
    [MenuItem("Tools/LivingRoom/CalGame/Set up scene")]
    public static void Build()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/Room3_LivingRoom.unity") throw new Exception("Open Room3_LivingRoom in Edit mode first.");
        var items = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).ToArray();
        var root = items.Single(x => x.name == "CalGame");
        var calendar = items.Single(x => x.name == "Calender").gameObject;
        var game = root.GetComponent<CalGameController>();
        var textures = new[] { "DecemberCal", "MayCal", "MayCal_circle" }.Select(name => AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/" + name + ".png")).ToArray();
        if (textures.Any(x => !x) || !game || !calendar.GetComponent<MeshRenderer>()) throw new Exception("Missing calendar, controller or PNG.");
        Undo.RecordObject(game, "Connect PNG calendar");
        Undo.SetTransformParent(calendar.transform, root, "Group calendar");
        Undo.RecordObject(calendar.transform, "Fit PNG aspect ratio");
        var scale = calendar.transform.localScale;
        scale.x = scale.y * textures[0].width / textures[0].height;
        calendar.transform.localScale = scale;
        var renderer = calendar.GetComponent<MeshRenderer>();
        const string materialPath = Folder + "/CalendarPNG.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!material)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) shader = Shader.Find("Unlit/Transparent Cutout");
            material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath);
        }
        material.mainTexture = textures[0];
        if (material.HasProperty("_AlphaClip")) { material.SetFloat("_AlphaClip", 1); material.EnableKeyword("_ALPHATEST_ON"); }
        if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.1f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0);
        EditorUtility.SetDirty(material);
        Undo.RecordObject(renderer, "Assign calendar material"); renderer.sharedMaterial = material;
        game.calendarRenderer = renderer;
        game.decemberCal = textures[0]; game.mayCal = textures[1]; game.mayCalCircle = textures[2];
        foreach (var name in new[] { "CalenderWin", "CalenderSpr", "Circle" })
        {
            var old = root.GetComponentsInChildren<Transform>(true).SingleOrDefault(x => x.name == name);
            if (old) Undo.DestroyObjectImmediate(old.gameObject);
        }
        var target = calendar.GetComponent<CalGameTarget>();
        if (!target) target = Undo.AddComponent<CalGameTarget>(calendar);
        Undo.RecordObject(target, "Connect calendar click"); target.game = game;
        Undo.RecordObject(calendar, "Enable calendar"); calendar.SetActive(true);
        if (!calendar.GetComponent<Collider>()) throw new Exception("Calendar collider missing.");
        EditorUtility.SetDirty(target); EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Save failed.");
        AssetDatabase.SaveAssets();
    }
    [MenuItem("Tools/LivingRoom/CalGame/Set up and verify")]
    public static void BuildAndVerify() { Build(); SessionState.SetBool("CalGame.VerifyPNG", true); EditorApplication.EnterPlaymode(); }
    static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("CalGame.VerifyPNG", false)) return;
        SessionState.SetBool("CalGame.VerifyPNG", false);
        test = UnityEngine.Object.FindFirstObjectByType<CalGameController>();
        step = 0; ready = EditorApplication.timeSinceStartup + 1;
        EditorApplication.update += Verify;
    }
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); }
    static void Verify()
    {
        if (EditorApplication.timeSinceStartup < ready) return;
        ready = EditorApplication.timeSinceStartup + 0.2;
        try
        {
            var manager = TeamRoulette.Room3.GameManager.Instance;
            if (step == 0)
            {
                // Only alter the temporary Play-mode state to test this game in isolation.
                if (manager) typeof(TeamRoulette.Room3.GameManager).GetProperty("CurrentStage").SetValue(manager, TeamRoulette.Room3.GameStage.Calendar);
                Time.timeScale = 1;
                Check(test && test.enabled && test.CurrentPage == test.decemberCal, "December initial page");
                Check(test.calendarRenderer.GetComponent<Collider>().GetComponentInParent<IInteractable>() is CalGameTarget, "Calendar collider interaction");
                test.calendarRenderer.GetComponent<CalGameTarget>().Interact();
                Check(test.Turned && !test.Marked && test.CurrentPage == test.mayCal, "First click May");
                Check(test.pageAudio.isPlaying, "Paper audio");
                test.Click(); Check(!test.Marked, "Same frame guard");
            }
            else if (step == 1)
            {
                Time.timeScale = 1;
                test.calendarRenderer.GetComponent<CalGameTarget>().Interact();
                Check(test.Marked && test.CurrentPage == test.mayCalCircle, "Second click marked May");
                Check(!manager || manager.IsComplete(TeamRoulette.Room3.GameStage.Calendar), "Game completion");
            }
            else
            {
                test.Click(); test.RestoreCompleted();
                var block = new MaterialPropertyBlock(); test.calendarRenderer.GetPropertyBlock(block);
                Check(block.GetTexture("_BaseMap") == test.mayCalCircle && test.calendarRenderer.gameObject.activeInHierarchy, "Final renderer texture and restore");
                Check(!test.GetComponentsInChildren<Transform>(true).Any(x => x.name == "CalenderWin" || x.name == "CalenderSpr" || x.name == "Circle"), "Unused objects removed");
                Report("PASS: December initial PNG; same-object collider; first click May; paper sound; separate-click guard; second click marked May; game completion; final texture persistence and restore; unused objects removed.");
                Finish();
            }
            step++;
        }
        catch (Exception e) { Report("FAILED: " + e); Debug.LogException(e); Finish(); }
    }
    static void Finish() { EditorApplication.update -= Verify; EditorApplication.ExitPlaymode(); }
}

