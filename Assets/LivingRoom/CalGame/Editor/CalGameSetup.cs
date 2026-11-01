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
    const string ScenePath = "Assets/Scenes/Room3_LivingRoom.unity";
    const string Request = Folder + "/Editor/Setup.request";
    static CalGameController test;
    static int step;
    static double next;

    static CalGameSetup()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += OnPlay;
    }

    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { BuildAndVerify(); }
        catch (Exception e) { File.WriteAllText(Folder + "/Verification.txt", "FAILED: " + e); Debug.LogException(e); }
    }

    [MenuItem("Tools/LivingRoom/CalGame/Set up scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play mode first.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) throw new Exception("Open Room3_LivingRoom before setting up CalGame.");
        var items = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).ToArray();
        Func<string, GameObject> find = name => items.Single(t => t.name == name).gameObject;
        var win = find("CalenderWin");
        var spr = find("CalenderSpr");
        var circle = find("Circle");
        var root = items.SingleOrDefault(t => t.name == "CalGame")?.gameObject;
        if (!root) { root = new GameObject("CalGame"); Undo.RegisterCreatedObjectUndo(root, "Create CalGame"); }
        foreach (var obj in new[] { win, spr, circle }) Undo.SetTransformParent(obj.transform, root.transform, "Group CalGame");
        var game = root.GetComponent<CalGameController>();
        if (!game) game = Undo.AddComponent<CalGameController>(root);
        Undo.RecordObject(game, "Configure CalGame");
        game.calenderWin = win; game.calenderSpr = spr; game.circle = circle;
        game.pageAudio = root.GetComponent<AudioSource>();
        if (!game.pageAudio) game.pageAudio = Undo.AddComponent<AudioSource>(root);
        Undo.RecordObject(game.pageAudio, "Configure page sound");
        game.pageAudio.playOnAwake = false;
        game.pageAudio.spatialBlend = 0;
        game.pageAudio.volume = 0.65f;
        if (!game.pageTurnSound) game.pageTurnSound = MakeSound();
        foreach (var obj in new[] { win, spr })
        {
            var target = obj.GetComponent<CalGameTarget>();
            if (!target) target = Undo.AddComponent<CalGameTarget>(obj);
            Undo.RecordObject(target, "Connect calendar click");
            target.game = game; target.springPage = obj == spr;
            if (!obj.GetComponentsInChildren<Collider>(true).Any()) throw new Exception(obj.name + " needs a Collider covering the calendar.");
            EditorUtility.SetDirty(target);
        }
        foreach (var collider in circle.GetComponentsInChildren<Collider>(true))
        { Undo.RecordObject(collider, "Make Circle nonblocking"); collider.enabled = false; }
        Undo.RecordObjects(new UnityEngine.Object[] { win, spr, circle }, "Set initial calendar state");
        win.SetActive(true); spr.SetActive(false); circle.SetActive(false);
        EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed.");
        AssetDatabase.SaveAssets();
    }

    static AudioClip MakeSound()
    {
        const string path = Folder + "/Audio/PageTurn_Placeholder.wav";
        Directory.CreateDirectory(Folder + "/Audio");
        if (!File.Exists(path))
        {
            const int rate = 44100, count = 22050;
            var random = new System.Random(51);
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                double low = 0;
                for (int i = 0; i < count; i++)
                {
                    double t = (double)i / count, noise = random.NextDouble() * 2 - 1;
                    low += 0.18 * (noise - low);
                    double envelope = Math.Sin(Math.PI * t) * Math.Exp(-2 * t);
                    double flutter = 0.6 + 0.4 * Math.Sin(t * 95 + t * t * 110);
                    writer.Write((short)((noise - low) * envelope * flutter * 14000));
                }
            }
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    [MenuItem("Tools/LivingRoom/CalGame/Set up and verify")]
    public static void BuildAndVerify()
    {
        Build();
        SessionState.SetBool("CalGame.Verify", true);
        EditorApplication.EnterPlaymode();
    }

    static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("CalGame.Verify", false)) return;
        SessionState.SetBool("CalGame.Verify", false);
        test = UnityEngine.Object.FindFirstObjectByType<CalGameController>();
        step = 0; next = EditorApplication.timeSinceStartup + 0.3;
        EditorApplication.update += VerifyStep;
    }

    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void VerifyStep()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + 0.15;
        try
        {
            if (step == 0)
            {
                Check(test && test.enabled && test.calenderWin.activeSelf && !test.calenderSpr.activeSelf && !test.circle.activeSelf, "Initial state");
                Check(test.pageTurnSound && test.pageTurnSound.samples > 0, "Sound imported");
                foreach (var page in new[] { test.calenderWin, test.calenderSpr })
                {
                    var collider = page.GetComponent<Collider>();
                    Check(collider && collider.enabled && collider.GetComponentInParent<IInteractable>(true) is CalGameTarget, "Calendar collider routes to CalGameTarget");
                }
                test.calenderWin.GetComponent<CalGameTarget>().Interact();
                Check(test.Turned && !test.Marked && !test.circle.activeSelf && !test.calenderWin.activeSelf && test.calenderSpr.activeSelf, "First click only turns page");
                Check(test.pageAudio.isPlaying, "Page sound plays");
                test.calenderSpr.GetComponent<CalGameTarget>().Interact();
                Check(!test.Marked, "Same input frame cannot also mark date");
            }
            else if (step == 1)
            {
                test.calenderSpr.GetComponent<CalGameTarget>().Interact();
                Check(test.Marked && test.circle.activeSelf, "Second click shows Circle");
            }
            else
            {
                test.calenderSpr.GetComponent<CalGameTarget>().Interact();
                test.enabled = false; test.enabled = true;
                Check(test.Marked && test.circle.activeSelf && test.calenderSpr.activeSelf && !test.calenderWin.activeSelf, "Repeated clicks and disable/enable preserve result");
                File.WriteAllText(Folder + "/Verification.txt", "PASS: initial state; collider interaction routing; imported sound playback; separate clicks; Circle visibility; repeated clicks; enable/disable persistence.\n");
                Finish();
            }
            step++;
        }
        catch (Exception e)
        {
            File.WriteAllText(Folder + "/Verification.txt", "FAILED: " + e);
            Debug.LogException(e); Finish();
        }
    }
    static void Finish() { EditorApplication.update -= VerifyStep; EditorApplication.ExitPlaymode(); }
}
