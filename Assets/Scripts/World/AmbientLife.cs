using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class Wind
{
    public static Vector2 Dir = new Vector2(0.85f, 0.5f).normalized;

    public static float Gust(float t)
    {
        return Mathf.Clamp01(0.45f + 0.35f * Mathf.Sin(t * 0.31f) + 0.2f * Mathf.Sin(t * 0.83f + 2f));
    }

    public static Vector3 Vec { get { return new Vector3(Dir.x, 0f, Dir.y); } }
}

[AddComponentMenu("Procedural World/Stages/Ambient Life")]
public class AmbientLifeStage : WorldStage
{

    public override int Order { get { return StageOrder.Vegetation + 70; } }
    public override string StageName { get { return "Ambient Life"; } }

    [Range(0f, 1f)] public float grassWaveStrength = 0.5f;
    public float grassWaveSpeed = 0.6f;
    public float grassWaveAmount = 0.5f;

    public override void Run(WorldContext c)
    {
        if (c.terrain != null && c.terrain.terrainData != null)
        {
            TerrainData td = c.terrain.terrainData;
            td.wavingGrassStrength = grassWaveStrength;
            td.wavingGrassSpeed = grassWaveSpeed;
            td.wavingGrassAmount = grassWaveAmount;
        }
        GameObject go = new GameObject("AmbientLife");
        go.transform.SetParent(c.root, false);
        go.AddComponent<AmbientLifeController>().Init(c, c.LayerRoot(PlacementLayer.Tree));
    }
}

public class AmbientLifeController : MonoBehaviour
{
    public int butterflyCount = 0;

    public static readonly List<Vector3> Chimneys = new List<Vector3>();

    class Ent
    {
        public Transform tr;
        public MeshRenderer mr;
        public float phase, size, age, life, heading, speed;
        public Vector3 off;
    }

    WorldContext ctx;
    Camera cam;
    Mesh quad;
    readonly List<Transform> trees = new List<Transform>();
    readonly List<Quaternion> baseRot = new List<Quaternion>();
    readonly List<float> treePhase = new List<float>();
    readonly List<bool> swaying = new List<bool>();
    readonly List<Ent> leaves = new List<Ent>(), fluff = new List<Ent>(), butter = new List<Ent>(), flies = new List<Ent>(), birds = new List<Ent>(), puffs = new List<Ent>();
    Material[] leafMats, butterMats;
    Material fluffMat, flyMat, birdMat;
    Material smokeMat;
    Color[] leafCols = { new Color(0.55f, 0.65f, 0.2f), new Color(0.85f, 0.55f, 0.15f), new Color(0.55f, 0.35f, 0.15f) };
    Color[] butterCols = { new Color(0.98f, 0.85f, 0.25f), new Color(0.95f, 0.95f, 0.92f), new Color(0.45f, 0.65f, 0.95f) };
    bool butterOn = true, fliesOn = true, birdsOn = true;
    float smokeTimer;
    int puffIndex;
    float[] flockAngle = new float[3];
    float[] flockRadius = { 90f, 120f, 150f };
    float[] flockAlt = { 55f, 70f, 48f };

