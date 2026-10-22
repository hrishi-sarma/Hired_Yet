using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using FPAnimation;

/// <summary>World-space text that hugs a moving anchor, faces the camera and keeps a readable size at any distance.
/// Used for the paper-gust prompt (it travels with the papers) and the wallet candidate name plates.</summary>
public class FloatingLabel : MonoBehaviour
{
    TextMeshPro tm;
    public Transform follow;
    public Vector3 offset = new Vector3(0f, 1f, 0f);
    public float maxRange = 30f, baseScale = 0.1f;

    public static FloatingLabel Create(string name, Transform parent, float fontSize)
    {
        GameObject g = new GameObject(name);
        if (parent != null) g.transform.SetParent(parent, false);
        FloatingLabel f = g.AddComponent<FloatingLabel>();
        f.tm = g.AddComponent<TextMeshPro>();
        f.tm.fontSize = fontSize;
        f.tm.alignment = TextAlignmentOptions.Center;
        f.tm.enableWordWrapping = false;
        f.tm.outlineWidth = 0.22f;
        f.tm.outlineColor = new Color32(0, 0, 0, 255);
        f.tm.rectTransform.sizeDelta = new Vector2(20f, 2f);
        return f;
    }

    public void Set(string text, Color c) { if (tm != null) { tm.text = text; tm.color = c; } }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null || tm == null) return;
        if (follow != null) transform.position = follow.position + offset;
        float d = Vector3.Distance(cam.transform.position, transform.position);
        tm.enabled = d < maxRange;
        transform.rotation = cam.transform.rotation;
        transform.localScale = Vector3.one * Mathf.Clamp(d * baseScale, 0.25f, 2.6f);
    }
}

/// <summary>Spawns gusts of loose papers at random moments, city only, one at a time, many over a session.
/// Not a slot event: it lives next to the WorldEventDirector (root object "World Events").</summary>
public class PaperGustSpawner : MonoBehaviour
{
    NpcDirector director;
    WorldEventConfig cfg;
    PaperGust active;
    float timer, msgEnd;
    string msg = "";

    public static PaperGustSpawner Attach(GameObject root, NpcDirector d)
    {
        PaperGustSpawner s = root.AddComponent<PaperGustSpawner>();
        s.director = d;
        s.cfg = WorldEventDefaults.Find("flying_documents");
        if (d.settings.events.library != null)
            foreach (WorldEventConfig c in d.settings.events.library.events) if (c != null && c.id == "flying_documents") s.cfg = c;
        s.timer = Random.Range(15f, 45f);
        return s;
    }

    public void Say(string text, float secs)
    {
        msg = text; msgEnd = Time.time + secs;
        if (InteractionSystem.Instance != null) InteractionSystem.Instance.OverridePrompt = text;
    }

    bool InCity(Vector3 p) { return director.Ctx != null && director.Ctx.zoneMap.ZoneAt(p.x, p.z) == ZoneType.City; }

    void Update()
    {
        if (msg.Length > 0 && Time.time > msgEnd)
        {
            if (InteractionSystem.Instance != null && InteractionSystem.Instance.OverridePrompt == msg) InteractionSystem.Instance.OverridePrompt = "";
            msg = "";
        }
        if (director == null || !director.Running || director.Player == null || cfg == null || !cfg.enabled || cfg.spawnWeight <= 0f) return;
        if (active != null) return;
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        if (!InCity(director.Player.position)) { timer = 5f; return; }
        timer = TrySpawn(false) ? Random.Range(40f, 120f) : 6f;
    }

    /// <summary>Puts the player on a random city sidewalk and starts a gust ahead of them (DevConsole "events").</summary>
    public bool SpawnNow() { return TrySpawn(true); }

    bool TrySpawn(bool debug)
    {
        PedestrianGraph g = director.PedGraph;
        if (g == null) return false;
        Transform pl = director.Player;
        Vector3 pp = pl.position;
        Vector3 fwd = pl.forward; fwd.y = 0f; fwd.Normalize();
        System.Random rng = director.Rng;
        for (int i = 0; i < 80; i++)
        {
            PedEdge e = g.Sidewalks[rng.Next(g.Sidewalks.Count)];
            if (e.zone != ZoneType.City || e.length < 16f) continue;
            float s = (float)rng.NextDouble() * (e.length - 8f) + 4f;
            Vector2 p2 = e.Point(s);
            Vector3 w = new Vector3(p2.x, director.GroundY(p2.x, p2.y, e.sideY + 3f), p2.y);
            Vector3 to = w - pp; to.y = 0f;
            float d = to.magnitude;
            if (!debug && (d < 14f || d > 34f || Vector3.Dot(to / d, fwd) < 0.3f)) continue;
            float sign = Vector3.Dot(new Vector3(e.dir.x, 0f, e.dir.y), to) >= 0f ? 1f : -1f;   // blows away from the player
            if (debug)
            {
                CharacterController cc = pl.GetComponent<CharacterController>();
                Vector2 start = e.Point(Mathf.Clamp(s - sign * 8f, 1f, e.length - 1f));
                if (cc != null) cc.enabled = false;
                pl.position = new Vector3(start.x, director.GroundY(start.x, start.y, e.sideY + 3f) + 0.3f, start.y);
                pl.rotation = Quaternion.LookRotation(new Vector3(e.dir.x * sign, 0f, e.dir.y * sign));
                if (cc != null) cc.enabled = true;
            }
            GameObject go = new GameObject("Paper Gust");
            go.transform.SetParent(transform, false);
            active = go.AddComponent<PaperGust>();
            active.Init(this, director, cfg, e, s, sign);
            return true;
        }
        return false;
    }
}

