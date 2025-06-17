// Assets/Editor/DisableProjectValidation.cs
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class DisableProjectValidation
{
    static DisableProjectValidation()
    {
        EditorPrefs.SetBool("Meta.XR.ProjectValidation.PromptOnReload", false);
        Debug.Log("🟢 Project Validation prompt disattivato.");
    }
}
