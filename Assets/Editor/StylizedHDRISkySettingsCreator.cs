#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

public static class StylizedHDRISkySettingsCreator
{
    [MenuItem("Assets/Create/World/Stylized HDRI Sky Settings (Resources)")]
    public static void CreateSettings()
    {
        const string resourcesPath = "Assets/Resources";
        if (!AssetDatabase.IsValidFolder(resourcesPath))
        {
            Directory.CreateDirectory(resourcesPath);
            AssetDatabase.Refresh();
        }

        string path = AssetDatabase.GenerateUniqueAssetPath(resourcesPath + "/StylizedHDRISkySettings.asset");
        var asset = ScriptableObject.CreateInstance<StylizedHDRISkySettings>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        Debug.Log("Created HDRI sky settings at " + path + ". Assign your Day and Night HDRIs in the Inspector.");
    }
}
#endif
