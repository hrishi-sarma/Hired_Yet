using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Fixes "the world looks flat / white / brown in the build but fine in the editor".
///
/// The world generator creates its materials at RUNTIME (Shader.Find + EnableKeyword("_NORMALMAP") ...) and its terrain at runtime.
/// A player build only contains
///   (a) shaders that some asset references or that are in Graphics Settings > Always Included Shaders, and
///   (b) the keyword variants that some material stored in the project uses.
/// Runtime-only shaders (terrain shaders, custom outline / sky shaders) and runtime-only keyword combinations are stripped,
/// so the generated materials render with a missing shader or the plain fallback variant.
///
/// This script (runs on every script reload and right before every build; also Tools > Build > Prepare Shaders For Build):
///   1. adds every shader the code looks up by name to Always Included Shaders;
///   2. creates small "keeper" materials in Assets/Resources/ShaderKeepers that enable the keyword combinations the generator uses,
///      so those variants survive stripping.
/// </summary>
public class BuildShaderSetup : IPreprocessBuildWithReport
{
    public int callbackOrder { get { return -1000; } }
    public void OnPreprocessBuild(BuildReport report) { Run(false); }

    [MenuItem("Tools/Build/Prepare Shaders For Build")]
    static void MenuRun() { Run(true); }

    [InitializeOnLoadMethod]
    static void OnLoad()
    {
        EditorApplication.delayCall += () => { if (!EditorApplication.isPlayingOrWillChangePlaymode) Run(false); };
    }

    // every shader looked up by name in the code base (Shader.Find) + the terrain shaders Unity needs for a RUNTIME-created terrain
    static readonly string[] AlwaysInclude =
    {
        "Universal Render Pipeline/Lit",
        "Universal Render Pipeline/Simple Lit",
        "Universal Render Pipeline/Unlit",
        "Universal Render Pipeline/Particles/Unlit",
        "Universal Render Pipeline/Particles/Lit",
        "Universal Render Pipeline/Particles/Simple Lit",
        "Universal Render Pipeline/Terrain/Lit",
        "Hidden/Universal Render Pipeline/Terrain/Lit (Add Pass)",
        "Hidden/Universal Render Pipeline/Terrain/Lit (Base Pass)",
        "Hidden/TerrainEngine/BillboardTree",
        "Hidden/TerrainEngine/CameraFacingBillboardTree",
        "Hidden/TerrainEngine/Details/Vertexlit",
        "Hidden/TerrainEngine/Details/WavingDoublePass",
        "Hidden/TerrainEngine/Details/BillboardWavingDoublePass",
        "Hidden/TerrainEngine/SplatmapDiffuse",
        "Hidden/TerrainEngine/HeightBlend",
        "Hidden/TerrainEngine/Splatmap/Standard-AddPass",
        "Sprites/Default",
        "Unlit/Transparent",
        "Unlit/Color",
        "Unlit/Texture",
        "Particles/Standard Unlit",
        "Legacy Shaders/Particles/Alpha Blended",
        "Legacy Shaders/Particles/Additive",
        "Mobile/Particles/Additive",
        "Particles/Additive",
        // NOTE: "Hidden/Internal-Colored" must NOT be listed: Unity's shader stripper crashes (native crash in
        // StripPasses) when this built-in shader is in Always Included Shaders. Only used by TrafficDebug (dev lines).
        "UI/Default",
        // project shaders (found by name wherever they live in Assets)
        "Stylized/InvertedHullOutline",
        "Hidden/RGOutlineHull",
        "Custom/AnimeProceduralSkybox",
        "Skybox/Stylized HDRI Moving Clouds",
    };

    sealed class Keeper
    {
        public string shader; public string[] keywords;
        public Keeper(string s, params string[] k) { shader = s; keywords = k; }
    }

