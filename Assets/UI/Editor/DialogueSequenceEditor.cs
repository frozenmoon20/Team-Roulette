using UnityEditor;
using UnityEngine;

namespace TeamRoulette.UI.Editor
{
    [CustomEditor(typeof(DialogueSequence))]
    public class DialogueSequenceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var lines = serializedObject.FindProperty("lines");
            var colors = serializedObject.FindProperty("colorCodes");
            int count = Mathf.Max(0, EditorGUILayout.IntField("문장 수", lines.arraySize));
            lines.arraySize = count;
            while (colors.arraySize < count)
            {
                int i = colors.arraySize++;
                colors.GetArrayElementAtIndex(i).stringValue = "#FFFFFF";
            }
            colors.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                EditorGUILayout.LabelField("문장 " + (i + 1));
                var text = lines.GetArrayElementAtIndex(i);
                text.stringValue = EditorGUILayout.TextArea(text.stringValue, GUILayout.MinHeight(44));
                EditorGUILayout.PropertyField(colors.GetArrayElementAtIndex(i), new GUIContent("색상 코드"));
                EditorGUILayout.Space(6);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
