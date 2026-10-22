using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class SoundCatalogBuilder : AssetPostprocessor
{
    const string Root = "Assets/Audio";
    const string OutDir = "Assets/Resources";
    const string OutPath = "Assets/Resources/SoundCatalog.asset";
    static readonly string[] Skip = { "guitar" };
    static bool queued;

    static bool Skipped(string path)
    {
        string rel = path.Substring(Root.Length + 1);
        string top = rel.Split('/')[0].ToLowerInvariant();
        for (int i = 0; i < Skip.Length; i++)
            if (top == Skip[i]) return true;
        return false;
    }

    static bool Under(string path)
    {
        return path.StartsWith(Root + "/") && !Skipped(path);
    }

    static bool Touches(string[] paths)
    {
        for (int i = 0; i < paths.Length; i++)
            if (Under(paths[i])) return true;
        return false;
    }

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (queued) return;
        if (!Touches(imported) && !Touches(deleted) && !Touches(moved) && !Touches(movedFrom)) return;
        queued = true;
        EditorApplication.delayCall += () =>
        {
            queued = false;
            Rebuild();
        };
    }

    static string[] Tags(string file)
    {
        string name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
        List<string> tags = new List<string>();
        string[] parts = Regex.Split(name, "[^a-z0-9]+");
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0 || Regex.IsMatch(parts[i], "^[0-9]+$")) continue;
            if (!tags.Contains(parts[i])) tags.Add(parts[i]);
        }
        string joined = Regex.Replace(name, "[^a-z0-9]+", "_").Trim('_');
        if (joined.Length > 0 && !tags.Contains(joined)) tags.Add(joined);
        return tags.ToArray();
    }

    [MenuItem("Tools/Audio/Rebuild Sound Catalog")]
    public static void Rebuild()
    {
        if (!AssetDatabase.IsValidFolder(Root)) return;
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { Root });
        List<string> paths = new List<string>();
        for (int i = 0; i < guids.Length; i++)
        {
            string p = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (Under(p)) paths.Add(p);
        }
        paths.Sort(System.StringComparer.Ordinal);

        List<SoundEntry> list = new List<SoundEntry>();
        for (int i = 0; i < paths.Count; i++)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(paths[i]);
            if (clip == null) continue;
            string[] parts = paths[i].Substring(Root.Length + 1).Split('/');
            SoundEntry e = new SoundEntry();
            e.clip = clip;
            e.category = parts[0].ToLowerInvariant();
            e.group = parts.Length > 2 ? parts[1].ToLowerInvariant() : "";
            e.tags = Tags(paths[i]);
            list.Add(e);
        }

        if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets", "Resources");
        SoundCatalog cat = AssetDatabase.LoadAssetAtPath<SoundCatalog>(OutPath);
        if (cat == null)
        {
            cat = ScriptableObject.CreateInstance<SoundCatalog>();
            AssetDatabase.CreateAsset(cat, OutPath);
        }
        cat.entries = list.ToArray();
        EditorUtility.SetDirty(cat);
        AssetDatabase.SaveAssets();
        Debug.Log("SoundCatalog rebuilt: " + list.Count + " clips");
    }

    static void ApplyImport(AudioImporter imp, string path)
    {
        string cat = path.Substring(Root.Length + 1).Split('/')[0].ToLowerInvariant();
        AudioImporterSampleSettings s = imp.defaultSampleSettings;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = 0.6f;
        s.preloadAudioData = false;
        if (cat == "ambient" || cat == "menu") s.loadType = AudioClipLoadType.Streaming;
        else if (cat == "cars") s.loadType = AudioClipLoadType.DecompressOnLoad;
        else s.loadType = AudioClipLoadType.CompressedInMemory;
        imp.defaultSampleSettings = s;
        imp.forceToMono = cat != "ambient" && cat != "menu";
        imp.loadInBackground = true;
    }

    void OnPreprocessAudio()
    {
        if (!Under(assetPath) || !assetImporter.importSettingsMissing) return;
        ApplyImport((AudioImporter)assetImporter, assetPath);
    }

    [MenuItem("Tools/Audio/Apply Import Settings")]
    public static void ApplyAll()
    {
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { Root });
        for (int i = 0; i < guids.Length; i++)
        {
            string p = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!Under(p)) continue;
            AudioImporter imp = AssetImporter.GetAtPath(p) as AudioImporter;
            if (imp == null) continue;
            ApplyImport(imp, p);
            imp.SaveAndReimport();
        }
    }
}
