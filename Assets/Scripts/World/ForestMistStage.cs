using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Procedural World/Stages/Forest Mist")]
public class ForestMistStage : WorldStage
{
    public float spacing = 34f;
    public float deepRadius = 38f;
    public int maxPatches = 320;
    public Vector2 patchSize = new Vector2(46f, 80f);
    public Vector2 layerHeights = new Vector2(0.35f, 1.15f);
    [Range(0f, 1f)] public float opacity = 0.2f;
    public float driftSpeed = 1.6f;
    public Color mistColor = new Color(0.80f, 0.87f, 0.90f, 1f);
    [Range(0f, 1f)] public float maxHeight01 = 0.34f;
    [Range(0f, 1f)] public float maxSlope = 0.5f;

    static readonly List<Material> mistMaterials = new List<Material>();
    static Color baseColor;
    static float baseOpacity;
    static Texture2D mistTexture;
    static float currentNight;

    public override int Order { get { return StageOrder.Vegetation + 50; } }
    public override string StageName { get { return "Forest Mist"; } }

    public static void ApplyNight(float night)
    {
        currentNight = night;
        float dim = Mathf.Lerp(1f, 0.22f, Mathf.Clamp01(night));
        for (int i = 0; i < mistMaterials.Count; i++)
        {
            Material m = mistMaterials[i];
            if (m == null) continue;
            Color col = new Color(baseColor.r * dim, baseColor.g * dim, baseColor.b * dim, baseOpacity);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
        }
    }

    static Texture2D BuildTexture()
    {
        if (mistTexture != null) return mistTexture;
        int size = 128;
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, true, false);
        t.name = "ForestMistTexture";
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = x / (size - 1f) * 2f - 1f;
            float v = y / (size - 1f) * 2f - 1f;
            float r = Mathf.Sqrt(u * u + v * v);
            float fall = Mathf.Clamp01(1f - r);
            fall = fall * fall * (3f - 2f * fall);
            float n = 0.55f * Mathf.PerlinNoise(x * 0.045f + 11f, y * 0.045f + 5f)
                    + 0.30f * Mathf.PerlinNoise(x * 0.11f + 40f, y * 0.11f + 70f)
                    + 0.15f * Mathf.PerlinNoise(x * 0.25f + 90f, y * 0.25f + 20f);
            float a = Mathf.Clamp01((n - 0.22f) * 1.7f) * fall;
            px[y * size + x] = new Color(1f, 1f, 1f, a);
        }
        t.SetPixels(px);
        t.Apply(true, false);
        mistTexture = t;
        return t;
    }

    Material MakeMaterial()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Unlit/Transparent");
        Material m = new Material(sh);
        m.name = "ForestMist";
        Texture2D tex = BuildTexture();
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_Surface"))
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", 5f);
            m.SetFloat("_DstBlend", 10f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetShaderPassEnabled("ShadowCaster", false);
        }
        m.renderQueue = 3000;
        mistMaterials.Add(m);
        return m;
    }

    public override void Run(WorldContext c)
    {
        mistMaterials.Clear();
        baseColor = mistColor;
        baseOpacity = opacity;
        if (c.zoneMap == null) return;

        Material matLow = MakeMaterial();
        Material matHigh = MakeMaterial();
        ApplyNight(currentNight);

        GameObject rootGo = new GameObject("ForestMist");
        rootGo.transform.SetParent(c.root, false);
        ForestMistDrift drift = rootGo.AddComponent<ForestMistDrift>();

        GameObject quadSource = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Mesh quad = quadSource.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(quadSource);

        System.Random rng = new System.Random(c.settings.seed + 7919);
        float half = c.Size * 0.5f;
        float maxH = Mathf.Max(1f, c.settings.maxTerrainHeight);
        int count = 0;
        List<Vector2> candidates = new List<Vector2>();
        for (float z = -half + spacing; z < half - spacing; z += spacing)
        for (float x = -half + spacing; x < half - spacing; x += spacing)
        {
            float jx = (float)(rng.NextDouble() - 0.5) * spacing * 0.8f;
            float jz = (float)(rng.NextDouble() - 0.5) * spacing * 0.8f;
            candidates.Add(new Vector2(x + jx, z + jz));
        }
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            Vector2 tmp = candidates[i]; candidates[i] = candidates[j]; candidates[j] = tmp;
        }

        for (int k = 0; k < candidates.Count && count < maxPatches; k++)
        {
            Vector2 p = candidates[k];
            if (c.zoneMap.ZoneAt(p) != ZoneType.Forest) continue;
            if (!IsDeep(c, p)) continue;
            float h = c.HeightAt(p.x, p.y);
            if (h / maxH > maxHeight01) continue;
            float gx = c.HeightAt(p.x + 3f, p.y) - c.HeightAt(p.x - 3f, p.y);
            float gz = c.HeightAt(p.x, p.y + 3f) - c.HeightAt(p.x, p.y - 3f);
            if (new Vector2(gx, gz).magnitude / 6f > maxSlope) continue;
            if (c.occupancy != null && !c.occupancy.IsFree(OrientedBox.FromBase(new Vector3(p.x, h, p.y), new Vector3(6f, 1f, 6f), 0f), PlacementLayer.Road, 0f)) continue;

            for (int layer = 0; layer < 2; layer++)
            {
                float size = Mathf.Lerp(patchSize.x, patchSize.y, (float)rng.NextDouble());
                float yaw = (float)rng.NextDouble() * 360f;
                float yOff = layer == 0 ? layerHeights.x : layerHeights.y;
                GameObject go = new GameObject("MistPatch");
                go.transform.SetParent(rootGo.transform, false);
                go.transform.position = new Vector3(p.x + layer * 6f, h + yOff + (float)rng.NextDouble() * 0.2f, p.y - layer * 5f);
                go.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
                go.transform.localScale = new Vector3(size, size, 1f);
                go.AddComponent<MeshFilter>().sharedMesh = quad;
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = layer == 0 ? matLow : matHigh;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                float dir = (rng.NextDouble() < 0.5 ? -1f : 1f) * (0.5f + (float)rng.NextDouble());
                drift.Add(go.transform, dir * driftSpeed * (layer == 0 ? 1f : -0.7f));
            }
            count++;
        }
        Debug.Log("Forest mist: " + count + " deep-woods patches.");
    }

    bool IsDeep(WorldContext c, Vector2 p)
    {
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 0.25f;
            Vector2 q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * deepRadius;
            if (c.zoneMap.ZoneAt(q) != ZoneType.Forest) return false;
        }
        return true;
    }
}

