using UnityEngine;
using UnityEngine.Rendering;

[AddComponentMenu("Procedural World/Stages/Valley Fog")]
public class ValleyFogStage : WorldStage
{
    public float gridStep = 8f;
    public int textureSize = 384;
    public float reach = 110f;
    public float highStart = 0.27f;
    public float highEnd = 0.42f;
    [Range(0f, 1f)] public float forestFog = 0.8f;
    public float exclusionRadius = 130f;
    [Range(0f, 1f)] public float opacity = 0.5f;
    public Vector2 layerLift = new Vector2(0.7f, 2.5f);
    [Range(0f, 1f)] public float fadeStartHeight = 0.2f;
    [Range(0f, 1f)] public float fadeEndHeight = 0.42f;
    public float noiseScale = 0.011f;
    public float swayDistance = 3f;

    public override int Order { get { return StageOrder.Vegetation + 60; } }
    public override string StageName { get { return "Valley Fog"; } }

    static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / Mathf.Max(0.0001f, b - a));
        return t * t * (3f - 2f * t);
    }

    static float[] Blur(float[] src, int n, int radius)
    {
        float[] a = (float[])src.Clone();
        float[] b = new float[a.Length];
        for (int pass = 0; pass < 2; pass++)
        {
            for (int y = 0; y < n; y++)
            {
                float sum = 0f;
                int cnt = 0;
                for (int x = -radius; x <= radius; x++)
                {
                    int xx = Mathf.Clamp(x, 0, n - 1);
                    sum += a[y * n + xx];
                    cnt++;
                }
                for (int x = 0; x < n; x++)
                {
                    b[y * n + x] = sum / cnt;
                    int add = Mathf.Clamp(x + radius + 1, 0, n - 1);
                    int rem = Mathf.Clamp(x - radius, 0, n - 1);
                    sum += a[y * n + add] - a[y * n + rem];
                }
            }
            for (int x = 0; x < n; x++)
            {
                float sum = 0f;
                int cnt = 0;
                for (int y = -radius; y <= radius; y++)
                {
                    int yy = Mathf.Clamp(y, 0, n - 1);
                    sum += b[yy * n + x];
                    cnt++;
                }
                for (int y = 0; y < n; y++)
                {
                    a[y * n + x] = sum / cnt;
                    int add = Mathf.Clamp(y + radius + 1, 0, n - 1);
                    int rem = Mathf.Clamp(y - radius, 0, n - 1);
                    sum += b[add * n + x] - b[rem * n + x];
                }
            }
        }
        return a;
    }

    Material MakeMaterial(Texture2D tex)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Unlit/Transparent");
        Material m = new Material(sh);
        m.name = "ValleyFog";
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
        m.renderQueue = 3001;
        return m;
    }

    public override void Run(WorldContext c)
    {
        if (c.zoneMap == null || c.zones == null) return;
        int n = Mathf.Clamp(textureSize, 128, 768);
        float size = c.Size;
        float texelM = size / (n - 1);
        float maxH = Mathf.Max(1f, c.settings.maxTerrainHeight);

        float[] mountain = new float[n * n];
        float[] built = new float[n * n];
        float[] height01 = new float[n * n];
        float[] forest = new float[n * n];
        float[] wide = new float[n * n];
        for (int j = 0; j < n; j++)
        for (int i = 0; i < n; i++)
        {
            float x = i * texelM - size * 0.5f;
            float z = j * texelM - size * 0.5f;
            ZoneType zt = c.zoneMap.ZoneAt(x, z);
            int k = j * n + i;
            mountain[k] = zt == ZoneType.Mountain ? 1f : 0f;
            built[k] = zt == ZoneType.Suburb || zt == ZoneType.City ? 1f : (zt == ZoneType.Highway ? 0.55f : 0f);
            height01[k] = c.HeightAt(x, z) / maxH;
            forest[k] = zt == ZoneType.Forest ? 1f : 0f;
            wide[k] = zt == ZoneType.Suburb || zt == ZoneType.City ? 1f : 0f;
        }
        mountain = Blur(mountain, n, Mathf.Max(2, Mathf.RoundToInt(reach / texelM)));
        built = Blur(built, n, Mathf.Max(1, Mathf.RoundToInt(18f / texelM)));
        wide = Blur(wide, n, Mathf.Max(2, Mathf.RoundToInt(exclusionRadius / texelM)));
        forest = Blur(forest, n, Mathf.Max(1, Mathf.RoundToInt(25f / texelM)));

        GameObject rootGo = new GameObject("ValleyFog");
        rootGo.transform.SetParent(c.root, false);
        ValleyFogDriver driver = rootGo.AddComponent<ValleyFogDriver>();
        float[] lifts = { layerLift.x, layerLift.y };
        int total = 0;

        for (int layer = 0; layer < 2; layer++)
        {
            float[] alpha = new float[n * n];
            Color[] px = new Color[n * n];
            float off = layer * 211f;
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int k = j * n + i;
                float x = i * texelM - size * 0.5f;
                float z = j * texelM - size * 0.5f;
                float noise = NoiseLib.Fbm(x * noiseScale + 31f + off, z * noiseScale + 77f + off, 4, 2f, 0.5f);
                float shape = Mathf.Lerp(0.15f, 1f, Smooth(0.30f, 0.68f, noise));
                float edge = Smooth(0f, 40f, Mathf.Min(Mathf.Min(x + size * 0.5f, size * 0.5f - x), Mathf.Min(z + size * 0.5f, size * 0.5f - z)));
                float fogMask = Mathf.Max(forest[k] * forestFog, mountain[k] * Smooth(highStart, highEnd, height01[k]));
                float a = fogMask * shape * (1f - built[k]) * (1f - Smooth(0.02f, 0.35f, wide[k])) * edge;
                if (layer == 1) a *= 0.7f;
                alpha[k] = a;
                px[k] = new Color(1f, 1f, 1f, a);
            }
            Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, true, false);
            tex.name = "ValleyFogTexture" + layer;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixels(px);
            tex.Apply(true, false);

            int g = Mathf.Max(8, Mathf.RoundToInt(size / gridStep)) + 1;
            float step = size / (g - 1);
            Vector3[] verts = new Vector3[g * g];
            Vector2[] uvs = new Vector2[g * g];
            bool[] live = new bool[g * g];
            float h2 = step * 0.5f;
            for (int j = 0; j < g; j++)
            for (int i = 0; i < g; i++)
            {
                float x = i * step - size * 0.5f;
                float z = j * step - size * 0.5f;
                float hy = Mathf.Max(Mathf.Max(c.HeightAt(x, z), c.HeightAt(x + h2, z)), Mathf.Max(c.HeightAt(x - h2, z), Mathf.Max(c.HeightAt(x, z + h2), c.HeightAt(x, z - h2))));
                verts[j * g + i] = new Vector3(x, hy + lifts[layer], z);
                uvs[j * g + i] = new Vector2(i / (g - 1f), j / (g - 1f));
                int ti = Mathf.Clamp(Mathf.RoundToInt(uvs[j * g + i].x * (n - 1)), 0, n - 1);
                int tj = Mathf.Clamp(Mathf.RoundToInt(uvs[j * g + i].y * (n - 1)), 0, n - 1);
                float best = 0f;
                for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                    best = Mathf.Max(best, alpha[Mathf.Clamp(tj + dj * 3, 0, n - 1) * n + Mathf.Clamp(ti + di * 3, 0, n - 1)]);
                live[j * g + i] = best > 0.004f;
            }
            System.Collections.Generic.List<int> tris = new System.Collections.Generic.List<int>();
            for (int j = 0; j < g - 1; j++)
            for (int i = 0; i < g - 1; i++)
            {
                int a0 = j * g + i, a1 = a0 + 1, a2 = a0 + g, a3 = a2 + 1;
                if (!live[a0] && !live[a1] && !live[a2] && !live[a3]) continue;
                tris.Add(a0); tris.Add(a2); tris.Add(a1);
                tris.Add(a1); tris.Add(a2); tris.Add(a3);
            }
            if (tris.Count == 0) continue;
            Mesh mesh = new Mesh();
            mesh.name = "ValleyFogMesh" + layer;
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            GameObject go = new GameObject("FogLayer" + layer);
            go.transform.SetParent(rootGo.transform, false);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            Material mat = MakeMaterial(tex);
            if (layer == 1) mat.renderQueue = 3002;
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            driver.Add(go.transform, mat, layer == 0 ? 1f : 0.8f, layer * 2.1f, swayDistance * (layer == 0 ? 1f : 0.7f));
            total += tris.Count / 3;
        }
        driver.opacity = opacity;
        Debug.Log("Valley fog: " + total + " triangles over the mountain foothills.");
    }
}

