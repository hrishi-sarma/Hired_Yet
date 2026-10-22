using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fixes "The file 'level0' is corrupted! [Position out of bounds!]" in builds.
///
/// The world generator builds thousands of Meshes / Materials / Textures in memory. When a scene is baked they have no asset file,
/// so Unity serializes ALL of that data inline into the .unity file. A baked town makes that file enormous and the player
/// can no longer read it back (crash at level 0).
///
/// This tool gives every such in-memory object a home inside .asset container files (Assets/BakedSceneAssets).
/// The objects stay the same instances, so every reference in the scene stays valid; the scene file just stores references.
/// Run automatically at the end of Bake World, and available for any scene (e.g. WorldTest1):
///   Tools > Start Menu > Move Generated Data Out Of Scene
/// </summary>
public static class SceneAssetPersister
{
    const string Folder = "Assets/BakedSceneAssets";
    const int PerContainer = 400;

    [MenuItem("Tools/Start Menu/Move Generated Data Out Of Scene")]
    static void MenuRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorUtility.DisplayDialog("Move Generated Data", "Stop Play mode first.", "OK"); return; }
        Scene scene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path)) { EditorUtility.DisplayDialog("Move Generated Data", "Save the scene first.", "OK"); return; }
        int n = Persist(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("Move Generated Data", "Moved " + n + " in-memory objects into '" + Folder + "' and saved the scene.\nScene file size: " + SceneSizeText(scene) + ".", "OK");
    }

    public static string SceneSizeText(Scene scene)
    {
        try { return (new FileInfo(scene.path).Length / (1024.0 * 1024.0)).ToString("0.0") + " MB"; } catch { return "unknown"; }
    }

    /// <summary>Returns the number of objects moved into asset files.</summary>
    public static int Persist(Scene scene)
    {
        var meshes = new List<Object>(); var mats = new List<Object>(); var texs = new List<Object>();
        var seen = new HashSet<int>(); var usedPaths = new HashSet<string>();

        void Take(Object o)
        {
            if (o == null || !seen.Add(o.GetInstanceID())) return;
            if (IsEngineOwned(o)) return;   // Unity's own white/black/grey textures etc. must never be touched
            if (EditorUtility.IsPersistent(o)) { string p = AssetDatabase.GetAssetPath(o); if (!string.IsNullOrEmpty(p)) usedPaths.Add(p); return; }
            if (o is Mesh) meshes.Add(o);
            else if (o is Material) { mats.Add(o); TakeMaterialTextures((Material)o, Take); }
            else if (o is RenderTexture) { /* cannot be a sub-asset */ }
            else if (o is Texture) texs.Add(o);
        }

        EditorUtility.DisplayProgressBar("Move generated data", "Scanning scene", 0f);
        try
        {
            var comps = Resources.FindObjectsOfTypeAll<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                Component c = comps[i];
                if (c == null || c is Transform || !c.gameObject.scene.IsValid() || c.gameObject.scene != scene) continue;
                if ((i & 255) == 0) EditorUtility.DisplayProgressBar("Move generated data", "Scanning scene", 0.4f * i / comps.Length);
                ScanComponent(c, Take);
            }
            if (RenderSettings.skybox != null) Take(RenderSettings.skybox);

            int total = meshes.Count + mats.Count + texs.Count;
            if (total > 0 && !AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "BakedSceneAssets");
            string prefix = Sanitize(scene.name) + "_" + System.DateTime.Now.ToString("yyMMdd_HHmmss");
            int done = 0;
            Save(meshes, "Meshes", prefix, usedPaths, ref done, total);
            Save(texs, "Textures", prefix, usedPaths, ref done, total);
            Save(mats, "Materials", prefix, usedPaths, ref done, total);
            AssetDatabase.SaveAssets();

            DeleteOrphans(Sanitize(scene.name) + "_", usedPaths);
            return total;
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    static void Save(List<Object> list, string kind, string prefix, HashSet<string> usedPaths, ref int done, int total)
    {
        for (int start = 0; start < list.Count; start += PerContainer)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + prefix + "_" + kind + "_" + (start / PerContainer) + ".asset");
            Object main = null;
            for (int i = start; i < Mathf.Min(list.Count, start + PerContainer); i++)
            {
                Object o = list[i];
                if (o == null) continue;
                if (string.IsNullOrEmpty(o.name)) o.name = kind.TrimEnd('s') + "_" + i;
                if (main == null) { AssetDatabase.CreateAsset(o, path); main = o; }
                else AssetDatabase.AddObjectToAsset(o, main);
                done++;
            }
            if (main != null) { EditorUtility.SetDirty(main); usedPaths.Add(path); }
            EditorUtility.DisplayProgressBar("Move generated data", kind + " " + done + " / " + total, 0.4f + 0.6f * done / Mathf.Max(1, total));
        }
    }

    static void DeleteOrphans(string scenePrefix, HashSet<string> usedPaths)
    {
        if (!AssetDatabase.IsValidFolder(Folder)) return;
        foreach (string guid in AssetDatabase.FindAssets("", new[] { Folder }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (!p.EndsWith(".asset") || !Path.GetFileName(p).StartsWith(scenePrefix)) continue;
            if (!usedPaths.Contains(p)) AssetDatabase.DeleteAsset(p);   // left over from an earlier bake of this scene
        }
    }

    static void ScanComponent(Component c, System.Action<Object> take)
    {
        // fast paths for the big spenders
        if (c is Renderer r) { var sm = r.sharedMaterials; for (int i = 0; i < sm.Length; i++) take(sm[i]); }
        if (c is MeshFilter mf) take(mf.sharedMesh);
        if (c is MeshCollider mc) take(mc.sharedMesh);
        if (c is SkinnedMeshRenderer smr) take(smr.sharedMesh);

        // generic: every other object reference on the component (e.g. MenuNightData.glowMaterials)
        if (c is MeshFilter || c is MeshRenderer || c is MeshCollider) return;
        try
        {
            var so = new SerializedObject(c);
            var it = so.GetIterator();
            bool enter = true;
            while (it.Next(enter))
            {
                enter = it.propertyType == SerializedPropertyType.Generic && !(it.isArray && it.arraySize > 2048);
                if (it.propertyType == SerializedPropertyType.ObjectReference) take(it.objectReferenceValue);
            }
        }
        catch { }
    }

    /// <summary>
    /// Objects Unity itself owns (Texture2D.whiteTexture, editor UI resources, internal materials ...) live in memory with hide flags.
    /// Moving one of those into an asset breaks the editor (blank UI, EditorGUIUtility.whiteTexture == null), so they are skipped.
    /// Generated world data has HideFlags.None and is the only thing moved.
    /// </summary>
    static bool IsEngineOwned(Object o)
    {
        if (o.hideFlags != HideFlags.None) return true;
        if (o == Texture2D.whiteTexture || o == Texture2D.blackTexture || o == Texture2D.grayTexture
            || o == Texture2D.linearGrayTexture || o == Texture2D.redTexture || o == Texture2D.normalTexture) return true;
        if (o == EditorGUIUtility.whiteTexture) return true;
        string n = o.name;
        return n == "UnityWhite" || n == "UnityBlack" || n == "UnityGray" || n == "UnityRed" || n == "UnityNormalTexture" || n == "UnityLinearGrey";
    }

    static void TakeMaterialTextures(Material m, System.Action<Object> take)
    {
        try { foreach (string n in m.GetTexturePropertyNames()) take(m.GetTexture(n)); } catch { }
    }

    static string Sanitize(string s)
    {
        foreach (char ch in Path.GetInvalidFileNameChars()) s = s.Replace(ch, '_');
        return s.Replace(' ', '_');
    }
}
