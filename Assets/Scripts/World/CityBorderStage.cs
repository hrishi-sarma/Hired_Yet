using System.Collections.Generic;
using UnityEngine;

public static class CityMaterials
{
    static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

    static Shader LitShader()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (s == null) s = Shader.Find("Standard");
        return s;
    }

    static Material Make(string key, System.Func<Texture2D> tex, Color tint, float smooth)
    {
        Material m;
        if (cache.TryGetValue(key, out m) && m != null) return m;
        m = new Material(LitShader());
        m.name = "City_" + key;
        if (tex != null)
        {
            Texture2D t = tex();
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
        if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        cache[key] = m;
        return m;
    }

    static Texture2D Tex(int w, int h, System.Func<int, int, Color> f)
    {
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, true);
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = f(x, y);
        t.SetPixels(px);
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Bilinear;
        t.anisoLevel = 4;
        t.Apply(true);
        return t;
    }

    public static Material Plain(string name, Color c, float smooth)
    {
        return Make("plain_" + name, null, c, smooth);
    }

    public static Material Stripe()
    {
        return Make("stripe", StripeTex, Color.white, 0.2f);
    }

    public static Material Sign()
    {
        return Make("sign", SignTex, Color.white, 0.2f);
    }

    public static Material Water()
    {
        return Make("water", WaterTex, Color.white, 0.85f);
    }

    public static Material Black()
    {
        Material m;
        if (cache.TryGetValue("black", out m) && m != null) return m;
        Shader s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Unlit/Color");
        if (s == null) return Plain("black", Color.black, 0f);
        m = new Material(s);
        m.name = "City_black";
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.black);
        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.black);
        cache["black"] = m;
        return m;
    }

    public static Material Beacon()
    {
        Material m = Make("beacon", null, new Color(1f, 0.1f, 0.08f), 0.2f);
        m.EnableKeyword("_EMISSION");
        return m;
    }

    static Texture2D StripeTex()
    {
        return Tex(96, 8, (x, y) => ((x + y * 2) / 16) % 2 == 0 ? new Color(0.85f, 0.15f, 0.12f) : new Color(0.96f, 0.96f, 0.96f));
    }

    static Texture2D SignTex()
    {
        return Tex(64, 64, (x, y) =>
        {
            if (x < 2 || y < 2 || x > 61 || y > 61) return new Color(0.2f, 0.2f, 0.22f);
            float dx = x - 31.5f, dy = y - 31.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d < 25f)
            {
                if (Mathf.Abs(dy) < 5f && Mathf.Abs(dx) < 17f) return Color.white;
                return new Color(0.85f, 0.12f, 0.1f);
            }
            return Color.white;
        });
    }

    static Texture2D WaterTex()
    {
        return Tex(64, 64, (x, y) =>
        {
            float u = x / 64f * Mathf.PI * 2f;
            float v = y / 64f * Mathf.PI * 2f;
            float f = 0.5f + 0.25f * Mathf.Sin(u * 3f + Mathf.Sin(v * 2f) * 1.5f) + 0.15f * Mathf.Sin((u + v) * 2f);
            return Color.Lerp(new Color(0.25f, 0.58f, 0.82f), new Color(0.6f, 0.88f, 0.97f), Mathf.Clamp01(f));
        });
    }
}

[AddComponentMenu("Procedural World/Stages/3b City Border")]
public class CityBorderStage : WorldStage
{
    public float wallHeight = 4.2f;
    public float segmentLength = 3.5f;
    public float highwayGap = 20f;
    public float stubMaxLength = 60f;
    [Range(0f, 1f)] public float barrierChance = 0.5f;
    public float barrierSpacing = 45f;

