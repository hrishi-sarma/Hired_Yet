using System.Collections.Generic;
using UnityEngine;

public enum FlyerSpecies { Butterfly, Sparrow, Robin, BlueJay, Cardinal, Crow, Pigeon }

/// <summary>Transform handles for a bird or butterfly built from primitives (forward = +Z, ground = y 0).</summary>
public sealed class FlyerRig
{
    public Transform visual;
    public Transform body;                 // pivot at body centre (pitch / roll for flight)
    public Transform head;                 // pivot for pecking / looking
    public Transform tail;
    public Transform[] wings = new Transform[2];   // shoulder pivots: 0 left, 1 right
    public Transform[] legs = new Transform[0];    // hip pivots (birds)
    public Transform legRoot;
    public Transform[] eyes = new Transform[2];
    public Vector3 wingScaleHome = Vector3.one;
    public float wingLength = 0.17f;
}

/// <summary>Builds the simple, toy-like birds and butterflies.</summary>
public static class FlyerModelFactory
{
    static Transform P(PrimitiveKind k, string n, Transform parent, Vector3 pos, Vector3 sc, Color c, int pattern)
    {
        return AnimalModelFactory.Part(k, n, parent, pos, sc, c, pattern);
    }

    public static FlyerRig Build(FlyerSpecies species, Transform root, System.Random rng, float scaleMul)
    {
        FlyerRig rig = new FlyerRig();
        rig.visual = AnimalModelFactory.Empty("Visual", root, Vector3.zero);
        if (species == FlyerSpecies.Butterfly) BuildButterfly(rig, rng);
        else BuildBird(rig, species);
        return rig;
    }

    // ------------------------------------------------------------------ birds
    static void BuildBird(FlyerRig rig, FlyerSpecies sp)
    {
        Color body, belly, wing, head, beak, accent = Color.clear, tailC;
        float size = 1f, beakMul = 1f;
        bool crest = false, mask = false, breast = false, neck = false, bars = false;
        switch (sp)
        {
            case FlyerSpecies.Sparrow:
                body = new Color(0.55f, 0.40f, 0.26f); belly = new Color(0.86f, 0.79f, 0.67f); wing = new Color(0.42f, 0.28f, 0.18f);
                head = new Color(0.52f, 0.30f, 0.18f); beak = new Color(0.55f, 0.48f, 0.38f); size = 0.55f; break;
            case FlyerSpecies.Robin:
                body = new Color(0.46f, 0.41f, 0.34f); belly = new Color(0.95f, 0.94f, 0.88f); wing = new Color(0.38f, 0.33f, 0.27f);
                head = body; beak = new Color(0.15f, 0.12f, 0.10f); accent = new Color(0.93f, 0.42f, 0.17f); breast = true; size = 0.62f; break;
            case FlyerSpecies.BlueJay:
                body = new Color(0.30f, 0.50f, 0.86f); belly = new Color(0.93f, 0.93f, 0.96f); wing = new Color(0.20f, 0.38f, 0.78f);
                head = body; beak = new Color(0.12f, 0.12f, 0.14f); crest = true; bars = true; size = 0.85f; break;
            case FlyerSpecies.Cardinal:
                body = new Color(0.84f, 0.12f, 0.12f); belly = new Color(0.88f, 0.22f, 0.20f); wing = new Color(0.70f, 0.09f, 0.10f);
                head = body; beak = new Color(0.97f, 0.58f, 0.16f); accent = new Color(0.08f, 0.06f, 0.06f); crest = true; mask = true; size = 0.75f; break;
            case FlyerSpecies.Crow:
                body = new Color(0.09f, 0.09f, 0.11f); belly = new Color(0.13f, 0.13f, 0.16f); wing = new Color(0.06f, 0.06f, 0.08f);
                head = body; beak = new Color(0.05f, 0.05f, 0.06f); size = 1.5f; beakMul = 1.5f; break;
            default: // Pigeon
                body = new Color(0.60f, 0.62f, 0.68f); belly = new Color(0.72f, 0.74f, 0.79f); wing = new Color(0.50f, 0.52f, 0.58f);
                head = new Color(0.52f, 0.54f, 0.60f); beak = new Color(0.62f, 0.56f, 0.56f); accent = new Color(0.30f, 0.62f, 0.55f); neck = true; bars = true; size = 1f; break;
        }
        tailC = wing;
        rig.visual.localScale = Vector3.one * size;
        Transform v = rig.visual;
        Color eyeC = sp == FlyerSpecies.Pigeon ? new Color(0.90f, 0.45f, 0.10f) : new Color(0.04f, 0.03f, 0.03f);

        rig.body = AnimalModelFactory.Empty("BodyPivot", v, new Vector3(0f, 0.095f, 0f));
        Transform b = rig.body;
        P(PrimitiveKind.Sphere, "Body", b, Vector3.zero, new Vector3(0.12f, 0.12f, 0.20f), body, 1);
        P(PrimitiveKind.Sphere, "Belly", b, new Vector3(0f, -0.022f, 0.025f), new Vector3(0.105f, 0.085f, 0.16f), belly, 1);
        if (breast) P(PrimitiveKind.Sphere, "Breast", b, new Vector3(0f, -0.005f, 0.06f), new Vector3(0.10f, 0.09f, 0.10f), accent, 1);

        rig.head = AnimalModelFactory.Empty("HeadPivot", b, new Vector3(0f, 0.062f, 0.095f));
        Transform h = rig.head;
        P(PrimitiveKind.Sphere, "Head", h, new Vector3(0f, 0.012f, 0.012f), new Vector3(0.088f, 0.085f, 0.088f), head, 1);
        if (neck) P(PrimitiveKind.Sphere, "NeckPatch", h, new Vector3(0f, -0.022f, -0.012f), new Vector3(0.09f, 0.05f, 0.075f), accent, 1);
        if (mask) P(PrimitiveKind.Sphere, "Mask", h, new Vector3(0f, 0.002f, 0.034f), new Vector3(0.05f, 0.05f, 0.03f), accent, -1);
        AnimalModelFactory.PartMesh("Beak", h, new Vector3(0f, -0.002f, 0.036f), new Vector3(0.03f * (beakMul > 1f ? 1.3f : 1f), 0.045f * beakMul, 0.028f * (beakMul > 1f ? 1.3f : 1f)), beak, -1)
            .localRotation = Quaternion.Euler(90f, 0f, 0f);
        for (int s = -1; s <= 1; s += 2)
            rig.eyes[s < 0 ? 0 : 1] = P(PrimitiveKind.Sphere, "Eye", h, new Vector3(s * 0.032f, 0.022f, 0.026f), new Vector3(0.017f, 0.017f, 0.017f), eyeC, -1);
        if (crest)
            AnimalModelFactory.PartMesh("Crest", h, new Vector3(0f, 0.04f, -0.012f), new Vector3(0.035f, 0.065f, 0.04f), head, 1)
                .localRotation = Quaternion.Euler(-28f, 0f, 0f);

        rig.tail = AnimalModelFactory.Empty("TailPivot", b, new Vector3(0f, 0.0f, -0.085f));
        AnimalModelFactory.PartMesh("Tail", rig.tail, Vector3.zero, new Vector3(0.09f, 0.10f, 0.014f), tailC, 1)
            .localRotation = Quaternion.Euler(-82f, 0f, 0f);

        rig.wingLength = 0.17f;
        for (int k = 0; k < 2; k++)
        {
            float s = k == 0 ? -1f : 1f;
            Transform wp = AnimalModelFactory.Empty(k == 0 ? "WingL" : "WingR", b, new Vector3(s * 0.052f, 0.03f, 0.02f));
            rig.wings[k] = wp;
            P(PrimitiveKind.Sphere, "Wing", wp, new Vector3(s * 0.085f, 0f, -0.01f), new Vector3(0.17f, 0.012f, 0.10f), wing, 1);
            if (bars) P(PrimitiveKind.Sphere, "WingBar", wp, new Vector3(s * 0.11f, 0.006f, -0.005f), new Vector3(0.08f, 0.006f, 0.05f), sp == FlyerSpecies.Pigeon ? new Color(0.15f, 0.15f, 0.2f) : new Color(0.10f, 0.15f, 0.35f), -1);
        }

        rig.legRoot = AnimalModelFactory.Empty("Legs", v, Vector3.zero);
        rig.legs = new Transform[2];
        Color legC = sp == FlyerSpecies.Pigeon ? new Color(0.80f, 0.35f, 0.40f) : (sp == FlyerSpecies.Crow ? new Color(0.08f, 0.08f, 0.09f) : new Color(0.62f, 0.45f, 0.35f));
        for (int k = 0; k < 2; k++)
        {
            float s = k == 0 ? -1f : 1f;
            Transform lp = AnimalModelFactory.Empty("LegPivot", rig.legRoot, new Vector3(s * 0.03f, 0.055f, 0.01f));
            rig.legs[k] = lp;
            P(PrimitiveKind.Capsule, "Leg", lp, new Vector3(0f, -0.0125f, 0f), new Vector3(0.012f, 0.0425f, 0.012f), legC, -1);
            P(PrimitiveKind.Sphere, "Foot", lp, new Vector3(0f, -0.049f, 0.012f), new Vector3(0.03f, 0.01f, 0.045f), legC, -1);
        }
    }

