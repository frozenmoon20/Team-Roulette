using UnityEditor;
using UnityEngine;

// Shared by photo setup and the existing material migration.
public static class BoardPhotoPaper
{
    public static void Apply(Material material)
    {
        material.shader = Shader.Find("Universal Render Pipeline/Lit");
        material.SetFloat("_WorkflowMode", 1);
        material.SetFloat("_Metallic", 0);
        material.SetFloat("_Smoothness", .05f);
        material.SetFloat("_SpecularHighlights", 0);
        material.SetFloat("_EnvironmentReflections", 0);
        material.SetFloat("_ReceiveShadows", 1);
        material.SetFloat("_ClearCoatMask", 0);
        material.SetFloat("_AlphaToMask", 0);
        material.SetColor("_EmissionColor", Color.black);
        material.DisableKeyword("_EMISSION");
        material.DisableKeyword("_SPECULAR_SETUP");
        material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        material.DisableKeyword("_RECEIVE_SHADOWS_OFF");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        EditorUtility.SetDirty(material);
    }
}
