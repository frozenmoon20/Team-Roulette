using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(OptionState))]
public class OptionStateEditor : Editor
{
    public override void OnInspectorGUI()
    {
        OptionState state = (OptionState)target;

        EditorGUILayout.LabelField("옵션 상태 (Play 모드에서만 작동)", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Play 모드에서만 토글할 수 있습니다.", MessageType.Info);
            return;
        }

        foreach (OptionType type in System.Enum.GetValues(typeof(OptionType)))
        {
            bool current = state.IsUnlocked(type);
            bool toggled = EditorGUILayout.Toggle(type.ToString(), current);

            if (toggled != current)
            {
                if (toggled) state.Unlock(type);
                else state.Lock(type);
            }
        }
    }
}