    // keyword combinations the generator switches on at runtime (see StylizedMaterialFactory, SurfaceDetailStage, PondStage, ...)
    static readonly Keeper[] Keepers =
    {
        new Keeper("Universal Render Pipeline/Lit"),
        new Keeper("Universal Render Pipeline/Lit", "_NORMALMAP", "_OCCLUSIONMAP"),
        new Keeper("Universal Render Pipeline/Lit", "_NORMALMAP"),
        new Keeper("Universal Render Pipeline/Lit", "_EMISSION"),
        new Keeper("Universal Render Pipeline/Lit", "_NORMALMAP", "_EMISSION"),
        new Keeper("Universal Render Pipeline/Lit", "_SPECULARHIGHLIGHTS_OFF", "_ENVIRONMENTREFLECTIONS_OFF"),
        new Keeper("Universal Render Pipeline/Lit", "_NORMALMAP", "_OCCLUSIONMAP", "_SPECULARHIGHLIGHTS_OFF", "_ENVIRONMENTREFLECTIONS_OFF"),
        new Keeper("Universal Render Pipeline/Lit", "_SPECULARHIGHLIGHTS_OFF", "_ENVIRONMENTREFLECTIONS_OFF", "_ALPHATEST_ON"),
        new Keeper("Universal Render Pipeline/Lit", "_SPECULARHIGHLIGHTS_OFF", "_ENVIRONMENTREFLECTIONS_OFF", "_SURFACE_TYPE_TRANSPARENT"),
        new Keeper("Universal Render Pipeline/Lit", "_SURFACE_TYPE_TRANSPARENT"),
        new Keeper("Universal Render Pipeline/Lit", "_ALPHATEST_ON"),
        new Keeper("Universal Render Pipeline/Simple Lit"),
        new Keeper("Universal Render Pipeline/Simple Lit", "_NORMALMAP"),
        new Keeper("Universal Render Pipeline/Simple Lit", "_EMISSION"),
        new Keeper("Universal Render Pipeline/Unlit"),
        new Keeper("Universal Render Pipeline/Unlit", "_SURFACE_TYPE_TRANSPARENT"),
        new Keeper("Universal Render Pipeline/Unlit", "_ALPHATEST_ON"),
        new Keeper("Universal Render Pipeline/Particles/Unlit"),
        new Keeper("Universal Render Pipeline/Particles/Unlit", "_SURFACE_TYPE_TRANSPARENT"),
        // runtime-created terrain (TerrainApplyStage): layers have diffuse + normal maps, no mask map
        new Keeper("Universal Render Pipeline/Terrain/Lit"),
        new Keeper("Universal Render Pipeline/Terrain/Lit", "_NORMALMAP"),
        new Keeper("Universal Render Pipeline/Terrain/Lit", "_NORMALMAP", "_MASKMAP"),
        new Keeper("Universal Render Pipeline/Terrain/Lit", "_NORMALMAP", "_TERRAIN_BLEND_HEIGHT"),
        new Keeper("Hidden/Universal Render Pipeline/Terrain/Lit (Base Pass)"),
        new Keeper("Hidden/Universal Render Pipeline/Terrain/Lit (Base Pass)", "_NORMALMAP"),
        new Keeper("Hidden/Universal Render Pipeline/Terrain/Lit (Add Pass)"),
        new Keeper("Hidden/Universal Render Pipeline/Terrain/Lit (Add Pass)", "_NORMALMAP"),
    };

    const string KeeperFolder = "Assets/Resources/ShaderKeepers";

    static void Run(bool dialog)
    {
        var missing = new List<string>();
        int added = EnsureAlwaysIncluded(missing);
        int made = EnsureKeepers(missing);
        int box = EnsureBoxProjection();
        string msg = "BuildShaderSetup: " + added + " shader(s) added to Always Included Shaders, " + made + " variant-keeper material(s) created."
            + (missing.Count > 0 ? "\nNot found in this project (skipped, usually harmless): " + string.Join(", ", missing) : "");
        if (box > 0) msg += "\nURP asset(s): Reflection Probe Box Projection / Blending switched on (used by the runtime rain-puddle probe): " + box;
        if (added > 0 || made > 0 || box > 0 || dialog) Debug.Log(msg);
        if (dialog) EditorUtility.DisplayDialog("Prepare Shaders For Build", msg, "OK");
    }

