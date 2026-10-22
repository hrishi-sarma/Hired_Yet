using System;
using UnityEngine;

/// <summary>
/// Soft, low forest mist made from transparent textured mesh wisps (not particle-system
/// billboards). This avoids green square particle planes and ParticleSystem curve-mode errors.
/// </summary>
[DisallowMultipleComponent]
public sealed class ForestSurfaceMist : MonoBehaviour
{
    [Range(0.03f, 0.30f)] public float opacity = 0.12f;
    [Min(18f)] public float patchSpacing = 52f;
    [Range(1, 36)] public int maxPatches = 22;
    [Min(0.05f)] public float heightAboveGround = 0.28f;
    public Vector2 patchSize = new Vector2(22f, 14f);
    public Color mistColor = new Color(0.68f, 0.76f, 0.72f, 0.42f);
    [Min(0f)] public float driftSpeed = 0.22f;

    WorldGenerator generator;
    WorldContext builtFor;
    Transform patchRoot;
    Material mistMaterial;
    Texture2D mistTexture;
    float nextSearch;
    int generation;

    // Walk-through fade: a wisp melts away as the camera approaches its edge and comes back as you leave,
    // so you never see its hard edge or its geometry cutting into the world. d is distance in "wisp radii" (1 = the visible rim).
    [Min(0.5f)] public float fadeOutAt = 1.0f;    // fully gone at or inside this distance
    [Min(0.6f)] public float fadeInAt = 1.7f;     // fully visible beyond this distance
    [Min(0.1f)] public float fadeSpeed = 1.6f;    // alpha units per second (smooths the transition)
    readonly System.Collections.Generic.List<MeshRenderer> wispRenderers = new System.Collections.Generic.List<MeshRenderer>();
    readonly System.Collections.Generic.List<float> wispAlpha = new System.Collections.Generic.List<float>();
    MaterialPropertyBlock mpb;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        // Remove orphaned objects created by the previous particle-plane implementation.
        GameObject legacyRoot = GameObject.Find("Forest Surface Mist Patches");
        if (legacyRoot != null) DestroyLegacyObject(legacyRoot);
        ParticleSystem[] oldParticles = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
        for (int i = 0; i < oldParticles.Length; i++)
        {
            if (oldParticles[i] != null && oldParticles[i].gameObject.name.StartsWith("ForestMistPatch", StringComparison.Ordinal))
                DestroyLegacyObject(oldParticles[i].gameObject);
        }

