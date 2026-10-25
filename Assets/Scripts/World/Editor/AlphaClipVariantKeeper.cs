#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Grass tufts and pond reeds get their alpha-clip keyword at runtime (SurfaceDetailStage.Mat). No asset uses it, so builds strip the
/// _ALPHATEST_ON shader variant and the quads render as solid squares. This keeps real materials in Resources so the variants ship.
/// Runs automatically on editor load; also available as World > Rebuild Alpha Clip Materials.
/// </summary>
public static class AlphaClipVariantKeeper
{
    const string Dir = "Assets/Resources";

    [InitializeOnLoadMethod]
    static void Auto() { EditorApplication.delayCall += () => Build(false); }

    [MenuItem("World/Rebuild Alpha Clip Materials")]
    static void Manual() { Build(true); }

    static void Build(bool force)
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets", "Resources");
        Make("KeepAlive_AlphaClip", true, force);
        Make("KeepAlive_Transparent", false, force);
        AssetDatabase.SaveAssets();
    }

    static void Make(string name, bool clip, bool force)
    {
        string path = Dir + "/" + name + ".mat";
        if (!force && AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) return;
        Material m = new Material(sh);
        m.name = name;
        m.SetFloat("_Cull", 0f);
        m.SetFloat("_Smoothness", 0f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_SpecularHighlights", 0f);
        m.SetFloat("_EnvironmentReflections", 0f);
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        if (clip)
        {
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = 2450;
        }
        else
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", 5f);
            m.SetFloat("_DstBlend", 10f);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
        }
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
    }
}
#endif