public class ForestMistDrift : MonoBehaviour
{
    readonly List<Transform> patches = new List<Transform>();
    readonly List<float> speeds = new List<float>();
    readonly List<MeshRenderer> rends = new List<MeshRenderer>();
    readonly List<float> alphas = new List<float>();
    MaterialPropertyBlock mpb;

    // Walk-through fade: a patch melts away as the camera nears its rim and returns as you move off, so you never see
    // a patch edge or the quad cutting through the world. Distances are in patch radii (1 = the quad's edge).
    public float fadeOutAt = 0.85f, fadeInAt = 1.35f, fadeSpeed = 1.5f;

    public void Add(Transform t, float degreesPerSecond)
    {
        patches.Add(t);
        speeds.Add(degreesPerSecond);
        rends.Add(t.GetComponent<MeshRenderer>());
        alphas.Add(1f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        Camera cam = Camera.main;
        Vector3 cp = cam != null ? cam.transform.position : Vector3.zero;
        float udt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        if (mpb == null) mpb = new MaterialPropertyBlock();
        for (int i = 0; i < patches.Count; i++)
        {
            Transform t = patches[i];
            if (t == null) continue;
            t.Rotate(0f, 0f, speeds[i] * dt, Space.Self);
            MeshRenderer r = rends[i];
            if (cam == null || r == null) continue;
            Vector3 d3 = t.position - cp;
            float radius = Mathf.Max(0.5f, t.localScale.x * 0.5f);
            float d = Mathf.Sqrt(d3.x * d3.x + d3.z * d3.z) / radius;
            float want = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeOutAt, fadeInAt, d));
            float a = Mathf.MoveTowards(alphas[i], want, fadeSpeed * udt);
            alphas[i] = a;
            bool vis = a > 0.01f;
            if (r.enabled != vis) r.enabled = vis;
            if (!vis) continue;
            Material m = r.sharedMaterial;   // keeps the night-time dimming that ApplyNight writes to the material
            Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            c.a *= a;
            mpb.SetColor("_BaseColor", c); mpb.SetColor("_Color", c);
            r.SetPropertyBlock(mpb);
        }
    }
}
