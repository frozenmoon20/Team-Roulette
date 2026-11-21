using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(RoomLightingPanel))]
public sealed class RoomLightingPanelEditor : Editor
{
    static readonly MethodInfo GetRenderSettings = typeof(RenderSettings).GetMethod(
        "GetRenderSettings", BindingFlags.Static | BindingFlags.NonPublic);
    static readonly System.Type LightingEditorType = typeof(Editor).Assembly.GetType("UnityEditor.LightingEditor");
    Editor environmentEditor;

    public override void OnInspectorGUI()
    {
        var panel = (RoomLightingPanel)target;
        var scene = panel.gameObject.scene;
        if (!scene.IsValid()) return;
        if (scene != SceneManager.GetActiveScene())
        {
            EditorGUILayout.HelpBox("이 씬을 활성 씬으로 선택하면 조명 설정을 편집할 수 있습니다.", MessageType.Info);
            if (GUILayout.Button("이 씬을 활성 씬으로 설정")) SceneManager.SetActiveScene(scene);
            return;
        }

        if (panel.Page == RoomLightingPanel.SettingsPage.Environment)
        {
            EditorGUILayout.LabelField("환경 조명", EditorStyles.boldLabel);
            if (LightingEditorType != null && GetRenderSettings?.Invoke(null, null) is Object settings && settings)
            {
                CreateCachedEditor(settings, LightingEditorType, ref environmentEditor);
                environmentEditor.OnInspectorGUI();
                return;
            }
            EditorGUILayout.HelpBox("Lighting 창의 Environment 탭에서 이 씬의 환경광을 편집할 수 있습니다.", MessageType.Info);
            DrawLightingWindowButton();
            return;
        }

        if (!Lightmapping.TryGetLightingSettings(out var lightingSettings))
        {
            EditorGUILayout.HelpBox("Lighting 창의 Scene 탭에서 Lighting Settings를 할당하세요.", MessageType.Info);
            DrawLightingWindowButton();
            return;
        }

        EditorGUILayout.LabelField("조명 베이크", EditorStyles.boldLabel);
        using var properties = new SerializedObject(lightingSettings);
        EditorGUILayout.PropertyField(properties.FindProperty("m_EnableBakedLightmaps"), new GUIContent("베이크된 전역 조명"));
        EditorGUILayout.PropertyField(properties.FindProperty("m_IndirectOutputScale"), new GUIContent("간접광 강도"));
        EditorGUILayout.PropertyField(properties.FindProperty("m_PVRBounces"), new GUIContent("최대 빛 반사 횟수"));
        if (properties.ApplyModifiedProperties()) EditorSceneManager.MarkSceneDirty(scene);
        EditorGUILayout.HelpBox("베이크 설정입니다. 변경한 값을 반영하려면 Lighting 창에서 Generate Lighting을 실행하세요.", MessageType.Info);
        DrawLightingWindowButton();
    }

    static void DrawLightingWindowButton()
    {
        if (GUILayout.Button("조명 설정 창 열기"))
            EditorApplication.ExecuteMenuItem("Window/Rendering/Lighting");
    }

    void OnDisable()
    {
        if (environmentEditor) DestroyImmediate(environmentEditor);
    }
}