/// <summary>One gust: a particle system of tumbling sheets drifting along a sidewalk with a floating prompt that travels with it.
/// Hold E within reach to gather them (about 2.8 s, 6 sheets); progress sinks slowly when you let go or fall behind.
/// If the gust blows out of reach after the player had tried to catch it, "lost to the wind" is shown.</summary>
public class PaperGust : MonoBehaviour
{
    const float CatchRange = 2.5f, CatchTime = 2.8f, Sheets = 6f;
    static Mesh sheetMesh;
    static Material sheetMat;

    PaperGustSpawner owner;
    NpcDirector d;
    WorldEventConfig cfg;
    PedEdge edge;
    float s, dir, speed, age, life, seed, progress, tried, linger, endAt;
    int counted;
    bool ended, anim;
    ParticleSystem ps;
    Vector3 logical;
    const float GuideRange = 30f;   // the arrow and prompt only appear this close to the papers

    public void Init(PaperGustSpawner o, NpcDirector director, WorldEventConfig c, PedEdge e, float start, float sign)
    {
        owner = o; d = director; cfg = c; edge = e; s = start; dir = sign;
        seed = Random.value * 10f;
        speed = Random.Range(1.5f, 2.3f);
        life = Random.Range(24f, 32f);                 // long enough to chase and catch, and the sheets linger visibly
        BuildParticles();
        UpdatePosition(0f);
        transform.position = logical;
    }

