using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// (에디터 전용 도구) Project 창에서 고른 폴더(또는 FBX) 안의 애니메이션 이름을 전부 모아 클립보드에 복사한다.
// 애니메이션이 많아서 아이콘으로는 이름이 잘려 보일 때, 한 번에 목록으로 보기 위한 용도. 게임 실행에는 영향 없음.
// 원리: FBX 안에 들어 있는 애니메이션(AnimationClip)까지 하나씩 꺼내서 "이름 (길이 초, 반복 여부)"로 적는다.
// 사용: Project 창에서 Anim 폴더(또는 FBX)를 클릭 → 위 메뉴 Tools > 애니메이션 이름 복사 → 채팅창에 붙여넣기
// 이 파일은 반드시 이름이 "Editor"인 폴더 안에 둬야 한다.
public static class AnimClipExport
{
    [MenuItem("Tools/애니메이션 이름 복사")]
    static void Export()
    {
        List<string> paths = new List<string>();
        foreach (Object o in Selection.objects)
        {
            string p = AssetDatabase.GetAssetPath(o);
            if (string.IsNullOrEmpty(p)) continue;
            if (AssetDatabase.IsValidFolder(p))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { p }))
                    paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
            else paths.Add(p);
        }

        HashSet<string> seen = new HashSet<string>();
        List<string> lines = new List<string>();
        foreach (string path in paths)
        {
            foreach (Object a in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                AnimationClip clip = a as AnimationClip;
                if (clip == null || clip.name.StartsWith("__preview__")) continue;
                if (!seen.Add(clip.name)) continue;
                lines.Add(clip.name + "  (" + clip.length.ToString("0.00") + "초" + (clip.isLooping ? ", 반복" : "") + ")");
            }
        }

        if (lines.Count == 0)
        {
            Debug.LogWarning("애니메이션 이름 복사: 고른 곳에서 애니메이션을 못 찾았어요. Project 창에서 Anim 폴더나 FBX를 먼저 클릭하세요.");
            return;
        }

        lines.Sort(System.StringComparer.Ordinal);
        StringBuilder sb = new StringBuilder();
        foreach (string line in lines) sb.AppendLine(line);
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log("애니메이션 이름 복사 완료: " + lines.Count + "개 (클립보드에 복사됨)");
    }
}