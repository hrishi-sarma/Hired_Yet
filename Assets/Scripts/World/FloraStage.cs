using System.Collections.Generic;
using UnityEngine;

public sealed class FloraMesh
{
    public readonly List<Vector3> v = new List<Vector3>();
    public readonly List<Vector3> n = new List<Vector3>();
    public readonly List<Vector2> uv = new List<Vector2>();
    public readonly List<int> t = new List<int>();
    public Matrix4x4 M = Matrix4x4.identity;
    Vector2 u0, u1;

    public void Cell(int index)
    {
        float s = 0.25f, inset = 0.014f;
        int col = index % 4, row = index / 4;
        u0 = new Vector2(col * s + inset, row * s + inset);
        u1 = new Vector2((col + 1) * s - inset, (row + 1) * s - inset);
    }

    Vector2 UV(float a, float b)
    {
        return new Vector2(Mathf.Lerp(u0.x, u1.x, a), Mathf.Lerp(u0.y, u1.y, b));
    }

    void Push(Vector3 p, Vector3 nr, Vector2 uvv)
    {
        v.Add(M.MultiplyPoint3x4(p));
        n.Add(M.MultiplyVector(nr).normalized);
        uv.Add(uvv);
    }

    public void Cylinder(Vector3 baseP, Quaternion rot, float r0, float r1, float h, int sides)
    {
        int k = v.Count;
        float slope = r0 - r1;
        for (int i = 0; i <= sides; i++)
        {
            float a = i / (float)sides * Mathf.PI * 2f;
            float cx = Mathf.Cos(a), sz = Mathf.Sin(a);
            Vector3 nr = rot * new Vector3(cx * h, slope, sz * h).normalized;
            Push(baseP + rot * new Vector3(cx * r0, 0f, sz * r0), nr, UV(i / (float)sides, 0f));
            Push(baseP + rot * new Vector3(cx * r1, h, sz * r1), nr, UV(i / (float)sides, 1f));
        }
        for (int i = 0; i < sides; i++)
        {
            int b0 = k + i * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
            t.Add(b0); t.Add(t0); t.Add(b1);
            if (r1 > 0.0001f) { t.Add(b1); t.Add(t0); t.Add(t1); }
        }
    }

    public void Ellipsoid(Vector3 center, Quaternion rot, Vector3 r, int lon, int lat)
    {
        int k = v.Count;
        for (int i = 0; i <= lat; i++)
        {
            float phi = i / (float)lat * Mathf.PI;
            float sp = Mathf.Sin(phi), cp = Mathf.Cos(phi);
            for (int j = 0; j <= lon; j++)
            {
                float th = j / (float)lon * Mathf.PI * 2f;
                Vector3 d = new Vector3(sp * Mathf.Cos(th), cp, sp * Mathf.Sin(th));
                Vector3 p = new Vector3(d.x * r.x, d.y * r.y, d.z * r.z);
                Vector3 nr = new Vector3(d.x / r.x, d.y / r.y, d.z / r.z);
                Push(center + rot * p, rot * nr, UV(j / (float)lon, 1f - i / (float)lat));
            }
        }
        int w = lon + 1;
        for (int i = 0; i < lat; i++)
        {
            for (int j = 0; j < lon; j++)
            {
                int p00 = k + i * w + j, p01 = p00 + 1, p10 = p00 + w, p11 = p10 + 1;
                if (i > 0) { t.Add(p00); t.Add(p01); t.Add(p10); }
                if (i < lat - 1) { t.Add(p01); t.Add(p11); t.Add(p10); }
            }
        }
    }

    public Mesh ToMesh(string name)
    {
        Mesh m = new Mesh();
        m.name = name;
        if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(v);
        m.SetNormals(n);
        m.SetUVs(0, uv);
        m.SetTriangles(t, 0);
        m.RecalculateBounds();
        return m;
    }
}

public static class FloraAtlas
{
    struct CellDef
    {
        public Color c;
        public int kind;
        public CellDef(Color c, int kind) { this.c = c; this.kind = kind; }
    }