    // ------------------------------------------------------------- butterflies
    static void BuildButterfly(FlyerRig rig, System.Random rng)
    {
        Color main, edge, spot;
        switch (rng.Next(6))
        {
            case 0: main = new Color(0.97f, 0.56f, 0.10f); edge = new Color(0.10f, 0.06f, 0.05f); spot = new Color(0.98f, 0.96f, 0.90f); break;   // monarch
            case 1: main = new Color(0.12f, 0.46f, 0.97f); edge = new Color(0.06f, 0.08f, 0.16f); spot = new Color(0.55f, 0.80f, 1f); break;      // morpho
            case 2: main = new Color(0.98f, 0.98f, 0.94f); edge = new Color(0.70f, 0.72f, 0.66f); spot = new Color(0.12f, 0.12f, 0.12f); break;   // white
            case 3: main = new Color(0.98f, 0.88f, 0.18f); edge = new Color(0.55f, 0.42f, 0.08f); spot = new Color(0.95f, 0.50f, 0.12f); break;   // yellow
            case 4: main = new Color(0.86f, 0.40f, 0.78f); edge = new Color(0.30f, 0.12f, 0.35f); spot = new Color(0.98f, 0.85f, 0.95f); break;   // pink
            default: main = new Color(0.16f, 0.10f, 0.12f); edge = new Color(0.05f, 0.04f, 0.04f); spot = new Color(0.90f, 0.15f, 0.12f); break; // red admiral
        }
        Transform v = rig.visual;
        rig.body = AnimalModelFactory.Empty("BodyPivot", v, Vector3.zero);
        Transform b = rig.body;
        Color bodyC = new Color(0.10f, 0.08f, 0.06f);
        P(PrimitiveKind.Capsule, "Body", b, Vector3.zero, new Vector3(0.016f, 0.03f, 0.016f), bodyC, -1).localRotation = Quaternion.Euler(90f, 0f, 0f);
        rig.head = AnimalModelFactory.Empty("HeadPivot", b, new Vector3(0f, 0.002f, 0.034f));
        P(PrimitiveKind.Sphere, "Head", rig.head, Vector3.zero, new Vector3(0.02f, 0.02f, 0.02f), bodyC, -1);
        for (int s = -1; s <= 1; s += 2)
            P(PrimitiveKind.Capsule, "Antenna", rig.head, new Vector3(s * 0.008f, 0.012f, 0.012f), new Vector3(0.0035f, 0.018f, 0.0035f), bodyC, -1)
                .localRotation = Quaternion.Euler(-35f, 0f, -s * 25f);

        rig.wingLength = 0.09f;
        for (int k = 0; k < 2; k++)
        {
            float s = k == 0 ? -1f : 1f;
            Transform wp = AnimalModelFactory.Empty(k == 0 ? "WingL" : "WingR", b, new Vector3(s * 0.006f, 0.006f, 0f));
            rig.wings[k] = wp;
            // forewing, hindwing: dark edge underlay with the coloured wing on top and a spot
            P(PrimitiveKind.Sphere, "ForeEdge", wp, new Vector3(s * 0.04f, -0.001f, 0.02f), new Vector3(0.088f, 0.0035f, 0.078f), edge, -1);
            P(PrimitiveKind.Sphere, "Fore", wp, new Vector3(s * 0.04f, 0.0006f, 0.02f), new Vector3(0.074f, 0.0035f, 0.062f), main, -1);
            P(PrimitiveKind.Sphere, "HindEdge", wp, new Vector3(s * 0.033f, -0.001f, -0.03f), new Vector3(0.066f, 0.0035f, 0.062f), edge, -1);
            P(PrimitiveKind.Sphere, "Hind", wp, new Vector3(s * 0.033f, 0.0006f, -0.03f), new Vector3(0.054f, 0.0035f, 0.050f), main, -1);
            P(PrimitiveKind.Sphere, "Spot", wp, new Vector3(s * 0.05f, 0.0018f, 0.03f), new Vector3(0.022f, 0.003f, 0.022f), spot, -1);
        }
    }
}

/// <summary>One bird or butterfly. Not an IAgent; purely ambient decoration.</summary>
[DisallowMultipleComponent]
public sealed class FlyerAgent : MonoBehaviour
{
    enum BState { Ground, Fly, Land }
    enum GroundAct { Look, Peck, Hop, Walk }

    public FlyerSpecies Species { get; private set; }
    public bool IsButterfly { get { return Species == FlyerSpecies.Butterfly; } }
    public bool OnGround { get { return !IsButterfly && state == BState.Ground; } }

    FlyerWorldController owner;
    NpcDirector director;
    AnimalAISettings settings;
    System.Random rng;
    FlyerRig rig;
    ZoneType habitat;
    bool anyHabitat;

    // shared
    float phase, seedPhase, flapOmega, visualScale = 1f;
    float yaw, pitch, roll, spread;
    AudioSource audioSrc;
    AnimalSoundSet sounds;
    float nextCallTime;
    readonly AnimalCaller caller = new AnimalCaller();

    // bird
    BState state = BState.Ground;
    GroundAct act = GroundAct.Look;
    float actTimer, hopT, hopLen, groundY, groundCheck, flyTimer, flyAlt, flySpeed, climbVel;
    Vector3 landTarget;
    Vector3 hopDir;
    float walkPhase, peckPhase;
    BreadCrumb feed, feedIgnore;
    float feedScan, feedWalkT, feedIgnoreUntil, feedBestDist;
    public bool Summoned;
    public bool Audience;            // guitar audience: lands near the player, watches, leaves a few seconds after the music stops
    float audienceLeaveAt = -1f;
    bool smallHopper;

    // butterfly
    float turnRate, turnTarget, turnTimer, heightWant, heightTimer, restTimer, bflySpeed;
    bool resting;

