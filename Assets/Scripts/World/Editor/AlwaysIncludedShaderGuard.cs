using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Removes shaders from Project Settings > Graphics > Always Included Shaders that crash Unity's
/// shader stripper during a player build (Editor.log: "Compiling shader Hidden/Internal-Colored"
/// followed by a native crash in ShaderCompilation::TransientData::StripPasses).
/// Runs last before every build (callbackOrder 10000), after BuildShaderSetup, and on script reload.
/// Manual run: Tools > Build > Remove Crashing Always-Included Shaders.
/// </summary>
[InitializeOnLoad]
public class AlwaysIncludedShaderGuard : IPreprocessBuildWithReport
{
    // Built-in shaders that live in Unity's internal resources and must NOT be force-included.
    static readonly string[] BadPrefixes = { "Hidden/Internal-" };

    public int callbackOrder => 10000;

    static AlwaysIncludedShaderGuard()
    {
        EditorApplication.delayCall += () => Clean(false);
    }

    public void OnPreprocessBuild(BuildReport report) { Clean(true); }

    [MenuItem("Tools/Build/Remove Crashing Always-Included Shaders")]
    static void Manual() { Clean(true); }

    static bool IsBad(Shader s)
    {
        if (s == null) return true;   // missing entries
        for (int i = 0; i < BadPrefixes.Length; i++)
            if (s.name.StartsWith(BadPrefixes[i])) return true;
        return false;
    }

    static void Clean(bool log)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        if (assets == null || assets.Length == 0) return;
        var so = new SerializedObject(assets[0]);
        var list = so.FindProperty("m_AlwaysIncludedShaders");
        if (list == null) return;

        var removed = new List<string>();
        for (int i = list.arraySize - 1; i >= 0; i--)
        {
            var el = list.GetArrayElementAtIndex(i);
            var sh = el.objectReferenceValue as Shader;
            if (!IsBad(sh)) continue;
            removed.Add(sh != null ? sh.name : "(missing)");
            el.objectReferenceValue = null;          // clear the slot first,
            list.DeleteArrayElementAtIndex(i);       // then delete it (Unity quirk with object arrays)
        }
        if (removed.Count == 0) { if (log) Debug.Log("[AlwaysIncludedShaderGuard] Nothing to remove."); return; }
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.LogWarning("[AlwaysIncludedShaderGuard] Removed from Always Included Shaders: " + string.Join(", ", removed));
    }
}
