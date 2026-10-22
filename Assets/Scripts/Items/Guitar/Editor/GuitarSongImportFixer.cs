using UnityEditor;
using UnityEngine;

namespace Items
{
    // Sets every clip in the song bank to Decompress On Load so the guitar can trim mp3 encoder padding and loop with no gap.
    public static class GuitarSongImportFixer
    {
        [MenuItem("Tools/Guitar/Fix Song Import Settings")]
        static void Fix()
        {
            var bank = Resources.Load<GuitarSongBank>(GuitarSongBank.ResourceName);
            if (bank == null) { Debug.LogWarning("[Guitar] No song bank found."); return; }
            int n = 0;
            foreach (var slot in bank.slots)
            {
                if (slot == null || slot.clip == null) continue;
                var imp = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(slot.clip)) as AudioImporter;
                if (imp == null) continue;
                var ss = imp.defaultSampleSettings;
                ss.loadType = AudioClipLoadType.DecompressOnLoad;
                imp.defaultSampleSettings = ss;
                imp.loadInBackground = false;
                imp.SaveAndReimport(); n++;
            }
            Debug.Log("[Guitar] Updated import settings on " + n + " clip(s).");
        }
    }
}