        GameObject go = GameObject.Find("Forest Surface Mist (Auto)");
        if (go == null) go = new GameObject("Forest Surface Mist (Auto)");
        if (go.GetComponent<ForestSurfaceMist>() == null) go.AddComponent<ForestSurfaceMist>();
        DontDestroyOnLoad(go);
    }

    static void DestroyLegacyObject(GameObject go)
    {
        if (go == null) return;
        if (Application.isPlaying) WorldUtil.Kill(go);
        else DestroyImmediate(go);
    }

    void Update()
    {
        if (generator == null)
        {
            if (Time.unscaledTime < nextSearch) return;
            nextSearch = Time.unscaledTime + 0.5f;
            generator = FindFirstObjectByType<WorldGenerator>();
            if (generator == null) return;
        }

        WorldContext context = generator.Context;
        if (context == null || context.zoneMap == null) return;
        if (builtFor != context)
        {
            ClearPatches();
            BuildPatches(context);
            builtFor = context;
        }

        if (patchRoot == null) return;
        for (int i = 0; i < patchRoot.childCount; i++)
        {
            Transform patch = patchRoot.GetChild(i);
            float phase = patch.GetInstanceID() * 0.0017f;
            Vector3 p = patch.localPosition;
            p.x += Mathf.Sin(Time.time * 0.13f + phase) * driftSpeed * Time.deltaTime;
            p.z += Mathf.Cos(Time.time * 0.10f + phase * 1.7f) * driftSpeed * Time.deltaTime;
            patch.localPosition = p;
        }
        FadeNearCamera();
    }

    void FadeNearCamera()
    {
        Camera cam = Camera.main;
        if (cam == null || wispRenderers.Count == 0) return;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        Vector3 cp = cam.transform.position;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        for (int i = 0; i < wispRenderers.Count; i++)
        {
            MeshRenderer r = wispRenderers[i];
            if (r == null) continue;
            Vector3 lp = r.transform.InverseTransformPoint(cp);
            Vector3 ex = r.localBounds.extents;
            float d = Mathf.Sqrt((lp.x * lp.x) / (ex.x * ex.x) + (lp.z * lp.z) / (ex.z * ex.z));
            float want = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeOutAt, fadeInAt, d));
            float a = Mathf.MoveTowards(wispAlpha[i], want, fadeSpeed * dt);
            wispAlpha[i] = a;
            bool vis = a > 0.01f;
            if (r.enabled != vis) r.enabled = vis;
            if (!vis) continue;
            Color c = mistColor; c.a *= a;
            mpb.SetColor("_BaseColor", c); mpb.SetColor("_Color", c);
            r.SetPropertyBlock(mpb);
        }
    }

    void BuildPatches(WorldContext context)
    {
        patchRoot = new GameObject("Forest Surface Mist Wisps").transform;
        patchRoot.SetParent(context.root, false);
        if (mistTexture == null) mistTexture = CreateMistTexture();
        if (mistMaterial == null) mistMaterial = CreateMistMaterial();

        System.Random rng = new System.Random(context.settings.seed ^ 0x2B41);
        float half = context.Size * 0.5f;
        float step = Mathf.Max(18f, patchSpacing);
        int made = 0;
        for (float z = -half + step; z < half - step && made < maxPatches; z += step)
        {
            for (float x = -half + step; x < half - step && made < maxPatches; x += step)
            {
                float px = x + (float)(rng.NextDouble() - 0.5) * step * 0.7f;
                float pz = z + (float)(rng.NextDouble() - 0.5) * step * 0.7f;
                if (context.zoneMap.ZoneAt(px, pz) != ZoneType.Forest) continue;
                if (context.zoneMap.ZoneAt(px + 9f, pz) != ZoneType.Forest ||
                    context.zoneMap.ZoneAt(px - 9f, pz) != ZoneType.Forest ||
                    context.zoneMap.ZoneAt(px, pz + 9f) != ZoneType.Forest ||
                    context.zoneMap.ZoneAt(px, pz - 9f) != ZoneType.Forest) continue;

                float ground = context.HeightAt(px, pz);
                float normalizedHeight = ground / Mathf.Max(1f, context.settings.maxTerrainHeight);
                if (normalizedHeight > 0.40f) continue;

                GameObject go = new GameObject("ForestMist_Wisp_" + made);
                go.transform.SetParent(patchRoot, false);
                go.transform.localPosition = new Vector3(px, ground + heightAboveGround, pz);
                go.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                float scale = Mathf.Lerp(0.72f, 1.28f, (float)rng.NextDouble());
                go.transform.localScale = new Vector3(scale, 1f, scale);

                MeshFilter filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = CreateQuadMesh(
                    patchSize.x * Mathf.Lerp(0.78f, 1.25f, (float)rng.NextDouble()),
                    patchSize.y * Mathf.Lerp(0.78f, 1.25f, (float)rng.NextDouble()));
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = mistMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                wispRenderers.Add(renderer); wispAlpha.Add(1f);
                made++;
            }
        }
    }

    Mesh CreateQuadMesh(float width, float depth)
    {
        Mesh mesh = new Mesh { name = "ForestMistWispMesh" };
        mesh.vertices = new Vector3[] {
            new Vector3(-width * .5f, 0f, -depth * .5f), new Vector3(-width * .5f, 0f, depth * .5f),
            new Vector3(width * .5f, 0f, depth * .5f), new Vector3(width * .5f, 0f, -depth * .5f)
        };
        mesh.uv = new Vector2[] { new Vector2(0,0), new Vector2(0,1), new Vector2(1,1), new Vector2(1,0) };
        // Both sides are visible, so camera angles cannot make a wisp disappear.
        mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3, 2, 1, 0, 3, 2, 0 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    Texture2D CreateMistTexture()
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        tex.name = "ProceduralSoftForestMistAlpha";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        System.Random rng = new System.Random(74123);
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f) / size * 2f - 1f;
            float v = (y + 0.5f) / size * 2f - 1f;
            float r = Mathf.Sqrt(u * u * 0.85f + v * v * 1.2f);
            float edge = 1f - Mathf.SmoothStep(0.40f, 1.0f, r);
            float noise = Mathf.Lerp(0.72f, 1.0f, (float)rng.NextDouble());
            float a = Mathf.Clamp01(edge * noise * opacity * 2.0f);
            byte alpha = (byte)Mathf.RoundToInt(a * 255f);
            pixels[y * size + x] = new Color32(255, 255, 255, alpha);
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return tex;
    }

    Material CreateMistMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        Material m = new Material(shader) { name = "Soft Forest Mist (Runtime)", renderQueue = 3000 };
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", mistTexture);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", mistTexture);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(mistColor.r, mistColor.g, mistColor.b, mistColor.a));
        if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(mistColor.r, mistColor.g, mistColor.b, mistColor.a));
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return m;
    }

    void ClearPatches()
    {
        if (patchRoot != null)
        {
            if (Application.isPlaying) WorldUtil.Kill(patchRoot.gameObject);
            else DestroyImmediate(patchRoot.gameObject);
        }
        patchRoot = null;
        builtFor = null;
        wispRenderers.Clear(); wispAlpha.Clear();
    }

    void OnDestroy()
    {
        ClearPatches();
        if (mistMaterial != null) WorldUtil.Kill(mistMaterial);
        if (mistTexture != null) WorldUtil.Kill(mistTexture);
    }
}