    // sleep
    bool sleeping, wantSleep, lastNight, sleepInit;
    float sleepSwitchAt, wakeUntil, sleepAmt, landTimer;
    int sleepVariant;
    float sleepSide = 1f;

    static float Ease(float a, float b, float k) { return a + (b - a) * k; }

    public void Initialize(FlyerWorldController controller, FlyerSpecies species, NpcDirector npcDirector, AnimalAISettings cfg, int seed, ZoneType zone)
    {
        owner = controller; Species = species; director = npcDirector; settings = cfg;
        rng = new System.Random(seed);
        habitat = zone;
        anyHabitat = species == FlyerSpecies.Butterfly || species == FlyerSpecies.Crow;
        seedPhase = R(0f, 50f);
        yaw = transform.eulerAngles.y;
        gameObject.layer = 2; // Ignore Raycast
        rig = FlyerModelFactory.Build(species, transform, rng, 1f);
        float mul = IsButterfly ? Mathf.Max(0.3f, settings.butterflyScale) : Mathf.Max(0.3f, settings.birdScale);
        visualScale = rig.visual.localScale.x * mul * (IsButterfly ? 1.9f : 1f);
        rig.visual.localScale = Vector3.one * visualScale;
        SetLayer(gameObject, 2);
        smallHopper = species == FlyerSpecies.Sparrow || species == FlyerSpecies.Robin || species == FlyerSpecies.BlueJay || species == FlyerSpecies.Cardinal;
        flapOmega = IsButterfly ? 42f : (species == FlyerSpecies.Crow ? 17f : (species == FlyerSpecies.Pigeon ? 30f : (species == FlyerSpecies.BlueJay ? 34f : 55f)));
        flySpeed = (species == FlyerSpecies.Crow ? 6.5f : (species == FlyerSpecies.Pigeon ? 7f : 5.2f)) * Mathf.Max(0.3f, settings.birdFlySpeed);
        bflySpeed = R(0.7f, 1.3f);
        heightWant = R(0.7f, 2.0f);
        restTimer = R(8f, 22f);
        groundY = transform.position.y;
        if (!IsButterfly) { PickGroundAct(); sounds = (Species == FlyerSpecies.Crow) ? settings.crowSounds : settings.songbirdSounds; if (sounds != null) AnimalSounds.Fill(sounds, "birds", Species == FlyerSpecies.Crow ? "crow" : null, Species == FlyerSpecies.Crow ? null : "crow"); }
        SetupAudio();
    }

    static void SetLayer(GameObject go, int l)
    {
        go.layer = l;
        for (int i = 0; i < go.transform.childCount; i++) SetLayer(go.transform.GetChild(i).gameObject, l);
    }

    float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    float V() { return (float)rng.NextDouble(); }

    void Update()
    {
        if (director == null || !director.Running || director.Player == null || director.Ctx == null) { WorldUtil.Kill(gameObject); return; }
        if (owner == null || owner.IsShuttingDown) return;
        Vector3 playerPos = director.Player.position;
        Vector3 fromPlayer = transform.position - playerPos; fromPlayer.y = 0f;
        float dist = fromPlayer.magnitude;
        if (dist > settings.despawnRadius) { owner.Remove(this); WorldUtil.Kill(gameObject); return; }
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        phase += dt;
        UpdateSleepState(dist);
        if (IsButterfly) UpdateButterfly(dt, dist, fromPlayer);
        else UpdateBird(dt, dist, fromPlayer);
        UpdateAmbientSound(dist);
    }

    bool ZoneOk(float x, float z)
    {
        if (director.Ctx.zoneMap == null) return true;
        ZoneType zt = director.Ctx.zoneMap.ZoneAt(x, z);
        if (anyHabitat) return zt == ZoneType.Forest || zt == ZoneType.Suburb;
        return zt == habitat;
    }

    void UpdateSleepState(float dist)
    {
        bool night = AnimalSleepClock.IsNight(settings);
        if (!sleepInit) { sleepInit = true; lastNight = night; wantSleep = night; sleepSwitchAt = 0f; }
        if (night != lastNight) { lastNight = night; sleepSwitchAt = Time.time + R(1f, 30f); }
        if (Time.time >= sleepSwitchAt) wantSleep = night;
        bool can = wantSleep && Time.time >= wakeUntil;
        if (IsButterfly)
        {
            if (sleeping && dist < 1.8f)
            {   // disturbed: flutter off and stay awake for a while
                sleeping = false; resting = false; wakeUntil = Time.time + R(25f, 45f); heightWant = R(0.8f, 2.0f);
            }
            else if (can && !sleeping && dist > 1.8f)
            {
                sleeping = true; resting = true; sleepVariant = rng.Next(2);
            }
            else if (!wantSleep && sleeping) { sleeping = false; resting = false; restTimer = R(2f, 8f); }
            return;
        }
        if (sleeping && !wantSleep) { sleeping = false; PickGroundAct(); return; }
        if (!can || sleeping) return;
        if (state == BState.Ground)
        {
            if (dist > settings.birdAlertDistance * 0.6f)
            {
                sleeping = true;
                sleepVariant = rng.Next(2);
                sleepSide = rng.Next(2) == 0 ? -1f : 1f;
            }
        }
        else if (state == BState.Fly) flyTimer = Mathf.Min(flyTimer, 0f);   // head for a roost spot
    }