public class ValleyFogDriver : MonoBehaviour
{
    public float opacity = 0.5f;
    public System.Collections.Generic.List<Transform> layers = new System.Collections.Generic.List<Transform>();
    public System.Collections.Generic.List<Material> mats = new System.Collections.Generic.List<Material>();
    public System.Collections.Generic.List<float> weights = new System.Collections.Generic.List<float>();
    public System.Collections.Generic.List<float> phases = new System.Collections.Generic.List<float>();
    public System.Collections.Generic.List<float> sways = new System.Collections.Generic.List<float>();

    public void Add(Transform t, Material m, float weight, float phase, float sway)
    {
        layers.Add(t);
        mats.Add(m);
        weights.Add(weight);
        phases.Add(phase);
        sways.Add(sway);
    }

    void Update()
    {
        Color fog = RenderSettings.fogColor;
        Color tint = Color.Lerp(fog, Color.white, 0.2f * Mathf.Clamp01(fog.grayscale * 1.6f));
        float t = Time.time;
        for (int i = 0; i < layers.Count; i++)
        {
            if (layers[i] == null || mats[i] == null) continue;
            float breathe = 0.9f + 0.1f * Mathf.Sin(t * 0.09f + phases[i]);
            Color col = new Color(tint.r, tint.g, tint.b, opacity * weights[i] * breathe);
            if (mats[i].HasProperty("_BaseColor")) mats[i].SetColor("_BaseColor", col);
            if (mats[i].HasProperty("_Color")) mats[i].SetColor("_Color", col);
            float s = sways[i];
            layers[i].position = new Vector3(Mathf.Sin(t * 0.045f + phases[i]) * s, Mathf.Sin(t * 0.08f + phases[i]) * 0.1f, Mathf.Cos(t * 0.037f + phases[i]) * s);
        }
    }
}