    public void Init(WorldContext c, Transform treeRoot)
    {
        ctx = c;
        for (int i = 0; i < treeRoot.childCount; i++)
        {
            Transform t = treeRoot.GetChild(i);
            trees.Add(t);
            baseRot.Add(t.rotation);
            treePhase.Add(Random.value * 6.28f);
            swaying.Add(false);
        }
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad = q.GetComponent<MeshFilter>().sharedMesh;
        if (Application.isPlaying) WorldUtil.Kill(q); else DestroyImmediate(q);
        leafMats = new Material[3];
        for (int i = 0; i < 3; i++) leafMats[i] = MakeMat("Leaf" + i, LifeTex.Leaf(), leafCols[i], false);
        butterMats = new Material[3];
        for (int i = 0; i < 3; i++) butterMats[i] = MakeMat("Butterfly" + i, LifeTex.Wing(), butterCols[i], false);
        fluffMat = MakeMat("Fluff", LifeTex.Soft(), new Color(0.97f, 0.97f, 0.95f, 0.85f), false);
        flyMat = MakeMat("Firefly", LifeTex.Soft(), new Color(0.75f, 1f, 0.3f, 0f), true);
        birdMat = MakeMat("Bird", LifeTex.Bird(), new Color(0.08f, 0.08f, 0.1f, 0.9f), false);
        smokeMat = MakeMat("Smoke", LifeTex.Smoke(), new Color(0.75f, 0.75f, 0.75f, 0.3f), false);
        for (int i = 0; i < 45; i++) leaves.Add(Make("Leaf", leafMats[i % 3], 0.22f));
        for (int i = 0; i < 30; i++) fluff.Add(Make("Fluff", fluffMat, 0.12f));
        for (int i = 0; i < butterflyCount; i++) butter.Add(Make("Butterfly", butterMats[i % 3], 0.32f));
        for (int i = 0; i < 40; i++) flies.Add(Make("Firefly", flyMat, 0.35f));
        for (int i = 0; i < 12; i++) birds.Add(Make("Bird", birdMat, 2.6f));
        for (int i = 0; i < 36; i++) { Ent e = Make("Smoke", smokeMat, 1f); e.life = 0f; e.tr.gameObject.SetActive(false); puffs.Add(e); }
        for (int i = 0; i < birds.Count; i++) birds[i].off = new Vector3(Random.Range(-9f, 9f), Random.Range(-3f, 3f), Random.Range(-9f, 9f));
        for (int i = 0; i < 3; i++) flockAngle[i] = Random.value * 6.28f;
    }

