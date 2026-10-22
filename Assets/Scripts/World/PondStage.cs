using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[AddComponentMenu("Procedural World/Stages/Pond")]
public class PondStage : WorldStage
{
    public float radius = 15f;
    public float depth = 2.2f;
    public float minDistanceFromSuburb = 45f;
    public int reedClumps = 50;
    public int lilyPads = 12;

    public static bool HasPond;
    public static Vector3 Center;

    public override int Order { get { return StageOrder.Flatten + 50; } }
    public override string StageName { get { return "Pond"; } }

    static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / Mathf.Max(0.0001f, b - a));
        return t * t * (3f - 2f * t);
    }

    static bool Natural(WorldContext c, Vector2 p)
    {
        ZoneType z = c.zoneMap.ZoneAt(p);
        return z == ZoneType.Open || z == ZoneType.Forest;
    }

    public override void Run(WorldContext c)
    {
        HasPond = false;
        if (c.zoneMap == null) return;
        WorldRng rng = c.Rng("pond");
        float maxH = Mathf.Max(1f, c.settings.maxTerrainHeight);
        float half = c.Size * 0.5f - 90f;
        float R = radius;
        bool found = false;
        Vector2 site = Vector2.zero;
        float bestScore = float.MaxValue;
        for (int i = 0; i < 500; i++)
        {
            Vector2 p = new Vector2(rng.Range(-half, half), rng.Range(-half, half));
            if (!Natural(c, p)) continue;
            float h01 = c.HeightAt(p.x, p.y) / maxH;
            if (h01 < 0.02f || h01 > 0.3f) continue;
            float d = (p - c.zoneMap.suburbCenter).magnitude;
            if (d < c.zoneMap.suburbRadius + minDistanceFromSuburb) continue;
            if (c.roads != null && c.roads.InFootprint(p, R * 2.5f + 25f)) continue;
            bool ok = true;
            float hc = c.HeightAt(p.x, p.y);
            for (int k = 0; k < 8 && ok; k++)
            {
                float a = k * Mathf.PI * 0.25f;
                Vector2 q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R * 2.3f;
                if (!Natural(c, q) || Mathf.Abs(c.HeightAt(q.x, q.y) - hc) > 6f) ok = false;
            }
            if (!ok) continue;
            if (d < bestScore) { bestScore = d; site = p; found = true; }
        }
        if (!found) { Debug.LogWarning("Pond: no suitable flat natural site found."); return; }

        float level = c.HeightAt(site.x, site.y);
        for (int k = 0; k < 8; k++)
        {
            float a = k * Mathf.PI * 0.25f;
            level += c.HeightAt(site.x + Mathf.Cos(a) * R * 1.6f, site.y + Mathf.Sin(a) * R * 1.6f);
        }
        level /= 9f;
        c.Flatten(new Rect(site.x - R * 1.5f, site.y - R * 1.5f, R * 3f, R * 3f), level, R * 1.8f);

        float texel = c.TexelSize;
        float reach = R * 1.5f;
        int x0 = Mathf.Max(0, Mathf.FloorToInt((site.x - reach + c.Size * 0.5f) / texel));
        int x1 = Mathf.Min(c.Res - 1, Mathf.CeilToInt((site.x + reach + c.Size * 0.5f) / texel));
        int z0 = Mathf.Max(0, Mathf.FloorToInt((site.y - reach + c.Size * 0.5f) / texel));
        int z1 = Mathf.Min(c.Res - 1, Mathf.CeilToInt((site.y + reach + c.Size * 0.5f) / texel));
        for (int zi = z0; zi <= z1; zi++)
        for (int xi = x0; xi <= x1; xi++)
        {
            float wx = xi * texel - c.Size * 0.5f - site.x;
            float wz = zi * texel - c.Size * 0.5f - site.y;
            float ang = Mathf.Atan2(wz, wx);
            float rad = R * (0.85f + 0.3f * Mathf.PerlinNoise(Mathf.Cos(ang) * 1.3f + 9f, Mathf.Sin(ang) * 1.3f + 9f));
            float t = Mathf.Sqrt(wx * wx + wz * wz) / rad;
            if (t >= 1.25f) continue;
            float dd = depth * (1f - Smooth(0.3f, 1.25f, t));
            c.heights[zi, xi] = Mathf.Clamp01(c.heights[zi, xi] - dd / maxH);
        }

        float wl = level - 0.3f;
        int seg = 64;
        float[] shore = new float[seg];
        for (int i = 0; i < seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float r = 0.5f;
            while (r < R * 2f && c.HeightAt(site.x + dir.x * r, site.y + dir.y * r) < wl) r += 0.25f;
            shore[i] = Mathf.Max(2f, r - 0.1f);
        }
        float[] sm = new float[seg];
        for (int i = 0; i < seg; i++) sm[i] = (shore[(i + seg - 1) % seg] + shore[i] * 2f + shore[(i + 1) % seg]) * 0.25f;
        shore = sm;

        GameObject water = new GameObject("PondWater");
        water.transform.SetParent(c.LayerRoot(PlacementLayer.Prop), false);
        water.transform.position = Vector3.zero;
        water.transform.rotation = Quaternion.identity;
        BuildWater(water, site, wl, shore);
        BuildFoam(water.transform, site, wl, shore);
        Decorate(c, rng, site, wl, shore);
        c.occupancy.Add(OrientedBox.FromBase(new Vector3(site.x, level, site.y), new Vector3(R * 3f, 5f, R * 3f), 0f), PlacementLayer.Prop, water);
        HasPond = true;
        Center = new Vector3(site.x, wl, site.y);
        Debug.Log("Pond at " + site + " level " + wl.ToString("0.0") + ".");
    }

    void BuildWater(GameObject go, Vector2 site, float wl, float[] shore)
    {
        int seg = shore.Length;
        float[] rings = { 0f, 0.4f, 0.75f, 1.02f };
        List<Vector3> v = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> tris = new List<int>();
        v.Add(new Vector3(site.x, wl, site.y));
        uv.Add(new Vector2(site.x, site.y) * 0.08f);
        for (int r = 1; r < rings.Length; r++)
        for (int i = 0; i < seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            float rr = shore[i] * rings[r];
            Vector3 p = new Vector3(site.x + Mathf.Cos(a) * rr, wl, site.y + Mathf.Sin(a) * rr);
            v.Add(p);
            uv.Add(new Vector2(p.x, p.z) * 0.08f);
        }
        for (int i = 0; i < seg; i++)
        {
            int n = (i + 1) % seg;
            tris.Add(0); tris.Add(1 + n); tris.Add(1 + i);
        }
        for (int r = 1; r < rings.Length - 1; r++)
        for (int i = 0; i < seg; i++)
        {
            int n = (i + 1) % seg;
            int a0 = 1 + (r - 1) * seg + i, a1 = 1 + (r - 1) * seg + n, b0 = 1 + r * seg + i, b1 = 1 + r * seg + n;
            tris.Add(a0); tris.Add(b1); tris.Add(b0);
            tris.Add(a0); tris.Add(a1); tris.Add(b1);
        }
        Mesh mesh = new Mesh();
        mesh.name = "PondWaterMesh";
        mesh.SetVertices(v);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        Material m = SurfaceDetailStage.Mat("PondWater", Texture2D.whiteTexture, new Color(0.1f, 0.3f, 0.38f, 0.86f), false, 2980, false);
        Texture2D n2 = NormalMap();
        if (m.HasProperty("_BumpMap")) { m.SetTexture("_BumpMap", n2); m.EnableKeyword("_NORMALMAP"); }
        if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", 0.7f);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.95f);
        if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 1f);
        if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 1f);
        m.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
        m.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        mr.sharedMaterial = m;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        go.AddComponent<PondDriver>().material = m;
    }

    static Texture2D NormalMap()
    {
        int s = 128;
        float[] h = new float[s * s];
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float u = x / (float)s, v = y / (float)s;
            h[y * s + x] = DetailTex.Pn(u, v, 12, 1) * 0.6f + DetailTex.Pn(u, v, 13, 2) * 0.4f;
        }
        Color[] px = new Color[s * s];
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float dx = h[y * s + (x + 1) % s] - h[y * s + (x + s - 1) % s];
            float dy = h[((y + 1) % s) * s + x] - h[((y + s - 1) % s) * s + x];
            Vector3 n = new Vector3(-dx * 6f, -dy * 6f, 1f).normalized;
            px[y * s + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
        }
        Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, true, true);
        t.wrapMode = TextureWrapMode.Repeat;
        t.SetPixels(px);
        t.Apply(true, false);
        return t;
    }

    void BuildFoam(Transform parent, Vector2 site, float wl, float[] shore)
    {
        int seg = shore.Length;
        int w = 64;
        Color[] px = new Color[w * 4];
        for (int y = 0; y < 4; y++)
        for (int x = 0; x < w; x++)
        {
            float u = x / (w - 1f);
            float a = Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);
            a = a * a * 0.55f;
            px[y * w + x] = new Color(0.9f, 0.95f, 0.95f, a);
        }
        Texture2D tex = new Texture2D(w, 4, TextureFormat.RGBA32, true, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels(px);
        tex.Apply(true, false);
        List<Vector3> v = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> tris = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            int k = i % seg;
            float a = k / (float)seg * Mathf.PI * 2f;
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float r0 = shore[k] * 0.97f - 0.2f, r1 = shore[k] + 0.9f;
            v.Add(new Vector3(site.x + d.x * r0, wl + 0.03f, site.y + d.y * r0));
            v.Add(new Vector3(site.x + d.x * r1, wl + 0.03f, site.y + d.y * r1));
            uv.Add(new Vector2(0f, 0f));
            uv.Add(new Vector2(1f, 0f));
            if (i < seg)
            {
                int b = i * 2;
                tris.Add(b); tris.Add(b + 3); tris.Add(b + 1);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
            }
        }
        Mesh mesh = new Mesh();
        mesh.name = "PondFoam";
        mesh.SetVertices(v);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        GameObject go = new GameObject("PondShoreFoam");
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = SurfaceDetailStage.Mat("PondFoam", tex, Color.white, false, 2981, true);
        mr.shadowCastingMode = ShadowCastingMode.Off;
    }

    void Decorate(WorldContext c, WorldRng rng, Vector2 site, float wl, float[] shore)
    {
        int seg = shore.Length;
        DetailBatch batch = new DetailBatch();
        Material reed = SurfaceDetailStage.Mat("PondReeds", DetailTex.Tuft(), new Color(0.85f, 0.9f, 0.55f, 1f), true, 0, true);
        Material pad = SurfaceDetailStage.Mat("PondLily", DetailTex.Puddle(), new Color(0.2f, 0.45f, 0.18f, 1f), false, 2982, false);
        for (int i = 0; i < reedClumps; i++)
        {
            int k = rng.Range(0, seg);
            float a = k / (float)seg * Mathf.PI * 2f;
            float r = shore[k] + rng.Range(-0.5f, 1.4f);
            Vector3 p = new Vector3(site.x + Mathf.Cos(a) * r, 0f, site.y + Mathf.Sin(a) * r);
            p.y = Mathf.Max(c.HeightAt(p.x, p.z), wl - 0.2f);
            float h = rng.Range(0.9f, 1.7f), w = rng.Range(0.6f, 0.9f);
            float ang = rng.Range(0f, 180f) * Mathf.Deg2Rad;
            Vector2 d1 = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            batch.CrossQuad(reed, p, d1, w, h);
            batch.CrossQuad(reed, p, new Vector2(-d1.y, d1.x), w, h);
        }
        for (int i = 0; i < lilyPads; i++)
        {
            int k = rng.Range(0, seg);
            float a = k / (float)seg * Mathf.PI * 2f;
            float r = shore[k] * rng.Range(0.2f, 0.78f);
            float sz = rng.Range(0.5f, 0.95f);
            batch.Quad(pad, new Vector3(site.x + Mathf.Cos(a) * r, wl + 0.05f, site.y + Mathf.Sin(a) * r), new Vector2(Mathf.Cos(a * 3f), Mathf.Sin(a * 3f)), new Vector2(sz, sz), Vector2.one);
        }
        batch.Flush(c.LayerRoot(PlacementLayer.Prop), "Pond");
    }
}

public class PondDriver : MonoBehaviour
{
    public Material material;
    float next;

    void Update()
    {
        if (material == null) return;
        float t = Time.time;
        material.SetTextureOffset("_BaseMap", new Vector2(t * 0.012f, t * 0.007f));
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + 0.25f;
        float k = Mathf.Clamp01(RenderSettings.fogColor.grayscale * 1.5f + 0.1f);
        Color c = new Color(Mathf.Lerp(0.01f, 0.10f, k), Mathf.Lerp(0.02f, 0.30f, k), Mathf.Lerp(0.03f, 0.38f, k), 0.86f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", c);
    }
}
