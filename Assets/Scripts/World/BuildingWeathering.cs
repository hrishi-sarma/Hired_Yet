using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class BuildingWeathering
{
    static Material roofMat, wallMat;
    static readonly string[] skip = { "window", "door", "glass", "fence", "picket", "frame", "platform", "foundation", "weather", "contactshadow" };

    static void EnsureMaterials()
    {
        if (roofMat != null && wallMat != null) return;
        roofMat = SurfaceDetailStage.Mat("WeatherRoof", DetailTex.RoofWeather(), Color.white, false, 2995, false);
        wallMat = SurfaceDetailStage.Mat("WeatherWall", DetailTex.WallWeather(), Color.white, false, 2994, false);
    }

    static bool Skipped(string n)
    {
        n = n.ToLowerInvariant();
        for (int i = 0; i < skip.Length; i++) if (n.Contains(skip[i])) return true;
        return false;
    }

    static bool NameHasRoof(Transform t, Transform stop)
    {
        while (t != null && t != stop)
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("roof") || n.Contains("shingle") || n.Contains("gable")) return true;
            t = t.parent;
        }
        return false;
    }

    static bool NameHasOpening(Transform t, Transform stop)
    {
        while (t != null && t != stop)
        {
            if (Skipped(t.name)) return true;
            t = t.parent;
        }
        return false;
    }

    static bool SubIsOpening(Material[] mats, int s)
    {
        if (mats == null || mats.Length == 0) return false;
        Material sm = mats[Mathf.Min(s, mats.Length - 1)];
        if (sm == null) return false;
        string n = sm.name.ToLowerInvariant();
        return n.Contains("glass") || n.Contains("window") || n.Contains("trim") || n.Contains("door") || n.Contains("wood");
    }

    class Acc
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> t = new List<int>();
    }

    public bool Apply(GameObject building, Bounds bb, WorldRng rng, DetailBatch batch, bool house)
    {
        EnsureMaterials();
        bool any = false;
        float hs = rng.Range(-0.03f, 0.03f), ss = rng.Range(0.85f, 1.1f), vs = rng.Range(0.82f, 1.12f);
        float ox = rng.Range(0f, 1f), oz = rng.Range(0f, 1f);
        float hBuilding = Mathf.Max(3f, bb.size.y);
        Acc roof = new Acc();
        Acc wall = new Acc();
        Transform root = building.transform;
        MeshRenderer[] rs = building.GetComponentsInChildren<MeshRenderer>(true);
        int budget = 6000;
        List<Vector3[]> roofTris = new List<Vector3[]>();

        for (int r = 0; r < rs.Length; r++)
        {
            MeshRenderer mr = rs[r];
            if (Skipped(mr.gameObject.name)) continue;
            MeshFilter mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            Mesh m = mf.sharedMesh;
            Material[] mats = mr.sharedMaterials;
            bool chainRoof = NameHasRoof(mr.transform, root);
            bool chainOpening = NameHasOpening(mr.transform, root);
            bool[] roofSub = new bool[Mathf.Max(1, m.subMeshCount)];
            for (int s = 0; s < roofSub.Length; s++)
            {
                Material sm = mats != null && mats.Length > 0 ? mats[Mathf.Min(s, mats.Length - 1)] : null;
                roofSub[s] = chainRoof || (sm != null && sm.name.ToLowerInvariant().Contains("roof"));
                if (roofSub[s] && sm != null && mats != null && s < mats.Length && sm.HasProperty("_BaseColor"))
                {
                    Color h = sm.GetColor("_BaseColor");
                    float hh, sat, val;
                    Color.RGBToHSV(h, out hh, out sat, out val);
                    Color shifted = Color.HSVToRGB(Mathf.Repeat(hh + hs, 1f), Mathf.Clamp01(sat * ss), Mathf.Clamp01(val * vs));
                    MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                    mpb.SetColor("_BaseColor", shifted);
                    mr.SetPropertyBlock(mpb, s);
                    any = true;
                }
            }
            if (!m.isReadable || budget <= 0) continue;
            Vector3[] verts = m.vertices;
            Matrix4x4 l2w = mr.localToWorldMatrix;
            for (int s = 0; s < m.subMeshCount && budget > 0; s++)
            {
                if (chainOpening || SubIsOpening(mats, s)) continue;
                int[] tris = m.GetTriangles(s);
                bool isRoof = roofSub[Mathf.Min(s, roofSub.Length - 1)];
                for (int i = 0; i + 2 < tris.Length && budget > 0; i += 3)
                {
                    Vector3 a = l2w.MultiplyPoint3x4(verts[tris[i]]);
                    Vector3 b = l2w.MultiplyPoint3x4(verts[tris[i + 1]]);
                    Vector3 c = l2w.MultiplyPoint3x4(verts[tris[i + 2]]);
                    Vector3 cr = Vector3.Cross(b - a, c - a);
                    float area = cr.magnitude * 0.5f;
                    if (area < 0.05f) continue;
                    Vector3 n = cr.normalized;
                    if (isRoof && n.y >= 0.2f && roofTris.Count < 8000) roofTris.Add(new Vector3[] { a, b, c });
                    Acc acc;
                    if (isRoof) { if (n.y < 0.2f) continue; acc = roof; }
                    else { if (Mathf.Abs(n.y) > 0.2f) continue; acc = wall; }
                    Vector3[] p = { a + n * 0.02f, b + n * 0.02f, c + n * 0.02f };
                    int baseIndex = acc.v.Count;
                    for (int q = 0; q < 3; q++)
                    {
                        acc.v.Add(root.InverseTransformPoint(p[q]));
                        acc.n.Add(root.InverseTransformDirection(n));
                        if (isRoof) acc.uv.Add(new Vector2(p[q].x * 0.22f + ox, p[q].z * 0.22f + oz));
                        else
                        {
                            float u = (Mathf.Abs(n.x) > Mathf.Abs(n.z) ? p[q].z : p[q].x) * 0.3f + ox;
                            acc.uv.Add(new Vector2(u, Mathf.Clamp(((p[q].y - bb.min.y) / hBuilding), 0.001f, 0.999f)));
                        }
                        acc.t.Add(baseIndex + q);
                    }
                    budget--;
                }
            }
        }

        any |= Overlay(root, roof, roofMat, "OverlayRoof", rng.Range(0.55f, 1f));
        any |= Overlay(root, wall, wallMat, "OverlayWall", rng.Range(0.5f, 0.95f));

        if (house && rng.Chance(0.55f)) { if (Chimney(bb, rng, batch, roofTris)) any = true; }
        return any;
    }

    static bool Overlay(Transform root, Acc a, Material mat, string name, float strength)
    {
        if (a.v.Count < 3) return false;
        Mesh mesh = new Mesh();
        mesh.name = name;
        mesh.indexFormat = a.v.Count > 60000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(a.v);
        mesh.SetNormals(a.n);
        mesh.SetUVs(0, a.uv);
        mesh.SetTriangles(a.t, 0);
        mesh.RecalculateBounds();
        GameObject go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        mpb.SetColor("_BaseColor", new Color(1f, 1f, 1f, strength));
        mr.SetPropertyBlock(mpb);
        return true;
    }

    static bool RoofY(List<Vector3[]> tris, float x, float z, out float y)
    {
        y = float.MinValue;
        bool hit = false;
        for (int i = 0; i < tris.Count; i++)
        {
            Vector3 a = tris[i][0], b = tris[i][1], c = tris[i][2];
            float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(d) < 1e-5f) continue;
            float l1 = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d;
            float l2 = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d;
            float l3 = 1f - l1 - l2;
            if (l1 < 0f || l2 < 0f || l3 < 0f) continue;
            float yy = l1 * a.y + l2 * b.y + l3 * c.y;
            if (yy > y) { y = yy; hit = true; }
        }
        return hit;
    }

    static bool Chimney(Bounds bb, WorldRng rng, DetailBatch batch, List<Vector3[]> roofTris)
    {
        if (roofTris.Count == 0) return false;
        List<Vector3> pts = new List<Vector3>();
        for (int i = 0; i < 16; i++)
        {
            float x = bb.center.x + rng.Range(-0.3f, 0.3f) * bb.size.x;
            float z = bb.center.z + rng.Range(-0.3f, 0.3f) * bb.size.z;
            float y;
            if (RoofY(roofTris, x, z, out y)) pts.Add(new Vector3(x, y, z));
        }
        if (pts.Count == 0) return false;
        pts.Sort((p, q) => q.y.CompareTo(p.y));
        Vector3 top = pts[0];
        Color[] bricks = { new Color(0.55f, 0.27f, 0.2f), new Color(0.62f, 0.58f, 0.52f), new Color(0.36f, 0.33f, 0.32f) };
        Material brick = StylizedMaterialFactory.Get("house_concrete", bricks[rng.Range(0, bricks.Length)], 0);
        Material cap = StylizedMaterialFactory.Get("house_concrete", new Color(0.55f, 0.55f, 0.55f), 0);
        Material dark = StylizedMaterialFactory.Get("paintedMetal", new Color(0.1f, 0.1f, 0.11f), 0);
        Vector3 pos = new Vector3(top.x, top.y - 0.7f, top.z);
        batch.Box(brick, pos, 0f, new Vector3(0.55f, 1.9f, 0.55f));
        batch.Box(cap, pos + Vector3.up * 1.9f, 0f, new Vector3(0.72f, 0.1f, 0.72f));
        AmbientLifeController.Chimneys.Add(pos + Vector3.up * 2.05f);
        batch.Box(dark, pos + Vector3.up * 2.0f, 0f, new Vector3(0.3f, 0.12f, 0.3f));
        Material vent = StylizedMaterialFactory.Get("paintedMetal", new Color(0.5f, 0.52f, 0.55f), 0);
        int vents = rng.Range(1, 3), placed = 0;
        for (int i = 1; i < pts.Count && placed < vents; i++)
        {
            Vector3 p = pts[i];
            if ((new Vector2(p.x - top.x, p.z - top.z)).magnitude < 1.4f) continue;
            Vector3 vp = new Vector3(p.x, p.y - 0.25f, p.z);
            batch.Prism(vent, vp, 0.11f, 0.6f, 6);
            batch.Prism(vent, vp + Vector3.up * 0.6f, 0.19f, 0.05f, 6);
            placed++;
        }
        return true;
    }
}