    // Rain puddles create a Custom ReflectionProbe with box projection at runtime. URP strips the box-projection shader variants at build
    // time when the asset has the option off, so the asset flags are switched on here (runtime code alone only works in the Editor).
    static int EnsureBoxProjection()
    {
        int changed = 0;
        var seen = new HashSet<Object>();
        var list = new List<Object>();
        list.Add(UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline);
        list.Add(QualitySettings.renderPipeline);
        for (int q = 0; q < QualitySettings.names.Length; q++) list.Add(QualitySettings.GetRenderPipelineAssetAt(q));
        for (int i = 0; i < list.Count; i++)
        {
            Object a = list[i];
            if (a == null || !seen.Add(a)) continue;
            var so = new SerializedObject(a);
            bool dirty = false;
            SerializedProperty box = so.FindProperty("m_ReflectionProbeBoxProjection");
            SerializedProperty blend = so.FindProperty("m_ReflectionProbeBlending");
            if (box != null && !box.boolValue) { box.boolValue = true; dirty = true; }
            if (blend != null && !blend.boolValue) { blend.boolValue = true; dirty = true; }
            if (!dirty) continue;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(a);
            changed++;
        }
        if (changed > 0) AssetDatabase.SaveAssets();
        return changed;
    }

    static int EnsureAlwaysIncluded(List<string> missing)
    {
        Object target = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
        if (target == null)
        {
            Object[] all = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (all != null && all.Length > 0) target = all[0];
        }
        if (target == null) { Debug.LogWarning("BuildShaderSetup: could not open GraphicsSettings, add the shaders manually."); return 0; }

        var so = new SerializedObject(target);
        SerializedProperty arr = so.FindProperty("m_AlwaysIncludedShaders");
        if (arr == null) { Debug.LogWarning("BuildShaderSetup: Always Included Shaders list not found."); return 0; }

        var have = new HashSet<Shader>();
        for (int i = 0; i < arr.arraySize; i++)
        {
            Shader s = arr.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
            if (s != null) have.Add(s);
        }
        int added = 0;
        for (int i = 0; i < AlwaysInclude.Length; i++)
        {
            if (AlwaysInclude[i].StartsWith("Hidden/Internal-")) continue;   // built-in internal shaders crash the build stripper
            Shader sh = Shader.Find(AlwaysInclude[i]);
            if (sh == null) { missing.Add(AlwaysInclude[i]); continue; }
            if (have.Contains(sh)) continue;
            int n = arr.arraySize;
            arr.arraySize = n + 1;
            arr.GetArrayElementAtIndex(n).objectReferenceValue = sh;
            have.Add(sh);
            added++;
        }
        if (added > 0) { so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets(); }
        return added;
    }

    static int EnsureKeepers(List<string> missing)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(KeeperFolder)) AssetDatabase.CreateFolder("Assets/Resources", "ShaderKeepers");

        int made = 0;
        for (int i = 0; i < Keepers.Length; i++)
        {
            Keeper k = Keepers[i];
            Shader sh = Shader.Find(k.shader);
            if (sh == null) { if (!missing.Contains(k.shader)) missing.Add(k.shader); continue; }

            string path = KeeperFolder + "/Keeper_" + i.ToString("00") + "_" + Safe(k.shader) + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null && existing.shader == sh && SameKeywords(existing, k.keywords)) continue;
            if (existing != null) AssetDatabase.DeleteAsset(path);

            var m = new Material(sh) { name = "Keeper_" + i.ToString("00") };
            for (int j = 0; j < k.keywords.Length; j++) m.EnableKeyword(k.keywords[j]);
            AssetDatabase.CreateAsset(m, path);
            made++;
        }
        if (made > 0) AssetDatabase.SaveAssets();
        return made;
    }

    static bool SameKeywords(Material m, string[] wanted)
    {
        for (int i = 0; i < wanted.Length; i++) if (!m.IsKeywordEnabled(wanted[i])) return false;
        return true;
    }

    static string Safe(string s)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        return sb.ToString();
    }
}