    Material MakeMat(string name, Texture2D tex, Color col, bool additive)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        Material m = new Material(sh);
        m.name = name;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        if (m.HasProperty("_Color")) m.SetColor("_Color", col);
        if (m.HasProperty("_Surface"))
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", 5f);
            m.SetFloat("_DstBlend", additive ? 1f : 10f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetShaderPassEnabled("ShadowCaster", false);
        }
        m.renderQueue = 3010;
        return m;
    }

    Ent Make(string name, Material m, float size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = quad;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = m;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        Ent e = new Ent();
        e.tr = go.transform;
        e.mr = mr;
        e.size = size;
        e.phase = Random.value * 6.28f;
        e.heading = Random.value * 6.28f;
        e.speed = Random.Range(0.8f, 1.6f);
        e.tr.localScale = Vector3.one * size;
        e.tr.position = new Vector3(0f, -1000f, 0f);
        e.age = 999f;
        return e;
    }

    float Ground(float x, float z) { return ctx.HeightAt(x, z); }

    Vector3 Around(Vector3 cp, float r, float minY, float maxY)
    {
        float a = Random.value * 6.28f;
        float d = r * Mathf.Sqrt(Random.value);
        float x = cp.x + Mathf.Cos(a) * d, z = cp.z + Mathf.Sin(a) * d;
        return new Vector3(x, Ground(x, z) + Random.Range(minY, maxY), z);
    }

    static void Tint(Material m, Color baseCol, float k, float alpha)
    {
        Color c = new Color(baseCol.r * k, baseCol.g * k, baseCol.b * k, alpha);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    static void SetGroup(List<Ent> list, bool on)
    {
        for (int i = 0; i < list.Count; i++) list[i].tr.gameObject.SetActive(on);
    }

    void Update()
    {
        if (ctx == null) return;
        if (cam == null) { cam = Camera.main; if (cam == null) return; }
        Vector3 cp = cam.transform.position;
        float dt = Time.deltaTime, t = Time.time;
        float night = StylizedVehiclePainter.CurrentNight, day = 1f - night;
        float gust = Wind.Gust(t);
        Vector3 wind = Wind.Vec;
        Quaternion face = cam.transform.rotation;
        float lit = 0.2f + 0.8f * day;
        for (int i = 0; i < 3; i++) Tint(leafMats[i], leafCols[i], lit, 1f);
        Tint(fluffMat, new Color(0.97f, 0.97f, 0.95f), lit, 0.85f);
        Tint(birdMat, new Color(0.08f, 0.08f, 0.1f), 1f, 0.9f);
        Tint(smokeMat, new Color(0.75f, 0.75f, 0.75f), lit, 1f);
        for (int i = 0; i < 3; i++) Tint(butterMats[i], butterCols[i], lit, 1f);
        Tint(flyMat, new Color(0.75f, 1f, 0.3f), 1f, Mathf.Clamp01((night - 0.35f) * 2f) * 0.9f);

        Trees(cp, t, gust, wind);
        Leaves(cp, t, dt, gust, wind, face);
        Fluff(cp, t, dt, gust, wind, face);

        bool bOn = day > 0.65f;
        if (bOn != butterOn) { butterOn = bOn; SetGroup(butter, bOn); }
        if (bOn != birdsOn) { birdsOn = bOn; SetGroup(birds, bOn); }
        bool fOn = night > 0.35f;
        if (fOn != fliesOn) { fliesOn = fOn; SetGroup(flies, fOn); }
        if (bOn) { Butterflies(cp, t, dt, face); Birds(cp, t, dt, face); }
        if (fOn) Fireflies(cp, t, dt, face);
        Smoke(cp, t, dt, gust, wind, face);
    }

    void Trees(Vector3 cp, float t, float gust, Vector3 wind)
    {
        Vector3 axis = Vector3.Cross(Vector3.up, wind);
        for (int i = 0; i < trees.Count; i++)
        {
            Transform tr = trees[i];
            if (tr == null) continue;
            Vector3 d = tr.position - cp;
            if (d.x * d.x + d.z * d.z > 16000f)
            {
                if (swaying[i]) { tr.rotation = baseRot[i]; swaying[i] = false; }
                continue;
            }
            float ph = treePhase[i];
            float a = (0.4f + gust * 1.4f) * Mathf.Sin(t * 1.4f + ph) * 0.5f + Mathf.Sin(t * 2.7f + ph * 2f) * 0.15f * gust + gust * 1.2f;
            tr.rotation = Quaternion.AngleAxis(a, axis) * baseRot[i];
            swaying[i] = true;
        }
    }

    void Leaves(Vector3 cp, float t, float dt, float gust, Vector3 wind, Quaternion face)
    {
        for (int i = 0; i < leaves.Count; i++)
        {
            Ent e = leaves[i];
            Vector3 p = e.tr.position;
            Vector3 v = wind * (1.5f + gust * 3f) + Vector3.down * 0.6f + new Vector3(Mathf.Sin(t * 1.7f + e.phase), 0f, Mathf.Cos(t * 1.3f + e.phase)) * 0.6f;
            p += v * dt;
            Vector3 dd = p - cp;
            if (e.age > 100f || dd.sqrMagnitude > 2200f || p.y < Ground(p.x, p.z) + 0.05f)
            {
                p = Around(cp, 35f, 2.5f, 9f) - wind * 8f;
                e.age = 0f;
            }
            e.age += dt;
            e.tr.position = p;
            e.tr.rotation = face * Quaternion.Euler(0f, 0f, t * 70f * (e.phase - 3f) * 0.3f + e.phase * 57f);
        }
    }

    void Fluff(Vector3 cp, float t, float dt, float gust, Vector3 wind, Quaternion face)
    {
        for (int i = 0; i < fluff.Count; i++)
        {
            Ent e = fluff[i];
            Vector3 p = e.tr.position;
            p += (wind * (0.6f + gust * 1.2f) + new Vector3(0f, Mathf.Sin(t * 0.8f + e.phase) * 0.35f, 0f)) * dt;
            e.age += dt;
            float g = Ground(p.x, p.z);
            if (e.age > 25f || (p - cp).sqrMagnitude > 1600f || p.y < g + 0.2f || p.y > g + 6f)
            {
                p = Around(cp, 30f, 0.8f, 4f);
                e.age = 0f;
            }
            e.tr.position = p;
            e.tr.rotation = face;
        }
    }

    void Butterflies(Vector3 cp, float t, float dt, Quaternion face)
    {
        for (int i = 0; i < butter.Count; i++)
        {
            Ent e = butter[i];
            Vector3 p = e.tr.position;
            e.heading += (Mathf.PerlinNoise(t * 0.5f + e.phase, e.phase) - 0.5f) * 5f * dt;
            p += new Vector3(Mathf.Cos(e.heading), 0f, Mathf.Sin(e.heading)) * e.speed * dt;
            float g = Ground(p.x, p.z);
            float target = g + 1f + 0.5f * Mathf.Sin(t * 2f + e.phase);
            p.y = Mathf.Lerp(p.y, target, dt * 2f);
            if ((p - cp).sqrMagnitude > 1100f || e.age > 100f)
            {
                p = Around(cp, 25f, 0.8f, 2f);
                e.age = 0f;
            }
            e.age += dt;
            e.tr.position = p;
            e.tr.rotation = face;
            float flap = 0.25f + 0.75f * Mathf.Abs(Mathf.Sin(t * 14f + e.phase));
            e.tr.localScale = new Vector3(e.size * flap, e.size, 1f);
        }
    }

    void Fireflies(Vector3 cp, float t, float dt, Quaternion face)
    {
        bool nearPond = PondStage.HasPond && (PondStage.Center - cp).sqrMagnitude < 14400f;
        for (int i = 0; i < flies.Count; i++)
        {
            Ent e = flies[i];
            Vector3 p = e.tr.position;
            p += new Vector3(Mathf.Sin(t * 0.6f + e.phase), Mathf.Sin(t * 0.9f + e.phase * 2f) * 0.4f, Mathf.Cos(t * 0.5f + e.phase * 1.3f)) * 0.5f * dt;
            float g = Ground(p.x, p.z);
            if ((p - cp).sqrMagnitude > 1400f || e.age > 100f || p.y < g + 0.3f)
            {
                Vector3 centre = nearPond && (i & 1) == 0 ? PondStage.Center : cp;
                p = Around(centre, nearPond && (i & 1) == 0 ? 22f : 30f, 0.5f, 2.5f);
                if (centre != cp) p.y = Mathf.Max(p.y, PondStage.Center.y + 0.5f);
                e.age = 0f;
            }
            e.age += dt;
            e.tr.position = p;
            e.tr.rotation = face;
            float pulse = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(t * 2.2f + e.phase * 3f));
            e.tr.localScale = Vector3.one * e.size * pulse;
        }
    }

    void Birds(Vector3 cp, float t, float dt, Quaternion face)
    {
        for (int f = 0; f < 3; f++) flockAngle[f] += dt * (0.05f + f * 0.01f) * (f == 1 ? -1f : 1f);
        for (int i = 0; i < birds.Count; i++)
        {
            Ent e = birds[i];
            int f = i % 3;
            float a = flockAngle[f];
            Vector3 centre = cp + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * flockRadius[f];
            centre.y = Ground(centre.x, centre.z) + flockAlt[f];
            Vector3 tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * (f == 1 ? -1f : 1f);
            Vector3 p = centre + e.off + new Vector3(0f, Mathf.Sin(t * 0.7f + e.phase) * 1.5f, 0f);
            e.tr.position = p;
            e.tr.rotation = face;
            float side = Mathf.Sign(Vector3.Dot(tangent, cam.transform.right) + 0.0001f);
            float flap = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(t * 5f + e.phase));
            e.tr.localScale = new Vector3(e.size * side, e.size * 0.5f * flap, 1f);
        }
    }

    void Smoke(Vector3 cp, float t, float dt, float gust, Vector3 wind, Quaternion face)
    {
        smokeTimer -= dt;
        if (smokeTimer <= 0f && Chimneys.Count > 0)
        {
            smokeTimer = 0.18f;
            for (int tries = 0; tries < 6; tries++)
            {
                int ci = Random.Range(0, Chimneys.Count);
                if ((ci & 1) == 1) continue;
                Vector3 cpos = Chimneys[ci];
                if ((cpos - cp).sqrMagnitude > 16000f) continue;
                Ent e = puffs[puffIndex];
                puffIndex = (puffIndex + 1) % puffs.Count;
                e.tr.position = cpos + new Vector3(Random.Range(-0.1f, 0.1f), 0f, Random.Range(-0.1f, 0.1f));
                e.age = 0f;
                e.life = Random.Range(5f, 7.5f);
                e.tr.gameObject.SetActive(true);
                break;
            }
        }
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        float lit = 0.2f + 0.8f * (1f - StylizedVehiclePainter.CurrentNight);
        for (int i = 0; i < puffs.Count; i++)
        {
            Ent e = puffs[i];
            if (!e.tr.gameObject.activeSelf) continue;
            e.age += dt;
            if (e.age >= e.life) { e.tr.gameObject.SetActive(false); continue; }
            float k = e.age / e.life;
            e.tr.position += (Vector3.up * 0.9f + wind * (0.4f + gust * 1.2f * k)) * dt;
            e.tr.rotation = face * Quaternion.Euler(0f, 0f, e.phase * 50f + e.age * 8f);
            e.tr.localScale = Vector3.one * Mathf.Lerp(0.5f, 2.2f, k);
            float a = Mathf.Pow(1f - k, 1.5f) * 0.35f * Mathf.Clamp01(k * 8f);
            mpb.SetColor("_BaseColor", new Color(0.78f * lit, 0.78f * lit, 0.78f * lit, a));
            e.mr.SetPropertyBlock(mpb);
        }
    }
}