    // ================================================================= butterfly
    void UpdateButterfly(float dt, float dist, Vector3 fromPlayer)
    {
        Vector3 pos = transform.position;
        float terrain = director.Ctx.HeightAt(pos.x, pos.z);
        if (resting)
        {
            restTimer -= dt;
            float target = terrain + 0.05f;
            pos.y = Ease(pos.y, target, 1f - Mathf.Exp(-3f * dt));
            if (!sleeping && (restTimer <= 0f || dist < 2.2f)) { resting = false; restTimer = R(10f, 25f); heightWant = R(0.8f, 2.0f); }
            transform.position = pos;
            ApplyButterflyPose(dt, 0f, true);
            return;
        }

        turnTimer -= dt;
        if (turnTimer <= 0f) { turnTarget = R(-80f, 80f); turnTimer = R(0.4f, 1.6f); }
        turnRate = Ease(turnRate, turnTarget, 1f - Mathf.Exp(-2.5f * dt));
        yaw += turnRate * dt;
        heightTimer -= dt;
        if (heightTimer <= 0f) { heightWant = R(0.7f, 2.2f); heightTimer = R(2.5f, 6f); }
        // flutter forward in short bursts
        float burst = 0.55f + 0.45f * Mathf.Sin(phase * 3.1f + seedPhase);
        float speed = bflySpeed * burst + (dist < 5f ? 0.8f : 0f);
        Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 next = pos + fwd * speed * dt;
        if (!ZoneOk(next.x, next.z)) { yaw += 180f * dt * 3f; next = pos; }   // turn back toward habitat
        // never fly through anything solid: look along the movement and turn away from the obstacle
        Vector3 move = next - pos;
        float moveLen = move.magnitude;
        if (moveLen > 0.0001f)
        {
            RaycastHit bh;
            if (Physics.SphereCast(pos, 0.12f, move / moveLen, out bh, moveLen + 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && bh.normal.y < 0.7f)
            {
                Vector3 n = bh.normal; n.y = 0f;
                if (n.sqrMagnitude > 0.01f) yaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg + R(-35f, 35f);   // steer away from the surface
                else yaw += R(120f, 200f);
                turnRate = 0f; turnTimer = R(0.5f, 1.2f);
                heightWant = Mathf.Max(heightWant, (pos.y - terrain) + 0.6f);   // gain a little height too
                next = pos;
            }
        }
        float wantY = terrain + heightWant + Mathf.Sin(phase * 4.2f + seedPhase) * 0.18f;
        next.y = Ease(pos.y, wantY, 1f - Mathf.Exp(-2f * dt));
        if (next.y < terrain + 0.25f) next.y = terrain + 0.25f;
        transform.position = next;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        restTimer -= dt;
        if (!sleeping && restTimer <= 0f && dist > 3f) { resting = true; restTimer = R(4f, 9f); }
        ApplyButterflyPose(dt, speed, false);
    }

    void ApplyButterflyPose(float dt, float speed, bool rest)
    {
        float ang, yawFold = 0f;
        if (rest && sleeping)
        {
            ang = sleepVariant == 0 ? 84f + Mathf.Sin(phase * 0.8f) * 2f      // asleep, wings folded up
                                    : 5f + Mathf.Sin(phase * 0.7f) * 1.5f;    // asleep, wings lying flat
        }
        else if (rest)
        {
            ang = 72f + Mathf.Sin(phase * 1.4f + seedPhase) * 14f;     // slow open/close, wings mostly up
        }
        else
        {
            ang = 18f + Mathf.Sin(phase * flapOmega + seedPhase) * 48f; // flutter
        }
        rig.wings[0].localRotation = Quaternion.Euler(0f, yawFold, -ang);
        rig.wings[1].localRotation = Quaternion.Euler(0f, -yawFold, ang);
        float bank = rest ? 0f : Mathf.Clamp(-turnRate * 0.25f, -25f, 25f);
        float bodyPitch = rest ? 35f : Mathf.Sin(phase * flapOmega + seedPhase + 1f) * 10f - 8f;
        rig.body.localRotation = Quaternion.Euler(bodyPitch, 0f, bank);
    }

    // ===================================================================== birds
    void PickGroundAct()
    {
        float r = V();
        if (smallHopper) act = r < 0.35f ? GroundAct.Look : (r < 0.7f ? GroundAct.Peck : GroundAct.Hop);
        else act = r < 0.3f ? GroundAct.Look : (r < 0.55f ? GroundAct.Peck : GroundAct.Walk);
        actTimer = act == GroundAct.Look ? R(0.8f, 2.2f) : (act == GroundAct.Peck ? R(1.2f, 3.0f) : R(0.8f, 2.4f));
        hopT = 0f;
        if (act == GroundAct.Hop || act == GroundAct.Walk)
        {
            float turn = R(-70f, 70f);
            yaw += turn;
            hopDir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        }
    }

    void UpdateBird(float dt, float dist, Vector3 fromPlayer)
    {
        if (state == BState.Ground) UpdateGround(dt, dist, fromPlayer);
        else UpdateFlight(dt, dist);
        ApplyBirdPose(dt);
    }

    float nextCarScan;

    void UpdateGround(float dt, float dist, Vector3 fromPlayer)
    {
        Vector3 pos = transform.position;
        // Approaching cars always flush birds, even ones that are eating (the player-proximity rule below is unchanged).
        if (Time.time >= nextCarScan)
        {
            nextCarScan = Time.time + 0.15f;
            AnimalCarSense.Threat t;
            if (AnimalCarSense.Find(director, pos, 6f, out t) && (t.ttc < 4.5f || (t.car.Pos2D - new Vector2(pos.x, pos.z)).sqrMagnitude < 100f))
            {
                Vector2 side = new Vector2(t.car.Heading.y, -t.car.Heading.x) * (Mathf.Abs(t.lateral) < 0.2f ? 1f : Mathf.Sign(t.lateral));
                Vector3 flee = new Vector3(side.x, 0f, side.y);
                Audience = false;   // cars always scare the audience off
                TakeOff(flee);
                owner.AlertNearby(this, 8f, flee);
                return;
            }
        }
        if (Audience && TickAudience(dt, dist, fromPlayer)) return;
        float alert = settings.birdAlertDistance * (Species == FlyerSpecies.Crow ? 1.3f : 1f) * (sleeping ? 0.5f : 1f) * (AnimalAgent.PlayerIsSprinting(settings) ? 2.2f : 1f);
        alert = BreadFeeding.AlertFor(alert, AnimalAgent.PlayerIsSprinting(settings));
        if (dist < alert && !BreadFeeding.HasCrumbs)   // birds keep eating while crumbs remain, even with the player close
        {
            Vector3 away = fromPlayer.sqrMagnitude > 0.01f ? fromPlayer.normalized : transform.forward;
            if (BreadFeeding.Active) BreadFeeding.Scared();
            TakeOff(away);
            owner.AlertNearby(this, 6f, away);
            return;
        }
        if (sleeping)
        {
            rig.visual.localPosition = Vector3.zero;
            return;
        }
        if (BreadFeeding.Active)
        {
            feedScan -= dt;
            if (feed != null && feed.Gone) DropFeed(false);
            if (feed == null && feedScan <= 0f)
            {
                feedScan = 0.2f;
                BreadCrumb ign = Time.time < feedIgnoreUntil ? feedIgnore : null;
                feed = BreadFeeding.Nearest(pos, 30f, ign, null);
                if (feed != null) { feed.claims++; feedWalkT = 0f; feedBestDist = 999f; }
            }
            if (feed != null)
            {
                Vector3 toCrumb = feed.Position - pos;
                toCrumb.y = 0f;
                float cd = toCrumb.magnitude;
                if (cd > 0.3f)
                {
                    feedWalkT += dt;
                    if (cd < feedBestDist - 0.1f) { feedBestDist = cd; feedWalkT = Mathf.Min(feedWalkT, 2f); }
                    // stuck walking (blocked, unreachable zone, pushed away): give up this crumb and pick another / idle
                    if (feedWalkT > 5f) { DropFeed(true); act = GroundAct.Look; actTimer = 1f; }
                    else
                    {
                        act = GroundAct.Walk;
                        yaw = Mathf.Atan2(toCrumb.x, toCrumb.z) * Mathf.Rad2Deg;
                        hopDir = toCrumb / cd;
                    }
                }
                else
                {
                    act = GroundAct.Peck;
                    feedWalkT = 0f;
                    feed.Eat(dt * 0.2f);
                }
                if (feed != null) actTimer = 0.6f;
            }
        }
        else DropFeed(false);
        actTimer -= dt;
        float speed = 0f;
        if (act == GroundAct.Hop)
        {
            hopT += dt * 4.5f;
            if (hopT >= 1f) { hopT = 0f; }
            speed = 0.45f;
        }
        else if (act == GroundAct.Walk)
        {
            walkPhase += dt * 8f;
            speed = (Species == FlyerSpecies.Crow ? 0.5f : 0.4f) * (feed != null ? 4.5f : 1f);
        }
        else if (act == GroundAct.Peck) peckPhase += dt * 9f;

        if (speed > 0f)
        {
            Vector3 next = pos + hopDir * speed * dt;
            if (ZoneOk(next.x, next.z)) { pos = next; }
            else if (feed != null) { DropFeed(true); act = GroundAct.Look; actTimer = 1f; }
            else { yaw += 150f; hopDir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward; }
            groundCheck -= dt;
            if (groundCheck <= 0f)
            {
                groundCheck = 0.15f;
                groundY = director.GroundY(pos.x, pos.z, pos.y + 2.5f);
            }
            pos.y = Ease(pos.y, groundY, 1f - Mathf.Exp(-20f * dt));
        }
        if (BreadFeeding.Active || feed != null)
        {
            Vector3 push = owner.SeparationFor(this, pos, Species == FlyerSpecies.Crow || Species == FlyerSpecies.Pigeon ? 0.55f : 0.42f);
            if (push.sqrMagnitude > 0f)
            {
                Vector3 np = pos + push * dt;
                if (ZoneOk(np.x, np.z)) pos = np;
            }
        }
        float hopArc = act == GroundAct.Hop ? Mathf.Sin(Mathf.Clamp01(hopT) * Mathf.PI) * 0.045f : 0f;
        transform.position = new Vector3(pos.x, pos.y, pos.z);
        rig.visual.localPosition = new Vector3(0f, hopArc, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, yaw, 0f), 1f - Mathf.Exp(-10f * dt));
        if (actTimer <= 0f)
        {
            if (V() < 0.06f && !BreadFeeding.Calm) { TakeOff(transform.forward); return; }   // spontaneous take-off
            PickGroundAct();
            // food still around but this bird has none: keep it short so it re-scans and goes for a crumb
            if (BreadFeeding.Active && BreadFeeding.HasCrumbs && feed == null) { actTimer = Mathf.Min(actTimer, 0.4f); feedScan = 0f; }
        }
    }

