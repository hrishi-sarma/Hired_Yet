using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Water effects for the water park (city plaza pool + fountain) and the ponds. Self-installing, works on generated or baked worlds.
///  - animated surface: scrolling ripple/caustic texture (when the mesh has UVs) + soft shimmer + high smoothness
///  - sparkle glints and expanding ripple rings on every water surface
///  - fountain: upward jet, falling arcs, splash droplets and mist at the base
/// </summary>
[DisallowMultipleComponent]
public sealed class WaterParkEffects : MonoBehaviour
{
    readonly HashSet<int> done = new HashSet<int>();
    float next, stamp;
    static Texture2D dotTex, ringTex, rippleTex;
    static Material dropMat, ringMat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindFirstObjectByType<WaterParkEffects>() != null) return;
        GameObject go = new GameObject("Water Park Effects (Auto)");
        go.AddComponent<WaterParkEffects>();
        DontDestroyOnLoad(go);
    }

    void OnEnable() { stamp = Time.unscaledTime; UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnScene; }
    void OnDisable() { UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnScene; }
    void OnScene(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) { done.Clear(); stamp = Time.unscaledTime; next = 0f; }

    void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + (Time.unscaledTime - stamp < 120f ? 2f : 12f);
        Scan();
    }

    static string Norm(string n) { return n.ToLowerInvariant().Replace(" ", "").Replace("_", ""); }

    static bool IsWater(Renderer r)
    {
        if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) return false;
        string n = Norm(r.name);
        if (n.Contains("waterfx") || n.Contains("tower") || n.Contains("bottle") || n.Contains("tank")) return false;
        if (n.Contains("pondwater") || n.Contains("fountainbowlwater") || n.Contains("fountainorb") || n.Contains("plazawater")) return true;
        if (!n.Contains("water")) return false;
        Transform t = r.transform.parent;
        for (int d = 0; d < 6 && t != null; d++, t = t.parent)
        {
            string p = Norm(t.name);
            if (p.Contains("plaza") || p.Contains("park") || p.Contains("pond") || p.Contains("fountain") || p.Contains("pool")) return true;
        }
        return false;
    }

    void Scan()
    {
        Renderer[] rs = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        for (int i = 0; i < rs.Length; i++)
        {
            Renderer r = rs[i];
            if (r == null) continue;
            int id = r.GetInstanceID();
            if (done.Contains(id)) continue;
            if (!IsWater(r)) continue;
            done.Add(id);
            Apply(r);
        }
    }

    // ------------------------------------------------------------------ build
    void Apply(Renderer r)
    {
        EnsureAssets();
        string n = Norm(r.name);
        bool orb = n.Contains("fountainorb");
        bool bowl = n.Contains("fountainbowl");
        Bounds b = r.bounds;

        if (r.GetComponent<WaterSurfaceAnim>() == null) r.gameObject.AddComponent<WaterSurfaceAnim>().Init(r, rippleTex, orb);

        Transform fx = new GameObject("WaterFx").transform;
        fx.SetParent(r.transform, false);
        Vector3 ls = r.transform.lossyScale;
        fx.rotation = Quaternion.identity;
        fx.localScale = new Vector3(1f / Mathf.Max(1e-4f, Mathf.Abs(ls.x)), 1f / Mathf.Max(1e-4f, Mathf.Abs(ls.y)), 1f / Mathf.Max(1e-4f, Mathf.Abs(ls.z)));

        if (orb)
        {
            // fountain: jet up from the orb, arcs falling into the bowl, mist
            fx.position = b.center + Vector3.up * b.extents.y * 0.6f;
            FountainWater.BuildFor(r);        // full particle fountain (jet, arcs, overflow, splashes, ripples, mist)
            return;
        }

        float radius = Mathf.Max(0.5f, Mathf.Min(b.extents.x, b.extents.z));
        float area = Mathf.Min(b.size.x * b.size.z, 900f);
        fx.position = new Vector3(b.center.x, b.max.y + 0.03f, b.center.z);
        Vector3 box = new Vector3(Mathf.Max(0.4f, b.size.x * 0.9f), 0.01f, Mathf.Max(0.4f, b.size.z * 0.9f));
        Sparkles(fx, box, Mathf.Clamp(area * 0.18f, 8f, 70f));
        Ripples(fx, box, Mathf.Clamp(area * 0.05f, 1.5f, 14f), Mathf.Clamp(radius * 0.12f, 0.35f, 1.1f));
        if (bowl)
        {
            Splash(fx, Mathf.Min(radius, 1.4f));
            Mist(fx, "BowlMist", 0.5f);
        }
    }

    static void EnsureAssets()
    {
        if (dotTex == null) dotTex = MakeDot(32);
        if (ringTex == null) ringTex = MakeRing(64);
        if (rippleTex == null) rippleTex = MakeRipple(128);
        if (dropMat == null) dropMat = ParticleMat(dotTex);
        if (ringMat == null) ringMat = ParticleMat(ringTex);
    }

    static Material ParticleMat(Texture2D tex)
    {
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        Material m = new Material(sh);
        m.mainTexture = tex;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        return m;
    }

    static ParticleSystem NewPS(Transform parent, string name, Material mat, ParticleSystemRenderMode mode)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule m = ps.main;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.loop = true;
        m.playOnAwake = true;
        ParticleSystemRenderer pr = g.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = mat;
        pr.renderMode = mode;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;
        return ps;
    }

    static void FadeInOut(ParticleSystem ps, Color c, float peak)
    {
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                   new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, 0.2f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
    }

    static void Sparkles(Transform p, Vector3 box, float rate)
    {
        ParticleSystem ps = NewPS(p, "Sparkles", dropMat, ParticleSystemRenderMode.Billboard);
        ParticleSystem.MainModule m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        m.startSpeed = 0f;
        m.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.24f);
        m.startColor = new Color(0.85f, 0.97f, 1f, 1f);
        m.maxParticles = 220;
        ParticleSystem.EmissionModule e = ps.emission; e.rateOverTime = rate;
        ParticleSystem.ShapeModule s = ps.shape; s.shapeType = ParticleSystemShapeType.Box; s.scale = box;
        FadeInOut(ps, Color.white, 0.95f);
        ParticleSystem.SizeOverLifetimeModule sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));
        ps.Play();
    }

    static void Ripples(Transform p, Vector3 box, float rate, float maxSize)
    {
        ParticleSystem ps = NewPS(p, "Ripples", ringMat, ParticleSystemRenderMode.HorizontalBillboard);
        ParticleSystem.MainModule m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.8f);
        m.startSpeed = 0f;
        m.startSize = maxSize;
        m.maxParticles = 120;
        ParticleSystem.EmissionModule e = ps.emission; e.rateOverTime = rate;
        ParticleSystem.ShapeModule s = ps.shape; s.shapeType = ParticleSystemShapeType.Box; s.scale = box;
        FadeInOut(ps, new Color(0.9f, 1f, 1f), 0.55f);
        ParticleSystem.SizeOverLifetimeModule sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.1f), new Keyframe(1f, 1f)));
        ps.Play();
    }

    static void Jet(Transform p, string name, float speed, float cone, float rate, float s0, float s1, float life)
    {
        ParticleSystem ps = NewPS(p, name, dropMat, ParticleSystemRenderMode.Billboard);
        ParticleSystem.MainModule m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.85f, life * 1.25f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.8f, speed * 1.1f);
        m.startSize = new ParticleSystem.MinMaxCurve(s0, s1);
        m.startColor = new Color(0.8f, 0.94f, 1f, 0.85f);
        m.gravityModifier = 1.5f;
        m.maxParticles = 400;
        ParticleSystem.EmissionModule e = ps.emission; e.rateOverTime = rate;
        ParticleSystem.ShapeModule s = ps.shape; s.shapeType = ParticleSystemShapeType.Cone; s.angle = cone; s.radius = 0.06f;
        s.rotation = new Vector3(-90f, 0f, 0f);       // cone points up
        FadeInOut(ps, new Color(0.85f, 0.96f, 1f), 0.85f);
        ps.Play();
    }

    static void Splash(Transform p, float radius)
    {
        ParticleSystem ps = NewPS(p, "Splash", dropMat, ParticleSystemRenderMode.Billboard);
        ParticleSystem.MainModule m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        m.gravityModifier = 1.6f;
        ParticleSystem.EmissionModule e = ps.emission; e.rateOverTime = 28f;
        ParticleSystem.ShapeModule s = ps.shape; s.shapeType = ParticleSystemShapeType.Circle; s.radius = radius; s.radiusThickness = 0.15f;
        s.rotation = new Vector3(-90f, 0f, 0f);
        FadeInOut(ps, new Color(0.9f, 0.98f, 1f), 0.8f);
        ps.Play();
    }

    static void Mist(Transform p, string name, float size)
    {
        ParticleSystem ps = NewPS(p, name, dropMat, ParticleSystemRenderMode.Billboard);
        ParticleSystem.MainModule m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        m.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.3f);
        m.gravityModifier = -0.02f;
        ParticleSystem.EmissionModule e = ps.emission; e.rateOverTime = 6f;
        ParticleSystem.ShapeModule s = ps.shape; s.shapeType = ParticleSystemShapeType.Circle; s.radius = size * 0.8f;
        s.rotation = new Vector3(-90f, 0f, 0f);
        FadeInOut(ps, new Color(0.92f, 0.97f, 1f), 0.16f);
        ps.Play();
    }

    // ------------------------------------------------------------------ textures
    static Texture2D MakeDot(int s)
    {
        Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, true);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float r = Mathf.Sqrt(Mathf.Pow(x / (s - 1f) - 0.5f, 2f) + Mathf.Pow(y / (s - 1f) - 0.5f, 2f)) * 2f;
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - r), 1.6f)));
            }
        t.wrapMode = TextureWrapMode.Clamp; t.Apply(true, false);
        return t;
    }

    static Texture2D MakeRing(int s)
    {
        Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, true);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float r = Mathf.Sqrt(Mathf.Pow(x / (s - 1f) - 0.5f, 2f) + Mathf.Pow(y / (s - 1f) - 0.5f, 2f)) * 2f;
                float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.8f) / 0.12f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        t.wrapMode = TextureWrapMode.Clamp; t.Apply(true, false);
        return t;
    }

    /// <summary>Tileable caustic-like ripple pattern (opaque, bright lines on a slightly darker base) multiplied onto the water tint.</summary>
    static Texture2D MakeRipple(int s)
    {
        Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, true);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = x / (float)s, v = y / (float)s;
                float a = Mathf.Abs(Mathf.Sin(Mathf.PI * (4f * u + 0.55f * Mathf.Sin(2f * Mathf.PI * 3f * v))));
                float b = Mathf.Abs(Mathf.Sin(Mathf.PI * (4f * v + 0.55f * Mathf.Sin(2f * Mathf.PI * 2f * u + 1.3f))));
                float line = Mathf.Pow(1f - Mathf.Min(a, b), 5f);
                float c = Mathf.Lerp(0.78f, 1.25f, line);
                t.SetPixel(x, y, new Color(Mathf.Clamp01(c * 0.96f), Mathf.Clamp01(c), 1f, 1f));
            }
        t.wrapMode = TextureWrapMode.Repeat; t.filterMode = FilterMode.Bilinear; t.Apply(true, false);
        return t;
    }
}

