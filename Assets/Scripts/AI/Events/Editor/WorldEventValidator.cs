using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class WorldEventValidator
{
    const int Runs = 3;

    [MenuItem("World/Events/Validate Placement (Play Mode)")]
    public static void Validate()
    {
        NpcDirector d = NpcDirector.Instance;
        if (!Application.isPlaying || d == null || d.Ctx == null || d.Events == null)
        {
            Debug.LogWarning("Validate Placement: enter Play Mode and wait for the world to finish generating.");
            return;
        }

        List<WorldEventConfig> list = d.settings.events.library != null && d.settings.events.library.events.Count > 0
            ? d.settings.events.library.events
            : WorldEventDefaults.All;

        WorldRng rng = new WorldRng(d.Ctx.settings.seed, "events-validate");
        StringBuilder sb = new StringBuilder("World event placement report\n");
        int failed = 0;
        for (int i = 0; i < list.Count; i++)
        {
            WorldEventConfig c = list[i];
            if (c == null) continue;
            EditorUtility.DisplayProgressBar("Validating events", c.id, i / (float)list.Count);
            d.Events.Placement.Rejects.Clear();
            int ok = 0;
            for (int r = 0; r < Runs; r++)
            {
                Vector3 pos;
                float yaw;
                List<OrientedBox> boxes;
                if (d.Events.TryFindSpot(c, rng, false, out pos, out yaw, out boxes)) ok++;
            }
            string top = "";
            int best = 0;
            foreach (KeyValuePair<string, int> kv in d.Events.Placement.Rejects)
                if (kv.Value > best)
                {
                    best = kv.Value;
                    top = kv.Key;
                }
            if (ok == 0) failed++;
            sb.Append(ok == 0 ? "FAIL  " : "ok    ").Append(c.id).Append("  (").Append(c.zone).Append(")  ").Append(ok).Append("/").Append(Runs);
            if (top.Length > 0) sb.Append("  main reject: ").Append(top).Append(" x").Append(best);
            sb.Append("\n");
        }
        EditorUtility.ClearProgressBar();
        sb.Append(failed).Append(" of ").Append(list.Count).Append(" events found no valid spot.");
        Debug.Log(sb.ToString());
    }
}