    static readonly CellDef[] cells =
    {
        new CellDef(new Color(0.20f, 0.46f, 0.18f), 0),
        new CellDef(new Color(0.50f, 0.68f, 0.22f), 0),
        new CellDef(new Color(0.09f, 0.30f, 0.16f), 1),
        new CellDef(new Color(0.16f, 0.38f, 0.38f), 1),
        new CellDef(new Color(0.95f, 0.62f, 0.74f), 0),
        new CellDef(new Color(0.85f, 0.40f, 0.60f), 0),
        new CellDef(new Color(0.90f, 0.50f, 0.15f), 0),
        new CellDef(new Color(0.75f, 0.20f, 0.14f), 0),
        new CellDef(new Color(0.90f, 0.74f, 0.20f), 0),
        new CellDef(new Color(0.34f, 0.40f, 0.14f), 0),
        new CellDef(new Color(0.36f, 0.24f, 0.15f), 2),
        new CellDef(new Color(0.90f, 0.90f, 0.86f), 3),
        new CellDef(new Color(0.48f, 0.46f, 0.43f), 2),
        new CellDef(new Color(0.52f, 0.28f, 0.18f), 2),
        new CellDef(new Color(0.25f, 0.28f, 0.18f), 2),
        new CellDef(new Color(0.14f, 0.50f, 0.40f), 0)
    };

    static Material cached;

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7fffffff) / (float)0x7fffffff;
        }
    }

    static float Vn(float x, float y, int px, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        float sx = fx * fx * (3f - 2f * fx), sy = fy * fy * (3f - 2f * fy);
        int xa = ((x0 % px) + px) % px, xb = (((x0 + 1) % px) + px) % px;
        float a = Hash(xa, y0, seed), b = Hash(xb, y0, seed), c = Hash(xa, y0 + 1, seed), d = Hash(xb, y0 + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
    }

    static Color Clamp(Color c)
    {
        return new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);
    }

    static Color Pixel(CellDef d, int cell, int lx, int ly)
    {
        float u = lx / 128f, v = ly / 128f;
        int seed = cell * 17 + 3;
        Color b = d.c;
        if (d.kind <= 1)
        {
            float blot = Vn(u * 8f, v * 8f, 8, seed) * 0.65f + Vn(u * 20f, v * 20f, 20, seed + 7) * 0.35f;
            float shade = Mathf.Lerp(0.55f, 1.12f, Mathf.Clamp01(v * 0.75f + blot * 0.45f));
            Color c = new Color(b.r * shade, b.g * shade, b.b * shade, 1f);
            float sp = Hash(lx / 3, ly / 3, seed + 3);
            if (sp > 0.94f) c = Color.Lerp(c, new Color(b.r * 1.5f, b.g * 1.5f, b.b * 1.4f, 1f), 0.5f);
            else if (sp < 0.04f) c = new Color(c.r * 0.7f, c.g * 0.7f, c.b * 0.7f, 1f);
            if (d.kind == 1)
            {
                float st = 1f + Mathf.Sin((lx + ly) * 0.85f) * 0.07f;
                c = new Color(c.r * st, c.g * st, c.b * st, 1f);
            }
            c = Color.Lerp(c, new Color(1f, 0.95f, 0.6f, 1f), Mathf.SmoothStep(0.8f, 1f, v) * 0.12f);
            return Clamp(c);
        }
        float streak = Vn(u * 14f, v * 2.5f, 14, seed);
        if (d.kind == 3)
        {
            Color c = Color.Lerp(new Color(0.78f, 0.78f, 0.74f, 1f), b, streak);
            float band = Hash(0, ly / 5, seed);
            if (band > 0.82f && Vn(u * 10f, v * 8f, 10, seed + 1) > 0.35f) c = new Color(0.14f, 0.13f, 0.12f, 1f);
            return Clamp(c);
        }
        float s2 = Mathf.Lerp(0.6f, 1.1f, streak);
        float grain = 0.92f + Hash(lx / 2, ly / 6, seed + 5) * 0.16f;
        return Clamp(new Color(b.r * s2 * grain, b.g * s2 * grain, b.b * s2 * grain, 1f));
    }

    public static Material Get()
    {
        if (cached != null) return cached;
        int size = 512;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.name = "TreeAtlas";
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int cell = (y / 128) * 4 + (x / 128);
                px[y * size + x] = Pixel(cells[cell], cell, x % 128, y % 128);
            }
        }
        tex.SetPixels(px);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.anisoLevel = 2;
        tex.Apply(true);

        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (s == null) s = Shader.Find("Standard");
        Material m = new Material(s);
        m.name = "TreeAtlas_Mat";
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.04f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        cached = m;
        return m;
    }
}

