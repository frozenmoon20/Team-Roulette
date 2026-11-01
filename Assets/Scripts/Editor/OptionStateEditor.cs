using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(OptionState))]
public class OptionStateEditor : Editor
{
    // These values live in a runtime dictionary, not SerializedProperties.
    public override bool RequiresConstantRepaint() => Application.isPlaying;

    public override void OnInspectorGUI()
    {
        OptionState state = (OptionState)target;
        EditorGUILayout.LabelField("옵션 상태 (Play 모드에서만 작동)", EditorStyles.boldLabel);
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Play 모드에서만 토글할 수 있습니다.", MessageType.Info);
            return;
        }
        if (state != OptionState.Instance)
        {
            EditorGUILayout.HelpBox("이 오브젝트는 현재 게임이 사용하는 OptionState가 아닙니다. 아래 활성 인스턴스를 선택하세요.", MessageType.Warning);
            EditorGUILayout.ObjectField("활성 OptionState", OptionState.Instance, typeof(OptionState), true);
            if (OptionState.Instance && GUILayout.Button("활성 OptionState 선택")) Selection.activeObject = OptionState.Instance;
            return;
        }
        EditorGUILayout.HelpBox("WashGame: Blur 체크 = 블러 켜짐. 세안 완료 시 체크가 해제됩니다.", MessageType.None);
        foreach (OptionType type in System.Enum.GetValues(typeof(OptionType)))
        {
            bool current = state.IsUnlocked(type);
            bool toggled = EditorGUILayout.Toggle(type.ToString(), current);
            if (toggled == current) continue;
            if (toggled) state.Unlock(type); else state.Lock(type);
        }
    }
}
