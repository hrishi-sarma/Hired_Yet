using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Finds (and optionally removes) components whose script is missing, in every prefab under Assets and every scene enabled in Build Settings.
/// Missing scripts are ignored by the editor but can break serialization of scene data in a build.</summary>
public static class MissingScriptCleaner
{
    [MenuItem("Tools/Build/Report Missing Scripts")]
    static void Report() { Run(false); }

    [MenuItem("Tools/Build/Remove Missing Scripts (scenes + prefabs)")]
    static void Remove()
    {
        if (!EditorUtility.DisplayDialog("Remove Missing Scripts", "This edits and saves prefabs under Assets and all scenes in Build Settings. Back up / commit first. Continue?", "Remove", "Cancel")) return;
        Run(true);
    }

    static string PathOf(Transform t)
    {
        var sb = new StringBuilder(t.name);
        for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
        return sb.ToString();
    }

    static void Run(bool remove)
    {
        int found = 0, removed = 0;
        var log = new StringBuilder();

        // ---- prefabs
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    if (n == 0) continue;
                    found += n; log.AppendLine("[Prefab] " + path + " : " + PathOf(t) + " (" + n + ")");
                    if (remove) { removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject); changed = true; }
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            catch (System.Exception e) { Debug.LogWarning("Skipped prefab " + path + ": " + e.Message); }
            finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
        }

        // ---- scenes in Build Settings
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { Debug.Log("Cancelled."); return; }
        string original = SceneManager.GetActiveScene().path;
        foreach (var es in EditorBuildSettings.scenes)
        {
            if (!es.enabled) continue;
            var sc = EditorSceneManager.OpenScene(es.path, OpenSceneMode.Single);
            bool changed = false;
            foreach (var r in sc.GetRootGameObjects())
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                {
                    int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    if (n == 0) continue;
                    found += n; log.AppendLine("[Scene] " + es.path + " : " + PathOf(t) + " (" + n + ")");
                    if (remove) { removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject); changed = true; }
                }
            if (changed) { EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc); }
        }
        if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);

        if (remove) AssetDatabase.SaveAssets();
        Debug.Log("Missing scripts found: " + found + (remove ? ", removed: " + removed : "") + (found > 0 ? "\n" + log : ""));
    }
}