    static readonly Vector2[] Dirs = { new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f) };

    public override int Order
    {
        get { return StageOrder.RoadPlan + 10; }
    }

    public override string StageName
    {
        get { return "City Border"; }
    }

    public override void Run(WorldContext c)
    {
        if (c.zoneMap == null || c.roads == null || c.roads.blocks.Count == 0) return;
        WorldRng rng = c.Rng("cityborder");
        Transform root = new GameObject("CityBorder").transform;
        root.SetParent(c.LayerRoot(PlacementLayer.Prop), false);
        DetailBatch batch = new DetailBatch();
        BuildStubs(c, rng, root, batch);
        BuildWall(c, root, batch);
        batch.Flush(root, "CityBorder", true);
    }

    void AddNode(Dictionary<long, Vector2> pos, Dictionary<long, int> mask, Dictionary<long, float> tops, Vector2 p, int bit, float top)
    {
        long key = (long)Mathf.RoundToInt(p.x * 2f) * 100003L + Mathf.RoundToInt(p.y * 2f);
        if (!pos.ContainsKey(key)) { pos[key] = p; mask[key] = 0; tops[key] = top; }
        mask[key] |= bit;
    }

    void BuildStubs(WorldContext c, WorldRng rng, Transform root, DetailBatch batch)
    {
        RoadStage rs = c.generator != null ? c.generator.GetComponent<RoadStage>() : null;
        if (rs == null || rs.city == null) return;
        ZoneMap zm = c.zoneMap;
        float rw = rs.city.roadWidth;
        Dictionary<long, Vector2> pos = new Dictionary<long, Vector2>();
        Dictionary<long, int> mask = new Dictionary<long, int>();
        Dictionary<long, float> tops = new Dictionary<long, float>();
        for (int i = 0; i < c.roads.lines.Count; i++)
        {
            RoadLine l = c.roads.lines[i];
            if (zm.ZoneAt((l.a + l.b) * 0.5f) != ZoneType.City) continue;
            bool hor = Mathf.Abs(l.b.x - l.a.x) > 0.01f;
            AddNode(pos, mask, tops, l.a, hor ? 1 : 4, l.roadTop);
            AddNode(pos, mask, tops, l.b, hor ? 2 : 8, l.roadTop);
        }
        List<Vector2> placed = new List<Vector2>();
        foreach (KeyValuePair<long, int> kv in mask)
        {
            Vector2 np = pos[kv.Key];
            int mk = kv.Value;
            float top = tops[kv.Key];
            for (int d = 0; d < 4; d++)
            {
                if ((mk & (1 << d)) != 0 || (mk & (1 << (d ^ 1))) == 0) continue;
                Vector2 dir = Dirs[d];
                if (zm.highway != null && zm.highway.DistanceTo(np) < 40f) continue;
                float h0 = c.HeightAt(np.x, np.y);
                float tStop = rw * 0.5f;
                bool tunnel = false;
                for (float t = rw * 0.5f + 1f; t <= stubMaxLength + rw * 0.5f; t += 2f)
                {
                    Vector2 p = np + dir * t;
                    float hp = c.HeightAt(p.x, p.y);
                    if (zm.ZoneAt(p) == ZoneType.Mountain || hp - h0 > 4f) { tunnel = true; break; }
                    if (zm.ZoneAt(p) != ZoneType.City || hp - h0 < -4f) break;
                    tStop = t;
                }
                float len = tunnel ? tStop - rw * 0.5f + 1f : tStop - rw * 0.5f - 6f;
                if (len < 8f) continue;
                if (!tunnel && !rng.Chance(barrierChance)) continue;
                Vector2 endP = np + dir * (rw * 0.5f + len);
                if (zm.highway != null && zm.highway.DistanceTo(endP) < 25f) continue;
                bool near = false;
                for (int k = 0; k < placed.Count; k++)
                    if ((placed[k] - endP).sqrMagnitude < (tunnel ? 30f : barrierSpacing) * (tunnel ? 30f : barrierSpacing)) { near = true; break; }
                if (near) continue;
                bool roadAhead = false;
                for (int li = 0; li < c.roads.lines.Count && !roadAhead; li++)
                {
                    RoadLine rl = c.roads.lines[li];
                    for (float ta = 0f; ta <= 24f; ta += 3f)
                        if (SegDist(endP + dir * ta, rl.a, rl.b) < rw * 0.5f + 1f) { roadAhead = true; break; }
                }
                if (roadAhead) continue;
                placed.Add(endP);
                BuildStub(c, root, batch, np, dir, rw, len, top, tunnel);
            }
        }
    }

    void BuildStub(WorldContext c, Transform root, DetailBatch batch, Vector2 np, Vector2 dir, float rw, float len, float top, bool tunnel)
    {
        Vector2 startP = np + dir * (rw * 0.5f);
        Vector2 endP = startP + dir * len;
        Vector2 mid = (startP + endP) * 0.5f;
        float thick = 1.4f;
        float yaw = dir.x != 0f ? 90f : 0f;
        Vector2 acr = new Vector2(-dir.y, dir.x);
        Rect area = RectOf(startP - dir * (rw * 0.5f) + acr * (rw * 0.5f), (tunnel ? endP : endP + dir * (rw * 0.5f)) - acr * (rw * 0.5f));
        c.Flatten(area, top - 0.25f, 5f);
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "RoadStub";
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3(mid.x, top - thick * 0.5f, mid.y);
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = new Vector3(rw, thick, len);
        go.isStatic = true;
        StylizedMaterialFactory.ApplyUniformRole(go.transform, "asphalt");
        c.occupancy.Add(OrientedBox.FromBase(new Vector3(mid.x, top - thick, mid.y), new Vector3(rw, thick, len), yaw), PlacementLayer.Road, go);
        c.roads.footprints.Add(area);

        Vector3 P = new Vector3(endP.x, top, endP.y);
        if (tunnel) P -= new Vector3(dir.x, 0f, dir.y) * 3f;
        if (tunnel) BuildTunnel(c, root, batch, endP, dir, rw, top);
        Vector2 across = new Vector2(-dir.y, dir.x);
        Vector3 fwd3 = new Vector3(dir.x, 0f, dir.y);
        Vector3 across3 = new Vector3(across.x, 0f, across.y);
        Material stripe = CityMaterials.Stripe();
        Material conc = StylizedMaterialFactory.Get("concrete", new Color(0.55f, 0.55f, 0.55f), 0);
        Material post = StylizedMaterialFactory.Get("paintedMetal", new Color(0.9f, 0.9f, 0.9f), 1);
        Material sign = CityMaterials.Sign();
        batch.Box(stripe, P + Vector3.up * 0.95f, yaw == 90f ? 0f : 90f, new Vector3(0.14f, 0.3f, rw - 1f));
        batch.Box(stripe, P + Vector3.up * 0.5f, yaw == 90f ? 0f : 90f, new Vector3(0.14f, 0.3f, rw - 1f));
        float yawA = dir.x != 0f ? 0f : 90f;
        for (int s = -1; s <= 1; s += 2)
        {
            batch.Box(post, P + across3 * (s * (rw * 0.5f - 0.45f)), yawA, new Vector3(0.28f, 1.5f, 0.28f));
            batch.Box(conc, P - fwd3 * 1.3f + across3 * (s * rw * 0.22f), yawA, new Vector3(0.7f, 0.85f, rw * 0.34f));
        }
        batch.Box(post, P - fwd3 * 0.3f, yawA, new Vector3(0.1f, 2.6f, 0.1f));
        batch.CrossQuad(sign, P - fwd3 * 0.37f + Vector3.up * 1.5f, across, 1.1f, 1.1f);
        float barYaw = yaw == 90f ? 0f : 90f;
        Transform cr = new GameObject("RoadClosedColliders").transform;
        cr.SetParent(root, false);
        // Both stripe bars plus the gap between them, spanning the closed road.
        AddBoxCol(cr, P + Vector3.up * 0.725f, barYaw, new Vector3(0.14f, 0.75f, rw - 1f));
        for (int s = -1; s <= 1; s += 2)
        {
            AddBoxCol(cr, P + across3 * (s * (rw * 0.5f - 0.45f)) + Vector3.up * 0.75f, yawA, new Vector3(0.28f, 1.5f, 0.28f));
            AddBoxCol(cr, P - fwd3 * 1.3f + across3 * (s * rw * 0.22f) + Vector3.up * 0.425f, yawA, new Vector3(0.7f, 0.85f, rw * 0.34f));
        }
        AddBoxCol(cr, P - fwd3 * 0.3f + Vector3.up * 1.3f, yawA, new Vector3(0.1f, 2.6f, 0.1f));
        AddBoxCol(cr, P - fwd3 * 0.37f + Vector3.up * 1.5f, yawA, new Vector3(0.1f, 1.1f, 1.1f));
    }

    static Mesh unitCube;
    /// <summary>Non-convex MeshCollider shaped exactly like the box (cube mesh scaled by a child).</summary>
    static void MeshCol(GameObject g, Vector3 size)
    {
        if (unitCube == null)
        {
            GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            unitCube = p.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(p);
        }
        GameObject c = new GameObject("MeshCol");
        c.transform.SetParent(g.transform, false);
        c.transform.localScale = size;
        c.isStatic = true;
        MeshCollider mc = c.AddComponent<MeshCollider>();
        mc.sharedMesh = unitCube;
        mc.convex = false;
    }

    static void AddBoxCol(Transform parent, Vector3 center, float yaw, Vector3 size)
    {
        GameObject g = new GameObject("BarrierCollider");
        g.transform.SetParent(parent, false);
        g.transform.position = center;
        g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        MeshCol(g, size);
        g.isStatic = true;
    }

    static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float l = ab.sqrMagnitude;
        float t = l < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / l);
        return (p - (a + ab * t)).magnitude;
    }

    static Rect RectOf(Vector2 p, Vector2 q)
    {
        return new Rect(Mathf.Min(p.x, q.x), Mathf.Min(p.y, q.y), Mathf.Abs(p.x - q.x), Mathf.Abs(p.y - q.y));
    }

    static void Quad(List<Vector3> v, List<int> t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 outward)
    {
        int k = v.Count;
        v.Add(p0); v.Add(p1); v.Add(p2); v.Add(p3);
        Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
        bool flip = Vector3.Dot(n, outward) < 0f;
        if (!flip) { t.Add(k); t.Add(k + 1); t.Add(k + 2); t.Add(k); t.Add(k + 2); t.Add(k + 3); }
        else { t.Add(k); t.Add(k + 2); t.Add(k + 1); t.Add(k); t.Add(k + 3); t.Add(k + 2); }
    }

    static void Wedge(Transform root, Material m, Vector3 pos, Vector3 fwd, float width, float length, float h0, float h1)
    {
        float w = width * 0.5f;
        Vector3 A0 = new Vector3(-w, 0f, 0f), A1 = new Vector3(w, 0f, 0f);
        Vector3 B0 = new Vector3(-w, h0, 0f), B1 = new Vector3(w, h0, 0f);
        Vector3 C0 = new Vector3(-w, h1, length), C1 = new Vector3(w, h1, length);
        Vector3 D0 = new Vector3(-w, 0f, length), D1 = new Vector3(w, 0f, length);
        List<Vector3> v = new List<Vector3>();
        List<int> t = new List<int>();
        Quad(v, t, B0, C0, C1, B1, Vector3.up);
        Quad(v, t, A0, B0, B1, A1, Vector3.back);
        Quad(v, t, D1, C1, C0, D0, Vector3.forward);
        Quad(v, t, D0, C0, B0, A0, Vector3.left);
        Quad(v, t, A1, B1, C1, D1, Vector3.right);
        Mesh mesh = new Mesh();
        mesh.name = "TunnelMound";
        mesh.SetVertices(v);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GameObject go = new GameObject("TunnelMound");
        go.transform.SetParent(root, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = m;
        go.isStatic = true;
    }

    void BuildTunnel(WorldContext c, Transform root, DetailBatch batch, Vector2 endP, Vector2 dir, float rw, float top)
    {
        float D = 14f;
        float wi = rw - 2f;
        float w2 = wi + 4.4f;
        float hIn = 6.5f;
        float yawA = dir.x != 0f ? 0f : 90f;
        Vector3 f3 = new Vector3(dir.x, 0f, dir.y);
        Vector3 a3 = new Vector3(-dir.y, 0f, dir.x);
        Vector2 ac = new Vector2(-dir.y, dir.x);
        Vector3 F = new Vector3(endP.x, top, endP.y);
        Material conc = StylizedMaterialFactory.Get("concrete", new Color(0.55f, 0.55f, 0.56f), 0);
        Material trim = StylizedMaterialFactory.Get("concrete", new Color(0.42f, 0.42f, 0.44f), 0);
        Material black = CityMaterials.Black();
        Material rock = CityMaterials.Plain("rock", new Color(0.43f, 0.38f, 0.3f), 0f);

        c.Flatten(RectOf(endP - dir * (rw * 0.5f) + ac * (w2 * 0.5f + 1f), endP + dir * 0.6f - ac * (w2 * 0.5f + 1f)), top - 0.25f, 2.5f);
        c.Flatten(RectOf(endP + ac * (w2 * 0.5f), endP + dir * (D + 1f) - ac * (w2 * 0.5f)), top - 0.9f, 1f);

        float bodyLen = D - 1.3f;
        Vector3 bodyMid = F + f3 * (1.3f + bodyLen * 0.5f);
        Vector3 inMid = F + f3 * (D * 0.5f);
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 off = a3 * (s * (wi * 0.5f + 1.1f));
            batch.Box(trim, F + f3 * 0.65f + off + Vector3.down * 0.3f, yawA, new Vector3(1.3f, hIn + 1.9f, 2.2f));
            batch.Box(conc, bodyMid + off + Vector3.down * 0.3f, yawA, new Vector3(bodyLen, hIn + 1.8f, 2.2f));
            batch.Box(black, inMid + a3 * (s * (wi * 0.5f - 0.05f)), yawA, new Vector3(D - 0.2f, hIn, 0.1f));
        }
        batch.Box(trim, F + f3 * 0.65f + Vector3.up * hIn, yawA, new Vector3(1.3f, 1.6f, w2));
        batch.Box(conc, bodyMid + Vector3.up * hIn, yawA, new Vector3(bodyLen, 1.5f, w2));
        batch.Box(black, inMid + Vector3.up * (hIn - 0.1f), yawA, new Vector3(D - 0.2f, 0.1f, wi));
        batch.Box(black, inMid + Vector3.down * 0.05f, yawA, new Vector3(D - 0.2f, 0.1f, wi));
        batch.Box(black, F + f3 * D, yawA, new Vector3(0.5f, hIn, wi));

        Wedge(root, rock, F + f3 * 1.3f + Vector3.down * 1.2f, f3, w2 + 3f, D + 1.7f, hIn + 2.9f, hIn + 10.2f);

        GameObject col = new GameObject("TunnelCollider");
        col.transform.SetParent(root, false);
        col.transform.position = inMid + Vector3.up * (hIn * 0.5f + 0.4f);
        col.transform.rotation = Quaternion.Euler(0f, yawA, 0f);
        MeshCol(col, new Vector3(D, hIn + 1.5f, w2));
        col.isStatic = true;
    }

    void BuildWall(WorldContext c, Transform root, DetailBatch batch)
    {
        ZoneMap zm = c.zoneMap;
        Vector2 center = zm.cityCenter;
        float R = zm.cityRadius;
        float half = c.Size * 0.5f;
        float G = 0f;
        int gn = 0;
        for (int i = 0; i < c.roads.blocks.Count; i++)
        {
            BlockInfo bl = c.roads.blocks[i];
            if (bl.zone != ZoneType.City) continue;
            G += c.HeightAt(bl.center.x, bl.center.y);
            gn++;
        }
        if (gn == 0) return;
        G /= gn;

        int samples = Mathf.Clamp(Mathf.CeilToInt(2f * Mathf.PI * R * 1.3f / segmentLength), 200, 900);
        float[] rr = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float ang = i / (float)samples * Mathf.PI * 2f;
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            float last = R * 0.5f;
            for (float r = R * 0.4f; r <= R * 1.7f; r += 1.5f)
            {
                Vector2 q = center + dir * r;
                if (Mathf.Abs(q.x) > half - 12f || Mathf.Abs(q.y) > half - 12f) break;
                bool inside = zm.ZoneAt(q) == ZoneType.City || c.roads.InFootprint(q, 0f);
                if (inside) last = r;
                else if (r > last + 24f) break;
            }
            rr[i] = last + 3f;
        }
        float[] mx = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float m = 0f;
            for (int k = -3; k <= 3; k++) m = Mathf.Max(m, rr[(i + k + samples) % samples]);
            mx[i] = m;
        }
        for (int pass = 0; pass < 3; pass++)
        {
            float[] tmp = new float[samples];
            for (int i = 0; i < samples; i++) tmp[i] = (mx[(i + samples - 1) % samples] + 2f * mx[i] + mx[(i + 1) % samples]) * 0.25f;
            mx = tmp;
        }
        Vector2[] path = new Vector2[samples];
        for (int i = 0; i < samples; i++)
        {
            float ang = i / (float)samples * Mathf.PI * 2f;
            path[i] = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (mx[i] + 2f);
        }

        Material wallMat = StylizedMaterialFactory.Get("concrete", new Color(0.66f, 0.66f, 0.63f), 0);
        Material capMat = StylizedMaterialFactory.Get("concrete", new Color(0.5f, 0.5f, 0.5f), 0);
        Transform colRoot = new GameObject("CityWallColliders").transform;
        colRoot.SetParent(root, false);
        float baseY = G - 0.3f;
        float h = wallHeight + 0.3f;
        for (int i = 0; i < samples; i++)
        {
            Vector2 a = path[i];
            Vector2 b = path[(i + 1) % samples];
            Vector2 delta = b - a;
            float len = delta.magnitude;
            if (len < 0.1f) continue;
            Vector2 mid = (a + b) * 0.5f;
            if (Mathf.Abs(mid.x) > half - 14f || Mathf.Abs(mid.y) > half - 14f) continue;
            if (zm.highway != null && zm.highway.DistanceTo(mid) < highwayGap) continue;
            if (c.HeightAt(mid.x, mid.y) > G + 8f) continue;
            c.Flatten(new Rect(mid.x - len * 0.5f - 2f, mid.y - len * 0.5f - 2f, len + 4f, len + 4f), G - 0.15f, 8f);
            float yaw = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
            Vector3 baseP = new Vector3(mid.x, baseY, mid.y);
            batch.Box(wallMat, baseP, yaw, new Vector3(0.6f, h, len + 0.7f));
            batch.Box(capMat, baseP + Vector3.up * (h - 0.02f), yaw, new Vector3(0.9f, 0.22f, len + 0.8f));
            if (i % 6 == 0) batch.Box(capMat, new Vector3(a.x, baseY, a.y), yaw, new Vector3(1f, h + 0.5f, 1f));
            GameObject col = new GameObject("WallCollider");
            col.transform.SetParent(colRoot, false);
            col.transform.position = new Vector3(mid.x, baseY + h * 0.5f, mid.y);
            col.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            MeshCol(col, new Vector3(0.9f, h + 0.2f, len + 0.8f));
            col.isStatic = true;
            if (i % 6 == 0)
            {
                GameObject pc = new GameObject("WallPostCollider");
                pc.transform.SetParent(colRoot, false);
                pc.transform.position = new Vector3(a.x, baseY + (h + 0.5f) * 0.5f, a.y);
                pc.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                MeshCol(pc, new Vector3(1f, h + 0.5f, 1f));
                pc.isStatic = true;
            }
        }
    }
}
