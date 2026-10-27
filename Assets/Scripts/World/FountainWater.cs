using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Real particle fountain for the plaza fountain: a tall central jet, a ring of arcing sprays falling into the basin,
/// overflow drops from the bowl rim, splash droplets and ripple rings where the water lands, plus a light mist.
/// Lifetimes are solved from ballistics so droplets disappear exactly when they reach the basin surface.
/// Called by WaterParkEffects for the "FountainOrb" renderer (works on generated and baked worlds).
/// </summary>
public static class FountainWater
{
    static Texture2D dot, ring;
    static Material dotMat, ringMat;

    static readonly HashSet<int> built = new HashSet<int>();

    /// <summary>Idempotent entry point: builds the fountain for this orb renderer once.</summary>
    public static void BuildFor(Renderer orb)
    {
        if (orb == null) return;
        int id = orb.gameObject.GetInstanceID();
        if (!built.Add(id)) return;
        Transform holder = new GameObject("FountainWaterFx").transform;
        holder.SetParent(orb.transform.parent, true);
        try { Build(holder, orb.bounds); Debug.Log("FountainWater: built at " + orb.bounds.center); }
        catch (System.Exception e) { Debug.LogError("FountainWater: " + e); }
    }

    public static void ResetBuilt() { built.Clear(); }

    public static void Build(Transform root, Bounds orb)
    {
        Init();
        float s = Mathf.Max(0.3f, orb.size.x / 0.9f);           // generator orb is 0.9 m: scale everything with it
        Vector3 c = orb.center;
        float basinY = c.y - 3.0f * s + 0.04f;                   // basin water surface
        float rimY = c.y - 1.55f * s;                            // bowl rim
        float bowlR = 1.65f * s;

        // central jet
        float v = 8.2f * s;
        float t = Land(c.y + 0.35f * s, basinY, v);
        Spray(root, "Jet", new Vector3(c.x, c.y + 0.35f * s, c.z), 0.04f, 2.2f, v, t, 150f, 0.085f, 0.95f);
        Splash(root, "JetSplash", new Vector3(c.x, basinY, c.z), 0.35f * s + 0.25f, 55f);
        Ripples(root, "JetRipples", new Vector3(c.x, basinY + 0.01f, c.z), 0.5f * s, 5f, 1.6f * s);

        // ring of arcing sprays falling from the bowl rim into the basin
        float av = 4.2f * s, ang = 38f;
        float at = Land(rimY + 0.05f, basinY, av * Mathf.Cos(ang * Mathf.Deg2Rad));
        float landR = bowlR * 0.9f + av * Mathf.Sin(ang * Mathf.Deg2Rad) * at;
        Spray(root, "Arcs", new Vector3(c.x, rimY + 0.05f, c.z), bowlR * 0.9f, ang, av, at, 260f, 0.07f, 0.9f);
        Splash(root, "ArcSplash", new Vector3(c.x, basinY, c.z), landR, 120f);
        Ripples(root, "ArcRipples", new Vector3(c.x, basinY + 0.01f, c.z), landR, 14f, 1.1f * s);

        // overflow drops running over the bowl rim
        float ot = Land(rimY, basinY, 0.3f);
        Spray(root, "Overflow", new Vector3(c.x, rimY, c.z), bowlR, 82f, 0.9f * s, ot, 90f, 0.05f, 0.8f);
        Splash(root, "OverflowSplash", new Vector3(c.x, basinY, c.z), bowlR + 0.5f * s, 30f);

        Mist(root, new Vector3(c.x, basinY + 0.3f, c.z), landR * 0.7f);
    }

    // time for a particle launched upward at speed vy from y0 to reach y1
    static float Land(float y0, float y1, float vy)
    {
        float g = 9.81f;
        return (vy + Mathf.Sqrt(vy * vy + 2f * g * Mathf.Max(0f, y0 - y1))) / g;
    }

    static ParticleSystem NewPS(Transform parent, string name, Vector3 pos, Quaternion rot, Material mat, ParticleSystemRenderMode mode)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.SetPositionAndRotation(pos, rot);
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

    static void Fade(ParticleSystem ps, Color c, float peak)
    {
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                   new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, 0.08f), new GradientAlphaKey(peak, 0.85f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
    }

