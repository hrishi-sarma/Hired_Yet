using System.Collections.Generic;
using UnityEngine;
using FPAnimation;

/// <summary>Lightweight stylized particle effects for events: watering-can stream and rain over the plants, spinning sprinkler jets,
/// and cauldron fire. Small pooled particle counts, no shadows, one shared material, and everything stops emitting when the player is far away.
/// Delete this file and the single EventFx.Attach call in PropBuilder to remove it.</summary>
public class EventFx : MonoBehaviour
{
    enum Kind { Plants, Sprinkler, Fire }

    Kind kind;
    WorldEvent ev;
    readonly List<ParticleSystem> main = new List<ParticleSystem>();
    readonly List<ParticleSystem> extra = new List<ParticleSystem>();
    readonly List<Transform> spinners = new List<Transform>();
    readonly List<Transform> plants = new List<Transform>();
    Light fireLight;
    float baseLight, seed;

    static Material mat;
    const float ActiveRange = 45f;

    public static void Attach(string id, Transform body)
    {
        Kind k;
        if (id == "thirsty_plants") k = Kind.Plants;
        else if (id == "sprinkler_valve") k = Kind.Sprinkler;
        else if (id == "witch_cauldron") k = Kind.Fire;
        else return;
        EventFx fx = body.gameObject.AddComponent<EventFx>();
        fx.Build(k, body);
    }

    void Build(Kind k, Transform body)
    {
        kind = k;
        seed = Random.value * 10f;
        Color water = new Color(0.55f, 0.82f, 1f, 0.9f), foam = new Color(0.9f, 0.97f, 1f, 0.8f);
        foreach (Transform t in body.GetComponentsInChildren<Transform>())
        {
            if (kind == Kind.Plants)
            {
                if (t.name == "Spout")
                    main.Add(Make(t, new Vector3(0f, 0.15f, 0f), Quaternion.Euler(-90f, 0f, 0f), water, foam, 0.07f, 0.55f, 1.4f, 1.6f, 6f, 0.4f));
                else if (t.name == "Pot")
                {
                    extra.Add(Make(body, t.localPosition + new Vector3(0f, 1.1f, 0f), Quaternion.Euler(90f, 0f, 0f), water, foam, 0.05f, 0.5f, 0.2f, 1.2f, 25f, 0.25f));
                }
                else if (t.name == "Plant") plants.Add(t);
            }
            else if (kind == Kind.Sprinkler && t.name == "Head")
            {
                Transform spin = new GameObject("Spin").transform;
                spin.SetParent(t, false);
                spin.localPosition = new Vector3(0f, 0.08f, 0f);
                spinners.Add(spin);
                main.Add(Make(spin, Vector3.zero, Quaternion.Euler(-62f, 0f, 0f), water, foam, 0.06f, 1.0f, 3.4f, 1f, 7f, 0.6f));
            }
            else if (kind == Kind.Fire && t.name == "Fire")
            {
                Renderer r = t.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
                Color flame = new Color(1f, 0.55f, 0.1f, 0.95f), hot = new Color(1f, 0.9f, 0.3f, 0.95f);
                main.Add(Make(body, t.localPosition + new Vector3(0f, 0.02f, 0f), Quaternion.Euler(-90f, 0f, 0f), flame, hot, 0.3f, 0.6f, 0.7f, -0.25f, 14f, 0.5f));
                extra.Add(Make(body, t.localPosition + new Vector3(0f, 0.15f, 0f), Quaternion.Euler(-90f, 0f, 0f), hot, hot, 0.04f, 1.1f, 1.4f, -0.05f, 20f, 1f));
                GameObject lg = new GameObject("FireLight");
                lg.transform.SetParent(body, false);
                lg.transform.localPosition = t.localPosition + new Vector3(0f, 0.45f, 0f);
                fireLight = lg.AddComponent<Light>();
                fireLight.type = LightType.Point;
                fireLight.color = new Color(1f, 0.6f, 0.25f);
                fireLight.range = 4.5f;
                fireLight.shadows = LightShadows.None;
                baseLight = 1.3f;
            }
        }
    }