public delegate void FloraBuild(FloraMesh vis, FloraMesh col, float h, float w, int fc, int bc, System.Random r);

public sealed class FloraSpecies
{
    public string name;
    public float weight, minH, maxH, footprint, altBias;
    public int[] foliage, bark;
    public FloraBuild build;

    public FloraSpecies(string name, float weight, float minH, float maxH, float footprint, float altBias, int[] foliage, int[] bark, FloraBuild build)
    {
        this.name = name; this.weight = weight; this.minH = minH; this.maxH = maxH; this.footprint = footprint; this.altBias = altBias;
        this.foliage = foliage; this.bark = bark; this.build = build;
    }
}

public static class FloraShapes
{
    static readonly Quaternion Id = Quaternion.identity;

    static Quaternion Branch(float yaw, float angle)
    {
        return Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, -angle);
    }

    static float Rnd(System.Random r)
    {
        return (float)r.NextDouble();
    }

    static void Trunk(FloraMesh vis, FloraMesh col, int bc, float r0, float r1, float th, int sides)
    {
        vis.Cell(bc);
        vis.Cylinder(Vector3.zero, Id, r0, r1, th, sides);
        col.Cylinder(Vector3.zero, Id, r0, r0 * 0.8f, Mathf.Min(th, 3f), 5);
    }

    public static void Oak(FloraMesh vis, FloraMesh col, float h, float w, int fc, int bc, System.Random r)
    {
        float th = h * 0.56f, r0 = 0.05f * h, r1 = 0.032f * h;
        Trunk(vis, col, bc, r0, r1, th, 6);
        float y0 = Rnd(r) * 360f;
        vis.Cylinder(new Vector3(0f, th * 0.62f, 0f), Branch(y0, 38f), r1 * 0.7f, r1 * 0.35f, h * 0.2f, 5);
        vis.Cylinder(new Vector3(0f, th * 0.5f, 0f), Branch(y0 + 160f, 32f), r1 * 0.7f, r1 * 0.35f, h * 0.18f, 5);
        vis.Cell(fc);
        float cr = 0.3f * h * w;
        vis.Ellipsoid(new Vector3(0f, h * 0.64f, 0f), Id, new Vector3(cr, h * 0.2f, cr), 7, 4);
        vis.Ellipsoid(new Vector3(cr * 0.62f, h * 0.52f, cr * 0.2f), Id, new Vector3(cr * 0.62f, h * 0.15f, cr * 0.62f), 6, 3);
        vis.Ellipsoid(new Vector3(-cr * 0.55f, h * 0.5f, -cr * 0.3f), Id, new Vector3(cr * 0.6f, h * 0.15f, cr * 0.6f), 6, 3);
        vis.Ellipsoid(new Vector3(0f, h * 0.82f, 0f), Id, new Vector3(cr * 0.65f, h * 0.14f, cr * 0.65f), 6, 3);
    }

    public static void Maple(FloraMesh vis, FloraMesh col, float h, float w, int fc, int bc, System.Random r)
    {
        float th = h * 0.5f;
        Trunk(vis, col, bc, 0.04f * h, 0.026f * h, th, 6);
        vis.Cell(fc);
        float cr = 0.24f * h * w;
        vis.Ellipsoid(new Vector3(0f, h * 0.55f, 0f), Id, new Vector3(cr, h * 0.18f, cr), 7, 4);
        vis.Ellipsoid(new Vector3(0f, h * 0.74f, 0f), Id, new Vector3(cr * 0.85f, h * 0.16f, cr * 0.85f), 6, 3);
        vis.Ellipsoid(new Vector3(cr * 0.7f, h * 0.62f, 0f), Id, new Vector3(cr * 0.6f, h * 0.14f, cr * 0.6f), 6, 3);
        vis.Ellipsoid(new Vector3(-cr * 0.6f, h * 0.6f, cr * 0.3f), Id, new Vector3(cr * 0.6f, h * 0.14f, cr * 0.6f), 6, 3);
        vis.Ellipsoid(new Vector3(0f, h * 0.9f, 0f), Id, new Vector3(cr * 0.45f, h * 0.1f, cr * 0.45f), 6, 3);
    }

    public static void Birch(FloraMesh vis, FloraMesh col, float h, float w, int fc, int bc, System.Random r)
    {
        Trunk(vis, col, bc, 0.022f * h, 0.014f * h, h * 0.72f, 5);
        vis.Cell(fc);
        float cr = 0.13f * h * w;
        vis.Ellipsoid(new Vector3(0f, h * 0.62f, 0f), Id, new Vector3(cr, h * 0.13f, cr), 6, 3);
        vis.Ellipsoid(new Vector3(0f, h * 0.78f, 0f), Id, new Vector3(cr * 0.9f, h * 0.12f, cr * 0.9f), 6, 3);
        vis.Ellipsoid(new Vector3(0f, h * 0.9f, 0f), Id, new Vector3(cr * 0.6f, h * 0.1f, cr * 0.6f), 6, 3);
        vis.Ellipsoid(new Vector3(cr * 0.8f, h * 0.7f, cr * 0.2f), Id, new Vector3(cr * 0.6f, h * 0.09f, cr * 0.6f), 5, 3);
        vis.Ellipsoid(new Vector3(-cr * 0.7f, h * 0.66f, -cr * 0.3f), Id, new Vector3(cr * 0.6f, h * 0.09f, cr * 0.6f), 5, 3);
    }

    public static void Spruce(FloraMesh vis, FloraMesh col, float h, float w, int fc, int bc, System.Random r)
    {
        Trunk(vis, col, bc, 0.025f * h, 0.018f * h, h * 0.3f, 5);
        vis.Cell(fc);
        for (int k = 0; k < 5; k++)
        {
            float rad = 0.26f * h * w * (1f - k * 0.19f);
            vis.Cylinder(new Vector3(0f, h * (0.16f + k * 0.16f), 0f), Id, rad, 0f, h * 0.3f, 8);
        }
    }

    public static void Fir(FloraMesh vis, FloraMesh col, float h, float w, int fc, int bc, System.Random r)
    {
        Trunk(vis, col, bc, 0.022f * h, 0.015f * h, h * 0.25f, 5);
        vis.Cell(fc);
        for (int k = 0; k < 7; k++)
        {
            float rad = 0.2f * h * w * (1f - k * 0.13f);
            vis.Cylinder(new Vector3(0f, h * (0.12f + k * 0.12f), 0f), Id, rad, 0f, h * 0.2f, 7);
        }
    }

    public static void Cypress(FloraMesh vis, FloraMesh col, float h, float w, int fc, int bc, System.Random r)
    {
        Trunk(vis, col, bc, 0.02f * h, 0.015f * h, h * 0.18f, 5);
        vis.Cell(fc);
        float cr = 0.11f * h * w;
        vis.Ellipsoid(new Vector3(0f, h * 0.5f, 0f), Id, new Vector3(cr, h * 0.42f, cr), 7, 5);
        vis.Ellipsoid(new Vector3(cr * 0.3f, h * 0.35f, 0f), Id, new Vector3(cr * 0.8f, h * 0.22f, cr * 0.8f), 6, 4);
    }

    public static void Umbrella(FloraMesh vis, FloraMesh col, float h, float w, int fc, int bc, System.Random r)
    {
        float lean = (Rnd(r) - 0.5f) * 14f;
        float y0 = Rnd(r) * 360f;
        vis.Cell(bc);
        vis.Cylinder(Vector3.zero, Id, 0.03f * h, 0.022f * h, h * 0.36f, 6);
        Vector3 top = new Vector3(0f, h * 0.36f, 0f);
        vis.Cylinder(top, Branch(y0, lean), 0.022f * h, 0.016f * h, h * 0.34f, 5);
        col.Cylinder(Vector3.zero, Id, 0.03f * h, 0.026f * h, Mathf.Min(h * 0.36f, 3f), 5);
        Vector3 c = top + Branch(y0, lean) * new Vector3(0f, h * 0.34f, 0f);
        vis.Cell(fc);
        float cr = 0.34f * h * w;
        vis.Ellipsoid(c + new Vector3(0f, h * 0.04f, 0f), Id, new Vector3(cr, h * 0.075f, cr), 8, 3);
        vis.Ellipsoid(c + new Vector3(0f, h * 0.11f, 0f), Id, new Vector3(cr * 0.62f, h * 0.065f, cr * 0.62f), 7, 3);
    }

    public static void Cherry(FloraMesh vis, FloraMesh col, float h, float w, int fc, int bc, System.Random r)
    {
        float y0 = Rnd(r) * 360f;
        float th = h * 0.5f;
        Trunk(vis, col, bc, 0.05f * h, 0.035f * h, th, 6);
        vis.Cylinder(new Vector3(0f, th * 0.7f, 0f), Branch(y0, 42f), 0.025f * h, 0.012f * h, h * 0.25f, 5);
        vis.Cylinder(new Vector3(0f, th * 0.6f, 0f), Branch(y0 + 180f, 40f), 0.025f * h, 0.012f * h, h * 0.25f, 5);
        vis.Cell(fc);
        float cr = 0.32f * h * w;
        vis.Ellipsoid(new Vector3(0f, h * 0.56f, 0f), Id, new Vector3(cr, h * 0.16f, cr * 0.95f), 8, 4);
        vis.Ellipsoid(new Vector3(cr * 0.7f, h * 0.46f, 0f), Id, new Vector3(cr * 0.6f, h * 0.12f, cr * 0.6f), 6, 3);
        vis.Ellipsoid(new Vector3(-cr * 0.7f, h * 0.46f, cr * 0.1f), Id, new Vector3(cr * 0.6f, h * 0.12f, cr * 0.6f), 6, 3);
        vis.Ellipsoid(new Vector3(0f, h * 0.72f, 0f), Id, new Vector3(cr * 0.6f, h * 0.12f, cr * 0.6f), 6, 3);
    }

    public static void Dead(FloraMesh vis, FloraMesh col, float h, float w, int fc, int bc, System.Random r)
    {
        Trunk(vis, col, bc, 0.035f * h, 0.01f * h, h * 0.92f, 5);
        float y0 = Rnd(r) * 360f;
        for (int k = 0; k < 5; k++)
        {
            float by = h * (0.35f + k * 0.11f);
            vis.Cylinder(new Vector3(0f, by, 0f), Branch(y0 + k * 137f, 48f + Rnd(r) * 12f), 0.014f * h, 0.004f * h, h * (0.3f - k * 0.03f), 4);
        }
    }
}