    // streaming droplets from a cone nozzle (angle from vertical, radius 0 = single nozzle, >0 = ring of nozzles)
    static void Spray(Transform root, string name, Vector3 pos, float radius, float angle, float speed, float life, float rate, float size, float alpha)
    {
        ParticleSystem ps = NewPS(root, name, pos, Quaternion.LookRotation(Vector3.up), dotMat, ParticleSystemRenderMode.Stretch);
        ParticleSystem.MainModule m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.97f, life * 1.03f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.93f, speed * 1.07f);
        m.startSize = new ParticleSystem.MinMaxCurve(size * 0.8f, size * 1.25f);
        m.gravityModifier = 1f;
        m.maxParticles = 1200;
        ParticleSystem.EmissionModule e = ps.emission; e.rateOverTime = rate;
        ParticleSystem.ShapeModule sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = angle;
        sh.radius = Mathf.Max(0.001f, radius);
        sh.radiusThickness = radius > 0.1f ? 0f : 1f;           // rings emit from the edge only
        ParticleSystemRenderer pr = ps.GetComponent<ParticleSystemRenderer>();
        pr.lengthScale = 1.4f; pr.velocityScale = 0.05f;
        Fade(ps, new Color(0.82f, 0.94f, 1f), alpha);
        ps.Play();
    }

    // droplets bouncing up where the water lands
    static void Splash(Transform root, string name, Vector3 pos, float radius, float rate)
    {
        ParticleSystem ps = NewPS(root, name, pos, Quaternion.LookRotation(Vector3.up), dotMat, ParticleSystemRenderMode.Billboard);
        ParticleSystem.MainModule m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        m.gravityModifier = 1f;
        m.maxParticles = 500;
        ParticleSystem.EmissionModule e = ps.emission; e.rateOverTime = rate;
        ParticleSystem.ShapeModule sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 28f; sh.radius = Mathf.Max(0.05f, radius); sh.radiusThickness = radius > 0.6f ? 0.12f : 1f;
        Fade(ps, new Color(0.9f, 0.97f, 1f), 0.8f);
        ps.Play();
    }

    // expanding rings on the basin surface
    static void Ripples(Transform root, string name, Vector3 pos, float radius, float rate, float maxSize)
    {
        ParticleSystem ps = NewPS(root, name, pos, Quaternion.LookRotation(Vector3.up), ringMat, ParticleSystemRenderMode.HorizontalBillboard);
        ParticleSystem.MainModule m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.9f);
        m.startSpeed = 0f;
        m.startSize = maxSize;
        m.maxParticles = 160;
        ParticleSystem.EmissionModule e = ps.emission; e.rateOverTime = rate;
        ParticleSystem.ShapeModule sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = Mathf.Max(0.05f, radius); sh.radiusThickness = radius > 0.6f ? 0.1f : 1f;
        Fade(ps, new Color(0.9f, 1f, 1f), 0.5f);
        ParticleSystem.SizeOverLifetimeModule sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.1f), new Keyframe(1f, 1f)));
        ps.Play();
    }

    static void Mist(Transform root, Vector3 pos, float radius)
    {
        ParticleSystem ps = NewPS(root, "Mist", pos, Quaternion.LookRotation(Vector3.up), dotMat, ParticleSystemRenderMode.Billboard);
        ParticleSystem.MainModule m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
        m.maxParticles = 60;
        ParticleSystem.EmissionModule e = ps.emission; e.rateOverTime = 7f;
        ParticleSystem.ShapeModule sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = Mathf.Max(0.3f, radius);
        Fade(ps, Color.white, 0.09f);
        ps.Play();
    }

    static void Init()
    {
        if (dot == null) dot = MakeTex(32, false);
        if (ring == null) ring = MakeTex(64, true);
        if (dotMat == null) dotMat = Mat(dot);
        if (ringMat == null) ringMat = Mat(ring);
    }

    static Material Mat(Texture2D tex)
    {
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
        if (sh == null) { Debug.LogError("FountainWater: no particle shader found (add Sprites/Default to Always Included Shaders)."); sh = Shader.Find("Unlit/Transparent"); }
        Material mt = new Material(sh);
        mt.mainTexture = tex;
        if (mt.HasProperty("_BaseMap")) mt.SetTexture("_BaseMap", tex);
        return mt;
    }

    static Texture2D MakeTex(int n, bool asRing)
    {
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, true);
        t.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                float a = asRing ? Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) / 0.14f) : Mathf.Clamp01(1f - d);
                if (!asRing) a = a * a * (3f - 2f * a);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply();
        return t;
    }
}

/// <summary>Finds the plaza fountain orb (also after loading from the start menu) and builds the particle fountain; independent of WaterParkEffects.</summary>
public sealed class FountainInstaller : MonoBehaviour
{
    float next;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindFirstObjectByType<FountainInstaller>() != null) return;
        GameObject g = new GameObject("FountainInstaller");
        DontDestroyOnLoad(g);
        g.AddComponent<FountainInstaller>();
    }

    void OnEnable() { UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnScene; }
    void OnDisable() { UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnScene; }
    void OnScene(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) { FountainWater.ResetBuilt(); next = 0f; }

    void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + 3f;
        GameObject go = GameObject.Find("FountainOrb");
        if (go == null) return;
        Renderer r = go.GetComponent<Renderer>();
        if (r != null) FountainWater.BuildFor(r);
    }
}
