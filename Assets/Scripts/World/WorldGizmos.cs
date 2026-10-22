using System.Collections.Generic;
using UnityEngine;

public static class WorldGizmos
{
    public static Color LayerColor(PlacementLayer layer)
    {
        switch (layer)
        {
            case PlacementLayer.Road: return Color.gray;
            case PlacementLayer.Sidewalk: return Color.white;
            case PlacementLayer.Lot: return Color.yellow;
            case PlacementLayer.Building: return Color.red;
            case PlacementLayer.Prop: return Color.cyan;
        }
        return Color.green;
    }

    public static void DrawOccupancy(WorldContext c)
    {
        if (c == null) return;
        Vector2[] corners = new Vector2[4];
        List<SpawnRecord> recs = c.occupancy.Records;
        for (int i = 0; i < recs.Count; i++)
        {
            SpawnRecord rec = recs[i];
            rec.box.Corners(corners);
            Gizmos.color = LayerColor(rec.layer);
            float y = c.HeightAt(rec.box.center.x, rec.box.center.y) + 0.3f;
            for (int k = 0; k < 4; k++)
            {
                Vector2 a = corners[k];
                Vector2 b = corners[(k + 1) % 4];
                Gizmos.DrawLine(new Vector3(a.x, y, a.y), new Vector3(b.x, y, b.y));
            }
        }
    }

    public static void DrawIssues(ClipReport report)
    {
        if (report == null) return;
        for (int i = 0; i < report.issues.Count; i++)
        {
            ClipIssue issue = report.issues[i];
            Gizmos.color = issue.warning ? new Color(1f, 0.6f, 0f) : Color.magenta;
            Gizmos.DrawWireSphere(issue.position, 2f);
            Gizmos.DrawLine(issue.position, issue.position + Vector3.up * 25f);
        }
    }
}

public static class WorldCombiner
{
    class Group
    {
        public PlacementLayer layer;
        public int cx;
        public int cz;
        public Material material;
        public List<CombineInstance> items = new List<CombineInstance>();
        public List<MeshRenderer> sources = new List<MeshRenderer>();
        public long verts;
    }

    public static void Run(WorldContext c, float chunkSize)
    {
        float chunk = Mathf.Max(16f, chunkSize);
        float half = c.Size * 0.5f;
        int merged = 0;
        int skipped = 0;
        int meshes = 0;

        foreach (PlacementLayer layer in System.Enum.GetValues(typeof(PlacementLayer)))
        {
            Transform lr = c.LayerRoot(layer);
            Dictionary<string, Group> groups = new Dictionary<string, Group>();
            MeshRenderer[] rs = lr.GetComponentsInChildren<MeshRenderer>(false);
            for (int i = 0; i < rs.Length; i++)
            {
                MeshRenderer r = rs[i];
                MeshFilter f = r.GetComponent<MeshFilter>();
                Material[] mats = r.sharedMaterials;
                if (f == null || f.sharedMesh == null || !f.sharedMesh.isReadable || !r.gameObject.isStatic || mats.Length != 1 || mats[0] == null || f.sharedMesh.subMeshCount != 1 || r.GetComponent<MeshCollider>() != null || r.GetComponentInParent<LODGroup>() != null)
                {
                    skipped++;
                    continue;
                }
                Vector3 bc = r.bounds.center;
                int cx = Mathf.FloorToInt((bc.x + half) / chunk);
                int cz = Mathf.FloorToInt((bc.z + half) / chunk);
                string key = cx + "|" + cz + "|" + mats[0].GetInstanceID();
                Group g;
                if (!groups.TryGetValue(key, out g))
                {
                    g = new Group();
                    g.layer = layer;
                    g.cx = cx;
                    g.cz = cz;
                    g.material = mats[0];
                    groups[key] = g;
                }
                CombineInstance ci = new CombineInstance();
                ci.mesh = f.sharedMesh;
                ci.transform = lr.worldToLocalMatrix * f.transform.localToWorldMatrix;
                g.items.Add(ci);
                g.sources.Add(r);
                g.verts += f.sharedMesh.vertexCount;
            }

            foreach (KeyValuePair<string, Group> kv in groups)
            {
                Group g = kv.Value;
                Mesh m = new Mesh();
                m.name = "Combined_" + g.layer + "_" + g.cx + "_" + g.cz;
                m.indexFormat = g.verts > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                m.CombineMeshes(g.items.ToArray(), true, true);
                m.RecalculateBounds();
                GameObject go = new GameObject(m.name);
                go.transform.SetParent(lr, false);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                go.AddComponent<MeshRenderer>().sharedMaterial = g.material;
                go.isStatic = true;
                meshes++;
                for (int i = 0; i < g.sources.Count; i++)
                {
                    MeshRenderer r = g.sources[i];
                    MeshFilter f = r.GetComponent<MeshFilter>();
                    WorldUtil.Kill(r);
                    WorldUtil.Kill(f);
                    merged++;
                }
            }
        }
        Debug.Log("Combine: " + merged + " renderers merged into " + meshes + " chunk meshes, " + skipped + " skipped (kept as static batching candidates)");
    }
}