    void Update()
    {
        NpcDirector d = NpcDirector.Instance;
        if (d == null || d.Player == null) return;
        if (ev == null) ev = GetComponentInParent<WorldEvent>();
        bool near = (transform.position - d.Player.position).sqrMagnitude < ActiveRange * ActiveRange;
        float rate = 0f, rain = 0f;
        float prog = ev != null ? ev.Progress : 0f;
        bool working = ev != null && ev.Working;
        switch (kind)
        {
            case Kind.Plants:
                PlantsHandler ph = ev != null ? ev.Plants : null;
                int act = ph != null && working ? ph.Active : -1;
                bool pour = near && act >= 0 && FPEvents.EventIntensity > 0.45f;
                rate = pour ? 38f : 0f;
                for (int i = 0; i < extra.Count; i++) SetRate(extra[i], pour && i == act ? 16f : 0f);   // rain only over the tub being watered
                for (int i = 0; i < plants.Count; i++)   // each plant perks up as its own tub fills
                    if (plants[i] != null) plants[i].localScale = new Vector3(0.4f, 0.35f * (0.8f + 0.35f * (ph != null ? ph.Fill01(i) : 0f)), 0.4f);
                break;
            case Kind.Sprinkler:
                bool off = ev != null && ev.Finished && ev.Succeeded;
                if (ev != null && ev.Done && ev.Succeeded) off = true;
                rate = near && !off ? 24f * (1f - prog * 0.85f) : 0f;
                for (int i = 0; i < spinners.Count; i++)
                    if (spinners[i] != null) spinners[i].localRotation = Quaternion.Euler(0f, Time.time * (150f - 80f * prog) + i * 90f, 0f);
                break;
            case Kind.Fire:
                rate = near ? 22f : 0f;
                break;
        }
        for (int i = 0; i < main.Count; i++) SetRate(main[i], rate);
        if (kind != Kind.Plants) for (int i = 0; i < extra.Count; i++) SetRate(extra[i], kind == Kind.Fire ? rate * 0.2f : rain);
        if (fireLight != null)
        {
            fireLight.enabled = near;
            fireLight.intensity = baseLight * (0.8f + 0.35f * Mathf.PerlinNoise(Time.time * 9f, seed));
        }
    }

    static void SetRate(ParticleSystem ps, float rate)
    {
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = rate;
    }

    static Material Mat()
    {
        if (mat != null) return mat;
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
        mat = new Material(sh);
        Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float dd = Mathf.Clamp01(1f - new Vector2(x - 15.5f, y - 15.5f).magnitude / 15.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, dd * 1.6f)));
            }
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        mat.mainTexture = tex;
        return mat;
    }

    // one cone emitter, world-space particles, emission driven from Update
    static ParticleSystem Make(Transform parent, Vector3 lp, Quaternion lr, Color c0, Color c1, float size, float life, float speed, float gravity, float angle, float spread)
    {
        GameObject go = new GameObject("Fx");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = lp;
        go.transform.localRotation = lr;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule m = ps.main;
        m.loop = true;
        m.playOnAwake = false;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
        m.startSpeed = new ParticleSystem.MinMaxCurve(speed * (1f - spread * 0.5f), speed);
        m.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
        m.startColor = new ParticleSystem.MinMaxGradient(c0, c1);
        m.gravityModifier = gravity;
        m.maxParticles = 60;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 0f;
        ParticleSystem.ShapeModule sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = angle;
        sh.radius = 0.01f;
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        ParticleSystem.SizeOverLifetimeModule sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.3f, 1f), new Keyframe(1f, gravity < 0f ? 0f : 0.5f)));
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Mat();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
        return ps;
    }
}