/// <summary>Scrolls the ripple texture across a water surface and adds a soft shimmer.
/// Uses a MaterialPropertyBlock only: the water material/shader itself is never cloned or modified.</summary>
public sealed class WaterSurfaceAnim : MonoBehaviour
{
    Renderer rend;
    MaterialPropertyBlock mpb;
    Texture2D ripple;
    Color baseCol = Color.white;
    bool hasBase, hasMain, scroll;
    Vector2 off;
    Vector2 tileScale = Vector2.one;
    float ph;
    static readonly int BaseMap = Shader.PropertyToID("_BaseMap"), BaseMapST = Shader.PropertyToID("_BaseMap_ST");
    static readonly int MainTex = Shader.PropertyToID("_MainTex"), MainTexST = Shader.PropertyToID("_MainTex_ST");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), ColorId = Shader.PropertyToID("_Color");

    public void Init(Renderer r, Texture2D tex, bool sphere)
    {
        rend = r; ripple = tex; ph = Random.value * 6.28f;
        Material m = r.sharedMaterial;
        if (m == null) { enabled = false; return; }
        hasBase = m.HasProperty(BaseMap); hasMain = m.HasProperty(MainTex);
        if (m.HasProperty(BaseColorId)) baseCol = m.GetColor(BaseColorId);
        else if (m.HasProperty(ColorId)) baseCol = m.GetColor(ColorId);
        mpb = new MaterialPropertyBlock();

        MeshFilter mf = r.GetComponent<MeshFilter>();
        Mesh mesh = mf != null ? mf.sharedMesh : null;
        if ((hasBase || hasMain) && mesh != null && mesh.isReadable)
        {
            Vector2[] uv = mesh.uv;
            if (uv != null && uv.Length > 0)
            {
                Vector2 mn = uv[0], mx = uv[0];
                for (int i = 1; i < uv.Length; i++) { mn = Vector2.Min(mn, uv[i]); mx = Vector2.Max(mx, uv[i]); }
                float range = Mathf.Max(mx.x - mn.x, mx.y - mn.y);
                if (range > 0.01f)
                {
                    float world = Mathf.Max(r.bounds.size.x, r.bounds.size.z);
                    float tile = sphere ? 2f : Mathf.Max(1f, world / 4f) / range;
                    tileScale = new Vector2(tile, tile);
                    scroll = true;
                }
            }
        }
    }

    void LateUpdate()
    {
        if (rend == null || mpb == null) return;
        float t = Time.time;
        rend.GetPropertyBlock(mpb);
        if (scroll)
        {
            off += new Vector2(0.018f, 0.011f) * Time.deltaTime;
            Vector2 o = off + new Vector2(Mathf.Sin(t * 0.3f + ph) * 0.02f, Mathf.Cos(t * 0.23f + ph) * 0.02f);
            Vector4 st = new Vector4(tileScale.x, tileScale.y, o.x, o.y);
            if (hasBase) { mpb.SetTexture(BaseMap, ripple); mpb.SetVector(BaseMapST, st); }
            if (hasMain) { mpb.SetTexture(MainTex, ripple); mpb.SetVector(MainTexST, st); }
        }
        float k = 1f + 0.07f * Mathf.Sin(t * 1.3f + ph) + 0.04f * Mathf.Sin(t * 2.9f + ph * 2f);
        Color c = new Color(baseCol.r * k, baseCol.g * k, baseCol.b * k, baseCol.a);
        mpb.SetColor(BaseColorId, c);
        mpb.SetColor(ColorId, c);
        rend.SetPropertyBlock(mpb);
    }
}
