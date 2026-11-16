using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using TeamRoulette.UI;

// (에디터 전용 도구) 지금 열린 씬의 대사를 전부 한 번에 모아서 클립보드에 복사 + 텍스트 파일로 저장한다.
// 대사 검토·수정할 때, 오브젝트마다 하나씩 열어 보지 않고 한눈에 보기 위한 용도. 게임 실행에는 영향 없음.
// 원리:
//   1) 씬 안의 모든 DialogueStep(꺼진 것 포함)을 찾아 "어느 오브젝트 → 어떤 대사 파일 → 대사 줄들" 순서로 적는다.
//   2) 프로젝트 안의 대사 파일(DialogueSequence) 중 위에서 안 나온 것도 따로 적는다 (오프닝 대사 등 다른 곳에서 쓰는 것).
// 사용: 위 메뉴 Tools > 대사 모두 내보내기 → 클립보드에 복사됨 (프로젝트 폴더에 Dialogue_Export.txt로도 저장)
// 이 파일은 반드시 이름이 "Editor"인 폴더 안에 둬야 한다 (빌드에 안 들어가게).
public static class DialogueExport
{
    const string FileName = "Dialogue_Export.txt";   // 프로젝트 폴더(Assets 바깥)에 저장됨

    [MenuItem("Tools/대사 모두 내보내기")]
    static void Export()
    {
        StringBuilder sb = new StringBuilder();
        HashSet<DialogueSequence> used = new HashSet<DialogueSequence>();

        // 1) 씬 안 DialogueStep들 (Hierarchy 경로 순으로 정렬)
        sb.AppendLine("===== 씬: " + SceneManager.GetActiveScene().name + " =====");
        DialogueStep[] steps = Object.FindObjectsByType<DialogueStep>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<DialogueStep> list = new List<DialogueStep>(steps);
        list.Sort((a, b) => string.Compare(PathOf(a.transform), PathOf(b.transform), System.StringComparison.Ordinal));

        foreach (DialogueStep step in list)
        {
            DialogueSequence d = step.dialogue;
            sb.AppendLine();
            sb.AppendLine("[" + PathOf(step.transform) + "]  (" + (d != null ? d.name : "대사 파일 없음") + ")");
            if (d == null) continue;
            used.Add(d);
            WriteLines(sb, d);
        }

        // 2) 씬의 DialogueStep에서 안 쓰는 대사 파일들
        sb.AppendLine();
        sb.AppendLine("===== DialogueStep에 연결 안 된 대사 파일 (오프닝 등 다른 곳에서 쓰거나 안 쓰는 것) =====");
        foreach (string guid in AssetDatabase.FindAssets("t:DialogueSequence"))
        {
            DialogueSequence d = AssetDatabase.LoadAssetAtPath<DialogueSequence>(AssetDatabase.GUIDToAssetPath(guid));
            if (d == null || used.Contains(d)) continue;
            sb.AppendLine();
            sb.AppendLine("(" + d.name + ")");
            WriteLines(sb, d);
        }

        string text = sb.ToString();
        EditorGUIUtility.systemCopyBuffer = text;
        File.WriteAllText(FileName, text, Encoding.UTF8);
        Debug.Log("대사 내보내기 완료: 클립보드에 복사됨 + 프로젝트 폴더의 " + FileName + " (DialogueStep " + list.Count + "개)");
    }

    // 대사 줄들을 "  - 대사" 형태로 (줄 안의 줄바꿈은 " / "로)
    static void WriteLines(StringBuilder sb, DialogueSequence d)
    {
        if (d.lines == null) return;
        foreach (string line in d.lines)
            sb.AppendLine("  - " + (line ?? "").Replace("\r", "").Replace("\n", " / "));
    }

    // Hierarchy 경로 (예: Room1/Desk/WinterFrame/Dlg_FramePhoto)
    static string PathOf(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}