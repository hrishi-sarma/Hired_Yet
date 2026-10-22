#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Builds the loading-screen TMP font asset from Assets/CustomFonts/Caveat-Regular.ttf into
// Assets/Resources/LoadingFontCaveat.asset (which LoadingScreen loads). Runs automatically once if the asset is missing.
// Manual rebuild: Tools > Loading Screen > Rebuild Caveat Font.
[InitializeOnLoad]
public static class LoadingFontBuilder
{
    const string SourcePath = "Assets/CustomFonts/Caveat-Regular.ttf";
    const string OutDir = "Assets/Resources";
    const string OutPath = OutDir + "/LoadingFontCaveat.asset";

    static LoadingFontBuilder()
    {
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutPath) == null && File.Exists(SourcePath)) Build();
        };
    }

    [MenuItem("Tools/Loading Screen/Rebuild Caveat Font")]
    public static void Build()
    {
        Font src = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
        if (src == null) { Debug.LogError("LoadingFontBuilder: font not found at " + SourcePath); return; }
        if (!Directory.Exists(OutDir)) { Directory.CreateDirectory(OutDir); AssetDatabase.Refresh(); }
        if (AssetDatabase.LoadAssetAtPath<Object>(OutPath) != null) AssetDatabase.DeleteAsset(OutPath);

        TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(src, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        fa.name = "LoadingFontCaveat";
        AssetDatabase.CreateAsset(fa, OutPath);
        if (fa.material != null) { fa.material.name = "LoadingFontCaveat Material"; AssetDatabase.AddObjectToAsset(fa.material, fa); }
        if (fa.atlasTextures != null)
            for (int i = 0; i < fa.atlasTextures.Length; i++)
            {
                fa.atlasTextures[i].name = "LoadingFontCaveat Atlas " + i;
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[i], fa);
            }
        // pre-bake everything the loading screen can show; any other character is added on demand at runtime
        string chars = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
        fa.TryAddCharacters(chars, out string missing);
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        Debug.Log("LoadingFontBuilder: created " + OutPath);
    }
}
#endif
