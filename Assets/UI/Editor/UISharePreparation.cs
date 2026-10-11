using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TeamRoulette.UI.Editor
{
    public static class UISharePreparation
    {
        [MenuItem("Tools/Team Roulette UI/Validate Shared Prefab")]
        public static void ValidatePrefab()
        {
            var prefab = PrefabUtility.LoadPrefabContents("Assets/UI/GameUI.prefab");
            try
            {
                var ui = prefab.GetComponent<GameUI>();
                var data = prefab.GetComponent<SceneUIContent>();
                if (data.objectives.Count != 0 || data.dialogue || data.playDialogueOnStart) throw new Exception("Shared prefab contains example content.");
                if (prefab.GetComponentsInChildren<Transform>(true).Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) != 0)) throw new Exception("Missing script.");
                if (!ui.objectiveText || !ui.dialogueText || !ui.settings || !prefab.GetComponent<PawProgress>().graphic) throw new Exception("Missing UI reference.");
                if (ui.keyGuides.Any(g => g.key == "SPACE" || g.key == "TAB")) throw new Exception("Removed key guide remains.");
                if (AssetDatabase.GetDependencies("Assets/UI/GameUI.prefab", true).Any(p => p.StartsWith("Assets/UI/Examples/") || p.StartsWith("Assets/StoreAssets/"))) throw new Exception("Example or private asset dependency.");
                File.WriteAllText("Assets/UI/ShareReport.txt", "PASS: Shared prefab references, clean content, key guides and dependencies.\n");
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
    }
}