public sealed class FloraCuller : MonoBehaviour
{
    public Renderer[] renderers;
    public Vector3[] centers;
    public float[] radii;
    public float distance = 320f;
    float next;

    void Update()
    {
        if (Time.unscaledTime < next || renderers == null) return;
        next = Time.unscaledTime + 0.5f;
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 cp = cam.transform.position;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            bool on = (centers[i] - cp).magnitude - radii[i] < distance;
            if (renderers[i].enabled != on) renderers[i].enabled = on;
        }
    }
}

[AddComponentMenu("Procedural World/Stages/6b Flora")]
public class FloraStage : WorldStage
{
    public float spacing = 5.2f;
    public int maxTrees = 4200;
    public float clearance = 0.6f;
    public float highwayClearance = 16f;
    [Range(5f, 60f)] public float maxSlopeDeg = 30f;
    public float sink = 0.2f;
    public float noiseFrequency = 0.012f;
    [Range(0f, 1f)] public float minDensity = 0.65f;
    public float chunkSize = 96f;
    public float cullDistance = 320f;
    public bool castShadows = true;

    public float blossomRadius = 55f;
    [Range(0.4f, 0.9f)] public float autumnThreshold = 0.64f;

    sealed class Biome
    {
        public float[] w;
        public int[] broad;
        public int[] accent;
        public float accentChance;
        public Biome(float[] w, int[] broad, int[] accent, float accentChance)
        {
            this.w = w; this.broad = broad; this.accent = accent; this.accentChance = accentChance;
        }
    }

