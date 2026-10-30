using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamRoulette.UI.Editor
{
    [InitializeOnLoad]
    public static class UIValidation
    {
        const string Request = "Assets/UI/Editor/Validate.request";
        const string Report = "Assets/UI/Examples/Reports/ValidationReport.txt";
        static UIValidation() { EditorApplication.update += Tick; }
        [MenuItem("Tools/Team Roulette UI/Validate Workbench")]
        public static void Run()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) File.WriteAllText(Request, "validate");
        }
        static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling) return;
            int stage = SessionState.GetInt("RouletteUI.RevisedValidation", 0);
            if (stage == 1 && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetInt("RouletteUI.RevisedValidation", 0);
                return;
            }
            if (stage == 0)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != "Assets/UI/Examples/UI_Workbench.unity") return;
                Directory.CreateDirectory(Path.GetDirectoryName(Report));
                File.WriteAllText(Report, "Revised UI runtime validation\n");
                SessionState.SetInt("RouletteUI.RevisedValidation", 1);
                EditorApplication.isPlaying = true;
            }
            else if (stage == 1 && EditorApplication.isPlaying && Time.frameCount > 5)
            {
                new GameObject("UI regression runner").AddComponent<UIRegressionProbe>();
                SessionState.SetInt("RouletteUI.RevisedValidation", 2);
            }
            else if (stage == 2 && File.Exists(Report))
            {
                string report = File.ReadAllText(Report);
                if (!report.Contains("ALL CHECKS PASSED") && !report.Contains("FAIL ")) return;
                AssetDatabase.DeleteAsset(Request);
                SessionState.SetInt("RouletteUI.RevisedValidation", 0);
                EditorApplication.isPlaying = false;
            }
        }
    }
}
