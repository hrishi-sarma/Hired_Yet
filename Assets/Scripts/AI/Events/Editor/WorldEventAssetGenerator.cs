using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class WorldEventAssetGenerator
{
    const string Folder = "Assets/WorldEvents";

    [MenuItem("World/Events/Create Default Event Assets")]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "WorldEvents");

        string libPath = Folder + "/WorldEventLibrary.asset";
        WorldEventLibrary lib = AssetDatabase.LoadAssetAtPath<WorldEventLibrary>(libPath);
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<WorldEventLibrary>();
            AssetDatabase.CreateAsset(lib, libPath);
        }

        List<WorldEventConfig> defaults = WorldEventDefaults.Build();
        int created = 0;
        for (int i = 0; i < defaults.Count; i++)
        {
            string path = Folder + "/" + defaults[i].id + ".asset";
            WorldEventConfig asset = AssetDatabase.LoadAssetAtPath<WorldEventConfig>(path);
            if (asset == null)
            {
                AssetDatabase.CreateAsset(defaults[i], path);
                asset = defaults[i];
                created++;
            }
            else Object.DestroyImmediate(defaults[i]);
            if (!lib.events.Contains(asset)) lib.events.Add(asset);
        }

        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        Selection.activeObject = lib;
        Debug.Log("World events: " + created + " new assets created, library has " + lib.events.Count + " entries. Assign '" + libPath + "' to AiSettings > Events > Library.");
    }
}