public static class LifeTex
{
    static Texture2D Build(int w, int h, System.Func<float, float, Color> f)
    {
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++) px[y * w + x] = f(x / (w - 1f), y / (h - 1f));
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, true, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.SetPixels(px);
        t.Apply(true, false);
        return t;
    }

    public static Texture2D Soft()
    {
        return Build(32, 32, (u, v) =>
        {
            float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
            float a = Mathf.Clamp01(1f - d);
            return new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
        });
    }

    public static Texture2D Leaf()
    {
        return Build(32, 32, (u, v) =>
        {
            float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f;
            float inside = 1f - (x * x * 0.55f + y * y * 2.6f);
            float a = Mathf.Clamp01(inside * 6f);
            float rib = Mathf.Abs(y) < 0.04f ? 0.75f : 1f;
            return new Color(rib, rib, rib, a);
        });
    }

    public static Texture2D Wing()
    {
        return Build(64, 32, (u, v) =>
        {
            float best = 0f;
            for (int s = 0; s < 2; s++)
            {
                float cx = s == 0 ? 0.28f : 0.72f;
                float cy = v > 0.5f ? 0.62f : 0.4f;
                float rx = 0.24f, ry = v > 0.5f ? 0.34f : 0.24f;
                float d = ((u - cx) / rx) * ((u - cx) / rx) + ((v - 0.5f - (cy - 0.5f)) / ry) * ((v - 0.5f - (cy - 0.5f)) / ry);
                best = Mathf.Max(best, Mathf.Clamp01((1f - d) * 5f));
            }
            bool body = Mathf.Abs(u - 0.5f) < 0.025f && v > 0.2f && v < 0.8f;
            float k = body ? 0.2f : 1f;
            return new Color(k, k, k, body ? 1f : best);
        });
    }

    public static Texture2D Bird()
    {
        return Build(64, 32, (u, v) =>
        {
            float w = Mathf.Abs(2f * u - 1f);
            float yc = 0.3f + 0.45f * w - 0.25f * w * w;
            float th = 0.05f + 0.07f * (1f - w);
            float a = Mathf.Clamp01((th - Mathf.Abs(v - yc)) / 0.03f);
            return new Color(1f, 1f, 1f, a);
        });
    }

    public static Texture2D Smoke()
    {
        return Build(64, 64, (u, v) =>
        {
            float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
            float fall = Mathf.Clamp01(1f - d);
            fall = fall * fall * (3f - 2f * fall);
            float n = 0.6f + 0.4f * Mathf.PerlinNoise(u * 4f + 3f, v * 4f + 7f);
            return new Color(1f, 1f, 1f, fall * n);
        });
    }
}