    static float MountainFraction(WorldContext c, Vector2 p)
    {
        int hit = 0;
        for (int ring = 1; ring <= 2; ring++)
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI * 0.25f;
                if (c.zoneMap.ZoneAt(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ring * 35f) == ZoneType.Mountain) hit++;
            }
        return hit / 16f;
    }

    class Chunk
    {
        public FloraMesh vis = new FloraMesh();
        public FloraMesh col = new FloraMesh();
        public int count;
    }

    public override int Order
    {
        get { return StageOrder.Vegetation + 10; }
    }

    public override string StageName
    {
        get { return "Flora"; }
    }

    static FloraSpecies[] Species()
    {
        return new FloraSpecies[]
        {
            new FloraSpecies("Oak", 3f, 8f, 13f, 0.38f, -0.6f, new int[] { 0, 9, 0, 15 }, new int[] { 10, 14 }, FloraShapes.Oak),
            new FloraSpecies("Maple", 2f, 8f, 13f, 0.33f, -0.2f, new int[] { 6, 7, 8, 8 }, new int[] { 10, 13 }, FloraShapes.Maple),
            new FloraSpecies("Birch", 2.5f, 8f, 13f, 0.2f, 0f, new int[] { 1, 8, 1, 0 }, new int[] { 11 }, FloraShapes.Birch),
            new FloraSpecies("Spruce", 3f, 11f, 19f, 0.3f, 0.7f, new int[] { 2, 3, 2 }, new int[] { 10, 13 }, FloraShapes.Spruce),
            new FloraSpecies("Fir", 2f, 12f, 20f, 0.26f, 0.8f, new int[] { 3, 2 }, new int[] { 10, 14 }, FloraShapes.Fir),
            new FloraSpecies("Cypress", 1.5f, 10f, 16f, 0.18f, 0.2f, new int[] { 2, 9 }, new int[] { 10 }, FloraShapes.Cypress),
            new FloraSpecies("Umbrella Pine", 1.2f, 9f, 14f, 0.4f, -0.3f, new int[] { 0, 9 }, new int[] { 13, 10 }, FloraShapes.Umbrella),
            new FloraSpecies("Cherry", 1f, 5f, 8f, 0.5f, -0.9f, new int[] { 4, 5, 4 }, new int[] { 13, 14 }, FloraShapes.Cherry),
            new FloraSpecies("Dead Tree", 0.5f, 6f, 11f, 0.15f, 0.5f, new int[] { 0 }, new int[] { 12, 14 }, FloraShapes.Dead)
        };
    }

    public override void Run(WorldContext c)
    {
        if (c.zoneMap == null) return;
        FloraSpecies[] sp = Species();
        WorldRng rng = c.Rng("flora");
        Vector2 off = new Vector2(rng.Range(2000f, 9000f), rng.Range(2000f, 9000f));
        Vector2[] spOff = new Vector2[sp.Length];
        for (int i = 0; i < sp.Length; i++) spOff[i] = new Vector2(rng.Range(100f, 5000f), rng.Range(100f, 5000f));
        float half = c.Size * 0.5f;
        float maxH = Mathf.Max(1f, c.settings.maxTerrainHeight);
        List<Vector2> cands = PoissonDisc.Generate(new Rect(-half, -half, c.Size, c.Size), Mathf.Max(2f, spacing), rng.Raw, 16);
        for (int i = cands.Count - 1; i > 0; i--)
        {
            int j = rng.Range(0, i + 1);
            Vector2 tmp = cands[i]; cands[i] = cands[j]; cands[j] = tmp;
        }

        GameObject rootGo = new GameObject("FloraTrees");
        rootGo.transform.SetParent(c.root, false);
        Dictionary<long, Chunk> chunks = new Dictionary<long, Chunk>();
        float minNormalY = Mathf.Cos(maxSlopeDeg * Mathf.Deg2Rad);
        float[] scores = new float[sp.Length];
        Vector2 bOff = new Vector2(rng.Range(100f, 5000f), rng.Range(100f, 5000f));
        Biome green = new Biome(new float[] { 3f, 1.6f, 2.2f, 1f, 0.4f, 1f, 0.8f, 0f, 0.15f }, new int[] { 0 }, new int[] { 1, 8 }, 0.04f);
        Biome pine = new Biome(new float[] { 0.25f, 0f, 0.5f, 4f, 3.5f, 0.6f, 0.2f, 0f, 0.6f }, new int[] { 0 }, new int[] { 1, 8 }, 0.03f);
        Biome blossom = new Biome(new float[] { 1.6f, 1.2f, 0.8f, 0.2f, 0f, 0f, 0f, 4f, 0f }, new int[] { 4, 5 }, new int[] { 1 }, 0.04f);
        float[] autumnW = { 3f, 3.5f, 2f, 0.5f, 0.2f, 0.2f, 0f, 0f, 0.4f };
        Biome autumnRed = new Biome(autumnW, new int[] { 7 }, new int[] { 6, 0 }, 0.05f);
        Biome autumnOrange = new Biome(autumnW, new int[] { 6 }, new int[] { 7, 0 }, 0.05f);
        Biome autumnGold = new Biome(autumnW, new int[] { 8 }, new int[] { 6, 0 }, 0.05f);
        Vector2 pondC = Vector2.zero;
        float pondR = 0f;
        bool hasPond = false;
        Transform pw = c.LayerRoot(PlacementLayer.Prop).Find("PondWater");
        if (pw != null)
        {
            Renderer pr = pw.GetComponentInChildren<Renderer>();
            if (pr != null)
            {
                pondC = new Vector2(pr.bounds.center.x, pr.bounds.center.z);
                pondR = Mathf.Max(pr.bounds.extents.x, pr.bounds.extents.z);
                hasPond = true;
            }
        }
        int placed = 0;
        for (int i = 0; i < cands.Count && placed < maxTrees; i++)
        {
            Vector2 p = cands[i];
            ZoneType zt = c.zoneMap.ZoneAt(p);
            float pd = hasPond ? (p - pondC).magnitude : float.MaxValue;
            bool nearPond = pd < pondR + blossomRadius;
            if (nearPond && pd < pondR + 4f) continue;
            if (zt != ZoneType.Forest && !(nearPond && zt == ZoneType.Open)) continue;
            float n = NoiseLib.Fbm(p.x * noiseFrequency + off.x, p.y * noiseFrequency + off.y, 3, 2f, 0.5f);
            float density = Mathf.Lerp(minDensity, 1f, Mathf.SmoothStep(0.25f, 0.65f, n));
            if (!rng.Chance(density)) continue;
            if (c.zoneMap.highway != null && c.zoneMap.highway.DistanceTo(p) < highwayClearance) continue;
            if (c.NormalAt(p.x, p.y).y < minNormalY) continue;

            float mf = MountainFraction(c, p);
            Biome bm;
            if (nearPond) bm = blossom;
            else if (mf > 0.2f) bm = pine;
            else
            {
                float bn = NoiseLib.Fbm(p.x * 0.0035f + bOff.x, p.y * 0.0035f + bOff.y, 2, 2f, 0.5f);
                if (bn > autumnThreshold)
                {
                    float cn = NoiseLib.Fbm(p.x * 0.0028f + bOff.y, p.y * 0.0028f + bOff.x, 2, 2f, 0.5f);
                    bm = cn < 0.3f ? autumnRed : (cn > 0.7f ? autumnGold : autumnOrange);
                }
                else bm = green;
            }
            float conBoost = 1f + mf * 6f;
            float total = 0f;
            for (int s = 0; s < sp.Length; s++)
            {
                float nz = NoiseLib.Fbm(p.x * 0.007f + spOff[s].x, p.y * 0.007f + spOff[s].y, 2, 2f, 0.5f);
                float wgt = bm.w[s];
                if (s == 3 || s == 4) wgt *= conBoost;
                scores[s] = wgt * (0.35f + nz * nz * 1.3f);
                total += scores[s];
            }
            if (total <= 0f) continue;
            float pick = rng.Value() * total;
            int si = sp.Length - 1;
            for (int s = 0; s < sp.Length; s++)
            {
                pick -= scores[s];
                if (pick <= 0f) { si = s; break; }
            }
            FloraSpecies spc = sp[si];

            float h = rng.Range(spc.minH, spc.maxH);
            float fp = Mathf.Max(0.5f, spc.footprint * h);
            float baseY = c.HeightAt(p.x, p.y) - sink;
            float yaw = rng.Range(0f, 360f);
            OrientedBox box = OrientedBox.FromBase(new Vector3(p.x, baseY, p.y), new Vector3(fp, h, fp), yaw);
            if (!c.occupancy.IsFree(box, PlacementLayer.Tree, clearance)) continue;

            int ci = Mathf.Clamp(Mathf.FloorToInt((p.x + half) / chunkSize), 0, 1000);
            int cj = Mathf.Clamp(Mathf.FloorToInt((p.y + half) / chunkSize), 0, 1000);
            long key = (long)ci * 1001L + cj;
            Chunk ch;
            if (!chunks.TryGetValue(key, out ch)) { ch = new Chunk(); chunks[key] = ch; }

            int fc;
            if (si == 0 || si == 1 || si == 2 || si == 7)
                fc = bm.accent.Length > 0 && rng.Chance(bm.accentChance) ? bm.accent[Mathf.FloorToInt(rng.Value() * 0.999f * bm.accent.Length)] : bm.broad[Mathf.FloorToInt(rng.Value() * 0.999f * bm.broad.Length)];
            else
                fc = spc.foliage[Mathf.FloorToInt(rng.Value() * 0.999f * spc.foliage.Length)];
            int bc = spc.bark[Mathf.FloorToInt(rng.Value() * 0.999f * spc.bark.Length)];
            Matrix4x4 m = Matrix4x4.TRS(new Vector3(p.x, baseY, p.y), Quaternion.Euler(rng.Range(-3f, 3f), yaw, rng.Range(-3f, 3f)), Vector3.one);
            ch.vis.M = m;
            ch.col.M = m;
            spc.build(ch.vis, ch.col, h, rng.Range(0.88f, 1.18f), fc, bc, rng.Raw);
            ch.count++;
            c.occupancy.Add(box, PlacementLayer.Tree, rootGo);
            placed++;
        }

        Material mat = FloraAtlas.Get();
        List<Renderer> rends = new List<Renderer>();
        List<Vector3> centers = new List<Vector3>();
        List<float> radii = new List<float>();
        int tris = 0;
        foreach (KeyValuePair<long, Chunk> kv in chunks)
        {
            Chunk ch = kv.Value;
            if (ch.vis.v.Count == 0) continue;
            GameObject go = new GameObject("FloraChunk_" + kv.Key);
            go.transform.SetParent(rootGo.transform, false);
            Mesh vm = ch.vis.ToMesh("FloraVis");
            go.AddComponent<MeshFilter>().sharedMesh = vm;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;
            if (ch.col.v.Count > 0) go.AddComponent<MeshCollider>().sharedMesh = ch.col.ToMesh("FloraCol");
            go.isStatic = true;
            rends.Add(mr);
            centers.Add(vm.bounds.center);
            radii.Add(vm.bounds.extents.magnitude);
            tris += ch.vis.t.Count / 3;
        }
        FloraCuller culler = rootGo.AddComponent<FloraCuller>();
        culler.renderers = rends.ToArray();
        culler.centers = centers.ToArray();
        culler.radii = radii.ToArray();
        culler.distance = cullDistance;
        Debug.Log("Flora: " + placed + " trees in " + rends.Count + " chunks, " + tris + " triangles total.");
    }
}
