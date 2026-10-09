using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamRoulette.UI.Editor
{
    [InitializeOnLoad]
    public static class UISharePreparation
    {
        const string Root = "Assets/UI";
        const string Request = Root + "/Editor/Share.request";
        static UISharePreparation() { EditorApplication.delayCall += PrepareRequested; }
        static void PrepareRequested()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            try { Prepare(); AssetDatabase.DeleteAsset(Request); }
            catch (Exception ex) { File.WriteAllText(Root + "/ShareReport.txt", "FAIL\n" + ex); Debug.LogException(ex); }
        }
        static void Move(string from, string to)
        {
            if (!File.Exists(from)) return;
            string error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(error)) throw new Exception(error);
        }
        static void Prepare()
        {
            if (!AssetDatabase.IsValidFolder(Root + "/Examples")) AssetDatabase.CreateFolder(Root, "Examples");
            if (!AssetDatabase.IsValidFolder(Root + "/Examples/Scripts")) AssetDatabase.CreateFolder(Root + "/Examples", "Scripts");
            if (!AssetDatabase.IsValidFolder(Root + "/Examples/Reports")) AssetDatabase.CreateFolder(Root + "/Examples", "Reports");
            string oldScenePath = Root + "/UI_Workbench.unity";
            if (!File.Exists(oldScenePath)) oldScenePath = Root + "/Examples/UI_Workbench.unity";
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(oldScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(oldScenePath, OpenSceneMode.Additive);
            if (scene.isDirty) throw new Exception("예시 씬에 저장하지 않은 변경이 있어 정리를 중단했습니다.");
            var instance = scene.GetRootGameObjects().Select(x => x.GetComponent<GameUI>()).First(x => x != null);
            var content = instance.GetComponent<SceneUIContent>();
            string exampleData = EditorJsonUtility.ToJson(content);
            // Keep the published prefab free of scene-specific content and test objects.
            var prefab = PrefabUtility.LoadPrefabContents(Root + "/GameUI.prefab");
            try
            {
                var data = prefab.GetComponent<SceneUIContent>();
                data.objectives.Clear(); data.dialogue = null; data.playDialogueOnStart = false;
                var ui = prefab.GetComponent<GameUI>();
                ui.dialogueText.text = ""; ui.dialoguePage.text = "SPACE";
                ui.objectiveTemplate.label.text = ""; ui.objectiveCount.text = "0 / 0";
                PrefabUtility.SaveAsPrefabAsset(prefab, Root + "/GameUI.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            // Store the sample data as explicit overrides on the example scene only.
            EditorJsonUtility.FromJsonOverwrite(exampleData, content);
            PrefabUtility.RecordPrefabInstancePropertyModifications(content);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, oldScenePath);
            if (opened) { EditorSceneManager.CloseScene(scene, true); if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
            Move(Root + "/UI_Workbench.unity", Root + "/Examples/UI_Workbench.unity");
            Move(Root + "/ExampleDialogue.asset", Root + "/Examples/ExampleDialogue.asset");
            Move(Root + "/PreviewSurface.mat", Root + "/Examples/PreviewSurface.mat");
            Move(Root + "/Scripts/UIPreview.cs", Root + "/Examples/Scripts/UIPreview.cs");
            Move(Root + "/BuildReport.txt", Root + "/Examples/Reports/BuildReport.txt");
            Move(Root + "/ValidationReport.txt", Root + "/Examples/Reports/ValidationReport.txt");
            ValidatePrefab();
        }
        [MenuItem("Tools/Team Roulette UI/Validate Shared Prefab")]
        public static void ValidatePrefab()
        {
            var prefab = PrefabUtility.LoadPrefabContents(Root + "/GameUI.prefab");
            try
            {
                var ui = prefab.GetComponent<GameUI>();
                var data = prefab.GetComponent<SceneUIContent>();
                if (data.objectives.Count != 0 || data.dialogue != null || data.playDialogueOnStart) throw new Exception("Example data remains in shared prefab.");
                if (prefab.GetComponentInChildren<UIPreview>(true)) throw new Exception("Preview script remains in shared prefab.");
                if (prefab.GetComponentsInChildren<Transform>(true).Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) != 0)) throw new Exception("Missing script in prefab.");
                if (!ui.canvas || !ui.dialogueText || !ui.objectiveTemplate || !ui.settings || !ui.blurMaterial || ui.tabButtons.Any(x => !x)) throw new Exception("Required UI reference missing.");
                var dependencies = AssetDatabase.GetDependencies(Root + "/GameUI.prefab", true);
                if (dependencies.Any(p => p.StartsWith(Root + "/Examples/", StringComparison.Ordinal) || p.StartsWith("Assets/StoreAssets/", StringComparison.Ordinal))) throw new Exception("Shared prefab depends on examples or private store assets.");
                File.WriteAllText(Root + "/ShareReport.txt", "PASS: Shared prefab contains no example dialogue or objectives.\nPASS: No preview component or missing scripts.\nPASS: Required UI references assigned.\nPASS: No example/private StoreAssets dependency.\n\nDependencies:\n" + string.Join("\n", dependencies));
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
    }
}