    static Mesh SheetMesh()
    {
        if (sheetMesh != null) return sheetMesh;
        float w = 0.105f, h = 0.15f;
        Vector3[] v = { new Vector3(-w, 0, -h), new Vector3(w, 0, -h), new Vector3(w, 0, h), new Vector3(-w, 0, h) };
        sheetMesh = new Mesh { name = "PaperSheet" };
        sheetMesh.vertices = new[] { v[0], v[1], v[2], v[3], v[0], v[1], v[2], v[3] };
        sheetMesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7 };
        Vector3[] n = new Vector3[8];
        for (int i = 0; i < 4; i++) { n[i] = Vector3.up; n[i + 4] = Vector3.down; }
        sheetMesh.normals = n;
        sheetMesh.RecalculateBounds();
        return sheetMesh;
    }

    static Material SheetMat()
    {
        if (sheetMat != null) return sheetMat;
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
        sheetMat = new Material(sh);
        sheetMat.color = Color.white;
        return sheetMat;
    }

    void BuildParticles()
    {
        GameObject g = new GameObject("Sheets");
        g.transform.SetParent(transform, false);
        ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 14f);      // sheets stay around, then fade
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.25f);
        main.gravityModifier = 0.015f;
        main.maxParticles = 40;
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.97f, 0.97f, 0.93f), new Color(0.86f, 0.89f, 0.96f));
        var em = ps.emission;
        em.rateOverTime = 3.5f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.9f;
        var inh = ps.inheritVelocity;
        inh.enabled = true; inh.mode = ParticleSystemInheritVelocityMode.Initial; inh.curve = 0.75f;
        var lim = ps.limitVelocityOverLifetime;
        lim.enabled = true; lim.dampen = 0.04f; lim.limit = 2.4f;
        var noise = ps.noise;
        noise.enabled = true; noise.strength = 0.9f; noise.frequency = 0.5f; noise.scrollSpeed = 0.6f; noise.damping = true;
        var rol = ps.rotationOverLifetime;
        rol.enabled = true; rol.separateAxes = true;
        rol.x = new ParticleSystem.MinMaxCurve(-4f, 4f); rol.y = new ParticleSystem.MinMaxCurve(-3f, 3f); rol.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(1f, 0.78f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        var rd = ps.GetComponent<ParticleSystemRenderer>();
        rd.renderMode = ParticleSystemRenderMode.Mesh;
        rd.mesh = SheetMesh();
        rd.sharedMaterial = SheetMat();
        rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rd.receiveShadows = false;
        ps.Play();
    }

    void UpdatePosition(float dt)
    {
        float v = speed * (0.75f + 0.25f * Mathf.Sin(age * 0.9f + seed));
        if (linger <= 0f)
        {
            s += dir * v * dt;
            if (s < 0.8f || s > edge.length - 0.8f) { s = Mathf.Clamp(s, 0.8f, edge.length - 0.8f); linger = 0.01f; endAt = age + 5f; }   // swirls at the end of the street, then gives up
        }
        else linger += dt;
        Vector2 p = edge.Point(s);
        Vector2 side = edge.left * edge.innerSign * (Mathf.Sin(age * 0.7f + seed) * 0.8f);
        float x = p.x + side.x, z = p.y + side.y;
        float y = d.GroundY(x, z, edge.sideY + 3f) + 1.5f + 0.5f * Mathf.Sin(age * 1.7f + seed);
        logical = new Vector3(x, y, z);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (d == null || d.Player == null) { WorldUtil.Kill(gameObject); return; }
        if (ended) { if (age > endAt) WorldUtil.Kill(gameObject); return; }
        UpdatePosition(dt);

        Vector3 pp = d.Player.position;
        Vector3 flat = logical - pp; flat.y = 0f;
        float dist = flat.magnitude;
        Keyboard kb = Keyboard.current;
        InteractionSystem sys = InteractionSystem.Instance;
        bool held = kb != null && kb.eKey.isPressed && (sys == null || !sys.Suppressed);
        bool inRange = dist < CatchRange;
        bool catching = held && inRange;
        if (held && dist < 7f) tried = 6f;
        tried = Mathf.Max(0f, tried - dt);

        progress = Mathf.Clamp01(progress + (catching ? dt / CatchTime : -dt * 0.12f));
        int n = Mathf.FloorToInt(progress * Sheets + 0.001f);
        if (catching && n > counted) FPEvents.KickEvent();
        counted = n;
        SetAnim(catching);
        if (anim) { FPEvents.EventIntensity = Mathf.MoveTowards(FPEvents.EventIntensity, 1f, dt * 4f); FPEvents.EventProgress = progress; }

        // sheets are drawn toward the player's hands while they are being caught
        Vector3 chest = pp + d.Player.forward * 0.7f + Vector3.up * 1.2f;
        transform.position = Vector3.Lerp(logical, chest, catching ? progress * 0.75f : 0f);

        // screen prompt and arrow, only while the player is near the papers
        if (dist < GuideRange)
        {
            string msg; Color mc = Color.white;
            if (catching) { msg = "CATCHING  " + EventUi.Bar(progress) + "  " + n + " / " + (int)Sheets; mc = new Color(0.6f, 1f, 0.6f); }
            else if (inRange) msg = "[ HOLD E ] CATCH THE PAPERS";
            else { msg = "PAPERS BLOWING AWAY  -  GET CLOSER"; mc = new Color(1f, 0.9f, 0.5f); }
            Vector3 f = Camera.main != null ? Camera.main.transform.forward : d.Player.forward;
            f.y = 0f;
            bool arrowOn = !inRange && flat.sqrMagnitude > 1e-4f && f.sqrMagnitude > 1e-4f;
            EventHud.Set("papers", arrowOn, arrowOn ? Vector3.SignedAngle(f, flat, Vector3.up) : 0f, msg, mc);
        }
        else EventHud.Clear("papers");

        if (progress >= 1f) { End(true); return; }
        if (age > life || (linger > 0f && age > endAt) || dist > 70f) End(false);
    }

    void SetAnim(bool on)
    {
        if (on == anim) return;
        anim = on;
        if (on) { FPEvents.EventFocusHeight = 0f; FPEvents.EventIntensity = 0.3f; FPEvents.RaiseEventStage(EventStage.During, PlayerEventAnim.Sort, transform, 0f); }
        else StopAnim();
    }

    static void StopAnim()
    {
        FPEvents.EventIntensity = 0f; FPEvents.EventProgress = 0f;
        FPEvents.RaiseEventStage(EventStage.During, PlayerEventAnim.None, null, 0f);
    }

    void End(bool ok)
    {
        ended = true;
        if (anim) { anim = false; StopAnim(); }
        EventHud.Clear("papers");
        var em = ps.emission; em.enabled = false;
        if (ok)
        {
            string text = EventNothing.Pick("flying_documents", d.Rng);
            float chance = Mathf.Clamp01(cfg.reward * d.settings.events.itemChanceScale);
            if (d.Rng.NextDouble() < chance)
            {
                Items.ItemInstance item = Items.ItemPickups.Roll(Items.SpawnSource.FlyingPaperTrail, d.Rng);
                if (item != null)
                {
                    text = "You gathered every page.  [ + " + item.def.name.ToUpperInvariant() + " ]";
                    // Stage 9: stored + held with a toast, or dropped with the swap UI when the inventory is full.
                    Items.ItemPickups.Offer(item);
                }
            }
            owner.Say(text, 3.5f);
            endAt = age + 1.5f;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        else
        {
            if (tried > 0f) owner.Say("The files were lost to the wind...", 3.5f);
            // the wind picks up and whisks the remaining sheets away
            var f = ps.forceOverLifetime;
            f.enabled = true; f.space = ParticleSystemSimulationSpace.World;
            Vector3 w = new Vector3(edge.dir.x * dir, 0f, edge.dir.y * dir) * 5f;
            f.x = w.x; f.y = 1.5f; f.z = w.z;
            endAt = age + 14f;                         // let the sheets finish their life, then clean up
        }
    }

    void OnDestroy() { if (anim) StopAnim(); EventHud.Clear("papers"); }
}
