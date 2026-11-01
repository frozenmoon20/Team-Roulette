using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CurtainGameSetup
{
    const string ScenePath = "Assets/Scenes/Room3_LivingRoom.unity";
    const string ReportPath = "Assets/LivingRoom/CurtainGame/Verification.txt";
    static IEnumerator checks;
    static double nextCheck;
    static string report;

    static CurtainGameSetup() { EditorApplication.playModeStateChanged += OnPlay; }

    [MenuItem("Tools/LivingRoom/CurtainGame/Set up scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play mode first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
        var objects = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        Func<string, GameObject> find = name => objects.Single(t => t.name == name).gameObject;
        var root = find("CurtainGame");
        var game = root.GetComponent<CurtainGameController>() ?? Undo.AddComponent<CurtainGameController>(root);
        Undo.RecordObject(game, "Configure CurtainGame");
        game.player = Object.FindFirstObjectByType<FirstPersonPlayer>(FindObjectsInactive.Include);
        game.startTrigger = find("CurtainTriggerStart").GetComponent<BoxCollider>();
        game.endTrigger = find("CurtainTriggerEnd").GetComponent<BoxCollider>();
        game.curtainL = find("CurtainL").transform;
        game.curtainR = find("CurtainR").transform;
        game.dog = find("Dog");
        foreach (var trigger in new[] { game.startTrigger, game.endTrigger })
        {
            Undo.RecordObjects(new Object[] { trigger, trigger.gameObject }, "Configure curtain trigger");
            trigger.gameObject.SetActive(true);
            trigger.enabled = true;
            trigger.isTrigger = true;
            var renderer = trigger.GetComponent<Renderer>();
            if (renderer) { Undo.RecordObject(renderer, "Hide marker"); renderer.enabled = false; }
            Undo.SetTransformParent(trigger.transform, root.transform, "Group curtain trigger");
        }
        foreach (var item in new[] { game.curtainL, game.curtainR, find("Window").transform })
            Undo.SetTransformParent(item, root.transform, "Group curtain objects");
        // Dog is intentionally not parented to CurtainGame or the shrinking player.
        if (game.dog.transform.IsChildOf(root.transform)) Undo.SetTransformParent(game.dog.transform, null, "Keep Dog independent");
        Undo.RecordObjects(new Object[] { game.dog, game.dog.transform }, "Configure guide");
        game.dog.transform.position = game.dogStart;
        game.dog.SetActive(false);
        // The guide must not obstruct the player before their Z coordinates meet.
        foreach (var collider in game.dog.GetComponentsInChildren<Collider>(true))
        { Undo.RecordObject(collider, "Make guide nonblocking"); collider.isTrigger = true; }

        var existing = root.transform.Find("CurtainWhiteScreen");
        if (!existing)
        {
            var overlay = new GameObject("CurtainWhiteScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            Undo.RegisterCreatedObjectUndo(overlay, "Create curtain light");
            overlay.transform.SetParent(root.transform, false);
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var white = new GameObject("White", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            white.transform.SetParent(overlay.transform, false);
            var rect = white.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            white.GetComponent<Image>().color = Color.white;
            white.GetComponent<Image>().raycastTarget = false;
            existing = overlay.transform;
        }
        game.whiteScreen = existing.GetComponent<CanvasGroup>();
        game.whiteScreen.alpha = 0;
        game.whiteScreen.blocksRaycasts = false;
        game.startZ = 9;
        game.minimumScale = 0.4f;
        EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save curtain scene.");
        AssetDatabase.SaveAssets();
        Debug.Log("CurtainGame: scene setup saved.");
    }

    [MenuItem("Tools/LivingRoom/CurtainGame/Set up and verify")]
    public static void BuildAndTest()
    {
        try
        {
            Build();
            SessionState.SetBool("CurtainGame.Verify", true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Debug.LogException(e); if (Application.isBatchMode) EditorApplication.Exit(1); }
    }

    static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("CurtainGame.Verify", false)) return;
        SessionState.SetBool("CurtainGame.Verify", false);
        report = "CurtainGame Play Mode verification\n";
        checks = Verify();
        nextCheck = EditorApplication.timeSinceStartup + 0.5;
        EditorApplication.update += Pump;
    }

    static void Pump()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        try
        {
            if (!checks.MoveNext())
            {
                File.WriteAllText(ReportPath, report + "ALL CHECKS PASSED\n");
                EditorApplication.update -= Pump;
                Finish(0);
                return;
            }
            nextCheck = EditorApplication.timeSinceStartup + (double)checks.Current;
        }
        catch (Exception e)
        {
            File.WriteAllText(ReportPath, report + "FAILED: " + e + "\n");
            Debug.LogException(e);
            EditorApplication.update -= Pump;
            Finish(1);
        }
    }

    static void Finish(int code)
    {
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else { EditorApplication.ExitPlaymode(); AssetDatabase.Refresh(); }
    }

    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        report += "PASS: " + label + "\n";
        Debug.Log("CurtainGame PASS: " + label);
    }

    static IEnumerator Verify()
    {
        var g = Object.FindFirstObjectByType<CurtainGameController>();
        var p = g.player;
        var cc = p.GetComponent<CharacterController>();
        float speed = p.moveSpeed, jump = p.jumpHeight;
        Vector3 scale = p.transform.localScale, dogScale = g.dog.transform.localScale;
        p.canMove = p.canLook = false;
        Action<float> move = z => {
            cc.enabled = false;
            p.transform.position = new Vector3(16.5f, 0.05f, z);
            cc.enabled = true;
            Physics.SyncTransforms();
        };
        Check(!g.dog.activeSelf && g.dog.transform.parent != g.transform, "Dog starts hidden and independent");
        Check(g.startTrigger.isTrigger && g.endTrigger.isTrigger, "Start and End are active trigger volumes");
        move(8); yield return 0.15;
        move(9); yield return 0.15;
        Check(g.Started && g.dog.activeSelf && Mathf.Abs(g.CurrentScale - 0.9f) < 0.005f, "Start contact shows Dog and shrinks player to 0.9");
        move(9.5f); yield return 0.15;
        Check(Mathf.Abs(g.CurrentScale - 0.85f) < 0.005f && Mathf.Abs(p.moveSpeed - speed * 0.85f) < 0.005f && Mathf.Abs(p.jumpHeight - jump * 0.85f) < 0.005f, "Fractional Z smoothly scales body, speed and jump");
        Check(Mathf.Abs(cc.bounds.size.x - cc.radius * 2 * scale.x * 0.85f) < 0.03f, "CharacterController world collision width shrinks");
        move(10); yield return 0.15;
        Check(g.dog.transform.position.z > 10 && g.dog.transform.position.z < 11, "Dog moves through intermediate positions, not teleporting");
        yield return 0.55;
        Check(Mathf.Abs(g.dog.transform.position.z - 11) < 0.001f, "Dog stops one unit ahead");
        move(9.5f); yield return 0.15;
        Check(Mathf.Abs(g.CurrentScale - 0.85f) < 0.005f && Mathf.Abs(g.dog.transform.position.z - 11) < 0.001f, "Walking backward grows player while Dog waits");
        move(8); yield return 0.15;
        Check(g.CurrentScale == 1, "Retreat past the entrance restores full size");
        g.endTrigger.enabled = false;
        move(14); yield return 2.3;
        Check(Mathf.Abs(g.CurrentScale - 0.4f) < 0.005f, "Minimum size is 0.4");
        Check(Mathf.Abs(g.dog.transform.position.z - 15) < 0.001f && g.dog.transform.localScale == dogScale, "Dog reaches Z15 and retains its size");
        var l = g.curtainL.position; var r = g.curtainR.position;
        g.endTrigger.enabled = true;
        move(14.4f); yield return 0.15;
        Check(g.PlayingFinale && !p.enabled && !g.dog.activeSelf, "End contact locks player and hides Dog");
        var fixedPosition = p.transform.position;
        yield return 0.6;
        Check(g.whiteScreen.alpha > 0.95f && p.transform.position == fixedPosition, "Screen is white and player stays fixed during finale");
        yield return 1.6;
        Check(g.Completed && p.enabled && g.CurrentScale == 1 && g.whiteScreen.alpha == 0, "Finale restores controls, size and brightness");
        Check(p.transform.position.z == fixedPosition.z && p.transform.localScale == scale && p.moveSpeed == speed && p.jumpHeight == jump, "Original values and arrival Z are restored");
        Check(Vector3.Distance(g.curtainL.position, l + Vector3.left * g.curtainTravel) < 0.001f && Vector3.Distance(g.curtainR.position, r + Vector3.right * g.curtainTravel) < 0.001f, "Both curtains reach their open positions");
        move(8); yield return 0.1;
        move(9); yield return 0.15;
        Check(!g.dog.activeSelf && g.CurrentScale == 1 && g.Completed, "Sequence runs once and curtains remain open");
    }
}
