#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TeamRoulette.UI;

[InitializeOnLoad]
public static class WindowGameSetup
{
    public const string Folder = "Assets/LivingRoom/WindowGame";
    const string Request = Folder + "/Editor/Install.request.txt";
    static WindowGameSetup() { EditorApplication.delayCall += Install; }
    static void Install()
    {
        if (!File.Exists(Request) || File.ReadAllText(Request).Trim() != "install") return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += Install; return; }
        try { Configure(); File.WriteAllText(Request, "installed"); AssetDatabase.Refresh(); EditorApplication.delayCall += WindowGameVerification.Run; }
        catch (Exception error) { File.WriteAllText(Request, "error: " + error); Debug.LogException(error); }
    }
    [MenuItem("Tools/Room3/WindowGame/Configure Living Room")]
    public static void Configure()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "Room3_LivingRoom" || EditorApplication.isPlaying)
            throw new InvalidOperationException("Room3_LivingRoom 씬을 열고 재생을 중지해주세요.");
        var nodes = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        Transform One(string name) => nodes.Single(t => t.name == name);
        var root = One("WindowGame");
        var left = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Wall_Interior_2x_door_double_L");
        var right = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Wall_Interior_2x_door_double_R");
        var curtain = nodes.Select(t => t.GetComponent<CurtainGameController>()).Single(c => c);
        var statics = new[] { One("curtaina_static_end").gameObject, One("curtainb_static_end").gameObject };
        var winds = new[] { root.Find("room2_curtaina").gameObject, root.Find("room2_curtainb").gameObject };
        var player = nodes.Select(t => t.GetComponent<FirstPersonPlayer>()).Single(p => p);
        var ui = nodes.Select(t => t.GetComponent<GameUI>()).Single(u => u);
        if (curtain.animatedCurtainA != statics[0] || curtain.animatedCurtainB != statics[1])
            throw new InvalidOperationException("CurtainGame 완료 커튼 참조를 확인해주세요.");
        var audio = nodes.Select(t => t.GetComponent<AudioManager>()).FirstOrDefault(a => a);
        if (!audio) throw new InvalidOperationException("기존 AudioManager가 씬에 없습니다.");
        foreach (var wind in winds)
        {
            var animations = wind.GetComponentsInChildren<Animation>(true);
            if (animations.Length == 0 || animations.Any(a => a["idle"] == null || !a["idle"].clip.legacy))
                throw new InvalidOperationException(wind.name + " Legacy idle 클립이 없습니다.");
        }
        // Preflight completes before changes. Keep an independent scene backup as well as Undo.
        Directory.CreateDirectory(Folder + "/Editor/Backup");
        string backup = Folder + "/Editor/Backup/Room3_LivingRoom.before-window.unity.txt";
        if (!File.Exists(backup)) File.Copy(scene.path, backup);
        Undo.IncrementCurrentGroup(); int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("창문 열기 게임 연결");
        var report = new StringBuilder("WindowGame scene audit and setup\n");
        report.AppendLine("Scene: " + scene.path);
        report.AppendLine("Pipeline: " + (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name : "Built-in"));
        report.AppendLine("Curtain movement: " + Path(curtain.curtainL) + ", " + Path(curtain.curtainR));
        report.AppendLine("Curtain completion: CurtainGameController.Completed + GameManager.IsComplete(Curtain)");
        report.AppendLine("Window left: " + Path(left) + "; pivot/local position " + left.localPosition.ToString("F4"));
        report.AppendLine("Window right preserved: " + Path(right));
        report.AppendLine("Separate yard exit candidates: " + string.Join(", ", nodes.Where(t => t.name.Contains("Door_exterior") || t.name == "Door" || t.name == "PorchGame").Select(Path)));
        var game = root.GetComponent<WindowGameController>();
        bool existing = game;
        if (!game) game = Undo.AddComponent<WindowGameController>(root.gameObject);
        Undo.RecordObject(game, "창문 설정");
        game.curtainGame = curtain; game.player = player; game.sharedUI = ui; game.slidingWindow = left;
        game.staticCurtains = statics; game.animatedCurtains = winds;
        if (!existing)
        {
            game.closedPosition = left.localPosition; game.closedRotation = left.localEulerAngles;
            game.openPosition = game.closedPosition + left.parent.InverseTransformVector(Vector3.left * 2f);
            game.openRotation = game.closedRotation; game.openDuration = 2;
        }
        for (int i = 0; i < winds.Length; i++) Align(winds[i], statics[i], report);
        foreach (var node in left.GetComponentsInChildren<Transform>(true))
        {
            Undo.RecordObject(node.gameObject, "이동 창문 정적 배칭 해제");
            GameObjectUtility.SetStaticEditorFlags(node.gameObject, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(node.gameObject);
        }
        var target = left.GetComponent<WindowGameTarget>();
        if (!target) target = Undo.AddComponent<WindowGameTarget>(left.gameObject);
        Undo.RecordObject(target, "창문 상호작용 연결"); target.game = game; target.enabled = false;
        game.interactionTarget = target;
        // The house's WindowPane collision sits just in front of the frame colliders.
        // Place a click surface on the existing moving frame, ahead of that pane.
        var clickSurface = left.Find("Window Interaction");
        if (!clickSurface)
        { clickSurface = new GameObject("Window Interaction").transform; Undo.RegisterCreatedObjectUndo(clickSurface.gameObject, "창문 클릭 면 추가"); clickSurface.SetParent(left, false); }
        var clickCollider = clickSurface.GetComponent<BoxCollider>();
        if (!clickCollider) clickCollider = Undo.AddComponent<BoxCollider>(clickSurface.gameObject);
        var frameBounds = GeometryBounds(left.gameObject);
        var clickCenter = frameBounds.center; clickCenter.z = frameBounds.max.z + .06f;
        clickSurface.position = clickCenter; clickSurface.rotation = left.rotation;
        clickCollider.center = Vector3.zero;
        clickCollider.size = new Vector3(Mathf.Max(.2f, frameBounds.size.x / left.lossyScale.x), Mathf.Max(.2f, frameBounds.size.y / left.lossyScale.y), .06f / Mathf.Abs(left.lossyScale.z));
        game.interactionCollider = clickCollider;
        report.AppendLine("Interaction surface ahead of house pane: " + clickCollider.bounds);
        if (!left.GetComponentsInChildren<Collider>(true).Any(c => c.enabled && !c.isTrigger))
        {
            var collider = Undo.AddComponent<BoxCollider>(left.gameObject);
            Bounds local = new Bounds(Vector3.zero, Vector3.zero); bool first = true;
            foreach (var renderer in left.GetComponentsInChildren<Renderer>(true))
            {
                var bounds = renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var p = left.InverseTransformPoint(bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (first) { local = new Bounds(p, Vector3.zero); first = false; } else local.Encapsulate(p);
                }
            }
            collider.center = local.center; collider.size = Vector3.Max(local.size, Vector3.one * .02f);
        }
        if (!game.windSource) game.windSource = Undo.AddComponent<AudioSource>(root.gameObject);
        game.windSource.playOnAwake = false; game.windSource.loop = true;
        game.windSource.spatialBlend = 0; game.windSource.volume = 0;
        if (!game.windClip) game.windClip = MakeWind();
        foreach (var wind in winds)
        {
            foreach (var animation in wind.GetComponentsInChildren<Animation>(true))
            {
                Undo.RecordObject(animation, "커튼 자동 재생 해제");
                animation.playAutomatically = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(animation);
                report.AppendLine(wind.name + ": Legacy=" + animation["idle"].clip.legacy + ", wrap=" + animation["idle"].clip.wrapMode + ", autoPlay=false");
            }
            Undo.RecordObject(wind, "바람 커튼 초기 숨김"); wind.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(wind);
        }
        // The user had the original game root disabled while arranging assets.
        Undo.RecordObject(curtain.gameObject, "기존 커튼 게임 활성화"); curtain.gameObject.SetActive(true);
        foreach (var placeholder in nodes.Where(t => t.name == "WindowClose" || t.name == "WindowOpen"))
        { Undo.RecordObject(placeholder.gameObject, "기존 임시 창문 숨김"); placeholder.gameObject.SetActive(false); }
        report.AppendLine("Input/interaction: existing PlayerInteractor spherecast + left click + IInteractable/IHasName");
        report.AppendLine("Player/Camera: " + Path(player.transform) + " / " + Path(player.cameraRoot));
        report.AppendLine("UI: existing ObjectiveManager/DialogueManager; no UI created");
        report.AppendLine("Audio: existing AudioManager.PlaySceneLoop; clip " + AssetDatabase.GetAssetPath(game.windClip));
        report.AppendLine("Open: " + game.closedPosition + " -> " + game.openPosition + "; seconds=" + game.openDuration);
        foreach (var wind in winds)
            foreach (var renderer in wind.GetComponentsInChildren<Renderer>(true))
                report.AppendLine("Material: " + string.Join(", ", renderer.sharedMaterials.Where(m => m).Select(AssetDatabase.GetAssetPath)));
        EditorUtility.SetDirty(game); EditorUtility.SetDirty(target); AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("거실 씬 저장에 실패했습니다.");
        Undo.CollapseUndoOperations(undoGroup);
        File.WriteAllText(Folder + "/Editor/SetupReport.txt", report.ToString(), new UTF8Encoding(false));
        Selection.activeGameObject = root.gameObject;
        Debug.Log("WindowGame: 거실 창문 이동·커튼·기존 진행 시스템 연결 완료.", game);
    }
    static string Path(Transform node) => node ? (node.parent ? Path(node.parent) + "/" : "") + node.name : "none";
    static Bounds GeometryBounds(GameObject root)
    {
        bool first = true; Bounds bounds = new Bounds();
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = null; Matrix4x4[] skinMatrices = null; BoneWeight[] weights = null;
            if (renderer is SkinnedMeshRenderer skin)
            {
                mesh = skin.sharedMesh;
                var bind = mesh.bindposes; var bones = skin.bones;
                skinMatrices = new Matrix4x4[bind.Length]; weights = mesh.boneWeights;
                for (int i = 0; i < bind.Length; i++) skinMatrices[i] = bones[i].localToWorldMatrix * bind[i];
            }
            else if (renderer.TryGetComponent<MeshFilter>(out var filter)) mesh = filter.sharedMesh;
            if (!mesh) continue;
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                var vertex = vertices[i]; var p = renderer.transform.TransformPoint(vertex);
                if (skinMatrices != null && weights.Length == vertices.Length)
                {
                    var w = weights[i];
                    p = skinMatrices[w.boneIndex0].MultiplyPoint3x4(vertex) * w.weight0
                        + skinMatrices[w.boneIndex1].MultiplyPoint3x4(vertex) * w.weight1
                        + skinMatrices[w.boneIndex2].MultiplyPoint3x4(vertex) * w.weight2
                        + skinMatrices[w.boneIndex3].MultiplyPoint3x4(vertex) * w.weight3;
                }
                if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
            }
        }
        if (first) throw new InvalidOperationException(root.name + " has no curtain mesh.");
        return bounds;
    }
    static void Align(GameObject wind, GameObject rest, StringBuilder report)
    {
        Undo.RecordObject(wind.transform, "걷힌 정지 커튼에 바람 커튼 정렬");
        foreach (var animation in wind.GetComponentsInChildren<Animation>(true))
        { animation.playAutomatically = false; animation.Stop(); animation["idle"].time = 0; animation["idle"].enabled = true; animation.Sample(); animation["idle"].enabled = false; }
        var desired = GeometryBounds(rest); var current = GeometryBounds(wind);
        for (int pass = 0; pass < 3; pass++)
        {
            float x = desired.size.x / Mathf.Max(.001f, current.size.x), y = desired.size.y / Mathf.Max(.001f, current.size.y);
            float Factor(Vector3 axis) { axis = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z)); return axis.x > axis.y && axis.x > axis.z ? x : axis.y > axis.z ? y : 1; }
            wind.transform.localScale = Vector3.Scale(wind.transform.localScale,
                new Vector3(Factor(wind.transform.right), Factor(wind.transform.up), Factor(wind.transform.forward)));
            foreach (var animation in wind.GetComponentsInChildren<Animation>(true))
            { animation["idle"].time = 0; animation["idle"].enabled = true; animation.Sample(); animation["idle"].enabled = false; }
            current = GeometryBounds(wind);
        }
        wind.transform.position += desired.center - current.center;
        PrefabUtility.RecordPrefabInstancePropertyModifications(wind.transform);
        report.AppendLine("Aligned " + wind.name + ": " + GeometryBounds(wind).ToString("F4") + " to " + desired.ToString("F4"));
    }
    static AudioClip MakeWind()
    {
        const string path = Folder + "/VentilationWind.wav";
        if (!File.Exists(path))
        {
            const int rate = 22050, count = rate * 8;
            var random = new System.Random(479); var samples = new float[count];
            double low = 0, slow = 0;
            for (int i = -rate; i < count; i++)
            {
                low += ((random.NextDouble() * 2 - 1) - low) * .12;
                slow += (low - slow) * .012;
                if (i >= 0) samples[i] = (float)((low - slow * .5) * (.65 + .12 * Math.Sin(i * Math.PI * 2 / count) + .08 * Math.Cos(i * Math.PI * 6 / count)));
            }
            // Circular crossfade removes the noise discontinuity at the loop seam.
            const int seam = rate / 4;
            for (int i = 0; i < seam; i++)
            { float t = i / (float)seam; samples[i] = Mathf.Lerp(samples[count - seam + i], samples[i], t); }
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                foreach (var sample in samples) writer.Write((short)(Mathf.Clamp(sample * .8f, -1, 1) * 32767));
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
#endif