    /// <summary>Bird arrives from the sky and glides down to land at a crumb spot (used when the player throws breadcrumbs).</summary>
    public void FlyIn(Vector3 spot)
    {
        if (IsButterfly) return;
        Vector3 to = spot - transform.position; to.y = 0f;
        if (to.sqrMagnitude > 0.01f) yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        state = BState.Land;
        sleeping = false;
        wakeUntil = Time.time + R(25f, 45f);
        landTarget = spot;
        landTimer = 0f;
        flyAlt = R(7f, 12f);
        climbVel = 0f;
    }

    /// <summary>Guitar audience bird: arrives from the sky and lands at spot.</summary>
    public void JoinAudience(Vector3 spot) { Audience = true; audienceLeaveAt = -1f; FlyIn(spot); }

    bool TickAudience(float dt, float dist, Vector3 fromPlayer)
    {
        bool playing = Items.GuitarPlayer.IsPlaying;
        if (playing) audienceLeaveAt = -1f;
        else if (audienceLeaveAt < 0f) audienceLeaveAt = Time.time + R(2.5f, 6.5f);
        bool scared = AnimalAgent.PlayerIsSprinting(settings) && dist < 6f;
        if (scared || (audienceLeaveAt > 0f && Time.time >= audienceLeaveAt))
        {
            Audience = false;
            TakeOff(fromPlayer.sqrMagnitude > 0.01f ? fromPlayer.normalized : transform.forward);
            return true;
        }
        if (sleeping) return false;
        act = GroundAct.Look; actTimer = 1f;
        Vector3 toP = -fromPlayer;
        if (toP.sqrMagnitude > 0.01f) yaw = Mathf.Atan2(toP.x, toP.z) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, yaw, 0f), 1f - Mathf.Exp(-8f * dt));
        rig.visual.localPosition = Vector3.zero;
        return true;
    }

    void DropFeed(bool ignoreIt)
    {
        if (feed != null)
        {
            if (feed.claims > 0) feed.claims--;
            if (ignoreIt) { feedIgnore = feed; feedIgnoreUntil = Time.time + 8f; }
        }
        feed = null;
    }

    void OnDestroy() { DropFeed(false); }

    public void TakeOff(Vector3 dir)
    {
        DropFeed(false);
        if (state != BState.Ground) return;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
        yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + R(-25f, 25f);
        state = BState.Fly;
        sleeping = false;
        wakeUntil = Time.time + R(25f, 45f);
        rig.visual.localPosition = Vector3.zero;
        flyTimer = R(6f, 14f);
        flyAlt = R(7f, 14f);
        climbVel = 0f;
        PlayReaction();
    }

    void UpdateFlight(float dt, float dist)
    {
        Vector3 pos = transform.position;
        float terrain = director.Ctx.HeightAt(pos.x, pos.z);
        flyTimer -= dt;
        float speedMul = 1f;
        float desiredYaw = yaw;
        float wantY;

        if (state == BState.Fly)
        {
            // gentle wandering curve
            yaw += (Mathf.Sin(phase * 0.6f + seedPhase) * 22f + Mathf.Sin(phase * 1.7f + seedPhase * 2f) * 8f) * dt;
            wantY = terrain + flyAlt;
            Vector3 crumbSpot;
            if (BreadFeeding.Active && V() < dt * 0.8f && BreadFeeding.TryAttract(pos, out crumbSpot))
            {
                landTarget = new Vector3(crumbSpot.x, director.GroundY(crumbSpot.x, crumbSpot.z, crumbSpot.y + 2f), crumbSpot.z);
                landTimer = 0f;
                state = BState.Land;
            }
            else if (flyTimer <= 0f && ChooseLanding()) state = BState.Land;
            else if (flyTimer <= -6f) flyTimer = R(4f, 8f);   // couldn't find a spot; keep flying
        }
        else
        {
            Vector3 to = landTarget - pos; to.y = 0f;
            float horiz = to.magnitude;
            desiredYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            yaw = Mathf.MoveTowardsAngle(yaw, desiredYaw, 240f * dt);
            landTimer += dt;
            if (landTimer > 7f && landTimer < 7f + dt * 1.5f) ChooseLanding();     // unreachable? pick another spot
            if (landTimer > 14f)
            {   // give up and settle right here so nothing circles forever
                float gy0 = director.GroundY(pos.x, pos.z, pos.y + 2f);
                landTarget = new Vector3(pos.x, gy0, pos.z);
                horiz = 0f;
            }
            // descend along a glide slope toward the target
            wantY = landTarget.y + Mathf.Clamp(horiz * 0.45f, 0f, flyAlt);
            speedMul = Mathf.Clamp(horiz / 3f, 0.4f, 1f);
            if ((horiz < 0.9f && pos.y - landTarget.y < 0.6f) || landTimer > 14f)
            {
                transform.position = new Vector3(landTarget.x, landTarget.y, landTarget.z);
                groundY = landTarget.y;
                state = BState.Ground; pitch = 0f; roll = 0f;
                rig.visual.localPosition = Vector3.zero;
                PickGroundAct();
                return;
            }
        }
        // vertical movement
        float dy = wantY - pos.y;
        climbVel = Ease(climbVel, Mathf.Clamp(dy * 1.2f, -4.5f, 4.5f), 1f - Mathf.Exp(-3f * dt));
        Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 next = pos + fwd * flySpeed * speedMul * dt;
        next.y = pos.y + climbVel * dt;
        float minY = (state == BState.Land ? Mathf.Min(terrain, landTarget.y) : terrain) + (state == BState.Land ? 0.02f : 0.3f);
        if (next.y < minY) next.y = minY;
        transform.position = next;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        float yawRate = Mathf.DeltaAngle(0f, (state == BState.Land ? desiredYaw - yaw : 0f));
        pitch = Ease(pitch, Mathf.Clamp(-climbVel * 6f, -30f, 30f), 1f - Mathf.Exp(-5f * dt));
        roll = Ease(roll, Mathf.Clamp(-yawRate * 0.4f, -35f, 35f), 1f - Mathf.Exp(-4f * dt));
    }

    bool ChooseLanding()
    {
        Vector3 pos = transform.position;
        for (int i = 0; i < 6; i++)
        {
            float a = yaw + R(-60f, 60f);
            float d = R(8f, 20f);
            Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
            Vector3 p = pos + dir * d;
            if (!ZoneOk(p.x, p.z)) continue;
            Vector3 pp = director.Player.position;
            float dxp = p.x - pp.x, dzp = p.z - pp.z;
            if (dxp * dxp + dzp * dzp < 8f * 8f) continue;
            float ty = director.Ctx.HeightAt(p.x, p.z);
            float gy = director.GroundY(p.x, p.z, ty + 20f);
            if (Mathf.Abs(gy - ty) > 2.5f) continue;     // landed on something odd (roof/tree): skip
            Vector3 c = new Vector3(p.x, gy + 0.25f, p.z);
            if (Physics.CheckSphere(c, 0.22f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            landTarget = new Vector3(p.x, gy, p.z);
            landTimer = 0f;
            return true;
        }
        return false;
    }

    void ApplyBirdPose(float dt)
    {
        bool flying = state != BState.Ground;
        sleepAmt = Ease(sleepAmt, sleeping ? 1f : 0f, 1f - Mathf.Exp(-3f * dt));
        spread = Ease(spread, flying ? 1f : 0f, 1f - Mathf.Exp(-(flying ? 14f : 9f) * dt));
        float time = phase + seedPhase;

        // wings: fold against the body (spread 0) or flap (spread 1)
        float flapAng;
        bool gliding = false;
        if (state == BState.Land) { flapAng = 30f + Mathf.Sin(phase * flapOmega) * 50f; }
        else if (Species == FlyerSpecies.Crow || Species == FlyerSpecies.Pigeon) {
            gliding = state == BState.Fly && Mathf.Sin(phase * 0.8f + seedPhase) > 0.35f && climbVel < 1f;
            flapAng = gliding ? 12f : 18f + Mathf.Sin(phase * flapOmega) * 48f;
        }
        else flapAng = 18f + Mathf.Sin(phase * flapOmega) * 50f;
        for (int k = 0; k < 2; k++)
        {
            float s = k == 0 ? -1f : 1f;
            Quaternion fold = Quaternion.LookRotation(Vector3.up, Vector3.right * -s);
            Quaternion open = Quaternion.Euler(0f, 0f, s * flapAng);
            rig.wings[k].localRotation = Quaternion.Slerp(fold, open, spread);
            float len = Mathf.Lerp(0.62f, 1f, spread);
            rig.wings[k].localScale = new Vector3(len, 1f, 1f);
        }
        // body & head
        float headPitch = 0f, headYaw = 0f, bodyPitch = 0f, headFwd = 0f;
        if (flying)
        {
            bodyPitch = pitch + (Species == FlyerSpecies.Crow ? 0f : 0f);
            rig.body.localRotation = Quaternion.Euler(bodyPitch, 0f, roll);
            headPitch = -pitch * 0.5f;
        }
        else if (sleeping || sleepAmt > 0.02f)
        {   // roosting: belly on the ground, eyes shut, head forward-low or tucked back over the wing
            float breathe = Mathf.Sin(time * 1.8f);
            bodyPitch = breathe * 0.8f;
            if (sleepVariant == 0) { headPitch = 16f; }
            else { headYaw = sleepSide * 150f; headPitch = -12f; }
            rig.body.localRotation = Quaternion.Euler(bodyPitch, 0f, 0f);
        }
        else
        {
            if (act == GroundAct.Peck)
            {
                float pk = Mathf.Max(0f, Mathf.Sin(peckPhase));
                headPitch = pk * 55f; bodyPitch = pk * 18f;
            }
            else if (act == GroundAct.Look) { headYaw = Mathf.Sin(time * 1.6f) * 40f; headPitch = Mathf.Sin(time * 0.9f) * 6f - 4f; }
            else if (act == GroundAct.Walk) { headFwd = Mathf.Sin(walkPhase) * 0.012f; headPitch = 6f; }
            else if (act == GroundAct.Hop) { bodyPitch = Mathf.Sin(Mathf.Clamp01(hopT) * Mathf.PI) * -12f; }
            rig.body.localRotation = Quaternion.Slerp(rig.body.localRotation, Quaternion.Euler(bodyPitch, 0f, 0f), 1f - Mathf.Exp(-14f * dt));
        }
        rig.body.localPosition = new Vector3(0f, 0.095f - 0.032f * sleepAmt, 0f);
        rig.head.localRotation = Quaternion.Euler(headPitch, headYaw, 0f);
        for (int e = 0; e < 2; e++)
            if (rig.eyes[e] != null) rig.eyes[e].localScale = new Vector3(0.017f, 0.017f * Mathf.Lerp(1f, 0.12f, sleepAmt), 0.017f);
        rig.head.localPosition = new Vector3(0f, 0.062f, 0.095f + headFwd);
        rig.tail.localRotation = Quaternion.Euler(flying ? -6f : (act == GroundAct.Hop ? 5f : 0f) + Mathf.Sin(time * 2f) * 2f, 0f, 0f);

        // legs: tucked in flight, walk-swing on the ground
        rig.legRoot.gameObject.SetActive((!flying || (state == BState.Land && spread < 0.5f)) && sleepAmt < 0.6f);
        if (!flying)
        {
            float sw = act == GroundAct.Walk ? Mathf.Sin(walkPhase) * 35f : (act == GroundAct.Hop ? -Mathf.Sin(Mathf.Clamp01(hopT) * Mathf.PI) * 30f : 0f);
            rig.legs[0].localRotation = Quaternion.Euler(sw, 0f, 0f);
            rig.legs[1].localRotation = Quaternion.Euler(act == GroundAct.Walk ? -sw : sw, 0f, 0f);
        }
    }

    // ---------------------------------------------------------------- sounds
    void SetupAudio()
    {
        if (IsButterfly || sounds == null) return;
        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.priority = 190;
        SpatialAudio.Configure(audioSrc, sounds.minDistance, sounds.maxDistance);   // strong 3D cues
        float lo = Mathf.Max(0.5f, sounds.ambientInterval.x), hi = Mathf.Max(lo, sounds.ambientInterval.y);
        nextCallTime = Time.time + R(0f, hi);
    }

    static AudioClip Pick(AudioClip[] list, System.Random r)
    {
        if (list == null || list.Length == 0) return null;
        int s = r.Next(list.Length);
        for (int i = 0; i < list.Length; i++) { AudioClip c = list[(s + i) % list.Length]; if (c != null) return c; }
        return null;
    }

    void Play(AudioClip c)
    {
        if (c == null || audioSrc == null || sleeping) return;   // asleep: silent
        audioSrc.pitch = R(Mathf.Min(sounds.pitchRange.x, sounds.pitchRange.y), Mathf.Max(sounds.pitchRange.x, sounds.pitchRange.y));
        audioSrc.PlayOneShot(c, AnimalSounds.BirdVol(sounds));
    }

    void UpdateAmbientSound(float dist)
    {
        if (sounds == null || audioSrc == null || !caller.Tick(sounds, dist)) return;
        Play(Pick(sounds.ambientSounds, rng));
    }

    void PlayReaction() { if (sounds != null) Play(Pick(sounds.reactionSounds, rng)); }
}

/// <summary>Spawns butterflies and birds around the player. Independent of the animal and NPC systems.</summary>
[DisallowMultipleComponent]
public sealed class FlyerWorldController : MonoBehaviour
{
    readonly List<FlyerAgent> flyers = new List<FlyerAgent>(48);
    NpcDirector director;
    AnimalAISettings settings;
    System.Random rng;
    Transform root;
    float spawnTimer;
    int nextSeed = 1;
    bool shuttingDown;

    public bool IsShuttingDown { get { return shuttingDown; } }

    public void Initialize(NpcDirector owner, AnimalAISettings cfg)
    {
        director = owner;
        settings = cfg ?? new AnimalAISettings();
        settings.Sanitize();
        int seed = 52711;
        if (director != null && director.Ctx != null && director.Ctx.settings != null) seed ^= director.Ctx.settings.seed;
        rng = new System.Random(seed);
        shuttingDown = false;
        spawnTimer = 1f;
        GameObject go = new GameObject("Butterflies And Birds");
        root = go.transform;
        root.SetParent(transform, false);
        BreadFeeding.Create(this, director, root);
    }

    float V() { return (float)rng.NextDouble(); }
    float R(float a, float b) { return a + V() * (b - a); }

    void Update()
    {
        if (shuttingDown || director == null || !director.Running || director.Ctx == null || director.Player == null) return;
        for (int i = flyers.Count - 1; i >= 0; i--) if (flyers[i] == null) flyers.RemoveAt(i);
        UpdateAudience();
        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f) return;
        spawnTimer = Mathf.Max(0.4f, settings.spawnInterval * 0.5f);
        int butterflies = 0, birds = 0;
        for (int i = 0; i < flyers.Count; i++) { if (flyers[i].IsButterfly) butterflies++; else birds++; }
        if (butterflies < settings.butterflyCount) TrySpawnMany(true);
        if (birds < settings.birdCount) TrySpawnMany(false);
    }

    void TrySpawnMany(bool butterfly)
    {
        for (int i = 0; i < settings.spawnAttemptsPerTick; i++) if (TrySpawn(butterfly)) return;
    }

    bool TrySpawn(bool butterfly)
    {
        WorldContext ctx = director.Ctx;
        if (ctx == null || ctx.zoneMap == null || ctx.Size <= 0f) return false;
        Vector3 player = director.Player.position;
        float x, z;
        FlyerAgent mate = null;
        if (!butterfly && V() < 0.45f && flyers.Count > 0)
        {   // join an existing ground bird to form small flocks
            FlyerAgent c = flyers[rng.Next(flyers.Count)];
            if (c != null && c.OnGround && c.Species != FlyerSpecies.Crow) mate = c;
        }
        if (mate != null)
        {
            Vector3 mp = mate.transform.position;
            x = mp.x + R(-3f, 3f); z = mp.z + R(-3f, 3f);
        }
        else
        {
            float minR2 = settings.spawnRadiusMin * settings.spawnRadiusMin, maxR2 = settings.spawnRadiusMax * settings.spawnRadiusMax;
            float distance = Mathf.Sqrt(Mathf.Lerp(minR2, maxR2, V()));
            float angle = R(0f, Mathf.PI * 2f);
            x = player.x + Mathf.Cos(angle) * distance; z = player.z + Mathf.Sin(angle) * distance;
        }
        float half = ctx.Size * 0.5f;
        if (x < -half || x > half || z < -half || z > half) return false;
        ZoneType zone = ctx.zoneMap.ZoneAt(x, z);
        bool okZone = zone == ZoneType.Forest || zone == ZoneType.Suburb || (zone == ZoneType.City && !butterfly);
        if (!okZone) return false;

        FlyerSpecies species;
        if (butterfly) species = FlyerSpecies.Butterfly;
        else if (mate != null) species = mate.Species;
        else
        {
            float r = V();
            if (zone == ZoneType.City) species = r < 0.72f ? FlyerSpecies.Pigeon : FlyerSpecies.Sparrow;
            else if (zone == ZoneType.Forest) species = r < 0.30f ? FlyerSpecies.Robin : (r < 0.58f ? FlyerSpecies.BlueJay : (r < 0.85f ? FlyerSpecies.Cardinal : FlyerSpecies.Crow));
            else species = r < 0.38f ? FlyerSpecies.Sparrow : (r < 0.78f ? FlyerSpecies.Pigeon : FlyerSpecies.Crow);
        }

        float terrain = ctx.HeightAt(x, z);
        float y = director.GroundY(x, z, terrain + 20f);
        if (Mathf.Abs(y - terrain) > 1.5f) return false;
        if (!butterfly && ctx.NormalAt(x, z).y < 0.9f) return false;
        Vector3 world = new Vector3(x, butterfly ? y + R(0.8f, 1.8f) : y, z);
        if (settings.avoidVisibleSpawns && mate == null && VisibilityTester.CanSeeSpawn(world, 0.5f)) return false;
        if (!butterfly && Physics.CheckSphere(new Vector3(x, y + 0.25f, z), 0.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;

        GameObject go = new GameObject(species.ToString());
        go.transform.SetParent(root, true);
        go.transform.SetPositionAndRotation(world, Quaternion.Euler(0f, R(0f, 360f), 0f));
        FlyerAgent agent = go.AddComponent<FlyerAgent>();
        agent.Initialize(this, species, director, settings, rng.Next() ^ (nextSeed++ * 7919), zone);
        flyers.Add(agent);
        if (!butterfly && mate == null && species != FlyerSpecies.Crow) SpawnFlock(species, world, zone, 3 + rng.Next(5));
        return true;
    }

    void SpawnFlock(FlyerSpecies species, Vector3 center, ZoneType zone, int count)
    {
        WorldContext ctx = director.Ctx;
        for (int i = 0; i < count; i++)
        {
            float a = R(0f, Mathf.PI * 2f), r = R(0.6f, 3.2f);
            float x = center.x + Mathf.Cos(a) * r, z = center.z + Mathf.Sin(a) * r;
            if (ctx.zoneMap.ZoneAt(x, z) != zone) continue;
            float terrain = ctx.HeightAt(x, z);
            float y = director.GroundY(x, z, terrain + 20f);
            if (Mathf.Abs(y - terrain) > 1.5f || ctx.NormalAt(x, z).y < 0.9f) continue;
            if (Physics.CheckSphere(new Vector3(x, y + 0.25f, z), 0.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            GameObject go = new GameObject(species.ToString());
            go.transform.SetParent(root, true);
            go.transform.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(0f, R(0f, 360f), 0f));
            FlyerAgent agent = go.AddComponent<FlyerAgent>();
            agent.Initialize(this, species, director, settings, rng.Next() ^ (nextSeed++ * 7919), zone);
            flyers.Add(agent);
        }
    }

    /// <summary>Flock mates near a startled bird take off together.</summary>
    public void AlertNearby(FlyerAgent source, float radius, Vector3 away)
    {
        float r2 = radius * radius;
        Vector3 sp = source.transform.position;
        for (int i = 0; i < flyers.Count; i++)
        {
            FlyerAgent f = flyers[i];
            if (f == null || f == source || !f.OnGround) continue;
            Vector3 d = f.transform.position - sp;
            if (d.sqrMagnitude < r2) f.TakeOff(away);
        }
    }

    /// <summary>Push vector (m/s) moving a ground bird away from others closer than minDist, so birds never overlap.</summary>
    public Vector3 SeparationFor(FlyerAgent self, Vector3 pos, float minDist)
    {
        Vector3 push = Vector3.zero;
        float m2 = minDist * minDist;
        for (int i = 0; i < flyers.Count; i++)
        {
            FlyerAgent f = flyers[i];
            if (f == null || f == self || !f.OnGround) continue;
            Vector3 d = pos - f.transform.position; d.y = 0f;
            float q = d.sqrMagnitude;
            if (q >= m2) continue;
            if (q < 0.0001f) { d = new Vector3(Mathf.Cos(self.GetInstanceID()), 0f, Mathf.Sin(self.GetInstanceID())); q = 1f; }
            float k = 1f - Mathf.Sqrt(q) / minDist;
            push += d.normalized * (k * 1.2f);
        }
        return push;
    }

    public void GroundBirds(Vector3 center, float radius, List<FlyerAgent> result)
    {
        result.Clear();
        float r2 = radius * radius;
        for (int i = 0; i < flyers.Count; i++)
        {
            FlyerAgent f = flyers[i];
            if (f == null || !f.OnGround) continue;
            Vector3 d = f.transform.position - center;
            d.y = 0f;
            if (d.sqrMagnitude < r2) result.Add(f);
        }
    }

    public void Startle(Vector3 center, float radius, Vector3 from)
    {
        float r2 = radius * radius;
        Vector3 away = center - from;
        away.y = 0f;
        for (int i = 0; i < flyers.Count; i++)
        {
            FlyerAgent f = flyers[i];
            if (f == null || !f.OnGround) continue;
            Vector3 d = f.transform.position - center;
            d.y = 0f;
            if (d.sqrMagnitude < r2) f.TakeOff(away.sqrMagnitude > 0.01f ? away : d);
        }
    }

    /// <summary>Called when breadcrumbs land: birds of any species (picked for the zone at the spot) fly in from the sky and land at the crumbs.</summary>
    public void SummonBirds(Vector3 spot, int count)
    {
        if (director == null || director.Ctx == null || director.Ctx.zoneMap == null) return;
        int birds = 0, summoned = 0;
        for (int i = 0; i < flyers.Count; i++)
        {
            if (flyers[i] == null || flyers[i].IsButterfly) continue;
            birds++;
            if (flyers[i].Summoned) summoned++;
        }
        count = Mathf.Min(count, Mathf.Max(0, BreadFeeding.MaxSummonedBirds - summoned), Mathf.Max(0, settings.birdCount + 12 - birds));
        ZoneType zone = director.Ctx.zoneMap.ZoneAt(spot.x, spot.z);
        for (int i = 0; i < count; i++)
        {
            float a = R(0f, Mathf.PI * 2f), r = R(14f, 24f);
            Vector3 from = new Vector3(spot.x + Mathf.Cos(a) * r, 0f, spot.z + Mathf.Sin(a) * r);
            from.y = director.Ctx.HeightAt(from.x, from.z) + R(7f, 12f);
            Vector3 land = new Vector3(spot.x + R(-1.4f, 1.4f), spot.y, spot.z + R(-1.4f, 1.4f));
            land.y = director.GroundY(land.x, land.z, spot.y + 2f);
            FlyerSpecies sp = SummonSpecies(zone);
            GameObject go = new GameObject(sp.ToString());
            go.transform.SetParent(root, true);
            go.transform.SetPositionAndRotation(from, Quaternion.Euler(0f, R(0f, 360f), 0f));
            FlyerAgent agent = go.AddComponent<FlyerAgent>();
            agent.Initialize(this, sp, director, settings, rng.Next() ^ (nextSeed++ * 7919), zone);
            agent.Summoned = true;
            agent.FlyIn(land);
            flyers.Add(agent);
        }
    }

    // ---- guitar audience: while the player plays, a few birds fly in and land in front of them
    const int MaxAudience = 5;
    float playTime, audienceTimer;
    int audienceTarget;

    void UpdateAudience()
    {
        if (!Items.GuitarPlayer.IsPlaying) { playTime = 0f; audienceTarget = 0; return; }
        float dt = Time.deltaTime;
        if (playTime <= 0f) { audienceTarget = 3 + rng.Next(MaxAudience - 2); audienceTimer = R(1.5f, 3f); }
        playTime += dt;
        audienceTimer -= dt;
        if (audienceTimer > 0f) return;
        audienceTimer = R(2f, 4.5f);
        int have = 0, birds = 0;
        for (int i = 0; i < flyers.Count; i++)
        {
            if (flyers[i] == null || flyers[i].IsButterfly) continue;
            birds++;
            if (flyers[i].Audience) have++;
        }
        if (have >= audienceTarget || birds >= settings.birdCount + 12) return;
        SummonAudience(1 + rng.Next(2));
    }

    void SummonAudience(int count)
    {
        if (director == null || director.Ctx == null || director.Ctx.zoneMap == null) return;
        Vector3 pp = director.Player.position;
        Camera cam = Camera.main;
        Vector3 fwd = cam != null ? cam.transform.forward : director.Player.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        fwd.Normalize();
        for (int i = 0; i < count; i++)
        {
            float a = R(-60f, 60f), r = R(2.6f, 5.5f);
            Vector3 d = Quaternion.Euler(0f, a, 0f) * fwd;
            Vector3 land = pp + d * r;
            land.y = director.GroundY(land.x, land.z, pp.y + 2f);
            if (Mathf.Abs(land.y - pp.y) > 2f) continue;
            if (Physics.CheckSphere(land + Vector3.up * 0.25f, 0.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            ZoneType zone = director.Ctx.zoneMap.ZoneAt(land.x, land.z);
            float ang = R(0f, Mathf.PI * 2f), rr = R(14f, 22f);
            Vector3 from = new Vector3(land.x + Mathf.Cos(ang) * rr, 0f, land.z + Mathf.Sin(ang) * rr);
            from.y = director.Ctx.HeightAt(from.x, from.z) + R(7f, 12f);
            FlyerSpecies sp = SummonSpecies(zone);
            GameObject go = new GameObject(sp.ToString());
            go.transform.SetParent(root, true);
            go.transform.SetPositionAndRotation(from, Quaternion.Euler(0f, R(0f, 360f), 0f));
            FlyerAgent agent = go.AddComponent<FlyerAgent>();
            agent.Initialize(this, sp, director, settings, rng.Next() ^ (nextSeed++ * 7919), zone);
            agent.JoinAudience(land);
            flyers.Add(agent);
        }
    }

    // every bird model can show up: zone favourites first, the rest at lower odds
    FlyerSpecies SummonSpecies(ZoneType zone)
    {
        FlyerSpecies[] all = { FlyerSpecies.Sparrow, FlyerSpecies.Robin, FlyerSpecies.BlueJay, FlyerSpecies.Cardinal, FlyerSpecies.Crow, FlyerSpecies.Pigeon };
        FlyerSpecies[] fav = zone == ZoneType.City ? new[] { FlyerSpecies.Pigeon, FlyerSpecies.Sparrow, FlyerSpecies.Crow }
            : zone == ZoneType.Forest ? new[] { FlyerSpecies.Robin, FlyerSpecies.BlueJay, FlyerSpecies.Cardinal }
            : new[] { FlyerSpecies.Sparrow, FlyerSpecies.Pigeon, FlyerSpecies.Cardinal };
        return V() < 0.65f ? fav[rng.Next(fav.Length)] : all[rng.Next(all.Length)];
    }

    public void Remove(FlyerAgent f) { if (f != null) flyers.Remove(f); }

    public void Shutdown()
    {
        if (shuttingDown) return;
        shuttingDown = true;
        for (int i = flyers.Count - 1; i >= 0; i--) if (flyers[i] != null) WorldUtil.Kill(flyers[i].gameObject);
        flyers.Clear();
        if (root != null) WorldUtil.Kill(root.gameObject);
        root = null;
        director = null;
    }

    void OnDestroy() { Shutdown(); }
}
