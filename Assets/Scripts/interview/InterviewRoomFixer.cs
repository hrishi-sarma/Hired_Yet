using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Safety net for the procedurally generated interview room: makes sure every desk lamp stands ON the desk top
/// (not floating beside it). Run once when an interview begins: InterviewRoomFixer.Run(position).
/// Finds the desk and lamps by name ("desk"/"table" and "lamp"), and if a lamp is off the desk it is moved to a free
/// spot on the desk surface (away from the laptop, mug, pen holder...). Its own light moves with it.
/// </summary>
public static class InterviewRoomFixer
{
    static readonly string[] NotDesk = { "lamp", "chair", "laptop", "mug", "cup", "holder", "book", "paper", "plant", "pen" };

    static bool NameHas(string n, string key) { return n.ToLowerInvariant().Contains(key); }

    static bool IsDeskPart(Transform t)
    {
        for (int d = 0; d < 4 && t != null; d++, t = t.parent)
        {
            string n = t.name.ToLowerInvariant();
            for (int i = 0; i < NotDesk.Length; i++) if (n.Contains(NotDesk[i])) return false;
            if (n.Contains("desk") || n.Contains("table")) return true;
        }
        return false;
    }

    static Transform LampRoot(Transform t)
    {
        Transform found = null;
        for (int d = 0; d < 5 && t != null; d++, t = t.parent)
        {
            if (t.name.ToLowerInvariant().Contains("lamp")) found = t;   // keep climbing while names still say lamp
            else if (found != null) break;
        }
        return found;
    }

    static bool Inside(Bounds b, Vector3 p, float inset)
    {
        return p.x > b.min.x + inset && p.x < b.max.x - inset && p.z > b.min.z + inset && p.z < b.max.z - inset;
    }

    public static void Run(Vector3 center, float radius = 14f)
    {
        Renderer[] all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        List<Renderer> near = new List<Renderer>();
        for (int i = 0; i < all.Length; i++)
        {
            Renderer r = all[i];
            if (r == null || r is ParticleSystemRenderer || !r.gameObject.activeInHierarchy) continue;
            if ((r.bounds.center - center).sqrMagnitude <= radius * radius) near.Add(r);
        }

        // --- desk top slabs: the highest, widest desk renderers (handles L-shaped desks) ---
        float deskTop = float.MinValue;
        List<Renderer> deskParts = new List<Renderer>();
        for (int i = 0; i < near.Count; i++)
            if (IsDeskPart(near[i].transform) && near[i].bounds.size.x * near[i].bounds.size.z > 0.2f)
            {
                deskParts.Add(near[i]);
                deskTop = Mathf.Max(deskTop, near[i].bounds.max.y);
            }
        if (deskParts.Count == 0) return;

        List<Bounds> slabs = new List<Bounds>();
        for (int i = 0; i < deskParts.Count; i++)
        {
            Bounds b = deskParts[i].bounds;
            if (b.max.y >= deskTop - 0.08f && b.size.x * b.size.z > 0.25f) slabs.Add(b);
        }
        if (slabs.Count == 0) return;

        // --- lamps ---
        Dictionary<Transform, Bounds> lamps = new Dictionary<Transform, Bounds>();
        for (int i = 0; i < near.Count; i++)
        {
            Transform root = LampRoot(near[i].transform);
            if (root == null) continue;
            Bounds b = near[i].bounds;
            if (lamps.ContainsKey(root)) { Bounds m = lamps[root]; m.Encapsulate(b); lamps[root] = m; }
            else lamps[root] = b;
        }

        // other things standing on the desk (to keep the lamp clear of them)
        List<Vector3> clutter = new List<Vector3>();
        for (int i = 0; i < near.Count; i++)
        {
            Bounds b = near[i].bounds;
            if (b.min.y < deskTop - 0.05f || b.min.y > deskTop + 0.3f) continue;
            if (IsDeskPart(near[i].transform) || LampRoot(near[i].transform) != null) continue;
            for (int s = 0; s < slabs.Count; s++)
                if (Inside(slabs[s], b.center, 0f)) { clutter.Add(b.center); break; }
        }

        foreach (KeyValuePair<Transform, Bounds> kv in lamps)
        {
            Transform root = kv.Key;
            Bounds lb = kv.Value;
            Vector3 foot = new Vector3(lb.center.x, lb.min.y, lb.center.z);
            bool xzOk = false;
            for (int s = 0; s < slabs.Count; s++) if (Inside(slabs[s], foot, 0f)) { xzOk = true; break; }
            if (xzOk && Mathf.Abs(lb.min.y - deskTop) < 0.08f) continue;                  // already on the desk

            float half = Mathf.Max(lb.extents.x, lb.extents.z);
            float inset = Mathf.Max(half + 0.05f, 0.12f);
            Vector3 best = Vector3.zero;
            float bestScore = float.MinValue;
            bool have = false;
            for (int s = 0; s < slabs.Count; s++)
            {
                Bounds sb = slabs[s];
                if (sb.size.x < inset * 2f || sb.size.z < inset * 2f) continue;
                for (int ix = 0; ix <= 4; ix++)
                    for (int iz = 0; iz <= 4; iz++)
                    {
                        Vector3 p = new Vector3(
                            Mathf.Lerp(sb.min.x + inset, sb.max.x - inset, ix / 4f), deskTop,
                            Mathf.Lerp(sb.min.z + inset, sb.max.z - inset, iz / 4f));
                        float score = float.MaxValue;
                        for (int c = 0; c < clutter.Count; c++)
                        {
                            Vector2 d = new Vector2(p.x - clutter[c].x, p.z - clutter[c].z);
                            score = Mathf.Min(score, d.magnitude);
                        }
                        float edge = Mathf.Min(Mathf.Min(p.x - sb.min.x, sb.max.x - p.x), Mathf.Min(p.z - sb.min.z, sb.max.z - p.z));
                        score = Mathf.Min(score, 1.5f) + Mathf.Min(edge, 0.4f) * 0.5f;      // clear of clutter, not hanging on the edge
                        if (score > bestScore) { bestScore = score; best = p; have = true; }
                    }
            }
            if (!have) continue;

            Vector3 delta = new Vector3(best.x - lb.center.x, deskTop - lb.min.y, best.z - lb.center.z);
            root.position += delta;

            // a lamp light that lives outside the lamp object moves with it
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light l = lights[i];
                if (l == null || l.type == LightType.Directional || l.transform.IsChildOf(root)) continue;
                if ((l.transform.position - lb.center).sqrMagnitude < 2.25f && NameHas(l.name, "lamp")) l.transform.position += delta;
            }
        }
    }
}
