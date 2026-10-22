using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class DetailBatch
{
    class Bucket
    {
        public Material mat;
        public bool shadows;
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> t = new List<int>();
    }

    const float Cell = 96f;
    readonly Dictionary<string, Bucket> buckets = new Dictionary<string, Bucket>();

    Bucket Get(Material m, Vector3 p, bool shadows)
    {
        string key = m.GetInstanceID() + "|" + Mathf.FloorToInt(p.x / Cell) + "|" + Mathf.FloorToInt(p.z / Cell) + "|" + (shadows ? 1 : 0);
        Bucket b;
        if (!buckets.TryGetValue(key, out b))
        {
            b = new Bucket();
            b.mat = m;
            b.shadows = shadows;
            buckets[key] = b;
        }
        return b;
    }

    void Face(Bucket k, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
    {
        if (n == Vector3.zero) n = Vector3.Cross(b - a, c - a).normalized;
        int i = k.v.Count;
        k.v.Add(a); k.v.Add(b); k.v.Add(c); k.v.Add(d);
        k.n.Add(n); k.n.Add(n); k.n.Add(n); k.n.Add(n);
        k.uv.Add(ua); k.uv.Add(ub); k.uv.Add(uc); k.uv.Add(ud);
        k.t.Add(i); k.t.Add(i + 1); k.t.Add(i + 2);
        k.t.Add(i); k.t.Add(i + 2); k.t.Add(i + 3);
    }

    public void Quad(Material m, Vector3 center, Vector2 u, Vector2 size, Vector2 tile)
    {
        Vector2 U = u.sqrMagnitude < 0.0001f ? Vector2.right : u.normalized;
        Vector2 V = new Vector2(-U.y, U.x);
        float hx = size.x * 0.5f, hz = size.y * 0.5f;
        Vector3 p0 = new Vector3(center.x - U.x * hx - V.x * hz, center.y, center.z - U.y * hx - V.y * hz);
        Vector3 p1 = new Vector3(center.x + U.x * hx - V.x * hz, center.y, center.z + U.y * hx - V.y * hz);
        Vector3 p2 = new Vector3(center.x + U.x * hx + V.x * hz, center.y, center.z + U.y * hx + V.y * hz);
        Vector3 p3 = new Vector3(center.x - U.x * hx + V.x * hz, center.y, center.z - U.y * hx + V.y * hz);
        Face(Get(m, center, false), p0, p3, p2, p1, Vector3.up, new Vector2(0f, 0f), new Vector2(0f, tile.y), tile, new Vector2(tile.x, 0f));
    }

    public void CrossQuad(Material m, Vector3 baseCenter, Vector2 dir, float width, float height)
    {
        Vector2 D = dir.sqrMagnitude < 0.0001f ? Vector2.right : dir.normalized;
        Vector3 a = new Vector3(baseCenter.x - D.x * width * 0.5f, baseCenter.y, baseCenter.z - D.y * width * 0.5f);
        Vector3 b = new Vector3(baseCenter.x + D.x * width * 0.5f, baseCenter.y, baseCenter.z + D.y * width * 0.5f);
        Vector3 up = Vector3.up * height;
        Bucket k = Get(m, baseCenter, false);
        Face(k, a, a + up, b + up, b, Vector3.up, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));
        Face(k, b, b + up, a + up, a, Vector3.up, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f));
    }

    public void Box(Material m, Vector3 baseCenter, float yawDeg, Vector3 size, bool shadows = true)
    {
        float r = yawDeg * Mathf.Deg2Rad;
        Vector2 ax = new Vector2(Mathf.Cos(r), -Mathf.Sin(r)) * (size.x * 0.5f);
        Vector2 az = new Vector2(Mathf.Sin(r), Mathf.Cos(r)) * (size.z * 0.5f);
        Vector2[] q = { -ax - az, ax - az, ax + az, -ax + az };
        Vector3[] b = new Vector3[4];
        Vector3[] t = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            b[i] = new Vector3(baseCenter.x + q[i].x, baseCenter.y, baseCenter.z + q[i].y);
            t[i] = b[i] + Vector3.up * size.y;
        }
        Bucket k = Get(m, baseCenter, shadows);
        float uw = Mathf.Max(0.1f, size.x) * 0.5f, ud = Mathf.Max(0.1f, size.z) * 0.5f, uh = Mathf.Max(0.1f, size.y) * 0.5f;
        Vector2 z0 = Vector2.zero;
        Face(k, t[0], t[3], t[2], t[1], Vector3.zero, z0, new Vector2(0f, ud), new Vector2(uw, ud), new Vector2(uw, 0f));
        Face(k, b[0], b[1], b[2], b[3], Vector3.zero, z0, new Vector2(uw, 0f), new Vector2(uw, ud), new Vector2(0f, ud));
        Face(k, b[0], t[0], t[1], b[1], Vector3.zero, z0, new Vector2(0f, uh), new Vector2(uw, uh), new Vector2(uw, 0f));
        Face(k, b[1], t[1], t[2], b[2], Vector3.zero, z0, new Vector2(0f, uh), new Vector2(ud, uh), new Vector2(ud, 0f));
        Face(k, b[2], t[2], t[3], b[3], Vector3.zero, z0, new Vector2(0f, uh), new Vector2(uw, uh), new Vector2(uw, 0f));
        Face(k, b[3], t[3], t[0], b[0], Vector3.zero, z0, new Vector2(0f, uh), new Vector2(ud, uh), new Vector2(ud, 0f));
    }

    public void Prism(Material m, Vector3 baseCenter, float radius, float height, int sides = 8, bool shadows = true)
    {
        Bucket k = Get(m, baseCenter, shadows);
        Vector3 top = baseCenter + Vector3.up * height;
        for (int i = 0; i < sides; i++)
        {
            float a0 = i / (float)sides * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
            Vector3 o0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
            Vector3 o1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
            float u0 = i / (float)sides, u1 = (i + 1) / (float)sides;
            Vector3 mid = (o0 + o1).normalized;
            Face(k, baseCenter + o0, top + o0, top + o1, baseCenter + o1, mid, new Vector2(u0, 0f), new Vector2(u0, 1f), new Vector2(u1, 1f), new Vector2(u1, 0f));
            Face(k, top, top + o1, top + o0, top, Vector3.up, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f));
        }
    }

    public Mesh ToMesh(string name)
    {
        List<Vector3> v = new List<Vector3>();
        List<Vector3> n = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> t = new List<int>();
        foreach (KeyValuePair<string, Bucket> kv in buckets)
        {
            int off = v.Count;
            v.AddRange(kv.Value.v);
            n.AddRange(kv.Value.n);
            uv.AddRange(kv.Value.uv);
            for (int i = 0; i < kv.Value.t.Count; i++) t.Add(kv.Value.t[i] + off);
        }
        Mesh mesh = new Mesh();
        mesh.name = name;
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    public int Flush(Transform parent, string prefix, bool collide = false)
    {
        int count = 0;
        foreach (KeyValuePair<string, Bucket> kv in buckets)
        {
            Bucket b = kv.Value;
            if (b.v.Count == 0) continue;
            Mesh mesh = new Mesh();
            mesh.name = prefix + "_" + b.mat.name;
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(b.v);
            mesh.SetNormals(b.n);
            mesh.SetUVs(0, b.uv);
            mesh.SetTriangles(b.t, 0);
            mesh.RecalculateBounds();
            GameObject go = new GameObject(prefix + "_" + b.mat.name);
            go.transform.SetParent(parent, false);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = b.mat;
            mr.shadowCastingMode = b.shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = true;
            if (collide) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            count++;
        }
        buckets.Clear();
        return count;
    }
}
