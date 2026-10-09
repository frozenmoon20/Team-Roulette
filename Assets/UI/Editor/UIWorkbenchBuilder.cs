using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamRoulette.UI.Editor
{
    // The prefab is shipped as an asset. Teammates do not run a generator.
    public static class UIWorkbenchBuilder
    {
        [MenuItem("Tools/Team Roulette UI/Open Example Scene")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
            {
                Debug.LogWarning("저장하지 않은 씬 변경이 있습니다. 저장 후 예시 씬을 열어 주세요.");
                return;
            }
            EditorSceneManager.OpenScene("Assets/UI/Examples/UI_Workbench.unity");
        }
    }
}
